using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AlFalah.Application.Common;
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

[CollectionDefinition("Readiness SQL", DisableParallelization = true)]
public sealed class ReadinessSqlCollection : ICollectionFixture<ReadinessSqlFixture>;
public sealed class ReadinessSqlFixture : IAsyncLifetime
{
    private StorageSqlFixture inner = new();
    public string Connection => inner.Connection;
    public AlFalahDbContext Db(DbCommandInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(Connection);
        if (interceptor != null) options.AddInterceptors(interceptor);
        return new(options.Options);
    }
    public async Task InitializeAsync()
    {
        var value = Environment.GetEnvironmentVariable("ALFALAH_STORAGE_TEST_CONNECTION");
        if (value != null) { var b = new SqlConnectionStringBuilder(value); b.InitialCatalog += "_Readiness"; inner = new(b.ConnectionString); }
        await inner.InitializeAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
}
[Collection("Readiness SQL")]
public sealed class ReadinessSqlTests(ReadinessSqlFixture fixture, ITestOutputHelper output)
{
    private sealed record World(int School, int Year, int Teacher, string TeacherUser, string Root, string TeacherRoot, FakeGoogleDrive Drive);
    private sealed record Services(ReadinessService Readiness, RequirementCatalogService Catalog, EvidenceLinkService Links, EvidenceReviewService Reviews,
        FileChangeRequestService Changes, StorageLibraryService Library, ReadinessExportService Exports);
    private Services Build(AlFalahDbContext db, World w, bool teacher = false, string? actor = null, bool enabled = true)
    {
        ICurrentUserService user = teacher ? new TeacherDriveHarness.TestCurrentUser(RoleNames.Instructor, actor ?? w.TeacherUser, w.School) : TeacherDriveHarness.Manager(w.School);
        var scopes = new StorageRepository(db); var repo = new EvidenceRepository(db); var provider = new GoogleStorageProvider(w.Drive);
        var auth = new StorageAuthorizationService(scopes, user, new StorageDriveBoundary(new(w.Drive)), TimeProvider.System);
        var options = Options.Create(new StorageOptions { ReadModelEnabled = enabled });
        var context = new EvidenceWorkflowContext(repo, scopes, auth, user, provider, options);
        var submissions = new EvidenceSubmissionService(db, new AuditLogWriter(db, new HttpContextAccessor(), NullLogger<AuditLogWriter>.Instance));
        var changes = new FileChangeRequestService(repo, scopes, context, provider);
        var library = new StorageLibraryService(new StorageLibraryRepository(db), scopes, auth, user, provider, submissions, options, changes);
        var readiness = new ReadinessService(new ReadinessRepository(db), repo, scopes, context, provider);
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        return new(readiness, new(repo, scopes, context), new(repo, scopes, context), new(repo, scopes, context), changes, library, new(readiness, new ReadinessExportRenderer()));
    }
    private async Task<World> Setup(AlFalahDbContext db, bool initialize = true)
    {
        var key = Guid.NewGuid().ToString("N");
        var school = new School { Name = "مدرسة التحقق S4 " + key, City = "الرياض", Stage = SchoolStage.Primary, ManagerUserId = TeacherDriveHarness.ManagerUserId };
        var user = new ApplicationUser { Id = "S4-teacher-" + key, UserName = "s4-" + key, FirstName = "معلم", LastName = "الاختبار" };
        var year = new AcademicYear { Code = "S4-" + key[..20], NameAr = "سنة التحقق", IsActive = false };
        db.AddRange(school, user, year); await db.SaveChangesAsync();
        var teacher = new InstructorProfile { UserId = user.Id, SchoolId = school.Id }; db.Add(teacher); await db.SaveChangesAsync();
        var root = "S4-root-" + key; var teacherRoot = "S4-teacher-root-" + key;
        db.UserSchoolRoles.AddRange(new() { SchoolId = school.Id, UserId = TeacherDriveHarness.ManagerUserId, RoleId = "manager" }, new() { SchoolId = school.Id, UserId = user.Id, RoleId = "teacher" });
        db.SchoolGoogleDrives.Add(new() { SchoolId = school.Id, RootFolderId = root, ProtectedCredential = "unchanged-isolated-sentinel", IsEnabled = true });
        db.TeacherDriveFolders.Add(new() { SchoolId = school.Id, TeacherId = teacher.Id, DriveId = "", RootItemId = teacherRoot, FolderDisplayName = "معلم" });
        await db.SaveChangesAsync();
        var drive = new FakeGoogleDrive().AddFolder(root, "مدرسة").AddFolder(teacherRoot, "معلم", root);
        var w = new World(school.Id, year.Id, teacher.Id, user.Id, root, teacherRoot, drive);
        if (initialize) { await Build(db, w).Catalog.InitializeAsync(year.Id); await Build(db, w).Readiness.InitializeAsync(new(year.Id)); }
        return w;
    }
    private static ReadinessFilter Filter(World w) => new(w.Year);
    private async Task<EvidenceLinkDto> Link(AlFalahDbContext db, World w, int requirement, string key, bool approve = false, bool teacher = false, int? file = null)
    {
        var s = Build(db, w, teacher);
        if (!teacher && !await db.StorageFolders.AnyAsync(x => x.SchoolId == w.School && x.Kind == StorageFolderKind.SchoolLibrary))
            await s.Library.CreateFolderAsync(new(null, "", "school-library-root"));
        var upload = file == null ? await StorageLibraryTests.Upload(s.Library, teacher, key) : null;
        var row = await s.Links.CreateAsync(file ?? upload!.StoredFileId!.Value, new(requirement, w.Year));
        row = await s.Links.SubmitAsync(row.Id, new(row.RowVersion));
        return approve ? await Build(db, w).Reviews.ReviewAsync(row.Id, new(EvidenceReviewStatus.Approved, "شاهد صحيح", row.RowVersion)) : row;
    }
    private async Task<EvaluationRequirementSelection> Selected(AlFalahDbContext db, World w)
    {
        var rows = await Build(db, w).Readiness.RequirementsAsync(Filter(w) with { TrackerOnly = true, PageSize = 100 });
        return new(rows.Items[0], rows.Items[1], rows.Items.First(x => x.Importance == "Critical"));
    }
    private sealed record EvaluationRequirementSelection(ReadinessRequirementDto First, ReadinessRequirementDto Second, ReadinessRequirementDto Critical);
    private static ConfigureFollowUpRequest Configuration(ReadinessRequirementDto r, EvidenceFulfillmentPolicy policy = EvidenceFulfillmentPolicy.AnyApprovedLink, int minimum = 1,
        bool mandatory = true, string? user = null) => new(user, r.ResponsibleRole, Enum.Parse<EvidenceImportance>(r.Importance), mandatory, policy, minimum,
            new DateOnly(2026, 10, 20), "InProgress", "متابعة موثقة", "تعيين ومتابعة", r.RowVersion);

