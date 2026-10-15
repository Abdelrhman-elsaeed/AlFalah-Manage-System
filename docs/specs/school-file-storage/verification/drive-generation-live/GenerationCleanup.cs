using System.Text.Json;
using AlFalah.Application.Duty;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.TestDoubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class GenerationCleanup
{
 public static async Task Run(string workspace,IConfiguration config,string expectedRoot)
 {
  var scratch=workspace+"/.audit/storage-drive-20261015";
  var reservation=JsonDocument.Parse(await File.ReadAllTextAsync(scratch+"/generation-reservation.json")).RootElement;
  if(reservation.GetProperty("root").GetString()!=expectedRoot)throw new Exception("Root mismatch.");
  var database=reservation.GetProperty("database").GetString()!;
  if(!database.StartsWith("AlFalahDutyTests_EmptyRootGeneration_",StringComparison.Ordinal))throw new Exception("Isolated generation SQL required.");
  var connection=new SqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection")){InitialCatalog=database};
  if(!connection.DataSource.StartsWith("(localdb)\\",StringComparison.OrdinalIgnoreCase)||!connection.IntegratedSecurity)throw new Exception("Local isolated SQL required.");
  // This is explicit recovery of the retained test catalog, not migration/reinitialization.
  await using var db=new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection.ConnectionString).Options);
  var school=reservation.GetProperty("school").GetInt32();var source=await db.SchoolGoogleDrives.AsNoTracking().SingleAsync(x=>x.SchoolId==school&&x.RootFolderId==expectedRoot);
  var manager=await db.Schools.Where(x=>x.Id==school).Select(x=>x.ManagerUserId).SingleAsync()??throw new Exception("Synthetic manager required.");
  var services=new ServiceCollection();services.AddLogging();services.AddMemoryCache();services.AddHttpClient("GoogleDrive",c=>c.Timeout=TimeSpan.FromSeconds(90));services.AddHttpClient("GoogleOAuth");
  var keys=config["DataProtection:KeysPath"]??"App_Data/DataProtectionKeys";keys=Path.GetFullPath(Path.IsPathRooted(keys)?keys:Path.Combine(workspace,"backend/AlFalah.Api",keys));
  services.AddDataProtection().SetApplicationName(config["DataProtection:ApplicationName"]??"AlFalah").PersistKeysToFileSystem(new(keys)).DisableAutomaticKeyGeneration();await using var dependencies=services.BuildServiceProvider();
  var tokens=new GoogleDriveTokenService(db,new GoogleDriveCredentialProtector(dependencies.GetRequiredService<IDataProtectionProvider>()),dependencies.GetRequiredService<IHttpClientFactory>(),dependencies.GetRequiredService<IMemoryCache>(),config,NullLogger<GoogleDriveTokenService>.Instance);
  var google=new GoogleDriveClient(tokens,dependencies.GetRequiredService<IHttpClientFactory>(),config,NullLogger<GoogleDriveClient>.Instance);var provider=new GoogleStorageProvider(google);
  var current=new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,manager,school,true);var storage=new StorageRepository(db);var auth=new StorageAuthorizationService(storage,current,new StorageDriveBoundary(new(google)),TimeProvider.System);
  var writer=new EvidenceSubmissionService(db,new AuditLogWriter(db,new HttpContextAccessor(),NullLogger<AuditLogWriter>.Instance));var repo=new StorageDriveBrowserRepository(db,writer);var handles=new StorageDriveHandles(dependencies.GetRequiredService<IDataProtectionProvider>());
  var browser=new StorageDriveBrowserService(repo,storage,auth,current,provider,handles,new SchoolDriveSetupRepository(db),new DutyRepository(db),Options.Create(new StorageOptions{AdministrationEnabled=true,ReadModelEnabled=true}));
  var known=(await db.Set<StorageOperation>().Where(x=>x.SchoolId==school&&x.Action=="CreateFolder"&&x.ParentItemId==expectedRoot).Select(x=>x.ProviderItemId).ToListAsync()).ToHashSet();
  foreach(var id in await db.StorageFolders.Where(x=>x.SchoolId==school&&x.Kind==StorageFolderKind.DutyArchive&&x.DutyIdentity=="duty").Select(x=>x.DriveItemId).ToListAsync())known.Add(id);
  var home=await browser.BrowseAsync(null,null,default);var page=await browser.BrowseAsync(home.Items.Single().Handle,null,default);
  if(page.NextPageToken!=null)throw new Exception("Unexpected large root; leave it untouched.");
  var output=new List<object>();
  foreach(var item in page.Items){var decoded=handles.ReadHandle(item.Handle);if(!known.Contains(decoded.Item))throw new Exception("An unrecognized root item was added; preserve it for review.");
   var impact=await browser.ImpactAsync(new(item.Handle,"Delete"),default);var result=await browser.MutateAsync(new(impact.Ticket,"cleanup-generation-"+decoded.Item),default);
   output.Add(new{name=item.Name,operation=result.Id,status=result.Status});await File.WriteAllTextAsync(scratch+"/generation-cleanup.json",JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));
   if(result.Status!="Completed")throw new Exception("Cleanup result uncertain; reconcile its recorded operation before any new mutation.");Console.WriteLine("Synthetic generated branch moved to trash through the actual storage workflow: "+item.Name);
  }
  var empty=await browser.BrowseAsync(home.Items.Single().Handle,null,default);if(empty.Items.Count!=0)throw new Exception("Root not empty after bounded cleanup.");
  Console.WriteLine("Synthetic generation cleanup verified empty; retained SQL snapshots/artifacts/audit were not removed.");
 }
}
