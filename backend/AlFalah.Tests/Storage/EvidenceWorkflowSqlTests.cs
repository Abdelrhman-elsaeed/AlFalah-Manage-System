using System.Diagnostics;
using System.Text.Json;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.TestDoubles;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace AlFalah.Tests.Storage;

[CollectionDefinition("Evidence workflow SQL", DisableParallelization = true)]
public sealed class EvidenceWorkflowSqlCollection : ICollectionFixture<EvidenceWorkflowSqlFixture>;
public sealed class EvidenceWorkflowSqlFixture : IAsyncLifetime
{
    private StorageSqlFixture inner = new();
    public string Connection => inner.Connection;
    public AlFalahDbContext Db(SaveChangesInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(Connection);
        if (interceptor != null) options.AddInterceptors(interceptor);
        return new(options.Options);
    }
    public async Task InitializeAsync()
    {
        var value = Environment.GetEnvironmentVariable("ALFALAH_STORAGE_TEST_CONNECTION");
        if (value != null) { var b = new SqlConnectionStringBuilder(value); b.InitialCatalog += "_Evidence"; inner = new(b.ConnectionString); }
        await inner.InitializeAsync();
        if (value != null)
        {
            await using var db = Db();
            foreach (var year in await db.AcademicYears.ToListAsync()) year.IsActive = year.Code == "S1";
            await db.SaveChangesAsync();
        }
    }
    public Task DisposeAsync() => Task.CompletedTask;
}

