using System.Text.Json;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.GatePasses;
using AlFalah.Application.StudentAffairs.GatePasses.Handlers;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class GatePassWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveChanges_WritesDomainEventToOutboxWithGeneratedGatePassId()
    {
        await using var context = CreateContext();
        var eventId = Guid.NewGuid();
        var gatePass = NewGatePass();
        gatePass.Id.Should().Be(0);
        gatePass.AppendDomainEvent(new GatePassRequestedEvent(
            eventId, 0, gatePass.StudentId, gatePass.SchoolId, gatePass.AcademicTermId,
            gatePass.RequestedByGuardianProfileId, Now, gatePass.RequestedExitAt, Now));

        context.GatePasses.Add(gatePass);
        await context.SaveChangesAsync();

        gatePass.Id.Should().BePositive();
        var outbox = await context.OutboxMessages.SingleAsync(message => message.EventId == eventId);
        outbox.SchoolId.Should().Be(gatePass.SchoolId);
        outbox.EventType.Should().EndWith(nameof(GatePassRequestedEvent));
        using var payload = JsonDocument.Parse(outbox.PayloadJson);
        payload.RootElement.GetProperty("gatePassId").GetInt32().Should().Be(gatePass.Id);
        gatePass.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_UsesActiveSchoolAndAppendsRequestedEvent()
    {
        var repository = new FakeRepository
        {
            Link = new GuardianGatePassLinkSnapshot(9, true, true, true,
                new DateOnly(2026, 1, 1), null),
            Enrollment = new GatePassEnrollmentSnapshot(4, 3, TimetableSemester.First, 12, "1/A")
        };
        var handler = new CreateGatePassCommandHandler(
            repository,
            CurrentUser(42, RoleNames.Guardian, PermissionNames.GatePassRequest),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new CreateGatePassCommand(
            new CreateGatePassRequestDto(
                17, Now.AddHours(2), "موعد طبي", "أحمد", "والد", "هوية"),
            "request-1"), CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        repository.SchoolIds.Should().OnlyContain(id => id == 42);
        repository.Added.Should().NotBeNull();
        repository.Added!.Status.Should().Be(GatePassStatus.Requested);
        repository.Added.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GatePassRequestedEvent>();
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Approve_WithMatchingVersion_SnapshotsTimetableAndAppendsEvent()
    {
        var gatePass = NewGatePass();
        gatePass.Id = 5;
        gatePass.RowVersion = new byte[] { 1, 2, 3 };
        var repository = new FakeRepository
        {
            Tracked = gatePass,
            Enrollment = new GatePassEnrollmentSnapshot(
                gatePass.AcademicTermId, 3, TimetableSemester.First, 12, "1/A"),
            GuardianLinkIsActive = true,
            Timetable = new GatePassTimetableSnapshot(7, 8, 9, 2)
        };
        var handler = new ApproveGatePassCommandHandler(
            repository,
            CurrentUser(42, RoleNames.StudentAffairsOfficer, PermissionNames.GatePassApprove),
            new FixedTimeProvider(Now));
        var request = new ApproveGatePassRequestDto(
            Now.AddMinutes(-15), Now.AddHours(1), "Approved",
            Convert.ToBase64String(gatePass.RowVersion));

        var response = await handler.Handle(
            new ApproveGatePassCommand(gatePass.Id, request), CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        gatePass.Status.Should().Be(GatePassStatus.Approved);
        gatePass.CurrentClassroomId.Should().Be(12);
        gatePass.SchoolTimetableEntryId.Should().Be(8);
        gatePass.CurrentInstructorProfileId.Should().Be(9);
        gatePass.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GatePassApprovedEvent>();
        repository.ExpectedRowVersion.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Approve_WithStaleVersion_ReturnsConflictWithoutMutation()
    {
        var gatePass = NewGatePass();
        gatePass.Id = 5;
        gatePass.RowVersion = new byte[] { 1, 2, 3 };
        var repository = new FakeRepository { Tracked = gatePass };
        var handler = new ApproveGatePassCommandHandler(
            repository,
            CurrentUser(42, RoleNames.StudentAffairsOfficer, PermissionNames.GatePassApprove),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new ApproveGatePassCommand(
            gatePass.Id,
            new ApproveGatePassRequestDto(
                Now.AddMinutes(-15), Now.AddHours(1), null,
                Convert.ToBase64String(new byte[] { 9, 9, 9 }))), CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.Errors.Should().ContainSingle("Gate pass was modified by another user");
        gatePass.Status.Should().Be(GatePassStatus.Requested);
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task SecurityAcknowledge_OutsideExecutionWindow_DoesNotMutateOrSave()
    {
        var gatePass = NewGatePass();
        gatePass.Id = 5;
        gatePass.Status = GatePassStatus.Approved;
        gatePass.RowVersion = new byte[] { 4 };
        gatePass.ApprovedWindowStartsAt = Now.AddHours(-2);
        gatePass.ApprovedWindowEndsAt = Now.AddMinutes(-1);
        var repository = new FakeRepository { Tracked = gatePass };
        var handler = new AcknowledgeGatePassBySecurityCommandHandler(
            repository,
            CurrentUser(42, RoleNames.SecurityGuard, PermissionNames.GatePassAcknowledgeSecurity),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(
            new AcknowledgeGatePassBySecurityCommand(
                gatePass.Id,
                new AcknowledgeGatePassRequestDto(Convert.ToBase64String(gatePass.RowVersion))),
            CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.Errors.Should().ContainSingle("Gate pass is outside its execution window");
        gatePass.Status.Should().Be(GatePassStatus.Approved);
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Execute_UsesServerTimeAndAppendsStudentExitedEvent()
    {
        var gatePass = NewGatePass();
        gatePass.Id = 5;
        gatePass.Status = GatePassStatus.SecurityAcknowledged;
        gatePass.RowVersion = new byte[] { 7 };
        gatePass.ApprovedWindowStartsAt = Now.AddMinutes(-15);
        gatePass.ApprovedWindowEndsAt = Now.AddMinutes(30);
        var repository = new FakeRepository { Tracked = gatePass };
        var handler = new ExecuteGatePassCommandHandler(
            repository,
            CurrentUser(42, RoleNames.SecurityGuard, PermissionNames.GatePassExecute),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(
            new ExecuteGatePassCommand(gatePass.Id, new ExecuteGatePassRequestDto(
                Now.AddDays(-1), PickupVerificationMethod.Visual, "Verified at gate", "Gate 1",
                Convert.ToBase64String(gatePass.RowVersion))),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        gatePass.Status.Should().Be(GatePassStatus.Exited);
        gatePass.ExitedAt.Should().Be(Now);
        gatePass.PickupVerificationMethod.Should().Be(PickupVerificationMethod.Visual);
        gatePass.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<StudentExitedSchoolEvent>();
    }

    private static GatePass NewGatePass() => new()
    {
        SchoolId = 42,
        StudentId = 17,
        AcademicTermId = 4,
        RequestedByGuardianProfileId = 9,
        IdempotencyKey = "request-1",
        RequestedAt = Now.AddHours(-1),
        RequestedExitAt = Now.AddMinutes(30),
        Reason = "Reason",
        PickupPersonName = "Pickup",
        CurrentClassroomId = 12,
        CreatedByUserId = "guardian",
        UpdatedByUserId = "guardian"
    };

    private static TestCurrentUser CurrentUser(int schoolId, string role, string permission) =>
        new(schoolId, "actor", role, permission);

    private static AlFalahDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"gate-pass-{Guid.NewGuid()}")
            .Options;
        return new AlFalahDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestCurrentUser(
        int schoolId,
        string userId,
        string role,
        string permission) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? Username => userId;
        public int? ActiveSchoolId => schoolId;
        public string? PreferredLanguage => "en";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == role;
        public bool HasPermission(string permissionName) => permissionName == permission;
        public IEnumerable<string> GetRoles() => new[] { role };
        public IEnumerable<string> GetPermissions() => new[] { permission };
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class FakeRepository : IGatePassWorkflowRepository
    {
        public GuardianGatePassLinkSnapshot? Link { get; init; }
        public bool GuardianLinkIsActive { get; init; }
        public GatePassEnrollmentSnapshot? Enrollment { get; init; }
        public GatePassTimetableSnapshot? Timetable { get; init; }
        public GatePass? Tracked { get; init; }
        public GatePass? Added { get; private set; }
        public List<int> SchoolIds { get; } = new();
        public byte[]? ExpectedRowVersion { get; private set; }
        public int SaveCount { get; private set; }

        public Task<GuardianGatePassLinkSnapshot?> GetGuardianLinkAsync(
            int schoolId, string guardianUserId, int studentId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Link);
        }

        public Task<bool> IsGuardianLinkActiveAsync(
            int schoolId, int guardianProfileId, int studentId, DateOnly onDate,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(GuardianLinkIsActive);
        }

        public Task<GatePassEnrollmentSnapshot?> GetActiveEnrollmentAsync(
            int schoolId, int studentId, DateOnly onDate, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Enrollment);
        }

        public Task<GatePassDto?> GetByIdempotencyKeyAsync(
            int schoolId, int guardianProfileId, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult<GatePassDto?>(null);
        }

        public Task<bool> HasOverlappingActivePassAsync(
            int schoolId, int studentId, DateTimeOffset windowStartsAt,
            DateTimeOffset windowEndsAt, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(false);
        }

        public Task<GatePass?> GetForUpdateAsync(
            int schoolId, int gatePassId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Tracked);
        }

        public Task<GatePassTimetableSnapshot?> ResolvePublishedTimetableAsync(
            int schoolId, int academicYearId, TimetableSemester semester, int classroomId,
            string classroomLabel, TimetableDay day, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Timetable);
        }

        public void Add(GatePass gatePass)
        {
            gatePass.Id = 100;
            gatePass.RowVersion = new byte[] { 9 };
            Added = gatePass;
        }

        public void SetExpectedRowVersion(GatePass gatePass, byte[] rowVersion) =>
            ExpectedRowVersion = rowVersion;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }

        public Task<GatePassDto?> GetDtoAsync(
            int schoolId, int gatePassId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            var gatePass = Added ?? Tracked;
            if (gatePass is null) return Task.FromResult<GatePassDto?>(null);
            return Task.FromResult<GatePassDto?>(new GatePassDto(
                gatePass.Id,
                new StudentSummaryDto(gatePass.StudentId, "S-1", "Student", gatePass.CurrentClassroomId,
                    "1/A", true, null),
                gatePass.RequestedAt,
                gatePass.RequestedExitAt,
                gatePass.Reason,
                new PickupPersonDto(gatePass.PickupPersonName, null, null),
                gatePass.Status,
                gatePass.ApprovedWindowStartsAt,
                gatePass.ApprovedWindowEndsAt,
                gatePass.ReviewedAt,
                gatePass.ExitedAt,
                null,
                null,
                Array.Empty<NotificationDeliveryDto>(),
                Convert.ToBase64String(gatePass.RowVersion)));
        }
    }
}