    [StorageSqlFact]
    public async Task Curated_template_4_11_36_145_is_repeatable_preserves_S3_tasks_and_never_inherits_completed()
    {
        await using var db = fixture.Db(); var w = await Setup(db, false); var s = Build(db, w);
        await s.Catalog.InitializeAsync(w.Year);
        var taskCount = await db.EvidenceTasks.CountAsync(); taskCount.Should().Be(32); // 31 current reference codes plus the retained S1 sentinel.
        await s.Readiness.InitializeAsync(new(w.Year));
        var template = await s.Readiness.TemplateAsync(1); template.Domains.Should().HaveCount(4); template.Standards.Should().HaveCount(11);
        template.Items.Should().HaveCount(36); template.MatrixReferences.Should().HaveCount(145).And.OnlyContain(x => x.State == "ReferenceOnly");
        template.Items.Where(x => x.SourceKey.StartsWith("item-11-")).Should().OnlyContain(x => x.StandardCode == "2.2");
        var result = await s.Readiness.ReadinessAsync(Filter(w)); result.Overall.Denominator.Should().Be(36); result.Overall.Numerator.Should().Be(0);
        result.Overall.UniqueFiles.Should().Be(0); result.Overall.Links.Should().Be(0); result.Overall.CriticalGaps.Should().Be(3);
        result.Standards.Single(x => x.Code == "1.5").Percentage.Should().BeNull();
        (await db.EvidenceTasks.CountAsync()).Should().Be(taskCount);
        (await db.EvidenceRequirements.CountAsync(x => x.SchoolId == w.School && x.SourceKey != null)).Should().Be(36);
        (await db.EvidenceRequirements.CountAsync(x => x.SchoolId == w.School && x.OriginalTaskId != null)).Should().Be(taskCount);
        var mapped = await db.EvidenceRequirements.Where(x => x.SchoolId == w.School && x.OriginalTaskId != null).ToListAsync();
        mapped.Select(x => x.OriginalTaskId).Distinct().Should().HaveCount(taskCount);
        foreach (var task in await db.EvidenceTasks.AsNoTracking().ToListAsync()) mapped.Single(x => x.OriginalTaskId == task.Id).Code.Should().Be(task.Code);
        (await db.Set<SchoolEvaluationScope>().CountAsync(x => x.SchoolId == w.School)).Should().Be(1);
    }
    [StorageSqlFact]
    public async Task Zero_mandatory_requirements_is_null_and_not_100_and_pending_or_rejected_do_not_fulfill()
    {
        await using var db = fixture.Db(); var w = await Setup(db, false); var s = Build(db, w);
        var zero = await s.Readiness.ReadinessAsync(Filter(w)); zero.Overall.Percentage.Should().BeNull(); zero.Overall.CalculationState.Should().Be("NoRequirements");
        await s.Readiness.InitializeAsync(new(w.Year)); var selected = await Selected(db, w);
        var link = await Link(db, w, selected.First.Id, "pending");
        var pending = (await s.Readiness.RequirementsAsync(Filter(w))).Items.Single(x => x.Id == selected.First.Id);
        pending.GapReasons.Should().Contain("AwaitingReview"); pending.Fulfilled.Should().BeFalse();
        await s.Reviews.ReviewAsync(link.Id, new(EvidenceReviewStatus.Rejected, "الشرح غير كاف", link.RowVersion));
        (await s.Readiness.RequirementsAsync(Filter(w))).Items.Single(x => x.Id == selected.First.Id).GapReasons.Should().Contain("Rejected");
        (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Numerator.Should().Be(0);
    }
    [StorageSqlFact]
    public async Task Multi_links_count_each_requirement_once_unique_file_once_and_minimum_policy_exactly()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var s = Build(db, w); var selected = await Selected(db, w);
        var a = await Link(db, w, selected.First.Id, "shared", true);
        await Link(db, w, selected.Second.Id, "unused", true, file: a.StoredFileId);
        var metric = (await s.Readiness.ReadinessAsync(Filter(w))).Overall;
        metric.Numerator.Should().Be(2); metric.UniqueFiles.Should().Be(1); metric.ApprovedLinks.Should().Be(2); metric.Percentage.Should().Be(5.56m);
        await s.Readiness.ConfigureAsync(selected.First.Id, Configuration(selected.First, EvidenceFulfillmentPolicy.MinimumApprovedLinks, 2));
        (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Numerator.Should().Be(1);
        (await s.Readiness.RequirementsAsync(Filter(w))).Items.Single(x => x.Id == a.RequirementId).GapReasons.Should().Contain("InsufficientApprovedLinks");
        await Link(db, w, selected.First.Id, "second-file", true);
        metric = (await s.Readiness.ReadinessAsync(Filter(w))).Overall; metric.Numerator.Should().Be(2); metric.UniqueFiles.Should().Be(2); metric.ApprovedLinks.Should().Be(3);
        (await s.Readiness.IndexAsync(Filter(w))).Items.Should().HaveCount(2);
    }
    [StorageSqlFact]
    public async Task Missing_trashed_outside_root_and_restored_files_correct_readiness_before_export()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var s = Build(db, w); var selected = await Selected(db, w);
        var link = await Link(db, w, selected.First.Id, "live", true);
        var id = await db.StoredFileVersions.Where(v => v.Id == link.VersionId).Select(v => v.DriveItemId).SingleAsync();
        w.Drive.TrashExternally(id); (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Numerator.Should().Be(0);
        (await s.Readiness.RequirementsAsync(Filter(w), true)).Items.Single(x => x.Id == selected.First.Id).GapReasons.Should().Contain("Unavailable");
        w.Drive.RestoreExternally(id); (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Numerator.Should().Be(1);
        w.Drive.MoveExternally(id, "outside"); (await s.Readiness.ExportDataAsync(Filter(w))).Summary.Overall.Numerator.Should().Be(0);
        var folder = await db.StoredFiles.Where(f => f.Id == link.StoredFileId).Select(f => f.Folder.DriveItemId).SingleAsync();
        w.Drive.MoveExternally(id, folder); (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Numerator.Should().Be(1);
        w.Drive.RemoveExternally(id); (await s.Readiness.IndexAsync(Filter(w))).Total.Should().Be(0);
    }
    [StorageSqlFact]
    public async Task Approved_current_replacement_requires_independent_reapproval_and_keeps_original_decision()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var s = Build(db, w); var selected = await Selected(db, w);
        var link = await Link(db, w, selected.First.Id, "old", true);
        var file = await db.StoredFiles.AsNoTracking().SingleAsync(x => x.Id == link.StoredFileId);
        var change = await s.Changes.CreateAsync(file.Id, new("Replace", "نسخة محدثة", Convert.ToBase64String(file.RowVersion)));
        var target = await s.Changes.RequireUploadAsync(change.Id);
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7\nNew version"));
        await s.Library.UploadAsync(new(bytes, "نسخة.pdf", bytes.Length, target.FolderId, "replacement", false, ChangeRequestId: change.Id));
        await s.Changes.CompleteCandidateAsync(change.Id);
        change = (await s.Changes.ListAsync(file.Id)).Single(); await s.Changes.ReviewAsync(change.Id, new(true, "نسخة صحيحة", change.RowVersion));
        (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Numerator.Should().Be(0);
        var current = (await s.Links.FileLinksAsync(file.Id)).Single(); current.Status.Should().Be("PendingReview");
        current.Decisions.Single().VersionId.Should().Be(link.VersionId);
        await s.Reviews.ReviewAsync(current.Id, new(EvidenceReviewStatus.Approved, "اعتماد النسخة الجديدة", current.RowVersion));
        (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Numerator.Should().Be(1);
        (await s.Links.FileLinksAsync(file.Id)).Single().Decisions.Should().HaveCount(2);
    }
    [StorageSqlFact]
    public async Task Owner_grant_membership_permission_and_delegation_revocation_take_effect_on_every_request()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var s = Build(db, w); var selected = await Selected(db, w);
        await Link(db, w, selected.First.Id, "teacher", true, true);
        (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Numerator.Should().Be(1);
        var grant = await db.TeacherDriveFolders.SingleAsync(x => x.TeacherId == w.Teacher); grant.IsActive = false; await db.SaveChangesAsync();
        (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Numerator.Should().Be(0); (await s.Readiness.IndexAsync(Filter(w))).Total.Should().Be(0);
        grant.IsActive = true; await db.SaveChangesAsync();
        var member = await db.UserSchoolRoles.SingleAsync(x => x.SchoolId == w.School && x.UserId == w.TeacherUser); member.IsActive = false; await db.SaveChangesAsync();
        (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Numerator.Should().Be(0);
        member.IsActive = true; await db.SaveChangesAsync();
        var delegateRow = new StorageDelegation { SchoolId = w.School, GranteeUserId = w.TeacherUser, GrantedByManagerUserId = TeacherDriveHarness.ManagerUserId, StartsAt = DateTimeOffset.UtcNow.AddMinutes(-1), Reason = "تفويض اختبار" };
        db.Add(delegateRow); await db.SaveChangesAsync(); var delegated = Build(db, w, true);
        (await delegated.Readiness.ReadinessAsync(Filter(w))).Overall.Numerator.Should().Be(1);
        delegateRow.RevokedAt = DateTimeOffset.UtcNow; delegateRow.RevokedByManagerUserId = TeacherDriveHarness.ManagerUserId; delegateRow.RevocationReason = "سحب"; await db.SaveChangesAsync();
        await delegated.Readiness.Invoking(x => x.ReadinessAsync(Filter(w))).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await delegated.Exports.Invoking(x => x.ExportAsync("csv", Filter(w))).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }
    [StorageSqlFact]
    public async Task Teacher_is_denied_school_requirements_gaps_manual_index_and_all_export_formats_and_flags_remain_required()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var teacher = Build(db, w, true);
        await teacher.Readiness.Invoking(x => x.RequirementsAsync(Filter(w))).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await teacher.Readiness.Invoking(x => x.RequirementsAsync(Filter(w), true)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await teacher.Readiness.Invoking(x => x.ManualsAsync(w.Year, 1)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await teacher.Readiness.Invoking(x => x.IndexAsync(Filter(w))).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        foreach (var format in new[] { "csv", "excel", "pdf" }) await teacher.Exports.Invoking(x => x.ExportAsync(format, Filter(w))).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await Build(db, w, enabled: false).Readiness.Invoking(x => x.ReadinessAsync(Filter(w))).Should().ThrowAsync<KeyNotFoundException>();
    }
    [StorageSqlFact]
    public async Task School_year_and_version_isolation_keep_old_snapshot_and_do_not_reinterpret_history()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var other = await Setup(db); var s = Build(db, w); var selected = await Selected(db, w);
        await Link(db, w, selected.First.Id, "one", true);
        (await Build(db, other).Readiness.ReadinessAsync(Filter(other))).Overall.Numerator.Should().Be(0);
        (await s.Readiness.ReadinessAsync(new(other.Year))).Overall.Denominator.Should().Be(0);
        var v2 = ReadinessService.BuiltInTemplate() with { Version = 2, Name = "قالب سنة لاحقة", Items = ReadinessService.BuiltInTemplate().Items.Take(1).ToArray() };
        await new ReadinessRepository(db).AddTemplateAsync(new() { Version = 2, Name = v2.Name, SHA256 = ReadinessService.TemplateHash(v2), SnapshotJson = JsonSerializer.Serialize(v2, new JsonSerializerOptions(JsonSerializerDefaults.Web)) }, default);
        await s.Readiness.InitializeAsync(new(w.Year, 2));
        (await s.Readiness.ReadinessAsync(Filter(w) with { TemplateVersion = 2 })).Overall.Should().Match<ReadinessMetric>(m => m.Denominator == 1 && m.Numerator == 0);
        (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Denominator.Should().Be(36);
        (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Numerator.Should().Be(1);
    }
    [StorageSqlFact]
    public async Task Manual_evaluation_is_independent_keeps_revisions_and_rejects_stale_rowversion_and_wrong_scope()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var s = Build(db, w); var before = await s.Readiness.ReadinessAsync(Filter(w));
        var first = await s.Readiness.SaveManualAsync(new(w.Year, 1, "school", "جيد", 92.5m, "حكم مهني مستقل", null));
        var second = await s.Readiness.SaveManualAsync(new(w.Year, 1, "school", "يحتاج تحسينًا", null, "مراجعة ثانية", first.RowVersion));
        second.Revision.Should().Be(2); (await s.Readiness.ManualHistoryAsync(first.Id)).Should().HaveCount(2);
        (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Should().BeEquivalentTo(before.Overall);
        await s.Readiness.Invoking(x => x.SaveManualAsync(new(w.Year, 1, "school", "ممتاز", 100, "قديم", first.RowVersion))).Should().ThrowAsync<StorageConflictException>();
        await s.Readiness.Invoking(x => x.SaveManualAsync(new(w.Year, 1, "99", "حكم", null, "سبب", null))).Should().ThrowAsync<ArgumentException>();
        (await Build(db, await Setup(db)).Readiness.ManualHistoryAsync(first.Id)).Should().BeEmpty();
        var valueOnly = await s.Readiness.SaveManualAsync(new(w.Year, 1, "1.1", "", 60m, "قيمة مهنية مستقلة", null));
        valueOnly.Value.Should().Be(60m); valueOnly.Judgment.Should().BeEmpty();
        await s.Readiness.Invoking(x => x.SaveManualAsync(new(w.Year, 1, "1.2", "", null, "سبب", null))).Should().ThrowAsync<ArgumentException>();
    }
    [StorageSqlFact]
    public async Task Follow_up_preserves_provenance_audits_assignments_due_dates_and_rejects_stale_versions()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var s = Build(db, w); var selected = await Selected(db, w);
        var after = await s.Readiness.ConfigureAsync(selected.First.Id, Configuration(selected.First, mandatory: false, user: w.TeacherUser));
        after.DueDate.Should().Be(new DateOnly(2026, 10, 20)); after.ResponsibleUserId.Should().Be(w.TeacherUser); after.Fulfilled.Should().BeFalse();
        after.SourceSHA256.Should().Be(selected.First.SourceSHA256); after.FollowUpStatus.Should().Be("InProgress");
        (await s.Readiness.ReadinessAsync(Filter(w))).Overall.Denominator.Should().Be(35);
        (await s.Readiness.FollowUpHistoryAsync(after.Id)).Should().HaveCount(1);
        (await db.AuditLogs.CountAsync(x => x.SchoolId == w.School && x.Action == "Storage.FollowUpConfigured")).Should().Be(1);
        await s.Readiness.Invoking(x => x.ConfigureAsync(after.Id, Configuration(selected.First))).Should().ThrowAsync<StorageConflictException>();
        await s.Readiness.Invoking(x => x.ConfigureAsync(after.Id, Configuration(after, user: TeacherDriveHarness.TeacherAUserId))).Should().ThrowAsync<ArgumentException>();
    }
    [StorageSqlFact]
    public async Task Critical_and_status_display_filters_keep_denominator_and_scope_filters_match_rows_and_summary()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var s = Build(db, w); var selected = await Selected(db, w);
        await Link(db, w, selected.First.Id, "a", true);
        var full = await s.Readiness.ReadinessAsync(Filter(w));
        (await s.Readiness.ReadinessAsync(Filter(w) with { CriticalOnly = true, HideCompleted = true, Status = "NoFile" })).Overall.Should().BeEquivalentTo(full.Overall);
        (await s.Readiness.RequirementsAsync(Filter(w) with { CriticalOnly = true }, true)).Total.Should().Be(3);
        var domain = await s.Readiness.ReadinessAsync(Filter(w) with { StandardCode = "1.1" }); domain.Overall.Denominator.Should().Be(4); domain.Overall.Numerator.Should().Be(1);
        (await s.Readiness.RequirementsAsync(Filter(w) with { StandardCode = "1.1" })).Items.Should().OnlyContain(x => x.StandardCode == "1.1");
        var gapsFilter = Filter(w) with { GapsOnly = true };
        var export = await s.Readiness.ExportDataAsync(gapsFilter);
        export.Rows.Should().HaveCount((await s.Readiness.RequirementsAsync(gapsFilter, true)).Total);
        export.Rows.Should().OnlyContain(x => x.IsMandatory && !x.Fulfilled);
    }
    [StorageSqlFact]
    public async Task Exported_CSV_Excel_and_PDF_are_actual_files_with_same_filtered_numbers_and_Arabic_rows()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var s = Build(db, w); var selected = await Selected(db, w);
        await Link(db, w, selected.First.Id, "export", true);
        await s.Readiness.SaveManualAsync(new(w.Year, 1, "1.1", "مراجعة مستقلة", 75m, "الجاهزية دليل وليست حكمًا", null));
        var filter = Filter(w) with { StandardCode = "1.1", HideCompleted = true, TrackerOnly = true };
        var data = await s.Readiness.ExportDataAsync(filter); data.Rows.Should().HaveCount(3); data.Summary.Overall.Numerator.Should().Be(1); data.Summary.Overall.Denominator.Should().Be(4);
        var csv = new ReadinessExportRenderer().Render("csv", data); csv.Content.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var text = Encoding.UTF8.GetString(csv.Content); text.Should().Contain("المدرسة").And.Contain("\"1\",\"4\",\"25.00\"");
        foreach (var row in data.Rows) text.Should().Contain(row.Name.Replace("\"", "\"\""));
        var excel = new ReadinessExportRenderer().Render("excel", data);
        using var book = new XLWorkbook(new MemoryStream(excel.Content)); book.Worksheet("الجاهزية").RightToLeft.Should().BeTrue();
        book.Worksheet("الجاهزية").Cell(2, 4).GetString().Should().Be("1"); book.Worksheet("الجاهزية").Cell(2, 5).GetString().Should().Be("4");
        book.Worksheet("الجاهزية").Cell(2, 6).GetString().Should().Be("25.00"); book.Worksheet("المتطلبات والنواقص").LastRowUsed()!.RowNumber().Should().Be(4);
        var pdf = new ReadinessExportRenderer().Render("pdf", data); Encoding.ASCII.GetString(pdf.Content.Take(8).ToArray()).Should().StartWith("%PDF");
        var folder = Environment.GetEnvironmentVariable("ALFALAH_S4_EXPORT_DIRECTORY");
        if (folder != null) { Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, "filtered.csv"), csv.Content); File.WriteAllBytes(Path.Combine(folder, "filtered.xlsx"), excel.Content); File.WriteAllBytes(Path.Combine(folder, "filtered.pdf"), pdf.Content);
            File.WriteAllText(Path.Combine(folder, "expected.json"), JsonSerializer.Serialize(new { summary = data.Summary, rows = data.Rows, manual = data.ManualEvaluations }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            File.WriteAllBytes(Path.Combine(folder, "full.pdf"), (await s.Exports.ExportAsync("pdf", Filter(w) with { TrackerOnly = true })).Content);
            File.WriteAllBytes(Path.Combine(folder, "empty.pdf"), (await s.Exports.ExportAsync("pdf", Filter(w) with { StandardCode = "1.5", TrackerOnly = true })).Content); }
        var injected = data with { Rows = [data.Rows[0] with { Name = " \t=HYPERLINK(\"bad\")" }] };
        Encoding.UTF8.GetString(new ReadinessExportRenderer().Render("csv", injected).Content).Should().Contain("' \t=HYPERLINK");
    }
    [StorageSqlFact]
    public async Task SQL_enforces_immutable_templates_scope_provenance_manual_revisions_and_follow_up_history()
    {
        await using var db = fixture.Db(); var w = await Setup(db); var s = Build(db, w); var selected = await Selected(db, w);
        var manual = await s.Readiness.SaveManualAsync(new(w.Year, 1, "school", "جيد", 70m, "سبب", null));
        await s.Readiness.ConfigureAsync(selected.First.Id, Configuration(selected.First));
        foreach (var sql in new[] { "UPDATE SelfEvaluationTemplates SET Name='changed' WHERE Version=1", "DELETE FROM ManualEvaluationRevisions WHERE ManualEvaluationId=" + manual.Id,
            "DELETE FROM RequirementFollowUpRevisions WHERE RequirementId=" + selected.First.Id, "UPDATE EvidenceRequirements SET SourceSHA256='changed' WHERE Id=" + selected.First.Id,
            "UPDATE ManualEvaluations SET AcademicYearId=1 WHERE Id=" + manual.Id, "DELETE FROM SchoolEvaluationScopes WHERE SchoolId=" + w.School })
            await FluentActions.Invoking(() => db.Database.ExecuteSqlRawAsync(sql)).Should().ThrowAsync<SqlException>();
        (await s.Readiness.ManualHistoryAsync(manual.Id)).Should().HaveCount(1);
        (await s.Readiness.FollowUpHistoryAsync(selected.First.Id)).Should().HaveCount(1);
    }
    [StorageSqlFact]
    public async Task S4_SQL_script_applies_twice_on_S3_preserving_old_requirements_and_model()
    {
        var connection = new SqlConnectionStringBuilder(fixture.Connection); connection.InitialCatalog += "_Script_" + Guid.NewGuid().ToString("N");
        await using var db = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection.ConnectionString).Options);
        var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20261003233933_SchoolFileStorageReviewerIdentity");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO EvidenceRequirements (TemplateVersion,Code,DisplayName,Importance,FulfillmentPolicy,MinimumApprovedLinks,SortOrder,IsActive,CreatedAtUtc,UpdatedAtUtc) VALUES (1,'sentinel','legacy',1,1,1,1,1,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET())");
        var script = migrator.GenerateScript("20261003233933_SchoolFileStorageReviewerIdentity", "20261004015709_SchoolFileStorageSelfEvaluation", MigrationsSqlGenerationOptions.Idempotent);
        var path = Environment.GetEnvironmentVariable("ALFALAH_S4_MIGRATION_SCRIPT"); if (path != null) File.WriteAllText(path, script);
        var batches = System.Text.RegularExpressions.Regex.Split(script, @"^GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        await db.Database.OpenConnectionAsync();
        for (var run = 0; run < 2; run++) foreach (var batch in batches.Where(x => !string.IsNullOrWhiteSpace(x)))
        { using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = batch; await command.ExecuteNonQueryAsync(); }
        await db.Database.CloseConnectionAsync();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty(); db.Database.HasPendingModelChanges().Should().BeFalse();
        var sentinel = await db.EvidenceRequirements.SingleAsync(x => x.Code == "sentinel"); sentinel.IsMandatory.Should().BeFalse(); sentinel.FollowUpStatus.Should().Be("NotStarted");
    }
    [StorageSqlFact]
    public async Task SQL_pagination_and_aggregations_over_5000_links_are_measured_and_export_limit_is_enforced()
    {
        var counter = new QueryCounter(); await using var db = fixture.Db(counter); var w = await Setup(db); var s = Build(db, w);
        await s.Library.CreateFolderAsync(new(null, "", "root")); var folder = await db.StorageFolders.SingleAsync(x => x.SchoolId == w.School && x.Kind == StorageFolderKind.SchoolLibrary);
        var files = Enumerable.Range(0, 250).Select(i => new StoredFile { SchoolId = w.School, FolderId = folder.Id, SourceKind = StoredFileSourceKind.SchoolUpload, DisplayName = $"شاهد {i}" }).ToList();
        db.AddRange(files); await db.SaveChangesAsync();
        var versions = files.Select((f, i) => new StoredFileVersion { SchoolId = w.School, StoredFileId = f.Id, VersionNumber = 1, DriveItemId = "perf-" + i, DriveFileName = f.DisplayName, Availability = StoredFileAvailability.Available }).ToList();
        foreach (var version in versions) w.Drive.AddFile(version.DriveItemId, version.DriveFileName, folder.DriveItemId);
        db.AddRange(versions); await db.SaveChangesAsync(); for (var i = 0; i < files.Count; i++) files[i].CurrentVersionId = versions[i].Id; await db.SaveChangesAsync();
        var requirements = Enumerable.Range(0, 5000).Select(i => new EvidenceRequirement { SchoolId = w.School, AcademicYearId = w.Year, Code = $"PERF-{i:D5}", DisplayName = $"متطلب {i}", IsMandatory = true, DomainCode = "2", StandardCode = "2.1", SortOrder = 1000 + i }).ToList();
        db.AddRange(requirements); await db.SaveChangesAsync();
        db.EvidenceLinks.AddRange(requirements.Select((r, i) => new EvidenceLink { SchoolId = w.School, AcademicYearId = w.Year, RequirementId = r.Id, StoredFileId = files[i % 250].Id, VersionId = versions[i % 250].Id, Status = EvidenceLinkStatus.PendingReview }));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var filter = Filter(w) with { Page = 100, PageSize = 25 }; await s.Readiness.RequirementsAsync(filter);
        var samples = new List<double>(); var queries = new List<int>(); var total = 5036 + await db.EvidenceTasks.CountAsync() + 11;
        for (var i = 0; i < 10; i++) { counter.Count = 0; var timer = Stopwatch.StartNew(); var page = await s.Readiness.RequirementsAsync(filter); timer.Stop(); page.Items.Should().HaveCount(25); page.Total.Should().Be(total); samples.Add(timer.Elapsed.TotalMilliseconds); queries.Add(counter.Count); }
        var median = samples.Order().ElementAt(samples.Count / 2); output.WriteLine($"5000 links, 250 files, 5036+optional requirements: median {median:F2}ms, max {samples.Max():F2}ms, queries {queries.Max()}");
        median.Should().BeLessThan(200); queries.Distinct().Should().HaveCount(1);
        var summary = await s.Readiness.ReadinessAsync(filter); summary.Overall.Numerator.Should().Be(0); summary.Overall.Denominator.Should().Be(5036); summary.Overall.UniqueFiles.Should().Be(250);
        await s.Readiness.Invoking(x => x.ExportDataAsync(filter)).Should().ThrowAsync<ArgumentException>().WithMessage("*5000*");
        var report = Environment.GetEnvironmentVariable("ALFALAH_S4_PERFORMANCE_REPORT"); if (report != null) File.WriteAllText(report, JsonSerializer.Serialize(new { stage = "S4", scope = "isolated SQL + fake Drive", links = 5000, files = 250, mandatoryRequirements = 5036, pageSize = 25, page = 100, warmup = 1, samples, medianMs = median, maxMs = samples.Max(), sqlQueries = queries, exportLimit = ReadinessService.ExportLimit, liveVerificationLimit = ReadinessService.LiveVerificationLimit, targetMs = 200 }));
    }
    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        { Count++; return base.ReaderExecutingAsync(command, eventData, result, cancellationToken); }
    }
}
