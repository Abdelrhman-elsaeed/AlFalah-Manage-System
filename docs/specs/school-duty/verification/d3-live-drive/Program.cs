using System.Text.Json;
using AlFalah.Application.Duty;
using AlFalah.Application.Storage;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.Duty;
using AlFalah.Tests.TestDoubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

// Explicitly authorized D3_Test only. Never starts hosted workers, changes the original
// database, generates keys, deletes provider data, or enables the original school.
var workspace=Path.GetFullPath(args.Single());
var privatePath=Path.Combine(workspace,".audit/duty-organized-20261014/live-drive");Directory.CreateDirectory(privatePath);
var config=new ConfigurationBuilder().SetBasePath(Path.Combine(workspace,"backend/AlFalah.Api"))
 .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json").Build();
foreach(var endpoint in new[]{("ApiBaseUrl","https://www.googleapis.com/drive/v3/"),("UploadBaseUrl","https://www.googleapis.com/upload/drive/v3/"),("TokenEndpoint","https://oauth2.googleapis.com/token")})
 if((config["GoogleDrive:"+endpoint.Item1]??endpoint.Item2)!=endpoint.Item2)throw new InvalidOperationException("Live verification requires official Google endpoints.");
var sourceConnection=new SqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
if(!sourceConnection.DataSource.StartsWith("(localdb)\\",StringComparison.OrdinalIgnoreCase)||!sourceConnection.IntegratedSecurity)throw new InvalidOperationException("Read-only source must be local.");
await using var original=new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(sourceConnection.ConnectionString).Options);
var school=await original.Schools.AsNoTracking().SingleAsync(x=>x.Name=="Al-Falah E2E Test School"&&x.IsActive);
var source=await original.SchoolGoogleDrives.AsNoTracking().SingleAsync(x=>x.SchoolId==school.Id&&x.IsEnabled);
if(source.RootFolderId.Length==0||source.ProtectedCredential.Length==0||source.DutyArchiveEnabled)throw new InvalidOperationException("Source boundary/disabled duty flag required.");
var before=JsonSerializer.Serialize(source);
var fixture=new DutySqlFixture();await fixture.InitializeAsync();
if(!fixture.SentinelPreserved)throw new InvalidOperationException("Fresh migration sentinel lost.");
var scene=await new DutyExecutionSqlTests(fixture).Seed();
await using var db=fixture.Db();
var clone=new SchoolGoogleDrive{SchoolId=scene.School,CredentialType=source.CredentialType,ProtectedCredential=source.ProtectedCredential,
 OAuthClientId=source.OAuthClientId,ProtectedOAuthClientSecret=source.ProtectedOAuthClientSecret,SchoolGoogleEmail=source.SchoolGoogleEmail,
 ImpersonatedUserEmail=source.ImpersonatedUserEmail,SharedDriveId=source.SharedDriveId,RootFolderId=source.RootFolderId,IsEnabled=true,DutyArchiveEnabled=false,VisitArchiveEnabled=false};
