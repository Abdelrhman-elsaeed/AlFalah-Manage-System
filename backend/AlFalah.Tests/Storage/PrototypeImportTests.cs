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
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.Storage;

public sealed class PrototypeImportParserTests
{
    [Theory]
    [InlineData("[{\"key\":\"a\",\"name\":\"a.pdf\"},{\"key\":\"a\",\"name\":\"b.pdf\"}]", "x.json")]
    [InlineData("[{\"key\":\"a\",\"name\":\"a.pdf\",\"size\":-1}]", "x.json")]
    [InlineData("key,name\na,\"unclosed", "x.csv")]
    [InlineData("key,name\na,name,extra", "x.csv")]
    [InlineData("alert('x')", "x.js")]
    [InlineData("[1]", "x.json")]
    public async Task Malformed_or_executable_sources_are_rejected(string source, string name)
    {
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(source));
        await FluentActions.Invoking(() => PrototypeImportParser.ReadAsync(bytes, name, default)).Should().ThrowAsync<ArgumentException>();
    }
    [Fact] public async Task Csv_quotes_newlines_windows_paths_and_source_status_are_data_only()
    {
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes("key,name,referencePath,sourceStatus\na,\"a,\"\"b\"\".pdf\",\"C:\\old\\a.pdf\",completed\n"));
        var parsed = await PrototypeImportParser.ReadAsync(bytes, "source.csv", default);
        parsed.Rows.Single().Name.Should().Be("a,\"b\".pdf"); parsed.Rows.Single().ReferencePath.Should().Be("C:\\old\\a.pdf");
        parsed.Rows.Single().SourceStatus.Should().Be("completed");
    }
    [Fact] public async Task Depth_and_row_limits_are_enforced()
    {
        using var deep = new MemoryStream(Encoding.UTF8.GetBytes(new string('[', 18) + new string(']', 18)));
        await FluentActions.Invoking(() => PrototypeImportParser.ReadAsync(deep, "x.json", default)).Should().ThrowAsync<ArgumentException>();
        using var many = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Enumerable.Range(0,10001).Select(x=>new {key=x.ToString(),name="x"}))));
        await FluentActions.Invoking(() => PrototypeImportParser.ReadAsync(many, "x.json", default)).Should().ThrowAsync<ArgumentException>();
    }
}

