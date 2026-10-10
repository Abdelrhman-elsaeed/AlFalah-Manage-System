using System.Text.Json;
using AlFalah.Application.Common;
using AlFalah.Application.Common.Exceptions;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data.Seeders;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.Storage;

public sealed class StorageFoundationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DryRun_and_two_applies_preserve_the_original_file_and_review_without_Drive_writes()
    {
        await using var h = await Setup();
        var upload = await h.UploadAsync(TeacherDriveHarness.TeacherA(), 1);
        var legacy = await h.Context.TeacherEvidenceSubmissions.SingleAsync();
        legacy.ReviewStatus = EvidenceReviewStatus.Approved;
        legacy.ReviewedAtUtc = Now.AddDays(-1);
        legacy.ReviewedByUserId = TeacherDriveHarness.ManagerUserId;
        legacy.ReviewNote = "شاهد معتمد سابقًا";
        await h.Context.SaveChangesAsync();
        var service = Backfill(h);
        var dry = await service.RunAsync();
        dry.CreatedFiles.Should().Be(1);
        (await h.Context.StoredFiles.CountAsync()).Should().Be(0);
        h.Context.ChangeTracker.Clear();
        var first = await service.RunAsync(false);
        var second = await service.RunAsync(false);
        first.Issues.Should().BeEmpty();
        second.Issues.Should().BeEmpty();
        second.ExistingCount.Should().Be(1);
        second.CreatedFiles.Should().Be(0);
        (await h.Context.StoredFileVersions.CountAsync()).Should().Be(1);
        var version = await h.Context.StoredFileVersions.SingleAsync();
        version.DriveItemId.Should().Be(legacy.DriveItemId);
        version.DriveId.Should().Be(legacy.DriveId);
        version.Availability.Should().Be(StoredFileAvailability.Unverified);
        version.SHA256.Should().BeNull();
        version.UploadedByUserId.Should().BeNull();
        version.UploadedAtUtc.Should().Be(legacy.UploadedAtUtc);
        var decision = await h.Context.EvidenceReviewDecisions.SingleAsync();
        decision.Decision.Should().Be(EvidenceReviewStatus.Approved);
        decision.Note.Should().Be(legacy.ReviewNote);
        decision.ReviewedAtUtc.Should().Be(legacy.ReviewedAtUtc);
        decision.ReviewedByUserId.Should().Be(legacy.ReviewedByUserId);
        (await h.Context.EvidenceLinks.SingleAsync()).Status.Should().Be(EvidenceLinkStatus.Approved);
        h.Drive.Uploads.Should().HaveCount(1);
        h.Drive.Trashed.Should().BeEmpty();
        (await h.Context.TeacherEvidenceSubmissions.SingleAsync()).ReviewStatus.Should().Be(legacy.ReviewStatus);
        upload.SubmissionId.Should().Be(legacy.Id);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Deleted_and_missing_files_retain_status_dates_and_historical_review(bool deleted, bool missing)
    {
        await using var h = await Setup();
        await h.UploadAsync(TeacherDriveHarness.TeacherA(), 1);
        var row = await h.Context.TeacherEvidenceSubmissions.SingleAsync();
        row.IsDeleted = deleted; row.DeletedAtUtc = deleted ? Now : null; row.DeletedByUserId = deleted ? TeacherDriveHarness.TeacherAUserId : null;
        row.IsMissingFromDrive = missing; row.MissingFromDriveAtUtc = missing ? Now : null;
        row.ReviewStatus = EvidenceReviewStatus.Rejected; row.ReviewNote = "مراجعة قديمة";
        await h.Context.SaveChangesAsync(); h.Context.ChangeTracker.Clear();
        await Backfill(h).RunAsync(false);
        var file = await h.Context.StoredFiles.IgnoreQueryFilters().SingleAsync();
        file.IsDeleted.Should().Be(deleted); file.DeletedAtUtc.Should().Be(row.DeletedAtUtc);
        var snapshot = JsonSerializer.Deserialize<LegacyStorageSubmission>(file.LegacyProvenanceJson!)!;
        snapshot.IsMissingFromDrive.Should().Be(missing);
        snapshot.MissingFromDriveAtUtc.Should().Be(row.MissingFromDriveAtUtc);
        (await h.Context.EvidenceLinks.SingleAsync()).IsActive.Should().Be(!deleted);
        (await h.Context.EvidenceReviewDecisions.SingleAsync()).Decision.Should().Be(EvidenceReviewStatus.Rejected);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(1, null)]
    [InlineData(null, 1)]
    public async Task Unlinked_legacy_is_a_file_with_provenance_and_never_a_fabricated_evidence_link(int? task, int? year)
    {
        await using var h = await Setup();
        await h.UploadAsync(TeacherDriveHarness.TeacherA(), 1);
        var row = await h.Context.TeacherEvidenceSubmissions.SingleAsync();
        row.TaskId = task; row.AcademicYearId = year; row.ReviewStatus = EvidenceReviewStatus.Approved;
        row.ReviewNote = "قرار موروث بلا سياق كامل";
        await h.Context.SaveChangesAsync(); h.Context.ChangeTracker.Clear();
        var result = await Backfill(h).RunAsync(false);
        result.NeedsLinkCount.Should().Be(1);
        result.CreatedLinks.Should().Be(0);
        result.CreatedDecisions.Should().Be(0);
        var file = await h.Context.StoredFiles.SingleAsync();
        file.NeedsLink.Should().BeTrue();
        JsonSerializer.Deserialize<LegacyStorageSubmission>(file.LegacyProvenanceJson!)!.ReviewNote.Should().Be(row.ReviewNote);
        (await Backfill(h).RunAsync(false)).Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Source_drift_is_reported_and_the_original_approved_snapshot_is_never_overwritten()
    {
        await using var h = await Setup();
        await h.UploadAsync(TeacherDriveHarness.TeacherA(), 1);
        await Backfill(h).RunAsync(false);
        var row = await h.Context.TeacherEvidenceSubmissions.SingleAsync();
        row.ReviewStatus = EvidenceReviewStatus.Approved;
        await h.Context.SaveChangesAsync(); h.Context.ChangeTracker.Clear();
        var report = await Backfill(h).RunAsync(false);
        report.Issues.Should().ContainSingle(x => x.Code == "LegacyOrTargetDrift");
        (await h.Context.EvidenceLinks.SingleAsync()).Status.Should().Be(EvidenceLinkStatus.PendingReview);
    }

    [Fact]
    public async Task Invalid_legacy_row_is_accounted_for_as_an_explicit_exception()
    {
        await using var h = await Setup();
        await h.UploadAsync(TeacherDriveHarness.TeacherA(), 1);
        var row = await h.Context.TeacherEvidenceSubmissions.SingleAsync(); row.ParentItemId = "";
        await h.Context.SaveChangesAsync(); h.Context.ChangeTracker.Clear();
        var result = await Backfill(h).RunAsync(false);
        result.Issues.Should().ContainSingle(x => x.SubmissionId == row.Id && x.Code == "InvalidDriveMetadata");
        result.Groups.Sum(x => x.Exceptions + x.PlannedFiles + x.Existing).Should().Be(result.SourceCount);
        (await h.Context.StoredFiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Teacher_can_access_own_file_but_cannot_access_a_colleague_or_another_school()
    {
        await using var h = await Setup();
        await h.UploadAsync(TeacherDriveHarness.TeacherA(), 1, requestId: "a");
        await h.UploadAsync(TeacherDriveHarness.TeacherB(), 1, requestId: "b");
        await Backfill(h).RunAsync(false);
        var own = await h.Context.StoredFiles.SingleAsync(x => x.OwnerTeacherId == 1);
        var other = await h.Context.StoredFiles.SingleAsync(x => x.OwnerTeacherId == 2);
        var service = Authorization(h, TeacherDriveHarness.TeacherA());
        (await service.RequireFileAsync(1, own.Id)).Id.Should().Be(own.Id);
        await service.Invoking(x => x.RequireFileAsync(1, other.Id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await service.Invoking(x => x.RequireFileAsync(2, own.Id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }

    [Fact]
    public async Task Teacher_grant_revocation_move_or_external_Drive_move_denies_access_immediately()
    {
        await using var h = await Setup();
        await h.UploadAsync(TeacherDriveHarness.TeacherA(), 1);
        await Backfill(h).RunAsync(false);
        var file = await h.Context.StoredFiles.SingleAsync();
        var version = await h.Context.StoredFileVersions.SingleAsync();
        var service = Authorization(h, TeacherDriveHarness.TeacherA());
        h.Drive.MoveExternally(version.DriveItemId, TeacherDriveHarness.FolderB);
        await service.Invoking(x => x.RequireFileAsync(1, file.Id)).Should().ThrowAsync<TeacherDriveAccessDeniedException>();
        h.Drive.MoveExternally(version.DriveItemId, TeacherDriveHarness.FolderA);
        var grant = await h.Context.TeacherDriveFolders.SingleAsync(x => x.TeacherId == 1); grant.IsActive = false;
        await h.Context.SaveChangesAsync();
        await service.Invoking(x => x.RequireFileAsync(1, file.Id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        grant.IsActive = true;
        (await h.Context.UserSchoolRoles.SingleAsync(x => x.UserId == TeacherDriveHarness.TeacherAUserId)).SchoolId = 2;
        await h.Context.SaveChangesAsync();
        await service.Invoking(x => x.RequireFileAsync(1, file.Id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }

    [Fact]
    public async Task Only_actual_manager_can_grant_or_revoke_even_with_forged_permissions()
    {
        await using var h = await Setup();
        var manager = Delegations(h, TeacherDriveHarness.Manager());
        var grant = await manager.GrantAsync(new(TeacherDriveHarness.TeacherAUserId, Now, Now.AddDays(1), "إدارة الملفات"));
        var forged = new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager, TeacherDriveHarness.TeacherAUserId, 1, true);
        await Delegations(h, forged).Invoking(x => x.GrantAsync(new(TeacherDriveHarness.TeacherBUserId, Now, null, "تفويض متسلسل")))
            .Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        var row = await h.Context.StorageDelegations.SingleAsync(); row.RowVersion = new byte[8]; await h.Context.SaveChangesAsync();
        await Delegations(h, forged).Invoking(x => x.RevokeAsync(grant.Id, new("سحب", Convert.ToBase64String(row.RowVersion))))
            .Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await Authorization(h, forged).Invoking(x => x.RequireSchoolPermissionAsync(1, PermissionNames.StorageDelegate))
            .Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }

    [Fact]
    public async Task Delegation_candidates_are_active_school_members_grouped_by_their_school_roles()
    {
        await using var h = await Setup();
        var manager = Delegations(h, TeacherDriveHarness.Manager());
        var candidates = await manager.CandidatesAsync();
        candidates.Select(x => x.UserId).Should().BeEquivalentTo(
            TeacherDriveHarness.TeacherAUserId, TeacherDriveHarness.TeacherBUserId);
        candidates.Should().OnlyContain(x => x.Roles.Contains(RoleNames.Instructor));

        (await h.Context.Users.SingleAsync(x => x.Id == TeacherDriveHarness.TeacherAUserId)).IsActive = false;
        (await h.Context.UserSchoolRoles.SingleAsync(x => x.UserId == TeacherDriveHarness.TeacherBUserId)).SchoolId = 2;
        await h.Context.SaveChangesAsync();
        (await manager.CandidatesAsync()).Should().BeEmpty();

        var forged = new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,
            TeacherDriveHarness.TeacherAUserId, 1, true);
        await Delegations(h, forged).Invoking(x => x.CandidatesAsync())
            .Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }

    [Fact]
    public async Task Delegation_revocation_expiration_and_membership_removal_revoke_operational_access()
    {
        await using var h = await Setup();
        var manager = Delegations(h, TeacherDriveHarness.Manager());
        var created = await manager.GrantAsync(new(TeacherDriveHarness.TeacherAUserId, Now, Now.AddHours(1), "إدارة الملفات"));
        var delegateAuth = Authorization(h, TeacherDriveHarness.TeacherA());
        await delegateAuth.RequireSchoolPermissionAsync(1, PermissionNames.StorageManageSchool);
        await Authorization(h, TeacherDriveHarness.TeacherA(), Now.AddHours(1)).Invoking(x => x.RequireSchoolPermissionAsync(1, PermissionNames.StorageManageSchool))
            .Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        var membership = await h.Context.UserSchoolRoles.SingleAsync(x => x.UserId == TeacherDriveHarness.TeacherAUserId);
        membership.IsActive = false; await h.Context.SaveChangesAsync();
        await delegateAuth.Invoking(x => x.RequireSchoolPermissionAsync(1, PermissionNames.StorageManageSchool)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        membership.IsActive = true; await h.Context.SaveChangesAsync();
        var row = await h.Context.StorageDelegations.SingleAsync(); row.RowVersion = new byte[8]; await h.Context.SaveChangesAsync();
        await manager.RevokeAsync(created.Id, new("انتهاء التكليف", Convert.ToBase64String(row.RowVersion)));
        await delegateAuth.Invoking(x => x.RequireSchoolPermissionAsync(1, PermissionNames.StorageManageSchool)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        (await h.Context.AuditLogs.CountAsync(x => x.Action == "Storage.DelegationRevoked")).Should().Be(1);
    }

    [Fact]
    public async Task New_manager_and_inactive_or_foreign_grantees_are_checked_from_database_each_time()
    {
        await using var h = await Setup();
        var manager = Delegations(h, TeacherDriveHarness.Manager());
        await manager.Invoking(x => x.GrantAsync(new("unassigned", Now, null, "سبب"))).Should().ThrowAsync<ArgumentException>();
        (await h.Context.Users.SingleAsync(x => x.Id == TeacherDriveHarness.TeacherAUserId)).IsActive = false;
        await h.Context.SaveChangesAsync();
        await manager.Invoking(x => x.GrantAsync(new(TeacherDriveHarness.TeacherAUserId, Now, null, "سبب"))).Should().ThrowAsync<ArgumentException>();
        (await h.Context.Schools.SingleAsync(x => x.Id == 1)).ManagerUserId = TeacherDriveHarness.TeacherBUserId;
        await h.Context.SaveChangesAsync();
        await manager.Invoking(x => x.ListAsync()).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }

    [Fact]
    public async Task Overlapping_dates_are_rejected_and_the_workspace_is_disabled_by_default()
    {
        await using var h = await Setup();
        var manager = Delegations(h, TeacherDriveHarness.Manager());
        await manager.GrantAsync(new(TeacherDriveHarness.TeacherAUserId, Now, Now.AddDays(1), "سبب"));
        await manager.Invoking(x => x.GrantAsync(new(TeacherDriveHarness.TeacherAUserId, Now.AddHours(1), null, "سبب")))
            .Should().ThrowAsync<StorageConflictException>();
        await manager.GrantAsync(new(TeacherDriveHarness.TeacherAUserId, Now.AddDays(1), null, "تكليف لاحق"));
        var closed = new StorageDelegationService(new StorageRepository(h.Context), Authorization(h, TeacherDriveHarness.Manager()),
            TeacherDriveHarness.Manager(), new FixedTime(Now), Options.Create(new StorageOptions()));
        await closed.Invoking(x => x.ListAsync()).Should().ThrowAsync<KeyNotFoundException>();
    }

    private static async Task<TeacherDriveHarness> Setup()
    {
        var h = await TeacherDriveHarness.CreateAsync();
        (await h.Context.Schools.SingleAsync(x => x.Id == 1)).ManagerUserId = TeacherDriveHarness.ManagerUserId;
        var managerRole = new ApplicationRole { Id = "storage-manager", Name = RoleNames.SchoolManager };
        var teacherRole = new ApplicationRole { Id = "storage-teacher", Name = RoleNames.Instructor };
        h.Context.Roles.AddRange(managerRole, teacherRole);
        foreach (var p in StoragePermissionCatalog.All)
        {
            var permission = new Permission { Name = p.Name, Group = p.Group };
            h.Context.Permissions.Add(permission);
            h.Context.RolePermissions.Add(new RolePermission { Role = managerRole, Permission = permission });
            if (p.Name is PermissionNames.StorageViewOwn or PermissionNames.StorageManageOwn)
                h.Context.RolePermissions.Add(new RolePermission { Role = teacherRole, Permission = permission });
        }
        h.Context.UserSchoolRoles.AddRange(
            new UserSchoolRole { SchoolId = 1, UserId = TeacherDriveHarness.ManagerUserId, Role = managerRole },
            new UserSchoolRole { SchoolId = 1, UserId = TeacherDriveHarness.TeacherAUserId, Role = teacherRole },
            new UserSchoolRole { SchoolId = 1, UserId = TeacherDriveHarness.TeacherBUserId, Role = teacherRole });
        await h.Context.SaveChangesAsync();
        return h;
    }

    [Fact]
    public async Task A_teacher_grant_moved_outside_the_school_root_is_denied_even_when_file_remains_in_the_grant()
    {
        await using var h = await Setup();
        await h.UploadAsync(TeacherDriveHarness.TeacherA(), 1);
        await Backfill(h).RunAsync(false);
        var file = await h.Context.StoredFiles.SingleAsync();
        h.Drive.MoveExternally(TeacherDriveHarness.FolderA, TeacherDriveHarness.OutsideRootFolderId);
        await Authorization(h, TeacherDriveHarness.TeacherA()).Invoking(x => x.RequireFileAsync(1, file.Id))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    private static StorageBackfillService Backfill(TeacherDriveHarness h) => new(new StorageBackfillRepository(h.Context));

    [Fact]
    public async Task A_requirement_code_collision_is_reported_without_creating_or_overwriting_a_file()
    {
        await using var h = await Setup();
        await h.UploadAsync(TeacherDriveHarness.TeacherA(), 1);
        var task = await h.Context.EvidenceTasks.SingleAsync(x => x.Id == 1);
        h.Context.EvidenceRequirements.Add(new EvidenceRequirement { SchoolId = 1, AcademicYearId = 1, Code = task.Code, DisplayName = "متطلب مستقل" });
        await h.Context.SaveChangesAsync(); h.Context.ChangeTracker.Clear();
        var report = await Backfill(h).RunAsync(false);
        report.Issues.Should().ContainSingle(x => x.Code == "ConflictingRequirementIdentity");
        report.CreatedFiles.Should().Be(0);
        (await h.Context.EvidenceRequirements.SingleAsync()).OriginalTaskId.Should().BeNull();
        (await h.Context.StorageFolders.CountAsync()).Should().Be(0);
    }
    private static StorageAuthorizationService Authorization(TeacherDriveHarness h, ICurrentUserService user, DateTimeOffset? now = null) =>
        new(new StorageRepository(h.Context), user, new StorageDriveBoundary(new TeacherDriveFolderGuard(h.Drive)), new FixedTime(now ?? Now));
    private static StorageDelegationService Delegations(TeacherDriveHarness h, ICurrentUserService user) =>
        new(new StorageRepository(h.Context), Authorization(h, user), user, new FixedTime(Now), Options.Create(new StorageOptions { AdministrationEnabled = true }));
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
