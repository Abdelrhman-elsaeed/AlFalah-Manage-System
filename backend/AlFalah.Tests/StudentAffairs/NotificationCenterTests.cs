using AlFalah.Application.Notifications;
using AlFalah.Application.Duty;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.Notifications;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class NotificationCenterTests
{
    private static readonly IOptions<InboxOptions> OptionsValue = Options.Create(new InboxOptions { CenterEnabled = true, JournalEnabled = true });
    [Fact]
    public async Task DutyAssignmentRevisionsSelectExactCurrentTaskAndFormerTeacherReceivesOnlyTheirChangeNotice()
    {
        await using var db = await Seed();
        db.Add(new ApplicationUser { Id = "teacher2" });
        db.Add(new UserSchoolRole { SchoolId = 1, UserId = "teacher2", RoleId = "teacher" });
        var plan = new DutyPlan { SchoolId = 1 }; db.Add(plan); await db.SaveChangesAsync();
        var assignment = new DutyAssignment { SchoolId = 1, DutyPlanId = plan.Id, EffectiveTeacherId = "teacher", Date = new(2026, 10, 18), Revision = 1 };
        db.Add(assignment); await db.SaveChangesAsync(); var repo = new CenterRepository(db, OptionsValue, TimeProvider.System);
        var original = (await repo.ListAsync(new(1, "teacher"), new(), null, default)).Single();
        (await repo.TargetAsync(original, default)).Query!["execution"].Should().Be(assignment.Id.ToString());
        assignment.EffectiveTeacherId = "teacher2"; assignment.Revision++;
        db.Add(new DutyAssignmentChange { SchoolId = 1, DutyPlanId = plan.Id, AssignmentId = assignment.Id, BeforeTeacherId = "teacher", AfterTeacherId = "teacher2", AfterRevision = assignment.Revision, Date = assignment.Date });
        await db.SaveChangesAsync();
        (await repo.FindAsync(new(1, "teacher"), original.Id, default)).Should().BeNull();
        var former = (await repo.ListAsync(new(1, "teacher"), new(), null, default)).Single();
        (await repo.TargetAsync(former, default)).Route.Should().Be("/notifications/detail/" + former.Id);
        assignment.IsCancelled = true; assignment.Revision++; await db.SaveChangesAsync();
        var current = (await repo.ListAsync(new(1, "teacher2"), new(), null, default)).First();
        (await repo.TaskStateAsync(current, default)).Should().Be("cancelled");
        (await repo.CountAsync(new(1, "moderator"), default)).Should().Be(0);
    }

    [Fact]
    public async Task StaffAttendanceRejectsPartialAndEmptySheetsAndFullSheetCorrectionsKeepOneManagerEntry()
    {
        await using var db = await Seed();
        db.Add(new ApplicationRole { Id = "secretary", Name = RoleNames.Secretary });
        db.Add(new UserSchoolRole { SchoolId = 1, UserId = "officer", RoleId = "secretary" }); await db.SaveChangesAsync();
        var user = new AlFalah.Tests.TestDoubles.TeacherDriveHarness.TestCurrentUser(RoleNames.Secretary, "officer", 1, true);
        var service = new AlFalah.Infrastructure.Services.AttendanceService(db, user,
            new AlFalah.Infrastructure.Services.SchoolScopeGuard(db, user, Microsoft.Extensions.Logging.Abstractions.NullLogger<AlFalah.Infrastructure.Services.SchoolScopeGuard>.Instance));
        var date = new DateOnly(2026, 10, 18);
        var partial = new AlFalah.Application.DTOs.Attendance.SaveAttendanceSheetRequestDto(date, [new("teacher", AttendanceStatus.Present, null)]);
        await FluentActions.Awaiting(() => service.SaveSheetAsync(partial)).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => service.SaveSheetAsync(partial with { Entries = [] })).Should().ThrowAsync<ArgumentException>();
        (await db.Set<PersonalNotification>().CountAsync()).Should().Be(0);
        var full = partial with { Entries = new[] { "manager", "teacher", "moderator" }.Select(id => new AlFalah.Application.DTOs.Attendance.SaveAttendanceEntryDto(id, AttendanceStatus.Present, null)).ToArray() };
        await service.SaveSheetAsync(full); await service.SaveSheetAsync(full);
        var repo = new CenterRepository(db, OptionsValue, TimeProvider.System);
        var entry = (await repo.ListAsync(new(1, "manager"), new(), null, default)).Single();
        entry.SourceType.Should().Be("StaffAttendance");
        (await repo.DetailAsync(entry, default)).Rows.Should().HaveCount(4);
        await repo.ReadAsync(new(1, "manager"), entry.Id, default);
        await service.SaveSheetAsync(full with { Entries = full.Entries.Select(x => x with { Status = AttendanceStatus.Absent }).ToArray() });
        (await repo.CountAsync(new(1, "manager"), default)).Should().Be(0);
        (await db.Set<PersonalNotification>().CountAsync()).Should().Be(1);
    }
    internal static async Task<AlFalahDbContext> Seed()
    {
        var db = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Schools.Add(new() { Id = 1, Name = "Synthetic N2" });
        foreach (var (id, role) in new[] { ("manager", RoleNames.SchoolManager), ("teacher", RoleNames.Instructor), ("moderator", RoleNames.Moderator), ("officer", RoleNames.StudentAffairsOfficer) })
        {
            db.Users.Add(new() { Id = id, UserName = id }); db.Roles.Add(new() { Id = id, Name = role });
            db.UserSchoolRoles.Add(new() { SchoolId = 1, UserId = id, RoleId = id });
        }
        foreach (var name in new[] { PermissionNames.ComplaintView, PermissionNames.AttendanceView, PermissionNames.AttendanceViewStudents, PermissionNames.StudentAffairsDashboardSchoolOversight,
            PermissionNames.NotificationViewOwn, PermissionNames.MessagingViewOwn, PermissionNames.VisitApprove, DutyPermissions.Manage, DutyPermissions.View })
        {
            var permission = new Permission { Name = name }; db.Add(permission); await db.SaveChangesAsync();
            foreach (var id in new[] { "manager", "teacher", "moderator", "officer" }) db.RolePermissions.Add(new() { RoleId = id, PermissionId = permission.Id });
        }
        await db.SaveChangesAsync(); db.CenterProjectionEnabled = true; db.InboxJournalEnabled = true;
        return db;
    }
    [Fact]
    public async Task ComplaintSubmissionAndResponseArePrivateAndCountAndReadUseCurrentPolicy()
    {
        await using var db = await Seed();
        var complaint = new Complaint { SchoolId = 1, InstructorUserId = "teacher", ModeratorUserId = "moderator", Subject = "private" };
        db.Add(complaint); await db.SaveChangesAsync();
        var repo = new CenterRepository(db, OptionsValue, TimeProvider.System);
        (await repo.CountAsync(new(1, "manager"), default)).Should().Be(1);
        (await repo.CountAsync(new(1, "moderator"), default)).Should().Be(0);
        (await repo.CountAsync(new(1, "teacher"), default)).Should().Be(0);
        var entry = await db.Set<PersonalNotification>().SingleAsync();
        await repo.ReadAsync(new(1, "teacher"), entry.Id, default);
        entry.ReadAtUtc.Should().BeNull();
        await repo.ReadAsync(new(1, "manager"), entry.Id, default);
        (await repo.TaskStateAsync(entry, default)).Should().Be("pending");
        (await repo.CountAsync(new(1, "manager"), default)).Should().Be(0);
        complaint.Status = ComplaintStatus.InReview; complaint.ResolutionNote = "تمت مراجعة الطلب"; complaint.HandledAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        (await repo.CountAsync(new(1, "teacher"), default)).Should().Be(1);
        var response = (await repo.ListAsync(new(1, "teacher"), new(), null, default)).Single();
        (await repo.TargetAsync(response, default)).Query!["complaintId"].Should().Be(complaint.Id.ToString());
        db.UserSchoolRoles.Add(new() { UserId = "teacher", SchoolId = 1, RoleId = "moderator" }); await db.SaveChangesAsync();
        (await repo.CountAsync(new(1, "teacher"), default)).Should().Be(0);
        (await repo.FindAsync(new(1, "teacher"), response.Id, default)).Should().BeNull();
        (await repo.CountAsync(new(2, "manager"), default)).Should().Be(0);
    }

    [Fact]
    public async Task AttendanceCompletionHasClassroomAndStaffIdentitiesAndCorrectionsDoNotResetRead()
    {
        await using var db = await Seed();
        foreach (var classroom in new int?[] { 10, 11, null }) db.Add(new AttendanceCompletion { SchoolId = 1, ClassroomId = classroom, Date = new(2026, 10, 18), RosterCount = 20 });
        await db.SaveChangesAsync();
        var repo = new CenterRepository(db, OptionsValue, TimeProvider.System);
        (await repo.CountAsync(new(1, "manager"), default)).Should().Be(3);
        await repo.ReadAsync(new(1, "manager"), null, default);
        db.Add(new AttendanceCompletion { SchoolId = 1, ClassroomId = 10, Date = new(2026, 10, 18), RosterCount = 21 });
        db.Add(new AttendanceCompletion { SchoolId = 1, ClassroomId = 12, Date = new(2026, 10, 18), RosterCount = 0 });
        await db.SaveChangesAsync();
        (await db.Set<PersonalNotification>().CountAsync()).Should().Be(3);
        (await repo.CountAsync(new(1, "manager"), default)).Should().Be(0);
        var membership = await db.UserSchoolRoles.SingleAsync(x => x.UserId == "manager"); membership.IsActive = false; await db.SaveChangesAsync();
        (await repo.ListAsync(new(1, "manager"), new(), null, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task SignedDutySubmissionIgnoresDraftLetterArchiveAndReplays()
    {
        await using var db = await Seed();
        var plan = new DutyPlan { SchoolId = 1 }; db.Add(plan); await db.SaveChangesAsync();
        var assignment = new DutyAssignment { SchoolId = 1, DutyPlanId = plan.Id, EffectiveTeacherId = "teacher", Date = new(2026, 10, 18) }; db.Add(assignment); await db.SaveChangesAsync();
        var draft = new DutyReportDraft { SchoolId = 1, AssignmentId = assignment.Id, AuthorUserId = "teacher" }; db.Add(draft); await db.SaveChangesAsync();
        (await db.Set<PersonalNotification>().CountAsync(x => x.SourceType == "DutyReport")).Should().Be(0);
        db.Add(new DutySignedDocument { SchoolId = 1, AssignmentId = assignment.Id, DraftId = draft.Id, Kind = "Letter", SignatureBytes = [1] }); await db.SaveChangesAsync();
        (await db.Set<PersonalNotification>().CountAsync(x => x.SourceType == "DutyReport")).Should().Be(0);
        var document = new DutySignedDocument { SchoolId = 1, AssignmentId = assignment.Id, DraftId = draft.Id, Kind = "Report", SignatureBytes = [1], SignedAt = DateTimeOffset.UtcNow };
        db.Add(document); await db.SaveChangesAsync(); draft.SubmittedAt = document.SignedAt; draft.Version++; await db.SaveChangesAsync();
        var entries = await db.Set<PersonalNotification>().Where(x => x.SourceType == "DutyReport").ToArrayAsync();
        entries.Should().NotBeEmpty(); entries.Should().OnlyContain(x => x.Audience == "DutyManager");
        var before = entries.Length;
        db.Add(new DutyArchiveIntent { SchoolId = 1, DocumentId = document.Id, State = "Failed" }); await db.SaveChangesAsync();
        draft.SubmittedAt = null; await db.SaveChangesAsync(); draft.SubmittedAt = DateTimeOffset.UtcNow; draft.Version++; await db.SaveChangesAsync();
        (await db.Set<PersonalNotification>().CountAsync(x => x.SourceType == "DutyReport")).Should().Be(before);
        var repo = new CenterRepository(db, OptionsValue, TimeProvider.System);
        var target = await repo.TargetAsync(entries.First(x => x.UserId == "manager"), default);
        target.Query!["documentId"].Should().Be(document.Id.ToString());
    }

    [Fact]
    public async Task PushEnrollmentProtectsMaterialRejectsOtherOwnersAndNeverReplaysOldEntries()
    {
        await using var db = await Seed();
        var center = new CenterRepository(db, OptionsValue, TimeProvider.System);
        var options = Options.Create(new WebPushOptions { VapidKeyId = "test" });
        var repo = new PushRepository(db, new EphemeralDataProtectionProvider(), center, options, TimeProvider.System);
        var material = new PushMaterial("https://fcm.googleapis.com/opaque-secret", new("abc", "def"));
        var first = await repo.RegisterAsync(new(1, "teacher"), new(Guid.NewGuid(), "test", material), default);
        first.Should().NotBeNull();
        (await db.Set<BrowserPushSubscription>().SingleAsync()).ProtectedMaterial.Should().NotContain("opaque-secret");
        (await repo.RegisterAsync(new(1, "manager"), new(Guid.NewGuid(), "test", material), default)).Should().BeNull();
        await repo.RevokeAsync(new(1, "manager"), first!.Id, default);
        (await repo.DevicesAsync(new(1, "teacher"), default)).Should().ContainSingle();
        var rotated = await repo.RegisterAsync(new(1, "teacher"), new(first.InstallationId, "test", material with { Keys = new("changed", "keys") }), default);
        rotated!.BindingVersion.Should().Be(first.BindingVersion + 1);
        await repo.RevokeAsync(new(1, "teacher"), first.Id, default);
        (await repo.RegisterAsync(new(1, "manager"), new(Guid.NewGuid(), "test", material), default)).Should().NotBeNull();
        (await db.Set<BrowserPushTarget>().CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("http://fcm.googleapis.com/x")]
    [InlineData("https://localhost/x")]
    [InlineData("https://127.0.0.1/x")]
    [InlineData("https://fcm.googleapis.com.evil.example/x")]
    [InlineData("https://fcm.googleapis.com:8443/x")]
    [InlineData("https://user:secret@fcm.googleapis.com/x")]
    [InlineData("https://fcm.googleapis.com/x#fragment")]
    public void PushEndpointMustBeExactAllowlistedHttpsOrigin(string endpoint)
    {
        var policy = new PushEndpointPolicy(Options.Create(new WebPushOptions { AllowedProviderHosts = ["fcm.googleapis.com"] }));
        policy.TryUri(endpoint, out _).Should().BeFalse();
    }
    [Fact]
    public void PushKeysAreValidatedAndNetworkAddressesCannotReachPrivateNetworks()
    {
        var policy = new PushEndpointPolicy(Options.Create(new WebPushOptions { AllowedProviderHosts = ["fcm.googleapis.com"] }));
        using var ec = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256); var key = ec.ExportParameters(false);
        string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        policy.IsValid(new("https://fcm.googleapis.com/x", new(Encode([4, .. key.Q.X!, .. key.Q.Y!]), Encode(RandomNumberGenerator.GetBytes(16))))).Should().BeTrue();
        policy.IsValid(new("https://fcm.googleapis.com/x", new("bad", "bad"))).Should().BeFalse();
        foreach (var address in new[] { "127.0.0.1", "10.1.2.3", "169.254.1.1", "172.16.0.1", "192.168.1.1", "::1", "::ffff:127.0.0.1", "fe80::1", "fc00::1" })
            PushEndpointPolicy.PublicAddress(System.Net.IPAddress.Parse(address)).Should().BeFalse();
    }
    [Theory]
    [InlineData(404, "InvalidSubscription")][InlineData(410, "InvalidSubscription")][InlineData(401, "ConfigurationError")]
    [InlineData(403, "ConfigurationError")][InlineData(413, "InvalidPayload")][InlineData(429, "Retry")][InlineData(503, "Retry")]
    public void ProviderClassificationDoesNotConfuseAcceptanceWithRead(int status, string state) => WebPushDeliveryClient.Classify(status).State.Should().Be(state);
}
