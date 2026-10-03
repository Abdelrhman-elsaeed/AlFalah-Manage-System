using System.Diagnostics;
using System.Text.Json;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace AlFalah.Tests.Storage;

[CollectionDefinition("Storage library SQL", DisableParallelization = true)]
public sealed class StorageLibrarySqlCollection : ICollectionFixture<StorageLibrarySqlFixture>;

public sealed class StorageLibrarySqlFixture : IAsyncLifetime
{
    private StorageSqlFixture inner = new();
    public string Connection => inner.Connection;
    public AlFalahDbContext CreateContext() => inner.CreateContext();
    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("ALFALAH_STORAGE_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(connection))
        {
            var builder = new SqlConnectionStringBuilder(connection);
            builder.InitialCatalog += "_Library";
            inner = new StorageSqlFixture(builder.ConnectionString);
        }
        await inner.InitializeAsync();
    }
    public Task DisposeAsync() => inner.DisposeAsync();
}

[Collection("Storage library SQL")]
public sealed class StorageLibrarySqlServerTests(StorageLibrarySqlFixture fixture, ITestOutputHelper output)
{
    private StorageLibraryService Service(AlFalahDbContext db, FakeGoogleDrive drive, int schoolId)
    {
        var user = TeacherDriveHarness.Manager(schoolId);
        var scopes = new StorageRepository(db);
        var audit = new AuditLogWriter(db, new Microsoft.AspNetCore.Http.HttpContextAccessor(), Microsoft.Extensions.Logging.Abstractions.NullLogger<AuditLogWriter>.Instance);
        return new(new StorageLibraryRepository(db), scopes,
            new StorageAuthorizationService(scopes, user, new StorageDriveBoundary(new(drive)), TimeProvider.System), user,
            new GoogleStorageProvider(drive), new EvidenceSubmissionService(db, audit), Options.Create(new StorageOptions { ReadModelEnabled = true }));
    }
    private async Task<(int School, FakeGoogleDrive Drive)> Setup(AlFalahDbContext db)
    {
        var school = new School { Name = "اختبار S2 " + Guid.NewGuid().ToString("N"), City = "الرياض", Stage = SchoolStage.Primary,
            ManagerUserId = TeacherDriveHarness.ManagerUserId, IsActive = true };
        db.Schools.Add(school); await db.SaveChangesAsync();
        db.UserSchoolRoles.Add(new() { SchoolId = school.Id, UserId = TeacherDriveHarness.ManagerUserId, RoleId = "manager" });
        db.SchoolGoogleDrives.Add(new() { SchoolId = school.Id, RootFolderId = "s2-root", ProtectedCredential = "test-only-no-decrypt", IsEnabled = true });
        await db.SaveChangesAsync();
        var drive = new FakeGoogleDrive().AddFolder("s2-root", "جذر اختبار");
        await Service(db, drive, school.Id).CreateFolderAsync(new(null, "", "init"));
        return (school.Id, drive);
    }
    [StorageSqlFact]
    public async Task Concurrent_same_key_has_one_provider_upload_and_one_asset()
    {
        await using var setup = fixture.CreateContext(); var (school, drive) = await Setup(setup);
        async Task<string> Attempt()
        {
            await using var db = fixture.CreateContext();
            try { return (await StorageLibraryTests.Upload(Service(db, drive, school), false, "concurrent")).Status; }
            catch (StorageConflictException) { return "Conflict"; }
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        results.Should().Contain("Completed"); drive.Uploads.Should().HaveCount(1);
        await using var check = fixture.CreateContext();
        (await check.StoredFiles.CountAsync(x => x.SchoolId == school)).Should().Be(1);
        (await check.StoredFileVersions.CountAsync(x => x.SchoolId == school)).Should().Be(1);
    }
    [StorageSqlFact]
    public async Task Persistence_failure_rolls_back_asset_version_pointer_then_reconciles_existing_Drive_item()
    {
        await using var setup = fixture.CreateContext(); var (school, drive) = await Setup(setup);
        var interceptor = new FailVersionSave();
        await using (var failing = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseSqlServer(fixture.Connection).AddInterceptors(interceptor).Options))
        {
            await FluentActions.Invoking(() => StorageLibraryTests.Upload(Service(failing, drive, school), false, "sql-failure"))
                .Should().ThrowAsync<StorageUnavailableException>();
        }
        await using var db = fixture.CreateContext();
        (await db.StoredFiles.CountAsync(x => x.SchoolId == school)).Should().Be(0);
        var op = await db.Set<StorageOperation>().SingleAsync(x => x.SchoolId == school && x.Action == "Upload");
        (await Service(db, drive, school).ReconcileAsync(op.Id)).Status.Should().Be("Completed");
        drive.Uploads.Should().HaveCount(1);
        (await db.StoredFiles.CountAsync(x => x.SchoolId == school)).Should().Be(1);
    }
    [StorageSqlFact]
    public async Task SQL_search_sort_and_pagination_over_5000_assets_are_measured_without_live_Drive()
    {
        await using var db = fixture.CreateContext(); var (school, drive) = await Setup(db);
        var folder = await db.StorageFolders.SingleAsync(x => x.SchoolId == school);
        var files = Enumerable.Range(0, 5000).Select(i => new StoredFile { SchoolId = school, FolderId = folder.Id,
            SourceKind = StoredFileSourceKind.SchoolUpload, DisplayName = $"خطة {i:D5}.pdf", NeedsLink = true }).ToList();
        db.StoredFiles.AddRange(files); await db.SaveChangesAsync();
        var versions = files.Select((file, i) => new StoredFileVersion { SchoolId = school, StoredFileId = file.Id,
            VersionNumber = 1, DriveId = "", DriveItemId = "performance-" + i, DriveFileName = file.DisplayName,
            SizeInBytes = 100, MimeType = "application/pdf", UploadedAtUtc = DateTimeOffset.UtcNow, Availability = StoredFileAvailability.Available }).ToList();
        db.StoredFileVersions.AddRange(versions); await db.SaveChangesAsync();
        var fileMap = files.ToDictionary(x => x.Id);
        foreach (var version in versions)
        {
            fileMap[version.StoredFileId].CurrentVersionId = version.Id;
            drive.AddFile(version.DriveItemId, version.DriveFileName, folder.DriveItemId);
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var repo = new StorageLibraryRepository(db);
        var request = new StorageListRequest(folder.Id, "خطة", Sort: "name", Page: 100, PageSize: 25);
        await repo.FilesAsync(school, null, request, default); // JIT/query-plan warmup.
        var times = new List<double>();
        for (var i = 0; i < 10; i++)
        {
            var watch = Stopwatch.StartNew();
            var page = await repo.FilesAsync(school, null, request, default); watch.Stop();
            page.Total.Should().Be(5000); page.Items.Should().HaveCount(25);
            page.Items.First().Dto.DisplayName.Should().Be("خطة 02475.pdf"); times.Add(watch.Elapsed.TotalMilliseconds);
        }
        var serviceWatch = Stopwatch.StartNew();
        var servicePage = await Service(db, drive, school).FilesAsync(false, request); serviceWatch.Stop();
        servicePage.Items.Should().HaveCount(25);
        var median = times.Order().Skip(4).Take(2).Average();
        var report = new { date = "2026-10-03", database = "isolated LocalDB fixture", assets = 5000, pageSize = 25, page = 100,
            samplesMs = times, medianMs = median, maxMs = times.Max(), serviceWithFakeDriveMs = serviceWatch.Elapsed.TotalMilliseconds,
            liveDrive = "Unavailable; S0 credential constraint unchanged", targetMs = 200 };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }); output.WriteLine(json);
        var path = Environment.GetEnvironmentVariable("ALFALAH_STORAGE_PERF_REPORT");
        if (path is not null) await File.WriteAllTextAsync(path, json);
        median.Should().BeLessThan(200);
    }
    private sealed class FailVersionSave : SaveChangesInterceptor
    {
        private bool failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!failed && eventData.Context!.ChangeTracker.Entries<StoredFileVersion>().Any(x => x.State == EntityState.Added))
            { failed = true; throw new InvalidOperationException("SQL connection interrupted while saving version"); }
            return ValueTask.FromResult(result);
        }
    }
}
