using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

// Explicit synthetic live smoke only on the owner's designated school 18 and a restored isolated SQL clone.
// No configuration, credential, key, original SQL, sharing or delete operations.
try
{
    if (args.Length != 4 || args[0] != "--synthetic-live-school-18") throw new ArgumentException("Explicit synthetic smoke switch, repository, fresh clone and output required.");
    var root=Path.GetFullPath(args[1]);var database=args[2];var output=Path.GetFullPath(args[3]);
    var api=Path.Combine(root,"backend/AlFalah.Api");var config=new ConfigurationBuilder().SetBasePath(api).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json").Build();
    var connection=new SqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
    if (!connection.DataSource.StartsWith("(localdb)\\",StringComparison.OrdinalIgnoreCase) || !database.StartsWith("AlFalahSFS_S6_",StringComparison.Ordinal) || database==connection.InitialCatalog) throw new ArgumentException("Isolated LocalDB clone required.");
    connection.InitialCatalog=database;
    await using var db=new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection.ConnectionString).Options);
    if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("Migrate clone first.");
    var school=await db.Schools.SingleAsync(x=>x.Id==18 && x.IsActive);
    var actor=new SmokeUser(school.ManagerUserId ?? throw new InvalidOperationException("Actual manager required."),18);
    var settings=await db.SchoolGoogleDrives.AsNoTracking().SingleAsync(x=>x.SchoolId==18 && x.IsEnabled);
    if (string.IsNullOrWhiteSpace(settings.RootFolderId)) throw new InvalidOperationException("Designated school root required.");
    var keyPath=config["DataProtection:KeysPath"]??"App_Data/DataProtectionKeys";keyPath=Path.GetFullPath(Path.IsPathRooted(keyPath)?keyPath:Path.Combine(api,keyPath));
    if(!Directory.Exists(keyPath))throw new InvalidOperationException("Existing keys required.");
    var services=new ServiceCollection();services.AddLogging(b=>b.ClearProviders());services.AddDataProtection().SetApplicationName(config["DataProtection:ApplicationName"]??"AlFalah.ManageSystem").PersistKeysToFileSystem(new DirectoryInfo(keyPath)).DisableAutomaticKeyGeneration();
    using var dependency=services.BuildServiceProvider();using var cache=new MemoryCache(new MemoryCacheOptions());using var factory=new SmokeFactory();
    var tokens=new GoogleDriveTokenService(db,new GoogleDriveCredentialProtector(dependency.GetRequiredService<IDataProtectionProvider>()),factory,cache,config,NullLogger<GoogleDriveTokenService>.Instance);
    var drive=new GoogleDriveClient(tokens,factory,config,NullLogger<GoogleDriveClient>.Instance);var provider=new GoogleStorageProvider(drive);
    var scopes=new StorageRepository(db);var evidence=new EvidenceRepository(db);var read=new ReadinessRepository(db);var uploadRepository=new StorageLibraryRepository(db);
    var auth=new StorageAuthorizationService(scopes,actor,new StorageDriveBoundary(new(drive)),TimeProvider.System);
    var options=Options.Create(new StorageOptions {AdministrationEnabled=true,ReadModelEnabled=true,ArchiveWorkerEnabled=false,ArchiveExternalWritesEnabled=false});
    var context=new EvidenceWorkflowContext(evidence,scopes,auth,actor,provider,options);var links=new EvidenceLinkService(evidence,scopes,context);
    var library=new StorageLibraryService(uploadRepository,scopes,auth,actor,provider,new EvidenceSubmissionService(db,new AuditLogWriter(db,new HttpContextAccessor(),NullLogger<AuditLogWriter>.Instance)),options);
    var importer=new PrototypeImportService(new PrototypeImportRepository(db,read),auth,actor,options,library,links,uploadRepository);
    var readiness=new ReadinessService(read,evidence,scopes,context,provider);
    var key=Guid.NewGuid().ToString("N");var year=new AcademicYear{Code="S6-LIVE-"+key[..16],NameAr="اختبار S6 معزول — ليس سنة تشغيل"};db.Add(year);await db.SaveChangesAsync();
    await readiness.InitializeAsync(new(year.Id));
    var folderTimer=Stopwatch.StartNew();await library.CreateFolderAsync(new(null,"","synthetic-live-"+key));folderTimer.Stop();
    var requirements=await db.EvidenceRequirements.Where(x=>x.SchoolId==18 && x.AcademicYearId==year.Id && x.SourceKey!=null).OrderBy(x=>x.Id).Take(2).ToListAsync();
    QuestPDF.Settings.License=QuestPDF.Infrastructure.LicenseType.Community;
    var bytes=Document.Create(d=>d.Page(p=>{p.Size(QuestPDF.Helpers.PageSizes.A4);p.Margin(40);p.Content().Column(c=>{c.Item().Text("S6 synthetic storage verification").FontSize(20);c.Item().Text("This file contains no school or personal records. Retained for test audit. "+key);});})).GeneratePdf();
    var filename="S6-synthetic-verification-"+key+".pdf";
    var rows=requirements.Select((r,i)=>new ImportSourceRow("synthetic-"+i,filename,RequirementCode:r.Code,Size:bytes.Length,Extension:".pdf",SourceStatus:"completed")).ToArray();
    using var source=new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase})));
    var batch=await importer.PreviewAsync(new(year.Id,1,"synthetic-1","synthetic-source.json",source));
    ImportReviewRequest Review(ImportBatchDto b)=>new(b.RowVersion,b.Digest,"Owner-requested synthetic storage smoke; no historical approvals");
    batch=await importer.ReviewAsync(batch.Id,Review(batch));batch=await importer.CommitAsync(batch.Id,Review(batch));
    source.Position=0;var repeat=await importer.PreviewAsync(new(year.Id,1,"synthetic-1","synthetic-source.json",source));
    var previewRows=(await importer.RowsAsync(batch.Id,1,null)).Items;var uploadTimer=Stopwatch.StartNew();
    using var payload=new MemoryStream(bytes);var first=await importer.BytesAsync(batch.Id,previewRows[0].Id,new(payload,filename,bytes.Length,previewRows[0].RowVersion,"Validated synthetic original"));uploadTimer.Stop();
    payload.Position=0;var second=await importer.BytesAsync(batch.Id,previewRows[1].Id,new(payload,filename,bytes.Length,previewRows[1].RowVersion,"Same bytes, independent requirement"));
    var file=first.StoredFileId!.Value;
    var content=await library.ContentAsync(file);string downloadedHash;await using(content.Content){downloadedHash=Convert.ToHexString(await SHA256.HashDataAsync(content.Content));}
    var expected=Convert.ToHexString(SHA256.HashData(bytes));if(expected!=downloadedHash || first.StoredFileId!=second.StoredFileId || repeat.Id!=batch.Id)throw new InvalidOperationException("Smoke equality failed.");
    var search=await library.FilesAsync(false,new(Search:filename,Global:true));
    var fileLinks=await links.FileLinksAsync(file);if(fileLinks.Count!=2 || fileLinks.Any(x=>x.Status!="Draft"))throw new InvalidOperationException("Independent draft links required.");
    var reviewService=new EvidenceReviewService(evidence,scopes,context);
    var submitted=await links.SubmitAsync(fileLinks[0].Id,new(fileLinks[0].RowVersion));await reviewService.ReviewAsync(submitted.Id,new(EvidenceReviewStatus.Approved,"Synthetic live review only",submitted.RowVersion));
    var summary=await readiness.ReadinessAsync(new(year.Id));var export=new ReadinessExportService(readiness,new ReadinessExportRenderer());
    var exported=await export.ExportAsync("csv",new(year.Id));if(exported.Content.Length<3)throw new InvalidOperationException("Export failed.");
    var report=new{capturedAtUtc=DateTimeOffset.UtcNow,school=18,isolatedDatabase=database,syntheticOnly=true,originalSqlMigrated=false,originalFlagsChanged=false,keysOrCredentialsChanged=false,
        liveGoogle=true,folderMs=folderTimer.Elapsed.TotalMilliseconds,uploadAndRoundTripValidationMs=uploadTimer.Elapsed.TotalMilliseconds,bytes=bytes.Length,sha256=expected,downloadHashEqual=expected==downloadedHash,
        sourceReplaySameBatch=repeat.Id==batch.Id,uniqueFiles=1,storedVersions=1,links=2,approvedLinks=1,draftLinks=1,searchResults=search.Total,readinessNumerator=summary.Overall.Numerator,readinessDenominator=summary.Overall.Denominator,
        csvBytes=exported.Content.Length,httpRequests=factory.Count,driveDeletes=0,publicSharing=0,retainedTestFile=true,retainedLibraryFolder=true,cutover=false};
    await File.WriteAllTextAsync(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Synthetic live Drive smoke passed; test file/folder and isolated SQL retained. No cutover.");return 0;
}
catch(Exception ex){Console.Error.WriteLine("Synthetic live smoke failed: "+ex.GetType().Name+". No credentials or provider identifiers logged.");return 1;}