db.Add(clone);
(await db.Schools.SingleAsync(x=>x.Id==scene.School)).Name="مدرسة اختبار المناوبات D3 — بيانات اصطناعية";
foreach(var user in new[]{scene.Teacher,scene.Replacement}){
 (await db.UserSignatures.SingleAsync(x=>x.UserId==user)).SignatureDrawnData="data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"TestAssets/Duty/signature-0.png")));
 var account=await db.Users.SingleAsync(x=>x.Id==user);account.FirstName="عبدالله عبدالرحمن";account.LastName="معلم اختبار اصطناعي";
}
db.SchoolReportSettings.Add(new(){SchoolId=scene.School,ReportHeaderText="المملكة العربية السعودية\nوزارة التعليم\nاختبار اصطناعي D3",ReportFooterText="اختبار اصطناعي معزول"});
await db.SaveChangesAsync();
var keys=config["DataProtection:KeysPath"]??"App_Data/DataProtectionKeys";
keys=Path.GetFullPath(Path.IsPathRooted(keys)?keys:Path.Combine(workspace,"backend/AlFalah.Api",keys));
if(!Directory.Exists(keys))throw new InvalidOperationException("Existing keys required.");
var services=new ServiceCollection();services.AddLogging();services.AddMemoryCache();
services.AddHttpClient("GoogleDrive",c=>c.Timeout=TimeSpan.FromSeconds(90));services.AddHttpClient("GoogleOAuth",c=>c.Timeout=TimeSpan.FromSeconds(30));
services.AddDataProtection().SetApplicationName(config["DataProtection:ApplicationName"]??"AlFalah.ManageSystem").PersistKeysToFileSystem(new DirectoryInfo(keys)).DisableAutomaticKeyGeneration();
using var dependencies=services.BuildServiceProvider();
var tokens=new GoogleDriveTokenService(db,new GoogleDriveCredentialProtector(dependencies.GetRequiredService<IDataProtectionProvider>()),dependencies.GetRequiredService<IHttpClientFactory>(),dependencies.GetRequiredService<IMemoryCache>(),config,NullLogger<GoogleDriveTokenService>.Instance);
var client=new GoogleDriveClient(tokens,dependencies.GetRequiredService<IHttpClientFactory>(),config,NullLogger<GoogleDriveClient>.Instance);
var real=new GoogleStorageProvider(client);var provider=new RecoverableRealProvider(real);
var root=await real.MetadataAsync(scene.School,source.RootFolderId,default);
if(root is not{IsFolder:true,Trashed:false}||(root.SharedDriveId??"")!=(source.SharedDriveId??"")||root.CanAddChildren==false)throw new InvalidOperationException("Authorized provider root could not be verified.");
Console.WriteLine("Verified authorized synthetic-school root. Original duty processing remains disabled.");
// Private durable reservation supports reconciliation after an uncertain create response.
var rootManifest=Path.Combine(privatePath,"destination-reservation.json");
string destination;
if(File.Exists(rootManifest)){
 var record=JsonDocument.Parse(await File.ReadAllTextAsync(rootManifest)).RootElement;
 if(record.GetProperty("root").GetString()!=source.RootFolderId)throw new InvalidOperationException("Destination reservation belongs to another root.");
 destination=record.GetProperty("id").GetString()!;
}else{
 string? page=null;do{var listing=await real.ChildrenAsync(scene.School,source.RootFolderId,source.SharedDriveId??"",page,default);
  if(listing.Files.Any(x=>x.Name=="D3_Test"&&!x.Trashed))throw new InvalidOperationException("An unreserved D3_Test already exists; do not adopt it.");page=listing.NextPageToken;
 }while(page!=null);
 destination=await real.AllocateIdAsync(scene.School,default);
 await File.WriteAllTextAsync(rootManifest,JsonSerializer.Serialize(new{root=source.RootFolderId,id=destination,name="D3_Test",authorizedBy="Explicit user destination selection",at=DateTimeOffset.UtcNow}));
}
real.ResetRequestCache();var destinationMeta=await real.MetadataAsync(scene.School,destination,default);
if(destinationMeta==null){await real.CreateFolderAsync(scene.School,destination,source.RootFolderId,"D3_Test",default);real.ResetRequestCache();destinationMeta=await real.MetadataAsync(scene.School,destination,default);}
if(destinationMeta is not{IsFolder:true,Trashed:false}||destinationMeta.Name!="D3_Test"||!destinationMeta.Parents.Contains(source.RootFolderId)||!await real.IsWithinAsync(scene.School,source.RootFolderId,destination,default))throw new InvalidOperationException("Test destination containment mismatch.");
var runName="Run-"+DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8];
var run=await real.AllocateIdAsync(scene.School,default);
await File.WriteAllTextAsync(Path.Combine(privatePath,runName+"-reservation.json"),JsonSerializer.Serialize(new{id=run,parent=destination,name=runName,database=new SqlConnectionStringBuilder(fixture.Connection).InitialCatalog}));
await real.CreateFolderAsync(scene.School,run,destination,runName,default);real.ResetRequestCache();
if(!await real.IsWithinAsync(scene.School,source.RootFolderId,run,default))throw new InvalidOperationException("Run root escaped authorized root.");
clone.RootFolderId=run;clone.RootFolderDisplayName=runName;await db.SaveChangesAsync();
var enabled=Options.Create(new DutyArchiveOptions{ProcessingEnabled=true});
var repo=new DutyDocumentRepository(db);var drive=new DutyArchiveDriveService(repo,new StorageRepository(db),new StorageLibraryRepository(db),provider);
DutyDocumentService Reader(bool manager=false)=>new(new DutyRepository(db),new DutyExecutionRepository(db),repo,new DutyPdfRenderer(),new TeacherDriveHarness.TestCurrentUser(manager?RoleNames.SchoolManager:RoleNames.Instructor,manager?scene.Manager:scene.Teacher,scene.School,true),drive,enabled);
DutyExecutionService Execution(bool manager=false)=>new(new DutyRepository(db),new DutyExecutionRepository(db),new TeacherDriveHarness.TestCurrentUser(manager?RoleNames.SchoolManager:RoleNames.Instructor,manager?scene.Manager:scene.Teacher,scene.School,true));
DutyArchiveProcessor Processor()=>new(repo,new DutyExecutionRepository(db),new DutyPdfRenderer(),drive,provider,enabled,NullLogger<DutyArchiveProcessor>.Instance);
var checks=new List<object>();void Check(bool condition,string step){if(!condition)throw new InvalidOperationException(step);checks.Add(new{step,passed=true});Console.WriteLine(step);}
QuestPDF.Settings.License=QuestPDF.Infrastructure.LicenseType.Community;
await Reader(true).EnableAsync(true,default);
var current=await Execution().GetAsync(scene.Assignment,default);
await Execution().AcknowledgeAsync(scene.Assignment,new(current.AssignmentRevision,Guid.NewGuid()),default);
var letter=await db.Set<DutySignedDocument>().AsNoTracking().SingleAsync(x=>x.SchoolId==scene.School&&x.Kind=="Letter");
var letterPdf=await Reader().ContentAsync(letter.Id,false,default);
provider.LoseResponse=true;await Processor().ProcessBatchAsync(default);db.ChangeTracker.Clear();
var letterIntent=await db.Set<DutyArchiveIntent>().SingleAsync(x=>x.DocumentId==letter.Id);
Check(letterIntent.State=="RetryScheduled"&&provider.Uploads==1&&await db.StoredFileVersions.CountAsync(x=>x.SchoolId==scene.School)==0,"Actual upload with deliberately lost response remains recoverable; no premature ledger completion");
Check((await Reader().ContentAsync(letter.Id,false,default)).Hash==letterPdf.Hash,"Local signed PDF remains available during external uncertainty");
await Reader(true).RetryAsync(letter.Id,default);await Processor().ProcessBatchAsync(default);db.ChangeTracker.Clear();
letterIntent=await db.Set<DutyArchiveIntent>().SingleAsync(x=>x.DocumentId==letter.Id);
Check(letterIntent.State=="Archived"&&provider.Uploads==1,"Reserved provider identity recovered without a second upload; actual Archived state");
Check(!await db.Set<DutySignedDocument>().AnyAsync(x=>x.SchoolId==scene.School&&x.Kind=="Report"),"Letter archived before its report exists");
var saved=await Execution().SaveDraftAsync(scene.Assignment,new(current.AssignmentRevision,0,new(true,true,[],"ملاحظات اختبار اصطناعي معزول — غادر آخر طالب الساعة 00:02",new(0,2))),default);
await Execution().SubmitAsync(scene.Assignment,new(current.AssignmentRevision,Guid.NewGuid(),saved.Draft!.Version),default);
var report=await db.Set<DutySignedDocument>().AsNoTracking().SingleAsync(x=>x.SchoolId==scene.School&&x.Kind=="Report");
var reportPdf=await Reader().ContentAsync(report.Id,false,default);
// Only the fresh isolated database receives this temporary fault injection.
// Identifiers contain only the integer generated by the fresh guarded SQL fixture.
var trigger=$"TR_D3_RealRollback_{scene.School}";
var createFault=$"CREATE TRIGGER {trigger} ON StoredFileVersions AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE SchoolId={scene.School}) THROW 51901,'Synthetic rollback after real upload',1; END";
var removeFault=$"DROP TRIGGER {trigger}";
await db.Database.ExecuteSqlRawAsync(createFault);
try{await Processor().ProcessBatchAsync(default);}finally{await db.Database.ExecuteSqlRawAsync(removeFault);}
db.ChangeTracker.Clear();
Check(provider.Uploads==2&&await db.StoredFileVersions.CountAsync(x=>x.SchoolId==scene.School)==1,"Actual report upload followed by real SQL rollback retains only the earlier letter ledger version");
await Reader(true).RetryAsync(report.Id,default);await Processor().ProcessBatchAsync(default);db.ChangeTracker.Clear();
Check(provider.Uploads==2&&await db.StoredFileVersions.CountAsync(x=>x.SchoolId==scene.School)==2,"SQL failure recovery reconciles existing bytes without duplicate file or version");
var hashes=new List<object>();
foreach(var doc in new[]{letter,report}){
 var artifact=await db.Set<DutyPdfArtifact>().AsNoTracking().SingleAsync(x=>x.DocumentId==doc.Id);
 var intent=await db.Set<DutyArchiveIntent>().AsNoTracking().SingleAsync(x=>x.DocumentId==doc.Id);
 var version=await db.StoredFileVersions.AsNoTracking().SingleAsync(x=>x.Id==intent.StoredFileVersionId);
 real.ResetRequestCache();var meta=await real.MetadataAsync(scene.School,intent.ProviderItemId!,default);
 var download=await real.ContentAsync(scene.School,intent.ProviderItemId!,default);await using var stream=download.Content;using var bytes=new MemoryStream();await stream.CopyToAsync(bytes);
 Check(intent.State=="Archived"&&DutyDocumentIntegrity.Hash(bytes.ToArray())==artifact.Hash&&version.SHA256==artifact.Hash&&meta!.Name==artifact.FileName,"Actual Drive download, frozen artifact and committed version hash match: "+doc.Kind);
 Check(await real.IsWithinAsync(scene.School,destination,intent.ProviderItemId!,default),"Delivered file contained in explicitly authorized D3_Test: "+doc.Kind);
 var local=await Reader().ContentAsync(doc.Id,false,default);var archived=await Reader(true).ContentAsync(doc.Id,true,default);
 Check(local.Hash==archived.Hash&&local.Hash==artifact.Hash,"Author local and manager protected archived reads return the identical artifact: "+doc.Kind);
 await File.WriteAllBytesAsync(Path.Combine(privatePath,doc.Kind.ToLowerInvariant()+".pdf"),bytes.ToArray());
 hashes.Add(new{kind=doc.Kind,document=doc.Id,providerItem=intent.ProviderItemId,sha256=artifact.Hash,bytes=artifact.Bytes.Length,providerVersion=meta!.Version});
}
await Processor().ProcessBatchAsync(default);await Processor().ReconcileAsync(default);db.ChangeTracker.Clear();
Check(provider.Uploads==2&&await db.StorageFolders.CountAsync(x=>x.SchoolId==scene.School)==5&&await db.StoredFileVersions.CountAsync(x=>x.SchoolId==scene.School)==2,"Repeated processing and reconciliation preserve one hierarchy and exactly two immutable ledger versions");
Check(await db.Set<DismissalNoticeBatch>().CountAsync(x=>x.SchoolId==scene.School)==0&&await db.ConversationMessages.CountAsync(x=>x.SchoolId==scene.School)==0,"Archive and PDF operations create no guardian notices or System/human messages");
var after=await original.SchoolGoogleDrives.AsNoTracking().SingleAsync(x=>x.SchoolId==school.Id);
Check(JsonSerializer.Serialize(after)==before,"Original school connection, root, flags and encrypted credentials unchanged by this harness");
var result=new{at=DateTimeOffset.UtcNow,liveProvider=true,provider="GoogleDriveClient",isolatedDatabase=new SqlConnectionStringBuilder(fixture.Connection).InitialCatalog,
 freshDatabaseGuard=true,sentinelPreserved=fixture.SentinelPreserved,school=scene.School,assignment=scene.Assignment,testDestination="D3_Test",destinationId=destination,runName,runRootId=run,
 originalSchoolDutyArchiveEnabled=false,actualUploads=provider.Uploads,simulatedLostResponses=1,realSqlRollback=1,checks,documents=hashes,existingProviderDataDeleted=false,ownerVisualAcceptance="Pending",physicalDeviceChecks="Pending"};
