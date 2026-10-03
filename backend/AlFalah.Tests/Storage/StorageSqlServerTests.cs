using System.Data;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.Storage;

public sealed class StorageSqlFactAttribute : FactAttribute
{
    public StorageSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALFALAH_STORAGE_TEST_CONNECTION")))
            Skip = "Set ALFALAH_STORAGE_TEST_CONNECTION to an isolated local database named AlFalahS1Tests_*";
    }
}

public sealed class StorageSqlTheoryAttribute : TheoryAttribute
{
    public StorageSqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALFALAH_STORAGE_TEST_CONNECTION")))
            Skip = "Set ALFALAH_STORAGE_TEST_CONNECTION to an isolated local database named AlFalahS1Tests_*";
    }
}

[CollectionDefinition("Storage SQL", DisableParallelization = true)]
public sealed class StorageSqlCollection : ICollectionFixture<StorageSqlFixture>;

public sealed class StorageSqlFixture : IAsyncLifetime
{
    public bool LegacyPreserved { get; private set; }
    public bool CredentialPreserved { get; private set; }
    public string Connection { get; private set; } = "";

    public AlFalahDbContext CreateContext() => new(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(Connection).Options);

    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("ALFALAH_STORAGE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var b = new SqlConnectionStringBuilder(connection);
        if (!b.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) ||
            !b.InitialCatalog.StartsWith("AlFalahS1Tests_", StringComparison.Ordinal) || !b.IntegratedSecurity)
            throw new InvalidOperationException("Storage SQL tests require an explicitly isolated LocalDB database.");
        Connection = b.ConnectionString;
        await using var db = CreateContext();
        // Test databases are fresh; no reset/delete command ever targets an existing database.
        await db.GetService<IMigrator>().MigrateAsync("20260929185322_AddW8SocialWorkerCaseWorkflow");
        if (await db.TeacherEvidenceSubmissions.AnyAsync())
            throw new InvalidOperationException("Choose a fresh AlFalahS1Tests_* database for migration verification.");
        db.Users.AddRange(new ApplicationUser { Id = TeacherDriveHarness.ManagerUserId, UserName = "manager" },
            new ApplicationUser { Id = TeacherDriveHarness.TeacherAUserId, UserName = "teacher-a" });
        db.Roles.AddRange(new ApplicationRole { Id = "manager", Name = RoleNames.SchoolManager },
            new ApplicationRole { Id = "teacher", Name = RoleNames.Instructor });
        var school = new School { Name = "مدرسة اختبار", City = "الرياض", Stage = SchoolStage.Primary, ManagerUserId = TeacherDriveHarness.ManagerUserId };
        db.Schools.Add(school);
        db.Schools.Add(new School { Name = "مدرسة أخرى", City = "الرياض", Stage = SchoolStage.Primary });
        var year = new AcademicYear { Code = "S1", NameAr = "سنة اختبار", IsActive = true };
        var task = new EvidenceTask { Code = "S1-TASK", NameAr = "مهمة اختبار", Category = "اختبار", IsActive = true };
        db.AddRange(year, task);
        await db.SaveChangesAsync();
        var teacher = new InstructorProfile { UserId = TeacherDriveHarness.TeacherAUserId, SchoolId = school.Id };
        db.InstructorProfiles.Add(teacher);
        db.UserSchoolRoles.AddRange(new UserSchoolRole { SchoolId = school.Id, UserId = TeacherDriveHarness.ManagerUserId, RoleId = "manager" },
            new UserSchoolRole { SchoolId = school.Id, UserId = TeacherDriveHarness.TeacherAUserId, RoleId = "teacher" });
        await db.SaveChangesAsync();
        db.TeacherEvidenceSubmissions.Add(new TeacherEvidenceSubmission
        {
            SchoolId = school.Id, TeacherId = teacher.Id, TaskId = task.Id, AcademicYearId = year.Id,
            DriveId = "test-drive", DriveItemId = "legacy-item", ParentItemId = "teacher-root", FileName = "شاهد.pdf",
            FileExtension = ".pdf", MimeType = "application/pdf", SizeInBytes = 25,
            UploadStatus = EvidenceUploadStatus.Completed, ReviewStatus = EvidenceReviewStatus.Approved,
            ReviewedByUserId = TeacherDriveHarness.ManagerUserId, ReviewedAtUtc = DateTimeOffset.Parse("2026-10-02T10:00:00Z"),
            ReviewNote = "مراجعة أصلية", UploadedAtUtc = DateTimeOffset.Parse("2026-10-01T10:00:00Z")
        });
        db.SchoolGoogleDrives.Add(new SchoolGoogleDrive { SchoolId = school.Id, ProtectedCredential = "ciphertext-sentinel", RootFolderId = "root" });
        await db.SaveChangesAsync();
        var rowBefore = await db.TeacherEvidenceSubmissions.AsNoTracking().SingleAsync();
        var credentialBefore = await db.SchoolGoogleDrives.Select(x => x.ProtectedCredential).SingleAsync();
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var rowAfter = await db.TeacherEvidenceSubmissions.AsNoTracking().SingleAsync();
        LegacyPreserved = rowBefore.ReviewNote == rowAfter.ReviewNote && rowBefore.DriveItemId == rowAfter.DriveItemId &&
            rowBefore.ReviewedAtUtc == rowAfter.ReviewedAtUtc && rowBefore.UploadedAtUtc == rowAfter.UploadedAtUtc;
        CredentialPreserved = credentialBefore == await db.SchoolGoogleDrives.Select(x => x.ProtectedCredential).SingleAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask; // Kept for inspection; never delete test/operational data implicitly.
}