[CollectionDefinition("PrototypeImport SQL", DisableParallelization=true)]
public sealed class PrototypeImportSqlCollection : ICollectionFixture<PrototypeImportSqlFixture>;
public sealed class PrototypeImportSqlFixture : IAsyncLifetime
{
    private StorageSqlFixture inner=new();
    public string Connection=>inner.Connection;
    public AlFalahDbContext CreateContext()=>inner.CreateContext();
    public async Task InitializeAsync()
    {
        var connection=Environment.GetEnvironmentVariable("ALFALAH_STORAGE_TEST_CONNECTION");
        if(connection!=null){var builder=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);builder.InitialCatalog+="_S6Import";inner=new(builder.ConnectionString);}
        await inner.InitializeAsync();
    }
    public Task DisposeAsync()=>Task.CompletedTask;
}
[Collection("PrototypeImport SQL")]
public sealed class PrototypeImportSqlTests(PrototypeImportSqlFixture fixture)
{
    private sealed record World(int School, int Year, FakeGoogleDrive Drive);
    private sealed record Services(PrototypeImportService Import, ReadinessService Readiness, StorageLibraryService Library);
    private Services Build(AlFalahDbContext db, World w, ICurrentUserService? user = null, bool enabled = true)
    {
        user ??= TeacherDriveHarness.Manager(w.School);
        var scope = new StorageRepository(db); var evidence = new EvidenceRepository(db); var read = new ReadinessRepository(db);
        var auth = new StorageAuthorizationService(scope,user,new StorageDriveBoundary(new(w.Drive)),TimeProvider.System);
        var options = Options.Create(new StorageOptions {AdministrationEnabled=enabled,ReadModelEnabled=enabled});
        var provider = new GoogleStorageProvider(w.Drive); var context = new EvidenceWorkflowContext(evidence,scope,auth,user,provider,options);
        var library = new StorageLibraryService(new StorageLibraryRepository(db),scope,auth,user,provider,
            new EvidenceSubmissionService(db,new AuditLogWriter(db,new HttpContextAccessor(),NullLogger<AuditLogWriter>.Instance)),options);
        return new(new(new PrototypeImportRepository(db,read),auth,user,options,library,new EvidenceLinkService(evidence,scope,context),new StorageLibraryRepository(db)),new(read,evidence,scope,context,provider),library);
    }
    private async Task<World> Setup(AlFalahDbContext db)
    {
        var key=Guid.NewGuid().ToString("N");var school=new School{Name="S6 isolated "+key,City="الرياض",Stage=SchoolStage.Primary,ManagerUserId=TeacherDriveHarness.ManagerUserId};
        var year=new AcademicYear{Code="S6-"+key[..20],NameAr="سنة اختبار"};db.AddRange(school,year);await db.SaveChangesAsync();
        db.UserSchoolRoles.Add(new(){SchoolId=school.Id,UserId=TeacherDriveHarness.ManagerUserId,RoleId="manager"});
        db.SchoolGoogleDrives.Add(new(){SchoolId=school.Id,IsEnabled=true,RootFolderId="root-"+key,ProtectedCredential="isolated-unchanged"});await db.SaveChangesAsync();
        var world=new World(school.Id,year.Id,new FakeGoogleDrive().AddFolder("root-"+key,"جذر"));
        await Build(db,world).Readiness.InitializeAsync(new(year.Id));await Build(db,world).Library.CreateFolderAsync(new(null,"","root"));return world;
    }
    private static async Task<ImportBatchDto> Preview(PrototypeImportService service, World w, params ImportSourceRow[] rows)
    {
        using var content=new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase})));
        return await service.PreviewAsync(new(w.Year,1,"1","source.json",content));
    }
    private static ImportReviewRequest Review(ImportBatchDto b)=>new(b.RowVersion,b.Digest,"مراجعة مصدر واحتفاظ بالاستثناءات دون اعتماد شواهد");
    [StorageSqlFact] public async Task Preview_review_commit_repeat_and_source_status_never_modify_operational_data()
    {
        await using var db=fixture.CreateContext();var w=await Setup(db);var s=Build(db,w);var req=await db.EvidenceRequirements.FirstAsync(x=>x.SchoolId==w.School);
        var source=new ImportSourceRow("a","شاهد.pdf",RequirementCode:req.Code,ReferencePath:@"C:\old\شاهد.pdf",SourceStatus:"completed");
        var b=await Preview(s.Import,w,source); b.Rows.Should().Be(1);b.Matched.Should().Be(1);
        (await db.StoredFiles.CountAsync(x=>x.SchoolId==w.School)).Should().Be(0);
        await FluentActions.Invoking(()=>s.Import.CommitAsync(b.Id,Review(b))).Should().ThrowAsync<StorageConflictException>();
        b=await s.Import.ReviewAsync(b.Id,Review(b));b=await s.Import.CommitAsync(b.Id,Review(b));
        (await s.Import.CommitAsync(b.Id,Review(b))).Id.Should().Be(b.Id);
        (await Preview(s.Import,w,source)).Id.Should().Be(b.Id);
        b.ReferenceOnly.Should().Be(1);b.ImportedFiles.Should().Be(0);
        (await db.EvidenceRequirements.CountAsync(x=>x.SchoolId==w.School)).Should().Be(36);
        (await db.EvidenceLinks.CountAsync(x=>x.SchoolId==w.School)).Should().Be(0);
        (await db.EvidenceReviewDecisions.CountAsync(x=>x.SchoolId==w.School)).Should().Be(0);
        (await db.Set<SelfEvaluationTemplate>().CountAsync()).Should().Be(1);
        (await s.Import.RowsAsync(b.Id,1,null)).Items.Single().StoredFileId.Should().BeNull();
        (await s.Import.ExportAsync(b.Id)).Take(3).Should().Equal(0xEF,0xBB,0xBF);
    }
    [StorageSqlFact] public async Task Changed_mapping_or_requirement_invalidates_review_and_foreign_ids_are_denied()
    {
        await using var db=fixture.CreateContext();var w=await Setup(db);var s=Build(db,w);var b=await Preview(s.Import,w,new ImportSourceRow("a","a.pdf"));
        var row=(await s.Import.RowsAsync(b.Id,1,null)).Items.Single();b=await s.Import.ReviewAsync(b.Id,Review(b));
        var req=await db.EvidenceRequirements.FirstAsync(x=>x.SchoolId==w.School);
        b=await s.Import.ResolveAsync(b.Id,row.Id,new(row.RowVersion,req.Id,null,"مطابقة صريحة"));b.ReviewedDigest.Should().BeNull();
        await FluentActions.Invoking(()=>s.Import.CommitAsync(b.Id,Review(b))).Should().ThrowAsync<StorageConflictException>();
        b=await s.Import.ReviewAsync(b.Id,Review(b));req.IsMandatory=false;req.UpdatedAtUtc=DateTimeOffset.UtcNow;await db.SaveChangesAsync();
        await FluentActions.Invoking(()=>s.Import.CommitAsync(b.Id,Review(b))).Should().ThrowAsync<StorageConflictException>();
        var other=await Setup(db);await FluentActions.Invoking(()=>Build(db,other).Import.GetAsync(b.Id)).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(()=>Build(db,w,TeacherDriveHarness.TeacherA(w.School)).Import.GetAsync(b.Id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(()=>Build(db,w,enabled:false).Import.GetAsync(b.Id)).Should().ThrowAsync<KeyNotFoundException>();
    }
    [StorageSqlFact] public async Task Conflict_and_missing_rows_remain_exceptions_and_formula_export_is_safe()
    {
        await using var db=fixture.CreateContext();var w=await Setup(db);var s=Build(db,w);var b=await Preview(s.Import,w,new("a","=CMD()",StandardCode:"99.9"),new("b","b.pdf"));
        b.Conflicts.Should().Be(1);b.Missing.Should().Be(1);b=await s.Import.ReviewAsync(b.Id,Review(b));b=await s.Import.CommitAsync(b.Id,Review(b));
        (await s.Import.RowsAsync(b.Id,1,"Conflict")).Items.Single().Status.Should().Be("NeedsReview");
        Encoding.UTF8.GetString(await s.Import.ExportAsync(b.Id)).Should().Contain("'=CMD()");
    }
    [StorageSqlFact] public async Task Bytes_lost_response_reconciles_and_duplicate_rows_share_one_asset_without_approval()
    {
        await using var db=fixture.CreateContext();var w=await Setup(db);var s=Build(db,w);var req=await db.EvidenceRequirements.FirstAsync(x=>x.SchoolId==w.School);
        var b=await Preview(s.Import,w,new("a","شاهد.pdf",RequirementCode:req.Code),new("b","شاهد.pdf",RequirementCode:req.Code));
        b=await s.Import.ReviewAsync(b.Id,Review(b));b=await s.Import.CommitAsync(b.Id,Review(b));var rows=(await s.Import.RowsAsync(b.Id,1,null)).Items;
        w.Drive.LoseNextUploadResponse=true;
        using var bytes=new MemoryStream("%PDF-1.7\nS6 original"u8.ToArray());
        await FluentActions.Invoking(()=>s.Import.BytesAsync(b.Id,rows[0].Id,new(bytes,"شاهد.pdf",bytes.Length,rows[0].RowVersion,"فحص الأصل"))).Should().ThrowAsync<StorageUnavailableException>();
        db.ChangeTracker.Clear();s=Build(db,w);
        var first=await s.Import.ReconcileAsync(b.Id,rows[0].Id);first.StoredFileId.Should().NotBeNull();
        bytes.Position=0;var second=await s.Import.BytesAsync(b.Id,rows[1].Id,new(bytes,"شاهد.pdf",bytes.Length,rows[1].RowVersion,"نفس الأصل"));
        second.StoredFileId.Should().Be(first.StoredFileId);w.Drive.Uploads.Should().HaveCount(1);
        (await db.StoredFiles.CountAsync(x=>x.SchoolId==w.School)).Should().Be(1);
        (await db.EvidenceLinks.CountAsync(x=>x.SchoolId==w.School)).Should().Be(1);
        (await db.EvidenceLinks.SingleAsync(x=>x.SchoolId==w.School)).Status.Should().Be(EvidenceLinkStatus.Draft);
        (await db.EvidenceReviewDecisions.CountAsync(x=>x.SchoolId==w.School)).Should().Be(0);
    }
    [StorageSqlFact] public async Task Thousands_of_actual_reference_rows_are_repeatable_and_measured_separately_from_bytes()
    {
        var repository=Environment.GetEnvironmentVariable("ALFALAH_S6_REPOSITORY");if(repository==null)throw new InvalidOperationException("Set ALFALAH_S6_REPOSITORY for actual prototype verification.");
        var baseline=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repository,"docs/specs/school-file-storage/baseline/prototype-inventory.json")));
        var directory=baseline.RootElement.GetProperty("sourceDirectory").GetString()!;
        await using var db=fixture.CreateContext();var w=await Setup(db);var s=Build(db,w);var results=new List<object>();
        var extracted = Path.Combine(Environment.GetEnvironmentVariable("ALFALAH_S6_EXPORT_DIRECTORY")!, "tracker-36.json");
        foreach(var file in Directory.GetFiles(directory,"*.json").Concat(Directory.GetFiles(directory,"*.csv")).Append(extracted))
        {
            var watch=Stopwatch.StartNew();await using var stream=File.OpenRead(file);
            var b=await s.Import.PreviewAsync(new(w.Year,1,"S0-source",Path.GetFileName(file),stream));var previewMs=watch.Elapsed.TotalMilliseconds;
            b=await s.Import.ReviewAsync(b.Id,Review(b));b=await s.Import.CommitAsync(b.Id,Review(b));var commitMs=watch.Elapsed.TotalMilliseconds-previewMs;
            stream.Position=0;var repeat=await s.Import.PreviewAsync(new(w.Year,1,"S0-source",Path.GetFileName(file),stream));repeat.Id.Should().Be(b.Id);
            b.ImportedFiles.Should().Be(0);results.Add(new{sourceHash=b.SourceSHA256,rows=b.Rows,previewMs,reviewAndCommitMs=commitMs,referenceOnly=b.ReferenceOnly,liveFiles=b.ImportedFiles,repeatedBatch=b.Id==repeat.Id});
        }
        var output=Environment.GetEnvironmentVariable("ALFALAH_S6_EXPORT_DIRECTORY")!;Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"source-performance.json"),JsonSerializer.Serialize(new{kind="actual prototype reference metadata on isolated SQL; no original bytes or live Google",sources=results},new JsonSerializerOptions{WriteIndented=true}));
        (await db.EvidenceRequirements.CountAsync(x=>x.SchoolId==w.School)).Should().Be(36);(await db.StoredFiles.CountAsync(x=>x.SchoolId==w.School)).Should().Be(0);
    }
    [StorageSqlFact] public async Task Concurrent_preview_and_commits_share_one_batch_and_rows_and_sql_provenance_is_retained()
    {
        World w;await using(var setup=fixture.CreateContext())w=await Setup(setup);
        async Task<ImportBatchDto> RunPreview(){await using var db=fixture.CreateContext();return await Preview(Build(db,w).Import,w,new ImportSourceRow("unique","metadata.pdf"));}
        var batches=await Task.WhenAll(RunPreview(),RunPreview());batches[0].Id.Should().Be(batches[1].Id);
        ImportBatchDto reviewed;await using(var db=fixture.CreateContext())reviewed=await Build(db,w).Import.ReviewAsync(batches[0].Id,Review(batches[0]));
        async Task<ImportBatchDto> Commit(){await using var db=fixture.CreateContext();return await Build(db,w).Import.CommitAsync(reviewed.Id,Review(reviewed));}
        var committed=await Task.WhenAll(Commit(),Commit());committed[0].Id.Should().Be(committed[1].Id);
        await using var check=fixture.CreateContext();(await check.Set<PrototypeImportBatch>().CountAsync(x=>x.SchoolId==w.School)).Should().Be(1);
        (await check.Set<PrototypeImportRow>().CountAsync(x=>x.SchoolId==w.School)).Should().Be(1);
        await FluentActions.Invoking(()=>check.Database.ExecuteSqlInterpolatedAsync($"UPDATE PrototypeImportRows SET SourceJson='{{}}' WHERE BatchId={reviewed.Id}")).Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>();
        await FluentActions.Invoking(()=>check.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM PrototypeImportBatches WHERE Id={reviewed.Id}")).Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>();
    }
    [StorageSqlFact] public async Task Additive_S6_script_applies_twice_and_retains_existing_S5_import_rows()
    {
        var connection=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(fixture.Connection);connection.InitialCatalog+="_S6Script_"+Guid.NewGuid().ToString("N");
        await using var db=new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection.ConnectionString).Options);
        var migrator=Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(db);
        await migrator.MigrateAsync("20261005112201_SchoolFileStorageVisitArchive");
        var school=new School{Name="sentinel",City="test",Stage=SchoolStage.Primary};var year=new AcademicYear{Code="S6-sentinel",NameAr="test"};db.AddRange(school,year);await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO PrototypeImportBatches (SchoolId,AcademicYearId,SourceSHA256,SourceName,Status,CreatedAtUtc,UpdatedAtUtc) VALUES ({school.Id},{year.Id},REPLICATE('A',64),'legacy-source',2,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET()); INSERT INTO PrototypeImportRows (SchoolId,BatchId,SourceOrdinal,SourceRowSHA256,Status,CreatedAtUtc,UpdatedAtUtc) VALUES ({school.Id},SCOPE_IDENTITY(),1,REPLICATE('B',64),2,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET());");
        var script=migrator.GenerateScript("20261005112201_SchoolFileStorageVisitArchive","20261005162718_SchoolFileStorageHistoricalImport",Microsoft.EntityFrameworkCore.Migrations.MigrationsSqlGenerationOptions.Idempotent);
        var output=Environment.GetEnvironmentVariable("ALFALAH_S6_EXPORT_DIRECTORY");if(output!=null)await File.WriteAllTextAsync(Path.Combine(output,"s6-migration.sql"),script);
        await db.Database.OpenConnectionAsync();
        for(var i=0;i<2;i++)foreach(var batch in System.Text.RegularExpressions.Regex.Split(script,@"^GO\s*$",System.Text.RegularExpressions.RegexOptions.Multiline).Where(x=>!string.IsNullOrWhiteSpace(x)))await db.Database.ExecuteSqlRawAsync(batch);
        await db.Database.CloseConnectionAsync();
        (await db.Set<PrototypeImportBatch>().SingleAsync()).SourceName.Should().Be("legacy-source");(await db.Set<PrototypeImportRow>().CountAsync()).Should().Be(1);
        db.Database.HasPendingModelChanges().Should().BeFalse();(await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }
    [StorageSqlFact] public async Task Revoked_delegate_during_provider_upload_retains_reservation_without_linking_or_disclosing_content()
    {
        await using var db=fixture.CreateContext();var w=await Setup(db);
        var actor=TeacherDriveHarness.TeacherAUserId;
        db.UserSchoolRoles.Add(new(){SchoolId=w.School,UserId=actor,RoleId="teacher"});
        var grant=new StorageDelegation{SchoolId=w.School,GranteeUserId=actor,GrantedByManagerUserId=TeacherDriveHarness.ManagerUserId,StartsAt=DateTimeOffset.UtcNow.AddMinutes(-1),Reason="تفويض اختبار"};
        db.Add(grant);await db.SaveChangesAsync();
        var s=Build(db,w,TeacherDriveHarness.TeacherA(w.School));var req=await db.EvidenceRequirements.FirstAsync(x=>x.SchoolId==w.School);
        var b=await Preview(s.Import,w,new ImportSourceRow("revocation","شاهد.pdf",RequirementCode:req.Code));b=await s.Import.ReviewAsync(b.Id,Review(b));b=await s.Import.CommitAsync(b.Id,Review(b));
        var row=(await s.Import.RowsAsync(b.Id,1,null)).Items.Single();
        w.Drive.AfterUpload=async()=>{grant.RevokedAt=DateTimeOffset.UtcNow;grant.RevokedByManagerUserId=TeacherDriveHarness.ManagerUserId;grant.RevocationReason="سحب أثناء الرفع";await db.SaveChangesAsync();};
        using var bytes=new MemoryStream("%PDF-1.7\nrevoke"u8.ToArray());
        await FluentActions.Invoking(()=>s.Import.BytesAsync(b.Id,row.Id,new(bytes,"شاهد.pdf",bytes.Length,row.RowVersion,"اختبار سحب"))).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        (await db.EvidenceLinks.CountAsync(x=>x.SchoolId==w.School)).Should().Be(0);
        (await db.Set<StorageOperation>().CountAsync(x=>x.SchoolId==w.School && x.Action=="Upload")).Should().Be(1);
        await FluentActions.Invoking(()=>s.Import.GetAsync(b.Id)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }
}