await File.WriteAllTextAsync(Path.Combine(privatePath,"results.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
var publicPath=Path.Combine(workspace,"docs/specs/school-duty/verification/d3-live-drive-results.json");await File.WriteAllTextAsync(publicPath,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Real Drive verification complete: {checks.Count} checks, {provider.Uploads} actual uploads.");

sealed class RecoverableRealProvider(IStorageProvider actual):IStorageProvider{
 public bool IsRealDelivery=>actual.IsRealDelivery;public bool LoseResponse;public int Uploads;
 public void ResetRequestCache()=>actual.ResetRequestCache();
 public Task<string> AllocateIdAsync(int s,CancellationToken c)=>actual.AllocateIdAsync(s,c);
 public Task<GoogleDriveFile?> MetadataAsync(int s,string id,CancellationToken c)=>actual.MetadataAsync(s,id,c);
 public Task<GoogleDriveFile> CreateFolderAsync(int s,string id,string p,string n,CancellationToken c)=>actual.CreateFolderAsync(s,id,p,n,c);
 public Task<GoogleDriveFile> UploadAsync(int s,string id,string p,string n,string mime,Stream bytes,CancellationToken c)=>throw new InvalidOperationException("Only immutable duty archive writes are authorized.");
 public async Task<GoogleDriveFile> UploadArchiveAsync(int s,string id,string p,string n,Stream bytes,IReadOnlyDictionary<string,string> identity,CancellationToken c){var result=await actual.UploadArchiveAsync(s,id,p,n,bytes,identity,c);Uploads++;if(LoseResponse){LoseResponse=false;throw new TimeoutException("Synthetic lost response after actual upload");}return result;}
 public Task<GoogleDriveFile> MoveAsync(int s,string id,string p,string n,CancellationToken c)=>throw new InvalidOperationException("External move prohibited in this test.");
 public Task<GoogleDriveFile> RenameAsync(int s,string id,string n,CancellationToken c)=>throw new InvalidOperationException("External rename prohibited in this test.");
 public Task<bool> TrashAsync(int s,string id,string d,CancellationToken c)=>throw new InvalidOperationException("External deletion prohibited in this test.");
 public Task<DriveFileContentDto> ContentAsync(int s,string id,CancellationToken c)=>actual.ContentAsync(s,id,c);
 public Task<bool> IsWithinAsync(int s,string root,string id,CancellationToken c)=>actual.IsWithinAsync(s,root,id,c);
 public Task<GoogleDriveFileList> ChildrenAsync(int s,string p,string d,string? token,CancellationToken c)=>actual.ChildrenAsync(s,p,d,token,c);
}