sealed class SmokeUser(string id,int school):ICurrentUserService
{
    public string? UserId=>id;public string? Username=>"s6-smoke";public int? ActiveSchoolId=>school;public string? PreferredLanguage=>"ar";public bool IsAuthenticated=>true;
    public bool IsInRole(string r)=>r==RoleNames.SchoolManager;public bool HasPermission(string p)=>false;public IEnumerable<string> GetRoles()=>[RoleNames.SchoolManager];public IEnumerable<string> GetPermissions()=>[];public bool IsGlobalAdmin()=>false;public bool IsSchoolScopedRole()=>true;
}
sealed class SmokeFactory:IHttpClientFactory,IDisposable
{
    private readonly SmokeGate gate=new(){InnerHandler=new HttpClientHandler{AllowAutoRedirect=false}};public int Count=>gate.Count;
    public HttpClient CreateClient(string name)=>new(gate,false){Timeout=TimeSpan.FromSeconds(60)};public void Dispose()=>gate.Dispose();
}
sealed class SmokeGate:DelegatingHandler
{
    public int Count;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)
    {
        var uri=r.RequestUri!;
        // No sharing, trashing, deletion or credentials mutation in this synthetic smoke.
        if(++Count>300 || uri.Scheme!="https" || r.Method==HttpMethod.Delete || r.Method==HttpMethod.Patch || uri.AbsolutePath.Contains("permissions") ||
            !(uri.Host=="www.googleapis.com" && (uri.AbsolutePath.StartsWith("/drive/v3/") || uri.AbsolutePath.StartsWith("/upload/drive/v3/")) || uri.Host=="oauth2.googleapis.com" && uri.AbsolutePath=="/token"))throw new InvalidOperationException("Synthetic smoke request gate");
        return base.SendAsync(r,ct);
    }
}
