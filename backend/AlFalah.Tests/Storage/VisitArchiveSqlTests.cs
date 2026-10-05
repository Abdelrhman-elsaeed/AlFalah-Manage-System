using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text.Json;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Data.Seeders;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.TestDoubles;
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

namespace AlFalah.Tests.Storage;

[CollectionDefinition("Visit archive SQL", DisableParallelization = true)]
public sealed class VisitArchiveSqlCollection : ICollectionFixture<VisitArchiveSqlFixture>;
public sealed class VisitArchiveSqlFixture : IAsyncLifetime
{
    private StorageSqlFixture inner = new();
    public string Connection => inner.Connection;
    public AlFalahDbContext Db(DbCommandInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(Connection);
        if (interceptor != null) builder.AddInterceptors(interceptor);
        return new(builder.Options);
    }
    public async Task InitializeAsync()
    {
        var value = Environment.GetEnvironmentVariable("ALFALAH_STORAGE_TEST_CONNECTION");
        if (value != null) { var builder = new SqlConnectionStringBuilder(value); builder.InitialCatalog += "_Archive"; inner = new(builder.ConnectionString); }
        await inner.InitializeAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
}

[Collection("Visit archive SQL")]
public sealed class VisitArchiveSqlTests(VisitArchiveSqlFixture fixture)
{
    private sealed record World(int School, string Teacher, string Moderator, string Root, FakeGoogleDrive Drive);
    private sealed class Flags : IFeatureFlagService { public bool IsVisitsV2Enabled(int? schoolId = null) => true; }
    private static readonly StorageOptions On = new() { ReadModelEnabled = true, AdministrationEnabled = true, ArchiveWorkerEnabled = true, ArchiveExternalWritesEnabled = true };
    private static AuditLogWriter Audit(AlFalahDbContext db) => new(db, new HttpContextAccessor(), NullLogger<AuditLogWriter>.Instance);
    private VisitV2Service Visits(AlFalahDbContext db, World world, bool moderator = false, IVisitArchiveAssetFreezer? freezer = null)
    {
        ICurrentUserService user = moderator ? new TeacherDriveHarness.TestCurrentUser(RoleNames.Moderator, world.Moderator, world.School, true) : TeacherDriveHarness.Manager(world.School);
        return new(new VisitV2Repository(db), new VisitV2DocumentService(new ImageAssetLoader()), user, new Flags(),
            new SchoolScopeGuard(db, user, NullLogger<SchoolScopeGuard>.Instance), Audit(db), NullLogger<VisitV2Service>.Instance,
            new VisitArchiveCaptureService(new VisitArchiveRepository(db), freezer ?? new VisitArchiveAssetFreezer(new ImageAssetLoader())));
    }
    private VisitArchiveProcessor Processor(AlFalahDbContext db, World world, StorageOptions? flags = null)
    {
        var repository = new VisitArchiveRepository(db); var provider = new GoogleStorageProvider(world.Drive);
        return new(repository, new(new StorageRepository(db), new StorageLibraryRepository(db), repository, provider), provider,
            new VisitV2DocumentService(new ImageAssetLoader()), Options.Create(flags ?? On), TimeProvider.System, NullLogger<VisitArchiveProcessor>.Instance);
    }
    private VisitArchiveService Reader(AlFalahDbContext db, World world, ICurrentUserService? user = null)
    {
        user ??= TeacherDriveHarness.Manager(world.School);
        var repository = new VisitArchiveRepository(db); var storage = new StorageRepository(db); var provider = new GoogleStorageProvider(world.Drive);
        return new(repository, storage, new StorageAuthorizationService(storage, user, new StorageDriveBoundary(new(world.Drive)), TimeProvider.System),
            user, new Flags(), new(storage, new StorageLibraryRepository(db), repository, provider), provider, Options.Create(On), TimeProvider.System);
    }
    private async Task<World> Setup(AlFalahDbContext db)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var key = Guid.NewGuid().ToString("N");
        var school = new School { Name = "مدرسة الفلاح للتحقق S5", City = "الرياض", Stage = SchoolStage.Primary, ManagerUserId = TeacherDriveHarness.ManagerUserId };
        var teacher = new ApplicationUser { Id = "s5-teacher-" + key, UserName = "t-" + key, FirstName = "معلم", LastName = "الاختبار" };
        var moderator = new ApplicationUser { Id = "s5-moderator-" + key, UserName = "m-" + key, FirstName = "مشرف", LastName = "الاختبار" };
        db.AddRange(school, teacher, moderator);
        if (!await db.Roles.AnyAsync(r => r.Id == "s5-moderator")) db.Roles.Add(new() { Id = "s5-moderator", Name = RoleNames.Moderator });
        if (!await db.Permissions.AnyAsync(p => p.Name == PermissionNames.VisitView)) db.Permissions.Add(new() { Name = PermissionNames.VisitView, Group = "Visits", DescriptionAr = "عرض الزيارات" });
        await db.SaveChangesAsync();
        var permission = await db.Permissions.SingleAsync(p => p.Name == PermissionNames.VisitView);
        if (!await db.RolePermissions.AnyAsync(p => p.RoleId == "manager" && p.PermissionId == permission.Id)) db.RolePermissions.Add(new() { RoleId = "manager", PermissionId = permission.Id });
        var profile = new InstructorProfile { UserId = teacher.Id, SchoolId = school.Id }; db.Add(profile);
        db.UserSchoolRoles.AddRange(new() { SchoolId = school.Id, UserId = TeacherDriveHarness.ManagerUserId, RoleId = "manager" },
            new() { SchoolId = school.Id, UserId = teacher.Id, RoleId = "teacher" }, new() { SchoolId = school.Id, UserId = moderator.Id, RoleId = "s5-moderator" });
        var root = "s5-root-" + key;
        db.SchoolGoogleDrives.Add(new() { SchoolId = school.Id, RootFolderId = root, ProtectedCredential = "s5-unchanged-sentinel", IsEnabled = true });
        db.SchoolReportSettings.Add(new() { SchoolId = school.Id, ReportHeaderText = "مدارس الفلاح — مدرسة التحقق", ReportFooterText = "وثيقة اعتماد مدرسية", PrimaryColor = "#0F7132" });
        await db.SaveChangesAsync();
        db.TeacherDriveFolders.Add(new() { SchoolId = school.Id, TeacherId = profile.Id, DriveId = "", RootItemId = root + "-teacher", FolderDisplayName = "المعلم" });
        // Three real PNG asset payloads stand in for authorized signatures in isolated SQL.
        var signature = "data:image/png;base64," + Convert.ToBase64String(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "Logo.png")));
        foreach (var userId in new[] { teacher.Id, moderator.Id }) db.UserSignatures.Add(new() { UserId = userId, SignatureDrawnData = signature });
        if (!await db.UserSignatures.AnyAsync(s => s.UserId == TeacherDriveHarness.ManagerUserId)) db.UserSignatures.Add(new() { UserId = TeacherDriveHarness.ManagerUserId, SignatureDrawnData = signature });
        await db.SaveChangesAsync();
        await new RubricV2Seeder(db, NullLogger<RubricV2Seeder>.Instance).SeedAsync();
        return new(school.Id, teacher.Id, moderator.Id, root, new FakeGoogleDrive().AddFolder(root, "مدرسة").AddFolder(root + "-teacher", "معلم", root).AddFolder("outside", "خارج المدرسة"));
    }
    private async Task<int> Create(AlFalahDbContext db, World world, bool moderator = false)
    {
        var visit = await Visits(db, world, moderator).CreateAsync(new(world.Teacher, 1, 1, DateTimeOffset.UtcNow, 2,
            "اللغة العربية", "الأول أ", "درس الاعتماد الأصلي", 24, 1, "ملاحظات محفوظة"));
        // Real score graph exists from the real V2 service, all scores default to one.
        return visit.Id;
    }
    private async Task<VisitArchiveOperation> Operation(AlFalahDbContext db, int id, int revision = 1)
    { db.ChangeTracker.Clear(); return await db.Set<VisitArchiveOperation>().SingleAsync(o => o.VisitId == id && o.ApprovalRevision == revision); }
    private async Task Ready(AlFalahDbContext db, int id)
    { await db.Set<VisitArchiveOperation>().Where(o => o.VisitId == id && o.Status == VisitArchiveStatus.RetryScheduled).ExecuteUpdateAsync(s => s.SetProperty(o => o.NextAttemptAtUtc, DateTimeOffset.UtcNow.AddSeconds(-1))); db.ChangeTracker.Clear(); }

    [StorageSqlTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Real_manual_and_automatic_approval_stage_exactly_one_snapshot_and_shared_official_PDF(bool manual)
    {
        var metrics = new Dictionary<string, double>();
        using var listener = new MeterListener { InstrumentPublished = (instrument, meterListener) => { if (instrument.Meter.Name == "AlFalah.Storage.Archive") meterListener.EnableMeasurementEvents(instrument); } };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => metrics[instrument.Name] = value); listener.Start();
        await using var db = fixture.Db(); var world = await Setup(db); var id = await Create(db, world, manual);
        var visits = Visits(db, world, manual); var finalized = await visits.FinalizeAsync(id);
        if (manual)
        {
            finalized.Status.Should().Be((int)VisitStatus.PendingApproval);
            (await db.Set<VisitArchiveOperation>().CountAsync(o => o.VisitId == id)).Should().Be(0);
            await Visits(db, world).ApproveAsync(id);
        }
        var operation = await Operation(db, id); operation.Status.Should().Be(VisitArchiveStatus.Pending);
        var snapshot = JsonSerializer.Deserialize<VisitApprovalSnapshot>(operation.SnapshotJson!)!;
        snapshot.Report.Analysis!.MaximumScore.Should().Be(100); snapshot.Report.Domains.Should().HaveCount(5);
        snapshot.Assets.ManagerSignatureSource.Should().StartWith("data:image/"); snapshot.Assets.Frozen.Should().BeTrue();
        world.Drive.Uploads.Should().BeEmpty(); // no PDF or Drive in HTTP approval
        operation.PdfBytes.Should().BeNull();
        await FluentActions.Invoking(() => Visits(db, world).ApproveAsync(id)).Should().ThrowAsync<BusinessRuleException>();
        await Processor(db, world).ProcessBatchAsync();
        operation = await Operation(db, id); operation.Status.Should().Be(VisitArchiveStatus.Completed);
        (await db.Set<VisitArchiveOperation>().CountAsync(o => o.VisitId == id)).Should().Be(1);
        world.Drive.Uploads.Should().ContainSingle();
        var pdf = await Visits(db, world).ExportPdfAsync(id);
        var zipped = await Visits(db,world).ExportZipAsync(new VisitV2ArchiveQuery());
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(zipped.ZipBytes));
        using var zipPdf = new MemoryStream();
        await zip.GetEntry($"visit-v2-{id}.pdf")!.Open().CopyToAsync(zipPdf);
        // PDF metadata may change between renders; compare rendered bytes in the export verifier.
        var folder = Environment.GetEnvironmentVariable("ALFALAH_S5_EXPORT_DIRECTORY");
        if (folder != null)
        {
            Directory.CreateDirectory(folder); var name = manual ? "manual" : "automatic";
            await File.WriteAllBytesAsync(Path.Combine(folder, name + "-archive.pdf"), operation.PdfBytes!);
            await File.WriteAllBytesAsync(Path.Combine(folder, name + "-official.pdf"), pdf.Content);
            await File.WriteAllBytesAsync(Path.Combine(folder, name + "-zip.pdf"), zipPdf.ToArray());
            await File.WriteAllTextAsync(Path.Combine(folder, name + "-snapshot.json"), operation.SnapshotJson!);
            await File.WriteAllTextAsync(Path.Combine(folder, name + "-metrics.json"), JsonSerializer.Serialize(new { metrics, PdfBytes=operation.PdfBytes!.Length, FakeProvider=true, LiveGoogleTimingMeasured=false }));
        }
        (await db.EvidenceLinks.CountAsync(l => l.SchoolId == world.School)).Should().Be(0);
        (await Reader(db, world).GetAsync(id)).Revisions.Should().ContainSingle(r => r.Status == "Completed" && r.IsCurrent);
    }
    [StorageSqlFact]
    public async Task Outbox_failure_rolls_back_approval_analysis_and_revision_in_the_same_SQL_transaction()
    {
        await using var db = fixture.Db(); var world = await Setup(db); var id = await Create(db, world);
        await using var failing = fixture.Db(new FailCommand("INSERT INTO [VisitArchiveOperations]"));
        await FluentActions.Invoking(() => Visits(failing, world).FinalizeAsync(id)).Should().ThrowAsync<Exception>();
        db.ChangeTracker.Clear(); var visit = await db.Visits.SingleAsync(v => v.Id == id);
        visit.Status.Should().Be(VisitStatus.Draft); visit.ApprovalRevision.Should().Be(0);
        (await db.Set<VisitArchiveOperation>().CountAsync(o => o.VisitId == id)).Should().Be(0);
        (await db.VisitAnalyses.CountAsync(a => a.VisitId == id)).Should().Be(0);
    }
    [StorageSqlFact]
    public async Task Reject_pending_and_reopen_never_create_an_extra_operation()
    {
        await using var db = fixture.Db(); var world = await Setup(db); var id = await Create(db, world, true);
        await Visits(db, world, true).FinalizeAsync(id); await Visits(db, world).RejectAsync(id, "تحتاج تعديلًا");
        (await db.Set<VisitArchiveOperation>().CountAsync(o => o.VisitId == id)).Should().Be(0);
    }
    [StorageSqlTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Drive_failure_or_lost_response_recovers_without_another_file_and_preserves_Approved(bool loseResponse)
    {
        await using var db = fixture.Db(); var world = await Setup(db); var id = await Create(db, world); await Visits(db, world).FinalizeAsync(id);
        world.Drive.LoseNextUploadResponse = loseResponse; world.Drive.FailNextUpload = !loseResponse;
        await Processor(db, world).ProcessBatchAsync(); var operation = await Operation(db, id);
        operation.Status.Should().Be(VisitArchiveStatus.RetryScheduled);
        (await db.Visits.AsNoTracking().SingleAsync(v => v.Id == id)).Status.Should().Be(VisitStatus.Approved);
        var reserved = operation.ProviderItemId; reserved.Should().NotBeNull();
        await Ready(db, id); await using var restarted = fixture.Db(); await Processor(restarted, world).ProcessBatchAsync();
        operation = await Operation(db, id); operation.Status.Should().Be(VisitArchiveStatus.Completed); operation.ProviderItemId.Should().Be(reserved);
        world.Drive.Uploads.Should().ContainSingle();
    }
    [StorageSqlFact]
    public async Task SQL_failure_after_Drive_commit_reconciles_reserved_identity_without_a_second_upload()
    {
        await using var db = fixture.Db(); var world = await Setup(db); var id = await Create(db, world); await Visits(db, world).FinalizeAsync(id);
        await using var failing = fixture.Db(new FailCommand("INSERT INTO [VisitArchiveArtifacts]"));
        await Processor(failing, world).ProcessBatchAsync();
        (await Operation(db, id)).Status.Should().Be(VisitArchiveStatus.RetryScheduled);
        (await db.StoredFiles.CountAsync(f => f.SchoolId == world.School)).Should().Be(0);
        world.Drive.Uploads.Should().ContainSingle(); await Ready(db, id);
        await using var restarted = fixture.Db(); await Processor(restarted, world).ProcessBatchAsync();
        (await Operation(db, id)).Status.Should().Be(VisitArchiveStatus.Completed); world.Drive.Uploads.Should().ContainSingle();
    }
    [StorageSqlFact]
    public async Task Two_workers_and_expired_lease_during_blocked_upload_do_not_duplicate_execution()
    {
        await using var db = fixture.Db(); var world = await Setup(db); var id = await Create(db, world); await Visits(db, world).FinalizeAsync(id);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        world.Drive.BeforeUpload = async () => { entered.TrySetResult(); await release.Task; };
        await using var first = fixture.Db(); await using var second = fixture.Db();
        var running = Processor(first, world).ProcessBatchAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await db.Set<VisitArchiveOperation>().Where(o => o.VisitId == id).ExecuteUpdateAsync(s => s.SetProperty(o => o.LeaseExpiresAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await Processor(second, world).ProcessBatchAsync(); world.Drive.Uploads.Should().BeEmpty();
        await FluentActions.Invoking(() => Reader(db, world).RetryAsync(id, new(1))).Should().ThrowAsync<StorageConflictException>();
        release.TrySetResult(); await running.WaitAsync(TimeSpan.FromSeconds(30));
        world.Drive.Uploads.Should().ContainSingle(); (await Operation(db, id)).Status.Should().Be(VisitArchiveStatus.Completed);
    }
    [StorageSqlFact]
    public async Task Abandoned_lease_is_claimable_after_process_restart_and_renewal_prevents_early_reclaim()
    {
        await using var db = fixture.Db(); var world = await Setup(db); var id = await Create(db, world); await Visits(db, world).FinalizeAsync(id);
        var repository = new VisitArchiveRepository(db); var o = await Operation(db, id); var lease = Guid.NewGuid();
        await repository.ClaimAsync(o.Id, DateTimeOffset.UtcNow, lease, default);
        await repository.RenewAsync(o.Id, lease, DateTimeOffset.UtcNow, default);
        (await repository.DueAsync(DateTimeOffset.UtcNow, 100, default)).Should().NotContain(o.Id);
        await db.Set<VisitArchiveOperation>().Where(x => x.Id == o.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseExpiresAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await using var restarted = fixture.Db(); await Processor(restarted, world).ProcessBatchAsync();
        (await Operation(db, id)).Status.Should().Be(VisitArchiveStatus.Completed); world.Drive.Uploads.Should().ContainSingle();
    }
    [StorageSqlFact]
    public async Task Reopen_reapprove_keeps_snapshot_history_and_a_late_old_worker_cannot_replace_new_Current()
    {
        await using var db = fixture.Db(); var world = await Setup(db); var id = await Create(db, world); await Visits(db, world).FinalizeAsync(id);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstUpload = true;
        world.Drive.BeforeUpload = async () => { if (!firstUpload) return; firstUpload = false; entered.TrySetResult(); await release.Task; };
        await using var oldDb = fixture.Db(); var oldWorker = Processor(oldDb, world).ProcessBatchAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await Visits(db, world).ReopenAsync(id, "تصحيح موثق");
        var visit = await db.Visits.SingleAsync(v => v.Id == id); visit.LessonTitle = "الدرس بعد إعادة الفتح"; await db.SaveChangesAsync();
        await Visits(db, world).FinalizeAsync(id);
        (await Reader(db, world).GetAsync(id)).Revisions.Should().NotContain(r => r.IsCurrent);
        await using var newDb = fixture.Db(); await Processor(newDb, world).ProcessBatchAsync();
        release.TrySetResult(); await oldWorker.WaitAsync(TimeSpan.FromSeconds(30));
        db.ChangeTracker.Clear(); var artifacts = await db.Set<VisitArchiveArtifact>().Where(a => a.VisitId == id).ToListAsync();
        artifacts.Should().HaveCount(2); artifacts.Single(a => a.IsCurrent).ApprovalRevision.Should().Be(2);
        JsonSerializer.Deserialize<VisitApprovalSnapshot>((await Operation(db, id)).SnapshotJson!)!.Report.LessonTitle.Should().Be("درس الاعتماد الأصلي");
        world.Drive.Uploads.Should().HaveCount(2);
        await Visits(db, world).ReopenAsync(id, "مراجعة أخرى"); db.ChangeTracker.Clear();
        (await db.Set<VisitArchiveArtifact>().Where(a => a.VisitId == id).AnyAsync(a => a.IsCurrent)).Should().BeFalse();
    }
    [StorageSqlTheory]
    [InlineData("delete")] [InlineData("trash")] [InlineData("move")] [InlineData("folder-move")]
    public async Task External_missing_is_not_SQL_success_and_authorized_recreation_preserves_original_file_version(string action)
    {
        await using var db = fixture.Db(); var world = await Setup(db); var id = await Create(db, world); await Visits(db, world).FinalizeAsync(id); await Processor(db, world).ProcessBatchAsync();
        var operation = await Operation(db, id); var hash = operation.PdfSHA256; var snapshot = operation.SnapshotJson;
        if (action == "delete") world.Drive.RemoveExternally(operation.ProviderItemId!);
        if (action == "trash") world.Drive.TrashExternally(operation.ProviderItemId!);
        if (action == "move") world.Drive.MoveExternally(operation.ProviderItemId!, "outside");
        if (action == "folder-move") world.Drive.MoveExternally(operation.ArchiveFolderItemId!, "outside");
        var state = await Reader(db, world).GetAsync(id); state.Revisions[0].Status.Should().Be("MissingFromDrive");
        (await Reader(db,world).ListAsync(new(Status:"MissingFromDrive"))).Total.Should().Be(1);
        (await Reader(db,world).ListAsync(new(Status:"Completed"))).Total.Should().Be(0);
        await FluentActions.Invoking(() => Reader(db, world).ContentAsync(id, 1, null)).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => Reader(db, world).RetryAsync(id, new(1))).Should().ThrowAsync<StorageConflictException>();
        if (action == "folder-move")
        {
            world.Drive.MoveExternally(operation.ArchiveFolderItemId!, world.Root);
            (await Reader(db, world).GetAsync(id)).Revisions[0].Status.Should().Be("Completed");
            await FluentActions.Invoking(() => Reader(db,world).RetryAsync(id,new(1,true,"already restored"))).Should().ThrowAsync<StorageConflictException>();
            world.Drive.Uploads.Should().ContainSingle(); return;
        }
        var queued=await Reader(db, world).RetryAsync(id, new(1, true, "استعادة مصرح بها"));
        queued.Revisions[0].Status.Should().Be("Pending");queued.Revisions[0].Versions[0].Availability.Should().Be("MissingFromDrive");
        await Processor(db, world).ProcessBatchAsync();
        operation = await Operation(db, id); operation.Status.Should().Be(VisitArchiveStatus.Completed); operation.ApprovalRevision.Should().Be(1);
        operation.PdfSHA256.Should().Be(hash); operation.SnapshotJson.Should().Be(snapshot);
        var artifact = await db.Set<VisitArchiveArtifact>().SingleAsync(a => a.VisitId == id);
        artifact.OriginalStoredFileVersionId.Should().NotBe(artifact.StoredFileVersionId);
        await FluentActions.Invoking(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE VisitArchiveArtifacts WHERE Id={artifact.Id}")).Should().ThrowAsync<SqlException>();
        await FluentActions.Invoking(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE VisitArchiveArtifacts SET OriginalStoredFileVersionId={artifact.StoredFileVersionId} WHERE Id={artifact.Id}")).Should().ThrowAsync<SqlException>();
        (await Reader(db, world).GetAsync(id)).Revisions[0].Versions.Should().HaveCount(2);
        world.Drive.Uploads.Should().HaveCount(2);
    }
    [StorageSqlFact]
    public async Task Current_school_teacher_and_revoked_delegate_permissions_protect_every_archive_and_generic_file_path()
    {
        await using var db = fixture.Db(); var world = await Setup(db); var id = await Create(db, world); await Visits(db, world).FinalizeAsync(id); await Processor(db, world).ProcessBatchAsync();
        var teacher = new TeacherDriveHarness.TestCurrentUser(RoleNames.Instructor, world.Teacher, world.School);
        var read = Reader(db, world, teacher); (await read.GetAsync(id)).CanManage.Should().BeFalse();
        await using var content = (await read.ContentAsync(id, 1, null)).Content; content.Length.Should().BeGreaterThan(10000);
        await FluentActions.Invoking(() => read.ListAsync(new())).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(() => read.RetryAsync(id, new(1))).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        var foreignTeacher = new TeacherDriveHarness.TestCurrentUser(RoleNames.Instructor, TeacherDriveHarness.TeacherAUserId, world.School);
        await FluentActions.Invoking(() => Reader(db, world, foreignTeacher).GetAsync(id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(() => Reader(db, world with { School = 1 }).GetAsync(id)).Should().ThrowAsync<KeyNotFoundException>();
        var file = await db.StoredFiles.SingleAsync(f => f.SchoolId == world.School);
        var scopes = new StorageRepository(db); var manager = TeacherDriveHarness.Manager(world.School);
        var auth = new StorageAuthorizationService(scopes, manager, new StorageDriveBoundary(new(world.Drive)), TimeProvider.System);
        await FluentActions.Invoking(() => auth.RequireFileAsync(world.School, file.Id, metadataOnly:true)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(() => auth.RequireFileAsync(world.School, file.Id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        var provider=new GoogleStorageProvider(world.Drive);
        var library=new StorageLibraryService(new StorageLibraryRepository(db),scopes,auth,manager,provider,
            new EvidenceSubmissionService(db,Audit(db)),Options.Create(On));
        await FluentActions.Invoking(()=>library.DetailsAsync(file.Id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(()=>library.ContentAsync(file.Id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        var context=new EvidenceWorkflowContext(new EvidenceRepository(db),scopes,auth,manager,provider,Options.Create(On));
        var links=new EvidenceLinkService(new EvidenceRepository(db),scopes,context);
        await FluentActions.Invoking(()=>links.FileLinksAsync(file.Id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(()=>links.CreateAsync(file.Id,new(1,1))).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        (await new StorageLibraryRepository(db).FilesAsync(world.School,null,new(Global:true),default)).Items.Should().BeEmpty();
        var year=new AcademicYear {Code="S5-"+Guid.NewGuid().ToString("N")[..20],NameAr="سنة التحقق",IsActive=false};db.Add(year);await db.SaveChangesAsync();
        var readiness=new ReadinessService(new ReadinessRepository(db),new EvidenceRepository(db),scopes,context,provider);
        await new RequirementCatalogService(new EvidenceRepository(db),scopes,context).InitializeAsync(year.Id);
        await readiness.InitializeAsync(new(year.Id));
        (await readiness.IndexAsync(new(year.Id))).Items.Should().BeEmpty();
        (await readiness.ExportDataAsync(new(year.Id))).Files.Should().BeEmpty();
        var permission = await db.Permissions.SingleAsync(p => p.Name == PermissionNames.VisitView);
        if (!await db.RolePermissions.AnyAsync(p => p.RoleId == "s5-moderator" && p.PermissionId == permission.Id)) { db.RolePermissions.Add(new() { RoleId = "s5-moderator", PermissionId = permission.Id }); await db.SaveChangesAsync(); }
        var delegateUser = new TeacherDriveHarness.TestCurrentUser("Secretary", world.Moderator, world.School);
        var grant = new StorageDelegation { SchoolId = world.School, GranteeUserId = world.Moderator, GrantedByManagerUserId = TeacherDriveHarness.ManagerUserId, StartsAt = DateTimeOffset.UtcNow.AddMinutes(-1), Reason = "تفويض اختبار" };
        db.StorageDelegations.Add(grant); await db.SaveChangesAsync();
        (await Reader(db, world, delegateUser).GetAsync(id)).CanManage.Should().BeTrue();
        grant.RevokedAt = DateTimeOffset.UtcNow; grant.RevocationReason = "سحب"; await db.SaveChangesAsync();
        await FluentActions.Invoking(() => Reader(db, world, delegateUser).GetAsync(id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await Visits(db, world).ReopenAsync(id, "إعادة فتح");
        await FluentActions.Invoking(() => read.GetAsync(id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }
    [StorageSqlFact]
    public async Task Flags_default_OFF_and_SQL_snapshots_and_PDF_bytes_are_immutable_and_retained_on_rollback()
    {
        await using var db = fixture.Db(); var world = await Setup(db); var id = await Create(db, world); await Visits(db, world).FinalizeAsync(id);
        (await Processor(db, world, new()).ProcessBatchAsync()).Should().Be(0); world.Drive.Uploads.Should().BeEmpty();
        var o = await Operation(db, id);
        await FluentActions.Invoking(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE VisitArchiveOperations SET SnapshotJson='changed' WHERE Id={o.Id}")).Should().ThrowAsync<SqlException>();
        await FluentActions.Invoking(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE VisitArchiveOperations WHERE Id={o.Id}")).Should().ThrowAsync<SqlException>();
        db.Database.HasPendingModelChanges().Should().BeFalse();
        var path = Environment.GetEnvironmentVariable("ALFALAH_S5_MIGRATION_SCRIPT");
        if (path != null) await File.WriteAllTextAsync(path, db.GetService<IMigrator>().GenerateScript("20261004015709_SchoolFileStorageSelfEvaluation", "20261005112201_SchoolFileStorageVisitArchive", MigrationsSqlGenerationOptions.Idempotent));
    }
    private sealed class FailCommand(string text) : DbCommandInterceptor
    {
        private bool armed = true;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { if (armed && command.CommandText.Contains(text, StringComparison.Ordinal)) { armed=false; throw new InvalidOperationException("Injected isolated SQL failure"); } return ValueTask.FromResult(result); }
    }
    [StorageSqlFact]
    public async Task Concurrent_approval_requests_use_SQL_rowversion_and_unique_revision_without_duplicate_outbox()
    {
        await using var db = fixture.Db(); var world = await Setup(db); var id = await Create(db, world, true); await Visits(db, world, true).FinalizeAsync(id);
        await using var first = fixture.Db(); await using var second = fixture.Db(); var freezer = new BarrierFreezer();
        async Task<Exception?> Approve(AlFalahDbContext context) {try {await Visits(context, world, freezer:freezer).ApproveAsync(id);return null;}catch(Exception e){return e;}}
        var outcomes = await Task.WhenAll(Approve(first), Approve(second)).WaitAsync(TimeSpan.FromSeconds(30));
        outcomes.Count(e => e == null).Should().Be(1); outcomes.OfType<StorageConflictException>().Should().ContainSingle();
        (await Operation(db, id)).ApprovalRevision.Should().Be(1);
        (await db.Set<VisitArchiveOperation>().CountAsync(o => o.VisitId == id)).Should().Be(1);
    }
    private sealed class BarrierFreezer : IVisitArchiveAssetFreezer
    {
        private readonly TaskCompletionSource both = new(TaskCreationOptions.RunContinuationsAsynchronously); private int entered;
        public async Task<VisitV2PdfAssetSources> FreezeAsync(VisitV2PdfAssetSources assets, CancellationToken ct)
        { var frozen = await new VisitArchiveAssetFreezer(new ImageAssetLoader()).FreezeAsync(assets,ct);if(Interlocked.Increment(ref entered)==2)both.TrySetResult();await both.Task.WaitAsync(TimeSpan.FromSeconds(15),ct);return frozen; }
    }
    [StorageSqlFact]
    public async Task Bounded_backoff_reaches_attention_and_success_cannot_be_retried_or_recreated()
    {
        await using var db = fixture.Db(); var world = await Setup(db); var id = await Create(db,world);await Visits(db,world).FinalizeAsync(id);
        for(var i=1;i<=5;i++)
        {
            world.Drive.FailNextUpload=true;await Processor(db,world).ProcessBatchAsync();var o=await Operation(db,id);
            o.Attempts.Should().Be(i);o.Status.Should().Be(i==5?VisitArchiveStatus.NeedsAttention:VisitArchiveStatus.RetryScheduled);
            if(i<5){o.NextAttemptAtUtc.Should().BeAfter(o.LastAttemptAtUtc!.Value.AddSeconds(20));await Ready(db,id);}
        }
        await Reader(db,world).RetryAsync(id,new(1));await Processor(db,world).ProcessBatchAsync();
        (await Operation(db,id)).Status.Should().Be(VisitArchiveStatus.Completed);
        await FluentActions.Invoking(()=>Reader(db,world).RetryAsync(id,new(1))).Should().ThrowAsync<StorageConflictException>();
        await FluentActions.Invoking(()=>Reader(db,world).RetryAsync(id,new(1,true,"not missing"))).Should().ThrowAsync<StorageConflictException>();
        world.Drive.Uploads.Should().ContainSingle();
    }
    [StorageSqlFact]
    public async Task Conflicting_provider_identity_never_becomes_success_or_triggers_blind_reupload()
    {
        await using var db=fixture.Db();var world=await Setup(db);var id=await Create(db,world);await Visits(db,world).FinalizeAsync(id);
        world.Drive.LoseNextUploadResponse=true;await Processor(db,world).ProcessBatchAsync();var operation=await Operation(db,id);
        world.Drive.AddFile(operation.ProviderItemId!,"same-name.pdf",operation.ArchiveFolderItemId!,"different bytes");
        await Ready(db,id);await Processor(db,world).ProcessBatchAsync();operation=await Operation(db,id);
        operation.Status.Should().Be(VisitArchiveStatus.NeedsAttention);operation.LastErrorCode.Should().Be("IdentityMismatch");
        world.Drive.Uploads.Should().ContainSingle();(await db.Set<VisitArchiveArtifact>().CountAsync(a=>a.VisitId==id)).Should().Be(0);
    }
    [StorageSqlFact]
    public async Task Archive_listing_is_SQL_paginated_scoped_and_stably_sorted_with_combined_filters()
    {
        await using var db=fixture.Db();var world=await Setup(db);var first=await Create(db,world);await Visits(db,world).FinalizeAsync(first);
        var second=await Create(db,world);await Visits(db,world).FinalizeAsync(second);
        var reader=Reader(db,world);var page=await reader.ListAsync(new(TeacherId:world.Teacher,Status:"Pending",PageSize:1));
        page.Total.Should().Be(2);page.Items.Should().ContainSingle(i=>i.VisitId==second);
        var next=await reader.ListAsync(new(TeacherId:world.Teacher,Status:"Pending",Page:2,PageSize:1));next.Items.Should().ContainSingle(i=>i.VisitId==first);
        (await reader.ListAsync(new(From:DateTimeOffset.UtcNow.AddDays(1)))).Total.Should().Be(0);
        (await reader.TeachersAsync()).Should().ContainSingle(t=>t.UserId==world.Teacher);
    }
    [StorageSqlFact]
    public async Task Archive_list_5000_operations_uses_bounded_SQL_pages_and_never_selects_report_blobs()
    {
        await using var db=fixture.Db();var world=await Setup(db);var rubric=await db.RubricVersions.SingleAsync(r=>r.VersionNumber==2);
        var visits=Enumerable.Range(0,5000).Select(i=>new Visit {SchoolId=world.School,InstructorId=world.Teacher,CreatedByUserId=TeacherDriveHarness.ManagerUserId,RubricVersionId=rubric.Id,
            ExperienceVersion=ExperienceVersion.PrototypeV2,ScoringRuleSetVersion=2,ApprovalRevision=1,Status=VisitStatus.Approved,VisitCategory=VisitCategory.ExploratoryGuidance,VisitSequence=VisitSequence.First,
            VisitDate=DateTimeOffset.UtcNow,ApprovedAt=DateTimeOffset.UtcNow,ApprovedByUserId=TeacherDriveHarness.ManagerUserId,LessonTitle="قياس SQL معزول"}).ToArray();
        db.Visits.AddRange(visits);await db.SaveChangesAsync();
        db.Set<VisitArchiveOperation>().AddRange(visits.Select(v=>new VisitArchiveOperation {SchoolId=world.School,VisitId=v.Id,ApprovalRevision=1,ApprovedAtUtc=v.ApprovedAt!.Value,Status=VisitArchiveStatus.Pending}));
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var watch=new CountCommands();await using var measured=fixture.Db(watch);var service=Reader(measured,world);var query=new VisitArchiveQuery(Page:100,PageSize:25,Status:"Pending");
        await service.ListAsync(query);var samples=new List<double>();var counts=new List<int>();
        for(var i=0;i<10;i++){watch.Count=0;var started=Stopwatch.GetTimestamp();var page=await service.ListAsync(query);samples.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);counts.Add(watch.Count);page.Total.Should().Be(5000);page.Items.Should().HaveCount(25);}
        watch.SelectedBlobs.Should().BeFalse();var report=Environment.GetEnvironmentVariable("ALFALAH_S5_PERFORMANCE_REPORT");
        if(report!=null)await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new { Operations=5000,Page=100,PageSize=25,Iterations=10,ServiceWithFakeProviderMs=samples,QueriesPerRequest=counts,MedianMs=samples.OrderBy(x=>x).Skip(5).First(),MaxMs=samples.Max(),SelectedPdfBlobs=false,FakeProvider=true,LiveGoogleTimingMeasured=false }));
    }
    private sealed class CountCommands : DbCommandInterceptor
    {
        public int Count; public bool SelectedBlobs;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default)
        {Count++;SelectedBlobs|=command.CommandText.Contains("[PdfBytes]") || command.CommandText.Contains("[SnapshotJson]");return ValueTask.FromResult(result);}
    }
    [StorageSqlFact]
    public async Task Legacy_teacher_browser_never_discloses_archive_moved_inside_teacher_grant_even_if_properties_are_removed()
    {
        await using var db=fixture.Db();var world=await Setup(db);var id=await Create(db,world);await Visits(db,world).FinalizeAsync(id);await Processor(db,world).ProcessBatchAsync();
        var operation=await Operation(db,id);var user=new TeacherDriveHarness.TestCurrentUser(RoleNames.Instructor,world.Teacher,world.School);
        (await new VisitArchiveRepository(db).ProtectedProviderIdsAsync(1,[operation.ProviderItemId!],default)).Should().Contain(operation.ProviderItemId!);
        var guard=new TeacherDriveFolderGuard(world.Drive,new VisitArchiveRepository(db));
        var scope=new SchoolScopeGuard(db,user,NullLogger<SchoolScopeGuard>.Instance);
        var mappings=new TeacherDriveMappingService(db,scope,world.Drive,guard,Audit(db),user);
        var browser=new GoogleDriveBrowserService(new TeacherDriveIdentityService(db,user),mappings,world.Drive,guard,db,Audit(db));
        world.Drive.MoveExternally(operation.ProviderItemId!,world.Root+"-teacher");
        (await browser.ListAsync(new(null,null,null,null,null))).Items.Should().BeEmpty();
        await FluentActions.Invoking(()=>browser.GetItemAsync(operation.ProviderItemId!)).Should().ThrowAsync<AlFalah.Application.Common.Exceptions.TeacherDriveAccessDeniedException>();
        await FluentActions.Invoking(()=>browser.DownloadAsync(operation.ProviderItemId!)).Should().ThrowAsync<AlFalah.Application.Common.Exceptions.TeacherDriveAccessDeniedException>();
        await FluentActions.Invoking(()=>browser.GetBreadcrumbAsync(operation.ProviderItemId!)).Should().ThrowAsync<AlFalah.Application.Common.Exceptions.TeacherDriveAccessDeniedException>();
        world.Drive.AddFile(operation.ProviderItemId!,"altered.pdf",world.Root+"-teacher","bytes with no properties");
        (await browser.ListAsync(new(null,null,null,null,null))).Items.Should().BeEmpty();
        await FluentActions.Invoking(()=>browser.DownloadAsync(operation.ProviderItemId!)).Should().ThrowAsync<AlFalah.Application.Common.Exceptions.TeacherDriveAccessDeniedException>();
        world.Drive.MoveExternally(operation.ArchiveFolderItemId!,world.Root+"-teacher");
        (await browser.ListAsync(new(null,null,null,null,null))).Items.Should().BeEmpty();
        await FluentActions.Invoking(()=>browser.ListAsync(new(operation.ArchiveFolderItemId!,null,null,null,null))).Should().ThrowAsync<AlFalah.Application.Common.Exceptions.TeacherDriveAccessDeniedException>();
        var parent=new StorageFolder {SchoolId=world.School,Kind=StorageFolderKind.SchoolLibrary,DriveId="",DriveItemId=world.Root+"-library",DisplayName="مكتبة المدرسة"};db.Add(parent);await db.SaveChangesAsync();
        world.Drive.AddFolder(parent.DriveItemId,"library",world.Root);
        world.Drive.MoveExternally(operation.ProviderItemId!,parent.DriveItemId);
        world.Drive.MoveExternally(operation.ArchiveFolderItemId!,parent.DriveItemId);
        var page=await world.Drive.ListChildrenAsync(world.School,new(parent.DriveItemId,null,"name",25,null,null));
        var discovery=await new StorageLibraryRepository(db).IndexDiscoveredFoldersAsync(parent,page,TeacherDriveHarness.ManagerUserId,default);
        discovery.Items.Should().BeEmpty();
        (await db.StorageFolders.SingleAsync(f=>f.SchoolId==world.School && f.DriveItemId==operation.ArchiveFolderItemId)).Kind.Should().Be(StorageFolderKind.VisitArchive);
    }
    [StorageSqlFact]
    public async Task Teacher_grant_nested_inside_protected_archive_blocks_read_and_external_writes()
    {
        await using var db=fixture.Db();var world=await Setup(db);var id=await Create(db,world);await Visits(db,world).FinalizeAsync(id);
        await Processor(db,world).ProcessBatchAsync();var operation=await Operation(db,id);
        world.Drive.MoveExternally(world.Root+"-teacher",operation.ArchiveFolderItemId!);
        await FluentActions.Invoking(()=>Reader(db,world).GetAsync(id)).Should().ThrowAsync<StorageUnavailableException>();
        var next=await Create(db,world);await Visits(db,world).FinalizeAsync(next);await Processor(db,world).ProcessBatchAsync();
        (await Operation(db,next)).Status.Should().Be(VisitArchiveStatus.RetryScheduled);world.Drive.Uploads.Should().ContainSingle();
    }
    [StorageSqlFact]
    public async Task Periodic_reconciliation_marks_external_loss_and_never_recreates_without_authorization()
    {
        await using var db=fixture.Db();var world=await Setup(db);var id=await Create(db,world);await Visits(db,world).FinalizeAsync(id);
        await Processor(db,world).ProcessBatchAsync();var operation=await Operation(db,id);world.Drive.RemoveExternally(operation.ProviderItemId!);
        await Processor(db,world).ReconcileAsync();db.ChangeTracker.Clear();
        var artifact=await db.Set<VisitArchiveArtifact>().SingleAsync(a=>a.VisitId==id);
        (await db.StoredFileVersions.SingleAsync(v=>v.Id==artifact.StoredFileVersionId)).Availability.Should().Be(StoredFileAvailability.Missing);
        world.Drive.Uploads.Should().ContainSingle();(await Operation(db,id)).Status.Should().Be(VisitArchiveStatus.Completed);
    }
    [StorageSqlFact]
    public async Task S5_idempotent_SQL_script_applies_twice_on_S4_and_preserves_legacy_data()
    {
        var connection=new SqlConnectionStringBuilder(fixture.Connection);connection.InitialCatalog+="_Script_"+Guid.NewGuid().ToString("N");
        await using var db=new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection.ConnectionString).Options);
        var migrator=db.GetService<IMigrator>();await migrator.MigrateAsync("20261004015709_SchoolFileStorageSelfEvaluation");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO EvidenceRequirements (TemplateVersion,Code,DisplayName,Importance,FulfillmentPolicy,MinimumApprovedLinks,SortOrder,IsActive,CreatedAtUtc,UpdatedAtUtc,IsMandatory,FollowUpStatus) VALUES (1,'s5-sentinel','legacy',1,1,1,1,1,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET(),0,'NotStarted')");
        var script=migrator.GenerateScript("20261004015709_SchoolFileStorageSelfEvaluation","20261005112201_SchoolFileStorageVisitArchive",MigrationsSqlGenerationOptions.Idempotent);
        var batches=System.Text.RegularExpressions.Regex.Split(script,@"^GO\s*$",System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        await db.Database.OpenConnectionAsync();
        for(var run=0;run<2;run++)foreach(var batch in batches.Where(x=>!string.IsNullOrWhiteSpace(x)))
        {await using var command=db.Database.GetDbConnection().CreateCommand();command.CommandText=batch;await command.ExecuteNonQueryAsync();}
        await db.Database.CloseConnectionAsync();
        (await db.Database.GetPendingMigrationsAsync()).Should().Equal("20261005162718_SchoolFileStorageHistoricalImport");db.Database.HasPendingModelChanges().Should().BeFalse();
        (await db.EvidenceRequirements.SingleAsync(r=>r.Code=="s5-sentinel")).DisplayName.Should().Be("legacy");
    }
}