[Collection("Evidence workflow SQL")]
public sealed class EvidenceWorkflowSqlTests(EvidenceWorkflowSqlFixture fixture, ITestOutputHelper output)
{
    private sealed record World(int School, int Year, int Teacher, string TeacherUser, int OtherTeacher, string OtherUser, FakeGoogleDrive Drive);
    private sealed record Services(RequirementCatalogService Catalog, EvidenceLinkService Links, EvidenceReviewService Reviews,
        FileChangeRequestService Changes, StorageLibraryService Library, StorageEvidenceReadService Reads, EvidenceRepository Repo, EvidenceMatrixService Matrix);
    private Services Build(AlFalahDbContext db, World w, bool teacher = false, string? actor = null, bool enabled = true)
    {
        ICurrentUserService user = actor != null ? new TeacherDriveHarness.TestCurrentUser(RoleNames.Secretary, actor, w.School) : teacher
            ? new TeacherDriveHarness.TestCurrentUser(RoleNames.Instructor, w.TeacherUser, w.School) : TeacherDriveHarness.Manager(w.School);
        var scopes = new StorageRepository(db); var repo = new EvidenceRepository(db); var provider = new GoogleStorageProvider(w.Drive);
        var auth = new StorageAuthorizationService(scopes, user, new StorageDriveBoundary(new(w.Drive)), TimeProvider.System);
        var options = Options.Create(new StorageOptions { ReadModelEnabled = enabled });
        var context = new EvidenceWorkflowContext(repo, scopes, auth, user, provider, options);
        var audit = new AuditLogWriter(db, new HttpContextAccessor(), NullLogger<AuditLogWriter>.Instance);
        var submissions = new EvidenceSubmissionService(db, audit);
        var changes = new FileChangeRequestService(repo, scopes, context, provider);
        var library = new StorageLibraryService(new StorageLibraryRepository(db), scopes, auth, user, provider, submissions, options, changes);
        var reads = new StorageEvidenceReadService(repo, context);
        return new(new(repo, scopes, context), new(repo, scopes, context), new(repo, scopes, context), changes, library, reads, repo,
            new(db, user, new(db, user, NullLogger<SchoolScopeGuard>.Instance), audit, submissions, w.Drive, reads, options, library));
    }
    private async Task<World> Setup(AlFalahDbContext db)
    {
        var key = Guid.NewGuid().ToString("N");
        var school = new School { Name = "S3 " + key, City = "الرياض", Stage = SchoolStage.Primary, ManagerUserId = TeacherDriveHarness.ManagerUserId };
        var a = new ApplicationUser { Id = "S3-A-" + key, UserName = "a-" + key, FirstName = "معلم", LastName = "أ" };
        var b = new ApplicationUser { Id = "S3-B-" + key, UserName = "b-" + key, FirstName = "معلم", LastName = "ب" };
        db.AddRange(school, a, b); await db.SaveChangesAsync();
        var ta = new InstructorProfile { SchoolId = school.Id, UserId = a.Id };
        var tb = new InstructorProfile { SchoolId = school.Id, UserId = b.Id };
        db.AddRange(ta, tb); await db.SaveChangesAsync();
        var root = "root-" + key; var ar = "a-" + key; var br = "b-" + key;
        db.UserSchoolRoles.AddRange(new() { SchoolId = school.Id, UserId = TeacherDriveHarness.ManagerUserId, RoleId = "manager" },
            new() { SchoolId = school.Id, UserId = a.Id, RoleId = "teacher" }, new() { SchoolId = school.Id, UserId = b.Id, RoleId = "teacher" });
        db.SchoolGoogleDrives.Add(new() { SchoolId = school.Id, RootFolderId = root, ProtectedCredential = "isolated-sentinel", IsEnabled = true });
        db.TeacherDriveFolders.AddRange(new() { SchoolId = school.Id, TeacherId = ta.Id, DriveId = "", RootItemId = ar, FolderDisplayName = "أ" },
            new() { SchoolId = school.Id, TeacherId = tb.Id, DriveId = "", RootItemId = br, FolderDisplayName = "ب" });
        await db.SaveChangesAsync();
        var drive = new FakeGoogleDrive().AddFolder(root, "مدرسة").AddFolder(ar, "أ", root).AddFolder(br, "ب", root);
        var year = await db.AcademicYears.Where(x => x.IsActive).Select(x => x.Id).SingleAsync();
        var w = new World(school.Id, year, ta.Id, a.Id, tb.Id, b.Id, drive);
        await Build(db, w).Catalog.InitializeAsync(year);
        return w;
    }
    private async Task<(StorageUploadDto Upload, EvidenceLinkDto A, EvidenceLinkDto B)> Pair(AlFalahDbContext db, World w)
    {
        var s = Build(db, w, true);
        var catalog = await s.Catalog.ListAsync(w.Year);
        var upload = await StorageLibraryTests.Upload(s.Library, true);
        var a = await s.Links.CreateAsync(upload.StoredFileId!.Value, new(catalog.First(x => x.OriginalTaskId != null).Id, w.Year));
        var b = await s.Links.CreateAsync(upload.StoredFileId.Value, new(catalog.Single(x => x.Code == "STD-2.1").Id, w.Year));
        a = await s.Links.SubmitAsync(a.Id, new(a.RowVersion));
        b = await s.Links.SubmitAsync(b.Id, new(b.RowVersion));
        return (upload, a, b);
    }
    [StorageSqlFact]
    public async Task Idempotent_S3_SQL_script_applies_and_reapplies_on_a_fresh_S2_database()
    {
        var connection = new SqlConnectionStringBuilder(fixture.Connection);
        connection.InitialCatalog += "_Script_" + Guid.NewGuid().ToString("N");
        await using var db = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection.ConnectionString).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261003040217_SchoolFileStorageLibraryIntegrity");
        var script = migrator.GenerateScript("20261003040217_SchoolFileStorageLibraryIntegrity", "20261003233933_SchoolFileStorageReviewerIdentity", MigrationsSqlGenerationOptions.Idempotent);
        var batches = System.Text.RegularExpressions.Regex.Split(script, @"^GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // GO splits batches, not the connection: BEGIN/COMMIT must share the same SQL session.
        await db.Database.OpenConnectionAsync();
        for (var run = 0; run < 2; run++)
            foreach (var batch in batches.Where(x => !string.IsNullOrWhiteSpace(x))) await db.Database.ExecuteSqlRawAsync(batch);
        await db.Database.CloseConnectionAsync();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        db.Database.HasPendingModelChanges().Should().BeFalse();
    }
    [StorageSqlFact]
    public async Task Catalog_reuses_exact_tasks_and_is_repeatable_and_school_year_scoped()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var s = Build(db, w);
        var before = await db.EvidenceTasks.CountAsync();
        var one = await s.Catalog.InitializeAsync(w.Year); var two = await s.Catalog.InitializeAsync(w.Year);
        one.Select(x => x.Id).Should().BeEquivalentTo(two.Select(x => x.Id));
        one.Count(x => x.StandardCode != null).Should().Be(11); one.Where(x => x.StandardCode != null).Select(x => x.DomainCode).Distinct().Should().HaveCount(4);
        one.Count(x => x.OriginalTaskId != null).Should().Be(before); (await db.EvidenceTasks.CountAsync()).Should().Be(before);
        await Build(db, w, true).Catalog.Invoking(x => x.InitializeAsync(w.Year)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await Build(db, w, enabled: false).Catalog.Invoking(x => x.ListAsync(w.Year)).Should().ThrowAsync<KeyNotFoundException>();
        await s.Catalog.Invoking(x => x.ListAsync(int.MaxValue)).Should().ThrowAsync<KeyNotFoundException>();
    }
    [StorageSqlFact]
    public async Task Single_asset_multiple_links_independent_review_and_approved_only_counts()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var (upload, a, b) = await Pair(db, w);
        var manager = Build(db, w); var teacher = Build(db, w, true);
        (await manager.Links.CountsAsync(w.Year, false)).FulfilledRequirements.Should().Be(0);
        await teacher.Reviews.Invoking(x => x.ReviewAsync(a.Id, new(EvidenceReviewStatus.Approved, null, a.RowVersion))).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        var approved = await manager.Reviews.ReviewAsync(a.Id, new(EvidenceReviewStatus.Approved, "موافق", a.RowVersion));
        (await manager.Repo.LinkDtoAsync(w.School, b.Id, default)).Status.Should().Be("PendingReview");
        var counts = await manager.Links.CountsAsync(w.Year, false);
        counts.Files.Should().Be(1); counts.Links.Should().Be(2); counts.ApprovedLinks.Should().Be(1); counts.FulfilledRequirements.Should().Be(1);
        w.Drive.Uploads.Should().HaveCount(1);
        await teacher.Links.Invoking(x => x.CreateAsync(upload.StoredFileId!.Value, new(a.RequirementId, w.Year))).Should().ThrowAsync<StorageConflictException>();
        await teacher.Library.Invoking(x => x.DeleteAsync(upload.StoredFileId!.Value, new((db.StoredFiles.Local.Single(x => x.Id == upload.StoredFileId)).RowVersion.Length == 0 ? "" : Convert.ToBase64String(db.StoredFiles.Local.Single(x => x.Id == upload.StoredFileId).RowVersion)))).Should().ThrowAsync<BusinessRuleException>();
        var second = await StorageLibraryTests.Upload(teacher.Library, true, "second-file");
        var c = await teacher.Links.CreateAsync(second.StoredFileId!.Value, new(a.RequirementId, w.Year)); c = await teacher.Links.SubmitAsync(c.Id, new(c.RowVersion));
        await manager.Reviews.ReviewAsync(c.Id, new(EvidenceReviewStatus.Approved, null, c.RowVersion));
        counts = await manager.Links.CountsAsync(w.Year, false); counts.Files.Should().Be(2); counts.ApprovedLinks.Should().Be(2); counts.FulfilledRequirements.Should().Be(1);
    }
    [StorageSqlFact]
    public async Task Rejection_reason_resubmission_stale_rowversion_and_append_only_history()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var (_, a, _) = await Pair(db, w); var manager = Build(db, w);
        await manager.Reviews.Invoking(x => x.ReviewAsync(a.Id, new(EvidenceReviewStatus.Rejected, " ", a.RowVersion))).Should().ThrowAsync<ArgumentException>();
        var rejected = await manager.Reviews.ReviewAsync(a.Id, new(EvidenceReviewStatus.Rejected, "أضف تفاصيل", a.RowVersion));
        await manager.Reviews.Invoking(x => x.ReviewAsync(a.Id, new(EvidenceReviewStatus.Approved, null, a.RowVersion))).Should().ThrowAsync<StorageConflictException>();
        var again = await Build(db, w, true).Links.SubmitAsync(a.Id, new(rejected.RowVersion)); again.Status.Should().Be("Resubmitted");
        var done = await manager.Reviews.ReviewAsync(a.Id, new(EvidenceReviewStatus.Approved, null, again.RowVersion)); done.Decisions.Should().HaveCount(2); done.Decisions.First().Note.Should().Be("أضف تفاصيل");
        var decision = done.Decisions.First().Id;
        await FluentActions.Invoking(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM EvidenceReviewDecisions WHERE Id={decision}")).Should().ThrowAsync<SqlException>();
        var audit = await db.AuditLogs.Where(x => x.SchoolId == w.School && x.Action == "Storage.LinkReviewed").Select(x => x.Id).FirstAsync();
        await FluentActions.Invoking(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE AuditLogs SET Reason=N'changed' WHERE Id={audit}")).Should().ThrowAsync<SqlException>();
    }
    [StorageSqlFact]
    public async Task Ownership_school_year_archive_and_revoked_delegation_are_enforced()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var (upload, a, _) = await Pair(db, w);
        var other = Build(db, w with { Teacher = w.OtherTeacher, TeacherUser = w.OtherUser }, true);
        await other.Links.Invoking(x => x.FileLinksAsync(upload.StoredFileId!.Value, true)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await other.Links.Invoking(x => x.CreateAsync(upload.StoredFileId!.Value, new(a.RequirementId, w.Year))).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        var teacher = Build(db, w, true);
        await teacher.Links.Invoking(x => x.CreateAsync(upload.StoredFileId!.Value, new(a.RequirementId, w.Year + 100))).Should().ThrowAsync<ArgumentException>();
        await teacher.Links.Invoking(x => x.CreateAsync(upload.StoredFileId!.Value, new(a.RequirementId, w.Year, w.OtherTeacher))).Should().ThrowAsync<ArgumentException>();
        var foreign = await db.Schools.Where(x => x.Id != w.School).Select(x => x.Id).FirstAsync();
        await Build(db, w with { School = foreign }, true).Links.Invoking(x => x.FileLinksAsync(upload.StoredFileId!.Value)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        db.StorageDelegations.Add(new() { SchoolId = w.School, GranteeUserId = w.OtherUser, GrantedByManagerUserId = TeacherDriveHarness.ManagerUserId, StartsAt = DateTimeOffset.UtcNow.AddMinutes(-1), Reason = "مراجعة" }); await db.SaveChangesAsync();
        (await Build(db, w, actor: w.OtherUser).Links.QueueAsync(new(w.Year))).Items.Should().HaveCount(2);
        var grant = await db.StorageDelegations.SingleAsync(x => x.SchoolId == w.School); grant.RevokedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        await Build(db, w, actor: w.OtherUser).Reviews.Invoking(x => x.ReviewAsync(a.Id, new(EvidenceReviewStatus.Approved, null, a.RowVersion))).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        var file = await db.StoredFiles.SingleAsync(x => x.Id == upload.StoredFileId); file.SourceKind = StoredFileSourceKind.VisitArchive; await db.SaveChangesAsync();
        await teacher.Links.Invoking(x => x.FileLinksAsync(file.Id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }
    [StorageSqlFact]
    public async Task Accepted_replacement_retains_versions_and_decisions_and_resets_every_link()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var (upload, a, _) = await Pair(db, w); var manager = Build(db, w); var teacher = Build(db, w, true);
        await manager.Reviews.ReviewAsync(a.Id, new(EvidenceReviewStatus.Approved, null, a.RowVersion));
        var details = await teacher.Library.DetailsAsync(upload.StoredFileId!.Value);
        var change = await teacher.Changes.CreateAsync(upload.StoredFileId.Value, new("Replace", "نسخة محدثة", details.File.RowVersion));
        using var content = new MemoryStream("%PDF-1.7\nVersion two"u8.ToArray());
        var req = new StorageUploadRequest(content, "نسخة.pdf", content.Length, null, "version-key", true, ChangeRequestId: change.Id);
        var candidate = await teacher.Library.UploadAsync(req); content.Position = 0;
        (await teacher.Library.UploadAsync(req)).VersionId.Should().Be(candidate.VersionId);
        (await db.StoredFiles.SingleAsync(x => x.Id == upload.StoredFileId)).CurrentVersionId.Should().Be(upload.VersionId);
        change = (await teacher.Changes.ListAsync(upload.StoredFileId.Value)).Single();
        await manager.Changes.ReviewAsync(change.Id, new(true, "قبول النسخة وإعادة المراجعة", change.RowVersion));
        var links = await teacher.Links.FileLinksAsync(upload.StoredFileId.Value, true); links.Should().OnlyContain(x => x.Status == "PendingReview" && x.VersionId == candidate.VersionId);
        links.Single(x => x.Id == a.Id).Decisions.Single().VersionId.Should().Be(upload.VersionId!.Value);
        (await manager.Links.CountsAsync(w.Year, false)).FulfilledRequirements.Should().Be(0);
        var historical = await teacher.Changes.VersionContentAsync(upload.StoredFileId.Value, upload.VersionId!.Value); using var reader = new StreamReader(historical.Content); (await reader.ReadToEndAsync()).Should().Contain("Test file");
        w.Drive.Uploads.Should().HaveCount(2); (await db.StoredFileVersions.CountAsync(x => x.StoredFileId == upload.StoredFileId)).Should().Be(2);
        await FluentActions.Invoking(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE FileChangeRequests SET Reason=N'changed' WHERE Id={change.Id}")).Should().ThrowAsync<SqlException>();
        await FluentActions.Invoking(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM FileChangeDecisions WHERE FileChangeRequestId={change.Id}")).Should().ThrowAsync<SqlException>();
        await FluentActions.Invoking(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StoredFileVersions SET DriveFileName=N'changed' WHERE Id={upload.VersionId}")).Should().ThrowAsync<SqlException>();
    }
    [StorageSqlFact]
    public async Task Accepted_withdrawal_stops_counting_and_keeps_authorized_history()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var (upload, a, _) = await Pair(db, w); var s = Build(db, w);
        await s.Reviews.ReviewAsync(a.Id, new(EvidenceReviewStatus.Approved, null, a.RowVersion));
        var details = await s.Library.DetailsAsync(upload.StoredFileId!.Value);
        var change = await Build(db, w, true).Changes.CreateAsync(upload.StoredFileId.Value, new("Delete", "استبدال المحتوى لاحقًا", details.File.RowVersion));
        await s.Changes.ReviewAsync(change.Id, new(true, null, change.RowVersion));
        (await s.Links.CountsAsync(w.Year, false)).FulfilledRequirements.Should().Be(0);
        (await s.Changes.ListAsync(upload.StoredFileId.Value)).Single().Status.Should().Be("Approved");
        var bytes = await s.Changes.VersionContentAsync(upload.StoredFileId.Value, upload.VersionId!.Value); await bytes.Content.DisposeAsync();
        await s.Library.Invoking(x => x.ContentAsync(upload.StoredFileId.Value)).Should().ThrowAsync<KeyNotFoundException>();
        w.Drive.Trashed.Should().BeEmpty();
        var grant = await db.TeacherDriveFolders.SingleAsync(x => x.TeacherId == w.Teacher); grant.IsActive = false; await db.SaveChangesAsync();
        await Build(db, w, true).Changes.Invoking(x => x.VersionContentAsync(upload.StoredFileId.Value, upload.VersionId.Value)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }
    [StorageSqlFact]
    public async Task Missing_moved_and_revoked_files_do_not_count_and_matrix_exports_agree()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var (upload, a, _) = await Pair(db, w); var s = Build(db, w);
        await s.Reviews.ReviewAsync(a.Id, new(EvidenceReviewStatus.Approved, null, a.RowVersion));
        var filter = new EvidenceMatrixFilterDto { AcademicYearId = w.Year, TeacherId = w.Teacher };
        var before = await s.Matrix.GetAsync(filter); before.Rows.Single().CompletedTasksCount.Should().Be(1);
        var exported = await s.Matrix.ExportExcelAsync(filter); using var workbook = new XLWorkbook(new MemoryStream(exported.Bytes)); workbook.Worksheet(1).Cell(3,3).GetValue<int>().Should().Be(1);
        w.Drive.MoveExternally(w.Drive.Uploads.Single().FileId, "outside");
        (await s.Matrix.GetAsync(filter)).Rows.Single().CompletedTasksCount.Should().Be(0);
        (await s.Links.CountsAsync(w.Year, false)).ApprovedLinks.Should().Be(0);
        var root = await db.TeacherDriveFolders.Where(x => x.TeacherId == w.Teacher).Select(x => x.RootItemId).SingleAsync();
        w.Drive.MoveExternally(w.Drive.Uploads.Single().FileId, root);
        (await s.Matrix.GetAsync(filter)).Rows.Single().CompletedTasksCount.Should().Be(1);
        w.Drive.TrashExternally(w.Drive.Uploads.Single().FileId);
        var after = await s.Matrix.GetAsync(filter); after.Rows.Single().CompletedTasksCount.Should().Be(0);
        var afterExport = await s.Matrix.ExportExcelAsync(filter); using var afterBook = new XLWorkbook(new MemoryStream(afterExport.Bytes)); afterBook.Worksheet(1).Cell(3,3).GetValue<int>().Should().Be(0);
        var report = new { stage="S3", scope="isolated SQL; fake Drive", before=before.Rows.Single(), after=after.Rows.Single(), excelBefore=1, excelAfter=0, cutover=false };
        var path = Environment.GetEnvironmentVariable("ALFALAH_S3_COMPARISON_REPORT"); if (path != null) await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented=true }));
    }
    [StorageSqlFact]
    public async Task Lost_response_on_version_upload_reconciles_existing_item_without_duplicate()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var (upload, _, _) = await Pair(db, w); var teacher = Build(db, w, true);
        var details = await teacher.Library.DetailsAsync(upload.StoredFileId!.Value);
        var change = await teacher.Changes.CreateAsync(upload.StoredFileId.Value, new("Replace", "تحديث", details.File.RowVersion));
        w.Drive.LoseNextUploadResponse = true;
        using var content = new MemoryStream("%PDF-1.7\nLost version"u8.ToArray());
        await teacher.Library.Invoking(x => x.UploadAsync(new(content,"نسخة.pdf",content.Length,null,"lost-version",true,ChangeRequestId:change.Id))).Should().ThrowAsync<StorageUnavailableException>();
        db.ChangeTracker.Clear();
        var op = await db.Set<StorageOperation>().SingleAsync(x => x.ChangeRequestId == change.Id);
        (await Build(db,w,true).Library.ReconcileAsync(op.Id)).Status.Should().Be("Completed");
        (await Build(db,w,true).Library.ReconcileAsync(op.Id)).Status.Should().Be("Completed");
        w.Drive.Uploads.Should().HaveCount(2); (await db.StoredFileVersions.CountAsync(x => x.StoredFileId == upload.StoredFileId)).Should().Be(2);
    }
    [StorageSqlFact]
    public async Task SQL_failure_during_candidate_save_preserves_old_pointer_then_recovers_once()
    {
        await using var setup = fixture.Db(); var w = await Setup(setup); var (upload, _, _) = await Pair(setup, w);
        var teacher = Build(setup,w,true); var details = await teacher.Library.DetailsAsync(upload.StoredFileId!.Value);
        var change = await teacher.Changes.CreateAsync(upload.StoredFileId.Value,new("Replace","تحديث",details.File.RowVersion));
        await using (var failing = fixture.Db(new FailCandidateSave()))
        {
            using var content = new MemoryStream("%PDF-1.7\nSQL failure"u8.ToArray());
            await Build(failing,w,true).Library.Invoking(x => x.UploadAsync(new(content,"نسخة.pdf",content.Length,null,"sql-version",true,ChangeRequestId:change.Id))).Should().ThrowAsync<StorageUnavailableException>();
        }
        await using var db = fixture.Db(); var op = await db.Set<StorageOperation>().SingleAsync(x=>x.ChangeRequestId == change.Id);
        (await db.StoredFiles.SingleAsync(x=>x.Id == upload.StoredFileId)).CurrentVersionId.Should().Be(upload.VersionId);
        (await Build(db,w,true).Library.ReconcileAsync(op.Id)).Status.Should().Be("Completed");
        w.Drive.Uploads.Should().HaveCount(2);
    }
    [StorageSqlFact]
    public async Task Concurrent_duplicate_link_and_review_have_one_winner_and_stale_tokens_conflict()
    {
        await using var setup = fixture.Db(); var w = await Setup(setup); var teacher = Build(setup,w,true);
        var upload = await StorageLibraryTests.Upload(teacher.Library,true);
        var req = (await teacher.Catalog.ListAsync(w.Year)).First().Id;
        async Task<string> Create()
        {
            await using var db = fixture.Db();
            try { await Build(db,w,true).Links.CreateAsync(upload.StoredFileId!.Value,new(req,w.Year)); return "Created"; }
            catch (StorageConflictException) { return "Conflict"; }
        }
        (await Task.WhenAll(Create(),Create())).Should().BeEquivalentTo("Created","Conflict");
        setup.ChangeTracker.Clear(); var a=(await Build(setup,w,true).Links.FileLinksAsync(upload.StoredFileId!.Value,true)).Single();
        a=await Build(setup,w,true).Links.SubmitAsync(a.Id,new(a.RowVersion));
        async Task<string> Review()
        {
            await using var db = fixture.Db();
            try { await Build(db,w).Reviews.ReviewAsync(a.Id,new(EvidenceReviewStatus.Approved,null,a.RowVersion)); return "Approved"; }
            catch(StorageConflictException) { return "Conflict"; }
        }
        (await Task.WhenAll(Review(),Review())).Should().BeEquivalentTo("Approved","Conflict");
        (await setup.EvidenceReviewDecisions.CountAsync(x=>x.EvidenceLinkId == a.Id)).Should().Be(1);
    }
    [StorageSqlFact]
    public async Task Shared_legacy_writer_backfill_has_provenance_and_no_competing_review_writer()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var teacher = Build(db,w,true);
        var task=await db.EvidenceTasks.Select(x=>x.Id).FirstAsync();
        using var content=new MemoryStream("%PDF-1.7\nLegacy shared"u8.ToArray());
        var upload=await teacher.Library.LegacyUploadAsync(new(content,"قديم.pdf","application/pdf",content.Length,null,task,"legacy-key"));
        var file=await db.StoredFiles.SingleAsync(x=>x.LegacySubmissionId == upload.SubmissionId);
        file.LegacyFingerprint.Should().NotBeNull(); file.SharedWriterFingerprint.Should().Be(file.LegacyFingerprint);
        var backfill=new StorageBackfillService(new StorageBackfillRepository(db));
        var before=await backfill.RunAsync(); before.Issues.Where(x=>x.SubmissionId == upload.SubmissionId).Should().BeEmpty();
        var link=(await teacher.Links.FileLinksAsync(file.Id,true)).Single(); var manager=Build(db,w);
        await manager.Reviews.ReviewAsync(link.Id,new(EvidenceReviewStatus.Approved,null,link.RowVersion));
        (await backfill.RunAsync()).Issues.Where(x=>x.SubmissionId == upload.SubmissionId).Should().BeEmpty();
        await manager.Matrix.Invoking(x=>x.ReviewAsync(upload.SubmissionId,EvidenceReviewStatus.Rejected,"سبب")).Should().ThrowAsync<BusinessRuleException>();
        await FluentActions.Invoking(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE TeacherEvidenceSubmissions SET ReviewStatus=4 WHERE Id={upload.SubmissionId}")).Should().ThrowAsync<SqlException>();
        (await db.TeacherEvidenceSubmissions.SingleAsync(x=>x.Id == upload.SubmissionId)).ReviewStatus.Should().Be(EvidenceReviewStatus.PendingReview);
    }
    [StorageSqlFact]
    public async Task Review_queue_over_5000_links_is_paged_in_SQL_and_measured()
    {
        await using var db=fixture.Db(); var w=await Setup(db); var s=Build(db,w);
        var folder=await s.Library.CreateFolderAsync(new(null,"","performance-root"));
        var requirement=(await s.Catalog.ListAsync(w.Year)).First().Id;
        var files=Enumerable.Range(0,5000).Select(i=>new StoredFile { SchoolId=w.School,FolderId=folder.Id,DisplayName=$"شاهد {i:D5}.pdf",SourceKind=StoredFileSourceKind.SchoolUpload }).ToArray();
        db.StoredFiles.AddRange(files);await db.SaveChangesAsync();
        var versions=files.Select((f,i)=>new StoredFileVersion { SchoolId=w.School,StoredFileId=f.Id,VersionNumber=1,DriveItemId="queue-"+Guid.NewGuid(),DriveFileName=f.DisplayName,SizeInBytes=10,MimeType="application/pdf",Availability=StoredFileAvailability.Available,UploadedAtUtc=DateTimeOffset.UtcNow }).ToArray();
        db.StoredFileVersions.AddRange(versions);await db.SaveChangesAsync();
        for(var i=0;i<files.Length;i++) { files[i].CurrentVersionId=versions[i].Id;db.EvidenceLinks.Add(new() { SchoolId=w.School,StoredFileId=files[i].Id,VersionId=versions[i].Id,RequirementId=requirement,AcademicYearId=w.Year,Status=EvidenceLinkStatus.PendingReview }); }
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var request=new EvidenceQueueRequest(w.Year,Page:100);
        await s.Links.QueueAsync(request);var times=new List<double>();
        for(var i=0;i<10;i++) {var watch=Stopwatch.StartNew();var page=await s.Links.QueueAsync(request);watch.Stop();page.Total.Should().Be(5000);page.Items.Should().HaveCount(25);times.Add(watch.Elapsed.TotalMilliseconds);}
        var median=times.Order().Skip(4).Take(2).Average();var report=new {stage="S3",links=5000,page=100,pageSize=25,samplesMs=times,medianMs=median,maxMs=times.Max(),provider="Fake Drive; queue metadata makes no content calls",targetMs=200};
        var json=JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true});output.WriteLine(json);
        var path=Environment.GetEnvironmentVariable("ALFALAH_S3_PERFORMANCE_REPORT");if(path!=null)await File.WriteAllTextAsync(path,json);
        median.Should().BeLessThan(200);
    }
    [StorageSqlFact]
    public async Task Preapproval_replacement_replays_once_and_cannot_bypass_prior_approval()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var (upload, a, _) = await Pair(db, w);
        var teacher = Build(db, w, true); var manager = Build(db, w);
        var details = await teacher.Library.DetailsAsync(upload.StoredFileId!.Value);
        var change = await teacher.Changes.CreateAsync(upload.StoredFileId.Value, new("Replace", "before first approval", details.File.RowVersion, true));
        using var stream = new MemoryStream("%PDF-1.7\nReplacement"u8.ToArray());
        var request = new StorageUploadRequest(stream, "replacement.pdf", stream.Length, null, "direct-version", true, ChangeRequestId: change.Id);
        var candidate = await teacher.Library.UploadAsync(request); stream.Position = 0;
        (await teacher.Library.UploadAsync(request)).VersionId.Should().Be(candidate.VersionId);
        (await teacher.Changes.ListAsync(upload.StoredFileId.Value)).Single().Status.Should().Be("Approved");
        var links = await teacher.Links.FileLinksAsync(upload.StoredFileId.Value, true);
        links.Should().OnlyContain(x => x.Status == "PendingReview" && x.VersionId == candidate.VersionId);
        w.Drive.Uploads.Should().HaveCount(2);
        a = links.Single(x => x.Id == a.Id);
        await manager.Reviews.ReviewAsync(a.Id, new(EvidenceReviewStatus.Approved, null, a.RowVersion));
        details = await teacher.Library.DetailsAsync(upload.StoredFileId.Value);
        await teacher.Changes.Invoking(x => x.CreateAsync(upload.StoredFileId.Value, new("Replace", "bypass", details.File.RowVersion, true))).Should().ThrowAsync<BusinessRuleException>();
        await FluentActions.Invoking(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE FileChangeRequests SET ReplaceBeforeReview=0 WHERE Id={change.Id}")).Should().ThrowAsync<SqlException>();
    }
    [StorageSqlFact]
    public async Task Rejected_candidate_keeps_approved_pointer_and_history_and_requires_fresh_token()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var (upload, a, _) = await Pair(db, w); var manager = Build(db, w); var teacher = Build(db, w, true);
        await manager.Reviews.ReviewAsync(a.Id, new(EvidenceReviewStatus.Approved, null, a.RowVersion));
        var details = await teacher.Library.DetailsAsync(upload.StoredFileId!.Value);
        var change = await teacher.Changes.CreateAsync(upload.StoredFileId.Value, new("Replace", "candidate", details.File.RowVersion));
        using var stream = new MemoryStream("%PDF-1.7\nRejected candidate"u8.ToArray());
        var candidate = await teacher.Library.UploadAsync(new(stream, "candidate.pdf", stream.Length, null, "reject-version", true, ChangeRequestId: change.Id));
        await manager.Changes.Invoking(x => x.ReviewAsync(change.Id, new(false, "reason", change.RowVersion))).Should().ThrowAsync<StorageConflictException>();
        change = (await teacher.Changes.ListAsync(upload.StoredFileId.Value)).Single();
        await manager.Changes.Invoking(x => x.ReviewAsync(change.Id, new(false, " ", change.RowVersion))).Should().ThrowAsync<ArgumentException>();
        var rejected = await manager.Changes.ReviewAsync(change.Id, new(false, "wrong contents", change.RowVersion));
        rejected.Status.Should().Be("Rejected"); rejected.Decisions.Single().Note.Should().Be("wrong contents");
        (await db.StoredFiles.SingleAsync(x => x.Id == upload.StoredFileId)).CurrentVersionId.Should().Be(upload.VersionId);
        (await manager.Links.CountsAsync(w.Year, false)).ApprovedLinks.Should().Be(1);
        var bytes = await teacher.Changes.VersionContentAsync(upload.StoredFileId.Value, candidate.VersionId!.Value); await bytes.Content.DisposeAsync();
        (await teacher.Changes.HistoryAsync(upload.StoredFileId.Value)).Versions.Should().HaveCount(2);
    }
    [StorageSqlFact]
    public async Task Minimum_policy_matrix_and_export_and_revoked_owner_membership_agree()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var (upload, a, _) = await Pair(db, w); var s = Build(db, w);
        var requirement = (await s.Catalog.ListAsync(w.Year)).Single(x => x.Id == a.RequirementId);
        await s.Catalog.ConfigureAsync(requirement.Id, new("2", "2.1", EvidenceImportance.Important, w.TeacherUser, RoleNames.Instructor, EvidenceFulfillmentPolicy.MinimumApprovedLinks, 2, requirement.RowVersion));
        await s.Reviews.ReviewAsync(a.Id, new(EvidenceReviewStatus.Approved, null, a.RowVersion));
        var filter = new EvidenceMatrixFilterDto { AcademicYearId = w.Year, TeacherId = w.Teacher };
        (await s.Links.CountsAsync(w.Year, false)).FulfilledRequirements.Should().Be(0);
        (await s.Matrix.GetAsync(filter)).Rows.Single().CompletedTasksCount.Should().Be(0);
        using var workbook = new XLWorkbook(new MemoryStream((await s.Matrix.ExportExcelAsync(filter)).Bytes)); workbook.Worksheet(1).Cell(3, 3).GetValue<int>().Should().Be(0);
        var membership = await db.UserSchoolRoles.SingleAsync(x => x.SchoolId == w.School && x.UserId == w.TeacherUser); membership.IsActive = false; await db.SaveChangesAsync();
        (await s.Links.CountsAsync(w.Year, false)).ApprovedLinks.Should().Be(0);
        (await s.Matrix.GetAsync(filter)).Rows.Single().CompletedTasksCount.Should().Be(0);
    }
    [StorageSqlFact]
    public async Task Legacy_and_S2_shared_writer_repair_backfill_matrix_and_excel_are_compared_offline()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var s = Build(db, w); var root = await db.TeacherDriveFolders.Where(x => x.TeacherId == w.Teacher).Select(x => x.RootItemId).SingleAsync();
        var tasks = await db.EvidenceTasks.OrderBy(x => x.Id).Take(3).ToListAsync();
        var states = new[] { EvidenceReviewStatus.Approved, EvidenceReviewStatus.PendingReview, EvidenceReviewStatus.Rejected };
        var sources = tasks.Select((task, i) => new TeacherEvidenceSubmission { SchoolId = w.School, TeacherId = w.Teacher, AcademicYearId = w.Year, TaskId = task.Id,
            DriveItemId = "comparison-" + Guid.NewGuid(), ParentItemId = root, FileName = "comparison.pdf", FileExtension = ".pdf", MimeType = "application/pdf", SizeInBytes = 8,
            UploadStatus = EvidenceUploadStatus.Completed, ReviewStatus = states[i], ReviewedByUserId = i == 1 ? null : TeacherDriveHarness.ManagerUserId,
            ReviewedAtUtc = i == 1 ? null : DateTimeOffset.UtcNow, ReviewNote = i == 2 ? "legacy rejection" : null, UploadedAtUtc = DateTimeOffset.UtcNow }).ToArray();
        db.TeacherEvidenceSubmissions.AddRange(sources);
        db.TeacherTaskStatuses.AddRange(tasks.Select((task, i) => new TeacherTaskStatus { SchoolId = w.School, TeacherId = w.Teacher, AcademicYearId = w.Year, TaskId = task.Id,
            ActiveFilesCount = 1, CellStatus = i == 0 ? EvidenceCellStatus.Approved : i == 1 ? EvidenceCellStatus.PendingReview : EvidenceCellStatus.Rejected }));
        await db.SaveChangesAsync();
        foreach (var source in sources) w.Drive.AddFile(source.DriveItemId, source.FileName, root, "evidence");
        // Reproduce a pre-S3 S2 completed writer: durable operation, file and version, no provenance or link.
        await Build(db, w, true).Library.ContextAsync(true);
        var folder = await db.StorageFolders.SingleAsync(x => x.SchoolId == w.School && x.DriveItemId == root);
        var shared = new StoredFile { SchoolId = w.School, FolderId = folder.Id, OwnerTeacherId = w.Teacher, SourceKind = StoredFileSourceKind.TeacherUpload,
            LegacySubmissionId = sources[1].Id, DisplayName = sources[1].FileName, NeedsLink = true };
        db.StoredFiles.Add(shared); await db.SaveChangesAsync();
        var version = new StoredFileVersion { SchoolId = w.School, StoredFileId = shared.Id, VersionNumber = 1, DriveItemId = sources[1].DriveItemId,
            DriveFileName = sources[1].FileName, FileExtension = ".pdf", MimeType = "application/pdf", SizeInBytes = 8, UploadedByUserId = w.TeacherUser,
            Availability = StoredFileAvailability.Available, UploadedAtUtc = sources[1].UploadedAtUtc };
        db.StoredFileVersions.Add(version); await db.SaveChangesAsync(); shared.CurrentVersionId = version.Id;
        db.Set<StorageOperation>().Add(new() { SchoolId = w.School, ActorUserId = w.TeacherUser, RequestKey = "old-S2-" + Guid.NewGuid(),
            Status = "Completed", Action = "Upload", FolderId = folder.Id, StoredFileId = shared.Id, VersionId = version.Id,
            LegacySubmissionId = sources[1].Id, ProviderItemId = version.DriveItemId });
        await db.SaveChangesAsync();
        var filter = new EvidenceMatrixFilterDto { AcademicYearId = w.Year, TeacherId = w.Teacher };
        var legacy = Build(db, w, enabled: false); var before = await legacy.Matrix.GetAsync(filter);
        using var beforeExcel = new XLWorkbook(new MemoryStream((await legacy.Matrix.ExportExcelAsync(filter)).Bytes));
        before.Rows.Single().CompletedTasksCount.Should().Be(3); beforeExcel.Worksheet(1).Cell(3, 3).GetValue<int>().Should().Be(3);
        var backfillRepository = new StorageBackfillRepository(db); var backfill = new StorageBackfillService(backfillRepository);
        (await backfill.RunAsync()).Issues.Should().Contain(x => x.SubmissionId == sources[1].Id && x.Code == "LegacyOrTargetDrift");
        var repair = new StorageSharedWriterRepairService(backfillRepository, s.Repo);
        (await repair.RunAsync()).Should().Be(1); shared.SharedWriterFingerprint.Should().BeNull();
        (await repair.RunAsync(false)).Should().Be(1); (await repair.RunAsync(false)).Should().Be(0);
        var first = await backfill.RunAsync(false); var second = await backfill.RunAsync(false);
        first.Issues.Where(x => sources.Select(v => v.Id).Contains(x.SubmissionId)).Should().BeEmpty(); second.Issues.Where(x => sources.Select(v => v.Id).Contains(x.SubmissionId)).Should().BeEmpty();
        second.CreatedFiles.Should().Be(0); second.CreatedLinks.Should().Be(0); second.CreatedDecisions.Should().Be(0);
        var after = await s.Matrix.GetAsync(filter); after.Rows.Single().CompletedTasksCount.Should().Be(1);
        using var afterExcel = new XLWorkbook(new MemoryStream((await s.Matrix.ExportExcelAsync(filter)).Bytes)); afterExcel.Worksheet(1).Cell(3, 3).GetValue<int>().Should().Be(1);
        after.Tasks.Should().BeEquivalentTo(before.Tasks); after.Rows.Single().Cells.Select(x => (x.TaskId, x.Status, x.ActiveFilesCount)).Should().BeEquivalentTo(before.Rows.Single().Cells.Select(x => (x.TaskId, x.Status, x.ActiveFilesCount)));
        var row = await db.StoredFiles.SingleAsync(x => x.Id == shared.Id);
        await FluentActions.Invoking(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StoredFiles SET SharedWriterFingerprint=N'changed' WHERE Id={row.Id}")).Should().ThrowAsync<SqlException>();
        var report = new { stage = "S3", scope = "Fresh isolated SQL, synthetic legacy and pre-S3 S2 shared writer, fake Drive availability; no Development clone or Google writes",
            repairDryRun = 1, repairApplied = 1, repairRepeated = 0, firstBackfill = first, repeatedBackfill = second, before = before.Rows.Single(), after = after.Rows.Single(),
            excelBefore = 3, excelAfter = 1, expectedDifferences = new[] { "Legacy IsChecked used ActiveFilesCount > 0. PendingReview and Rejected stop counting; only Approved counts." }, cutover = false };
        var path = Environment.GetEnvironmentVariable("ALFALAH_S3_BACKFILL_REPORT"); if (path != null) await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
    private sealed class FailCandidateSave : SaveChangesInterceptor
    {
        private bool failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct=default)
        {
            if(!failed && data.Context!.ChangeTracker.Entries<StoredFileVersion>().Any(x=>x.State == EntityState.Added && x.Entity.VersionNumber>1))
            {failed=true;throw new InvalidOperationException("SQL interrupted after Drive");}
            return ValueTask.FromResult(result);
        }
    }
}