[Collection("Storage SQL")]
public sealed class StorageSqlServerTests(StorageSqlFixture fixture)
{
    [StorageSqlFact]
    public async Task Migration_preserves_legacy_and_credentials_and_seeds_only_manager_and_own_permissions()
    {
        fixture.LegacyPreserved.Should().BeTrue(); fixture.CredentialPreserved.Should().BeTrue();
        await using var db = fixture.CreateContext();
        (await db.Permissions.CountAsync(x => x.Group == "Storage")).Should().Be(8);
        (await db.RolePermissions.CountAsync(x => x.RoleId == "manager" && x.Permission.Group == "Storage")).Should().Be(8);
        (await db.RolePermissions.CountAsync(x => x.RoleId == "teacher" && x.Permission.Group == "Storage")).Should().Be(2);
        (await db.UserSchoolRoles.CountAsync()).Should().Be(2);
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        db.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [StorageSqlFact]
    public async Task DryRun_and_two_backfills_keep_exact_Drive_and_review_metadata_and_row_counts()
    {
        await using var db = fixture.CreateContext();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var service = new StorageBackfillService(new StorageBackfillRepository(db));
        var dry = await service.RunAsync();
        dry.CreatedFiles.Should().Be(1); (await db.StoredFiles.CountAsync()).Should().Be(0);
        var first = await service.RunAsync(false); first.Issues.Should().BeEmpty();
        var second = await service.RunAsync(false); second.ExistingCount.Should().Be(1); second.CreatedFiles.Should().Be(0); second.Issues.Should().BeEmpty();
        var original = await db.TeacherEvidenceSubmissions.SingleAsync();
        var file = await db.StoredFiles.SingleAsync(); file.CurrentVersionId.Should().NotBeNull();
        var version = await db.StoredFileVersions.SingleAsync(); version.DriveItemId.Should().Be(original.DriveItemId);
        version.UploadedAtUtc.Should().Be(original.UploadedAtUtc); version.Availability.Should().Be(StoredFileAvailability.Unverified);
        var decision = await db.EvidenceReviewDecisions.SingleAsync(); decision.Note.Should().Be(original.ReviewNote);
        decision.ReviewedAtUtc.Should().Be(original.ReviewedAtUtc); decision.ReviewedByUserId.Should().Be(original.ReviewedByUserId);
        (await db.EvidenceLinks.SingleAsync()).Status.Should().Be(EvidenceLinkStatus.Approved);
    }

    [StorageSqlTheory]
    [InlineData("folder")]
    [InlineData("file")]
    [InlineData("version")]
    [InlineData("requirement-school")]
    [InlineData("requirement-year")]
    [InlineData("requirement-template")]
    [InlineData("current-version-owner")]
    [InlineData("link-version-owner")]
    [InlineData("decision-version-owner")]
    public async Task Cross_school_file_version_and_requirement_scope_are_rejected_by_SQL(string boundary)
    {
        await using var db = fixture.CreateContext();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await new StorageBackfillService(new StorageBackfillRepository(db)).RunAsync(false);
        var file = await db.StoredFiles.SingleAsync();
        var folder = await db.StorageFolders.SingleAsync();
        var link = await db.EvidenceLinks.SingleAsync();
        var foreignSchool = await db.Schools.Where(x => x.Id != file.SchoolId).Select(x => x.Id).SingleAsync();
        Func<Task> act;
        switch (boundary)
        {
            case "folder":
                act = async () =>
                {
                    db.StorageFolders.Add(new StorageFolder { SchoolId = foreignSchool, ParentFolderId = folder.Id, Kind = StorageFolderKind.SchoolLibrary, DisplayName = "خارج النطاق", DriveItemId = "foreign" });
                    await db.SaveChangesAsync();
                }; break;
            case "file":
                act = async () =>
                {
                    db.StoredFiles.Add(new StoredFile { SchoolId = foreignSchool, FolderId = folder.Id, DisplayName = "خارج النطاق", SourceKind = StoredFileSourceKind.SchoolUpload });
                    await db.SaveChangesAsync();
                }; break;
            case "version":
                act = async () =>
                {
                    db.StoredFileVersions.Add(new StoredFileVersion { SchoolId = foreignSchool, StoredFileId = file.Id, VersionNumber = 1, DriveItemId = "foreign", Availability = StoredFileAvailability.Unverified });
                    await db.SaveChangesAsync();
                }; break;
            case "current-version-owner":
            case "link-version-owner":
            case "decision-version-owner":
                var another = new StoredFile { SchoolId = file.SchoolId, FolderId = folder.Id, DisplayName = "ملف آخر", SourceKind = StoredFileSourceKind.SchoolUpload };
                db.StoredFiles.Add(another); await db.SaveChangesAsync();
                var anotherVersion = new StoredFileVersion { SchoolId = file.SchoolId, StoredFileId = another.Id, VersionNumber = 1, DriveItemId = "other-version", Availability = StoredFileAvailability.Unverified };
                db.StoredFileVersions.Add(anotherVersion); await db.SaveChangesAsync();
                if (boundary == "current-version-owner")
                    act = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StoredFiles SET CurrentVersionId={anotherVersion.Id} WHERE Id={file.Id}");
                else if (boundary == "link-version-owner")
                    act = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE EvidenceLinks SET VersionId={anotherVersion.Id} WHERE Id={link.Id}");
                else
                {
                    var decision = await db.EvidenceReviewDecisions.SingleAsync();
                    // A new decision cannot point to another file's version either.
                    act = async () =>
                    {
                        db.EvidenceReviewDecisions.Add(new EvidenceReviewDecision { SchoolId = file.SchoolId, StoredFileId = file.Id,
                            EvidenceLinkId = link.Id, VersionId = anotherVersion.Id, Decision = EvidenceReviewStatus.Approved, IsLegacyImported = true });
                        await db.SaveChangesAsync();
                    };
                }
                break;
            default:
                var requirement = new EvidenceRequirement { Code = "OTHER", DisplayName = "متطلب", SchoolId = boundary == "requirement-school" ? foreignSchool : file.SchoolId,
                    AcademicYearId = boundary == "requirement-template" ? null : link.AcademicYearId };
                if (boundary == "requirement-year")
                {
                    var year = new AcademicYear { Code = "OTHER", NameAr = "سنة أخرى" }; db.AcademicYears.Add(year); await db.SaveChangesAsync(); requirement.AcademicYearId = year.Id;
                }
                db.EvidenceRequirements.Add(requirement); await db.SaveChangesAsync();
                act = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE EvidenceLinks SET RequirementId={requirement.Id} WHERE Id={link.Id}"); break;
        }
        await act.Should().ThrowAsync<Exception>().Where(x => x is DbUpdateException || x is SqlException);
    }

    [StorageSqlTheory]
    [InlineData(null, null)]
    [InlineData(1, null)]
    [InlineData(null, 1)]
    [InlineData(1, 1)]
    public async Task Nullable_template_scopes_have_real_unique_indexes(int? schoolId, int? yearId)
    {
        await using var db = fixture.CreateContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        db.EvidenceRequirements.Add(new EvidenceRequirement { SchoolId = schoolId, AcademicYearId = yearId, Code = "NULL-UNIQUE", DisplayName = "متطلب" });
        await db.SaveChangesAsync();
        db.EvidenceRequirements.Add(new EvidenceRequirement { SchoolId = schoolId, AcademicYearId = yearId, Code = "NULL-UNIQUE", DisplayName = "متطلب مكرر" });
        await db.Invoking(x => x.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    [StorageSqlFact]
    public async Task Nullable_teacher_active_links_are_unique_but_inactive_history_can_repeat()
    {
        await using var db = fixture.CreateContext();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await new StorageBackfillService(new StorageBackfillRepository(db)).RunAsync(false);
        var folder = await db.StorageFolders.SingleAsync(); var requirement = await db.EvidenceRequirements.SingleAsync();
        var file = new StoredFile { SchoolId = folder.SchoolId, FolderId = folder.Id, SourceKind = StoredFileSourceKind.SchoolUpload, DisplayName = "عام" };
        db.StoredFiles.Add(file); await db.SaveChangesAsync();
        var version = new StoredFileVersion { SchoolId = file.SchoolId, StoredFileId = file.Id, VersionNumber = 1, DriveId = "test-drive", DriveItemId = "school-file", Availability = StoredFileAvailability.Unverified };
        db.StoredFileVersions.Add(version); await db.SaveChangesAsync();
        EvidenceLink NewLink(bool active) => new() { SchoolId = file.SchoolId, AcademicYearId = requirement.AcademicYearId!.Value, StoredFileId = file.Id, VersionId = version.Id,
            RequirementId = requirement.Id, Status = EvidenceLinkStatus.Draft, IsActive = active };
        db.EvidenceLinks.AddRange(NewLink(false), NewLink(false), NewLink(true)); await db.SaveChangesAsync();
        db.EvidenceLinks.Add(NewLink(true));
        await db.Invoking(x => x.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    [StorageSqlTheory]
    [InlineData("cycle")]
    [InlineData("decision")]
    [InlineData("version")]
    [InlineData("provenance")]
    public async Task SQL_rejects_cycles_and_history_rewrites_even_without_the_application(string boundary)
    {
        await using var db = fixture.CreateContext();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await new StorageBackfillService(new StorageBackfillRepository(db)).RunAsync(false);
        Func<Task> act;
        if (boundary == "cycle")
        {
            var parent = await db.StorageFolders.SingleAsync();
            var child = new StorageFolder { SchoolId = parent.SchoolId, ParentFolderId = parent.Id, Kind = StorageFolderKind.Teacher, DisplayName = "فرعي", DriveItemId = "child" };
            db.StorageFolders.Add(child); await db.SaveChangesAsync();
            act = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StorageFolders SET ParentFolderId={child.Id} WHERE Id={parent.Id}");
        }
        else if (boundary == "decision") act = () => db.Database.ExecuteSqlRawAsync("UPDATE EvidenceReviewDecisions SET Note=N'changed'");
        else if (boundary == "version") act = () => db.Database.ExecuteSqlRawAsync("UPDATE StoredFileVersions SET DriveItemId=N'changed'");
        else act = () => db.Database.ExecuteSqlRawAsync("UPDATE StoredFiles SET LegacyProvenanceJson=N'changed'");
        await act.Should().ThrowAsync<SqlException>();
    }

    [StorageSqlFact]
    public async Task Stale_rowversion_cannot_revoke_and_the_audit_is_atomic()
    {
        await using var db = fixture.CreateContext();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var schoolId = await db.Schools.Where(x => x.ManagerUserId == TeacherDriveHarness.ManagerUserId).Select(x => x.Id).SingleAsync();
        var user = TeacherDriveHarness.Manager(schoolId);
        var repo = new StorageRepository(db);
        var auth = new StorageAuthorizationService(repo, user, new NoDriveBoundary(), TimeProvider.System);
        var service = new StorageDelegationService(repo, auth, user, TimeProvider.System, Options.Create(new StorageOptions { AdministrationEnabled = true }));
        var grant = await service.GrantAsync(new(TeacherDriveHarness.TeacherAUserId, DateTimeOffset.UtcNow, null, "تكليف"));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StorageDelegations SET Reason=N'changed' WHERE Id={grant.Id}");
        db.ChangeTracker.Clear();
        await service.Invoking(x => x.RevokeAsync(grant.Id, new("سحب", grant.RowVersion))).Should().ThrowAsync<StorageConflictException>();
        db.ChangeTracker.Clear();
        (await db.StorageDelegations.SingleAsync(x => x.Id == grant.Id)).RevokedAt.Should().BeNull();
        (await db.AuditLogs.CountAsync(x => x.Action == "Storage.DelegationRevoked")).Should().Be(0);
    }

    private sealed class NoDriveBoundary : IStorageDriveBoundary
    {
        public Task EnsureWithinAsync(int schoolId, StorageDriveRoot root, string itemId, CancellationToken ct) =>
            throw new InvalidOperationException("Delegation must never contact Drive.");
    }

    [StorageSqlFact]
    public async Task Moving_a_teacher_denies_old_school_access_without_rewriting_the_old_file_or_review()
    {
        await using var db = fixture.CreateContext();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await new StorageBackfillService(new StorageBackfillRepository(db)).RunAsync(false);
        var file = await db.StoredFiles.SingleAsync();
        var foreignSchool = await db.Schools.Where(x => x.Id != file.SchoolId).Select(x => x.Id).SingleAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE InstructorProfiles SET SchoolId={foreignSchool} WHERE Id={file.OwnerTeacherId}");
        db.ChangeTracker.Clear();
        var user = TeacherDriveHarness.TeacherA(file.SchoolId);
        var authorization = new StorageAuthorizationService(new StorageRepository(db), user, new NoDriveBoundary(), TimeProvider.System);
        await authorization.Invoking(x => x.RequireFileAsync(file.SchoolId, file.Id))
            .Should().ThrowAsync<AlFalah.Application.Common.UnauthorizedSchoolAccessException>();
        (await db.StoredFiles.SingleAsync()).SchoolId.Should().Be(file.SchoolId);
        (await db.EvidenceReviewDecisions.SingleAsync()).Note.Should().Be("مراجعة أصلية");
    }

    [StorageSqlFact]
    public async Task Concurrent_overlapping_grants_commit_one_delegation_and_one_audit()
    {
        string granteeId = Guid.NewGuid().ToString("N");
        int schoolId;
        await using (var setup = fixture.CreateContext())
        {
            schoolId = await setup.Schools.Where(x => x.ManagerUserId == TeacherDriveHarness.ManagerUserId).Select(x => x.Id).SingleAsync();
            setup.Users.Add(new ApplicationUser { Id = granteeId, UserName = granteeId });
            setup.UserSchoolRoles.Add(new UserSchoolRole { UserId = granteeId, SchoolId = schoolId, RoleId = "teacher" });
            await setup.SaveChangesAsync();
        }
        var startsAt = DateTimeOffset.UtcNow;
        async Task<bool> Grant()
        {
            await using var context = fixture.CreateContext();
            var user = TeacherDriveHarness.Manager(schoolId);
            var repo = new StorageRepository(context);
            var auth = new StorageAuthorizationService(repo, user, new NoDriveBoundary(), TimeProvider.System);
            var service = new StorageDelegationService(repo, auth, user, TimeProvider.System,
                Options.Create(new StorageOptions { AdministrationEnabled = true }));
            try { await service.GrantAsync(new(granteeId, startsAt, null, "تكليف متزامن")); return true; }
            catch (StorageConflictException) { return false; }
        }
        var results = await Task.WhenAll(Grant(), Grant());
        results.Count(x => x).Should().Be(1);
        await using var db = fixture.CreateContext();
        (await db.StorageDelegations.CountAsync(x => x.GranteeUserId == granteeId)).Should().Be(1);
        var id = await db.StorageDelegations.Where(x => x.GranteeUserId == granteeId).Select(x => x.Id).SingleAsync();
        (await db.AuditLogs.CountAsync(x => x.Action == "Storage.DelegationGranted" && x.EntityId == id.ToString())).Should().Be(1);
    }

    [StorageSqlFact]
    public async Task A_failure_after_graph_persistence_rolls_back_the_whole_backfill()
    {
        await using var db = fixture.CreateContext();
        var service = new StorageBackfillService(new FailingBackfillRepository(new StorageBackfillRepository(db)));
        await service.Invoking(x => x.RunAsync(false)).Should().ThrowAsync<InvalidOperationException>();
        await using var verify = fixture.CreateContext();
        (await verify.StoredFiles.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await verify.StoredFileVersions.CountAsync()).Should().Be(0);
        (await verify.EvidenceLinks.CountAsync()).Should().Be(0);
        (await verify.EvidenceReviewDecisions.CountAsync()).Should().Be(0);
        (await verify.TeacherEvidenceSubmissions.CountAsync()).Should().Be(1);
    }

    private sealed class FailingBackfillRepository(IStorageBackfillRepository repository) : IStorageBackfillRepository
    {
        public Task<StorageBackfillInput> LoadAsync(CancellationToken ct) => repository.LoadAsync(ct);
        public Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct) => repository.InSerializableTransactionAsync(operation, ct);
        public async Task SaveGraphAsync(IReadOnlyList<StorageFolder> folders, IReadOnlyList<EvidenceRequirement> requirements,
            IReadOnlyList<StoredFile> files, IReadOnlyList<StoredFileVersion> versions, IReadOnlyList<EvidenceLink> links,
            IReadOnlyList<EvidenceReviewDecision> decisions, CancellationToken ct)
        {
            await repository.SaveGraphAsync(folders, requirements, files, versions, links, decisions, ct);
            throw new InvalidOperationException("Simulated failure before commit.");
        }
    }
}
