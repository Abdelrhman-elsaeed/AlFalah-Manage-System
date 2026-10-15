using System.Security.Cryptography;
using System.Text.Json;
using AlFalah.Application.Duty;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Data.Seeders;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.Duty;
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

// C27 only: the owner explicitly authorized emptying this existing synthetic school's root.
// This executable creates files only when the guarded, previously inventoried root is empty.
// Original SQL/credentials/flags/grants are read-only. Fresh SQL is never reused/reset/dropped.
if(Environment.GetEnvironmentVariable("ALFALAH_STORAGE_EMPTY_ROOT_TEST")!="1")throw new Exception("Explicit empty-root verification boundary required.");
var workspace=Path.GetFullPath(args.Single());var scratch=Path.Combine(workspace,".audit/storage-drive-20261015");
var config=new ConfigurationBuilder().SetBasePath(workspace+"/backend/AlFalah.Api").AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json").Build();
var sourceConnection=new SqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
if(!sourceConnection.DataSource.StartsWith("(localdb)\\",StringComparison.OrdinalIgnoreCase)||!sourceConnection.IntegratedSecurity)throw new Exception("Original SQL must be local.");
await using var original=new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(sourceConnection.ConnectionString).Options);
var school=await original.Schools.AsNoTracking().SingleAsync(x=>x.Id==18&&x.Name=="Al-Falah E2E Test School"&&x.IsActive);
var source=await original.SchoolGoogleDrives.AsNoTracking().SingleAsync(x=>x.SchoolId==school.Id&&x.IsEnabled);var before=JsonSerializer.Serialize(source);
var inventory=JsonDocument.Parse(await File.ReadAllTextAsync(scratch+"/inventory.json")).RootElement;
if(inventory.GetProperty("root").GetString()!=source.RootFolderId)throw new Exception("Configured root changed after the authorized inventory.");
if(Environment.GetEnvironmentVariable("ALFALAH_STORAGE_GENERATION_CLEANUP")=="1"){
 await GenerationCleanup.Run(workspace,config,source.RootFolderId);return;
}
var fixture=new DutySqlFixture();await fixture.InitializeAsync();if(!fixture.SentinelPreserved)throw new Exception("Migration sentinel not retained.");
var scene=await new DutyExecutionSqlTests(fixture).Seed(guardian:false);await using var db=fixture.Db();
var clone=new SchoolGoogleDrive{SchoolId=scene.School,CredentialType=source.CredentialType,ProtectedCredential=source.ProtectedCredential,
 OAuthClientId=source.OAuthClientId,ProtectedOAuthClientSecret=source.ProtectedOAuthClientSecret,SchoolGoogleEmail=source.SchoolGoogleEmail,
 ImpersonatedUserEmail=source.ImpersonatedUserEmail,SharedDriveId=source.SharedDriveId,RootFolderId=source.RootFolderId,IsEnabled=true,DutyArchiveEnabled=true,VisitArchiveEnabled=true};db.Add(clone);
var signature="data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"TestAssets/Duty/signature-0.png")));
foreach(var id in new[]{scene.Manager,scene.Teacher,scene.Replacement}){
 var account=await db.Users.SingleAsync(x=>x.Id==id);account.FirstName="اختبار توليد الملفات";account.LastName=id==scene.Teacher?"معلم اصطناعي":id==scene.Manager?"مدير اصطناعي":"بديل اصطناعي";
 var sign=await db.UserSignatures.SingleOrDefaultAsync(x=>x.UserId==id);if(sign==null){sign=new(){UserId=id};db.Add(sign);}sign.SignatureDrawnData=signature;
}
foreach(var actor in new[]{scene.Manager,scene.Teacher}){
 var role="generation-"+actor;db.Roles.Add(new(){Id=role,Name=role,NormalizedName=role.ToUpperInvariant()});db.UserSchoolRoles.Add(new(){UserId=actor,SchoolId=scene.School,RoleId=role});
 foreach(var name in actor==scene.Manager?new[]{PermissionNames.StorageViewSchool,PermissionNames.StorageManageSchool,PermissionNames.StorageDelegate,PermissionNames.StorageViewArchive,PermissionNames.VisitView}:new[]{PermissionNames.StorageViewOwn,PermissionNames.StorageManageOwn}){
  var p=await db.Permissions.SingleOrDefaultAsync(x=>x.Name==name);if(p==null){p=new(){Name=name,Group="Synthetic generation"};db.Add(p);await db.SaveChangesAsync();}db.RolePermissions.Add(new(){RoleId=role,PermissionId=p.Id});
 }
}
db.SchoolReportSettings.Add(new(){SchoolId=scene.School,ReportHeaderText="المملكة العربية السعودية\nوزارة التعليم\nاختبار توليد اصطناعي معزول",ReportFooterText="وثيقة اختبار اصطناعي"});await db.SaveChangesAsync();
var keys=config["DataProtection:KeysPath"]??"App_Data/DataProtectionKeys";keys=Path.GetFullPath(Path.IsPathRooted(keys)?keys:Path.Combine(workspace,"backend/AlFalah.Api",keys));if(!Directory.Exists(keys))throw new Exception("Existing keys required.");
var services=new ServiceCollection();services.AddLogging();services.AddMemoryCache();services.AddHttpClient("GoogleDrive",c=>c.Timeout=TimeSpan.FromSeconds(90));services.AddHttpClient("GoogleOAuth");
services.AddDataProtection().SetApplicationName(config["DataProtection:ApplicationName"]??"AlFalah").PersistKeysToFileSystem(new(keys)).DisableAutomaticKeyGeneration();await using var dependencies=services.BuildServiceProvider();
var tokens=new GoogleDriveTokenService(db,new GoogleDriveCredentialProtector(dependencies.GetRequiredService<IDataProtectionProvider>()),dependencies.GetRequiredService<IHttpClientFactory>(),dependencies.GetRequiredService<IMemoryCache>(),config,NullLogger<GoogleDriveTokenService>.Instance);
var google=new GoogleDriveClient(tokens,dependencies.GetRequiredService<IHttpClientFactory>(),config,NullLogger<GoogleDriveClient>.Instance);var provider=new GoogleStorageProvider(google);
var root=await provider.MetadataAsync(scene.School,source.RootFolderId,default);
if(root is not{IsFolder:true,Trashed:false}||(root.SharedDriveId??"")!=(source.SharedDriveId??""))throw new Exception("Invalid authorized root.");
var empty=await provider.ChildrenAsync(scene.School,root.Id,source.SharedDriveId??"",null,default);if(empty.Files.Count!=0||empty.NextPageToken!=null)throw new Exception("Root is not empty; never adopt/delete its contents here.");
var checks=new List<object>();void Check(bool value,string step){if(!value)throw new Exception(step);checks.Add(new{step,passed=true});Console.WriteLine(step);}
Check(true,"Authorized original synthetic-school root verified completely empty before creation");
await File.WriteAllTextAsync(scratch+"/generation-reservation.json",JsonSerializer.Serialize(new{root=root.Id,school=scene.School,database=new SqlConnectionStringBuilder(fixture.Connection).InitialCatalog,at=DateTimeOffset.UtcNow}));
var current=new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,scene.Manager,scene.School,true);
var storage=new StorageRepository(db);var library=new StorageLibraryRepository(db);var auth=new StorageAuthorizationService(storage,current,new StorageDriveBoundary(new(google)),TimeProvider.System);
var dutyRepo=new DutyDocumentRepository(db);var dutyDrive=new DutyArchiveDriveService(dutyRepo,storage,library,provider);var options=Options.Create(new StorageOptions{AdministrationEnabled=true,ReadModelEnabled=true,ArchiveProcessingEnabled=true});
var mappingAudit=new AuditLogWriter(db,new HttpContextAccessor(),NullLogger<AuditLogWriter>.Instance);
var mappings=new TeacherDriveMappingService(db,new SchoolScopeGuard(db,current,NullLogger<SchoolScopeGuard>.Instance),google,new TeacherDriveFolderGuard(google),mappingAudit,current);
var setup=new StorageSetupService(new StorageSetupRepository(db),storage,library,auth,current,provider,mappings,options,dutyDrive);
var created=await setup.ApplyAsync(true,true);Check(created.Warnings.Count==0,"Actual initial setup creates the library, teacher files, visits and protected duty hierarchy from an empty root");
var first=(await db.StorageFolders.Where(x=>x.SchoolId==scene.School).Select(x=>x.DriveItemId).ToListAsync()).Order().ToArray();
var replay=await setup.ApplyAsync(true,true);Check(replay.Warnings.Count==0&&first.SequenceEqual((await db.StorageFolders.Where(x=>x.SchoolId==scene.School).Select(x=>x.DriveItemId).ToListAsync()).Order()),"Repeated initial setup keeps the same provider folder identities");
QuestPDF.Settings.License=QuestPDF.Infrastructure.LicenseType.Community;var dutyOptions=Options.Create(new DutyArchiveOptions{ProcessingEnabled=true});
DutyExecutionService Execution()=>new(new DutyRepository(db),new DutyExecutionRepository(db),new TeacherDriveHarness.TestCurrentUser(RoleNames.Instructor,scene.Teacher,scene.School,true));
DutyArchiveProcessor Duties()=>new(dutyRepo,new DutyExecutionRepository(db),new DutyPdfRenderer(),dutyDrive,provider,dutyOptions,NullLogger<DutyArchiveProcessor>.Instance);
var detail=await Execution().GetAsync(scene.Assignment,default);await Execution().AcknowledgeAsync(scene.Assignment,new(detail.AssignmentRevision,Guid.NewGuid()),default);
await Duties().ProcessBatchAsync(default);db.ChangeTracker.Clear();Check(await db.Set<DutyArchiveIntent>().AnyAsync(x=>x.SchoolId==scene.School&&x.State=="Archived"),"Teacher's actual acknowledgement automatically renders and delivers its letter before a report exists");
var saved=await Execution().SaveDraftAsync(scene.Assignment,new(detail.AssignmentRevision,0,new(true,true,[],"اختبار تلقائي اصطناعي — مغادرة آخر طالب 00:02",new(0,2))),default);
await Execution().SubmitAsync(scene.Assignment,new(detail.AssignmentRevision,Guid.NewGuid(),saved.Draft!.Version),default);await Duties().ProcessBatchAsync(default);db.ChangeTracker.Clear();
Check(await db.Set<DutyArchiveIntent>().CountAsync(x=>x.SchoolId==scene.School&&x.State=="Archived")==2,"Actual signed report submission automatically renders and delivers the separate report");
await new RubricV2Seeder(db,NullLogger<RubricV2Seeder>.Instance).SeedAsync();var audit=new AuditLogWriter(db,new HttpContextAccessor(),NullLogger<AuditLogWriter>.Instance);var visitRepo=new VisitArchiveRepository(db);
var visits=new VisitV2Service(new VisitV2Repository(db),new VisitV2DocumentService(new ImageAssetLoader()),current,new Flags(),new SchoolScopeGuard(db,current,NullLogger<SchoolScopeGuard>.Instance),audit,NullLogger<VisitV2Service>.Instance,new VisitArchiveCaptureService(visitRepo,new VisitArchiveAssetFreezer(new ImageAssetLoader())));
var visit=await visits.CreateAsync(new(scene.Teacher,1,1,DateTimeOffset.UtcNow,2,"مادة اختبار اصطناعي","فصل اختبار","تحقق التوليد التلقائي",1,0,"لا بيانات حقيقية"));var finalized=await visits.FinalizeAsync(visit.Id);
if(finalized.Status!=(int)VisitStatus.Approved)await visits.ApproveAsync(visit.Id);
var visitProcessor=new VisitArchiveProcessor(visitRepo,new(storage,library,visitRepo,new StorageSetupRepository(db),provider),provider,new VisitV2DocumentService(new ImageAssetLoader()),options,TimeProvider.System,NullLogger<VisitArchiveProcessor>.Instance);
await visitProcessor.ProcessBatchAsync();db.ChangeTracker.Clear();Check(await db.Set<VisitArchiveOperation>().AnyAsync(x=>x.SchoolId==scene.School&&x.VisitId==visit.Id&&x.Status==VisitArchiveStatus.Completed),"Actual visit approval captures the official snapshot and automatically delivers its PDF");
var letter=await db.Set<DutyPdfArtifact>().Where(x=>x.SchoolId==scene.School&&x.DocumentId!=null).OrderBy(x=>x.Id).FirstAsync();
var libraryRoot=await db.StorageFolders.SingleAsync(x=>x.SchoolId==scene.School&&x.Kind==StorageFolderKind.SchoolLibrary&&x.ParentFolderId==null);
var schoolUpload=new StorageLibraryService(library,storage,auth,current,provider,new EvidenceSubmissionService(db,audit),options);
using(var bytes=new MemoryStream(letter.Bytes))Check((await schoolUpload.UploadAsync(new(bytes,"اختبار مكتبة المدرسة.pdf",bytes.Length,libraryRoot.Id,"generation-school-file",false))).Status=="Completed","Actual school library upload reaches its provisioned folder");
var teacher=new TeacherDriveHarness.TestCurrentUser(RoleNames.Instructor,scene.Teacher,scene.School,true);var teacherAuth=new StorageAuthorizationService(storage,teacher,new StorageDriveBoundary(new(google)),TimeProvider.System);
var teacherUpload=new StorageLibraryService(library,storage,teacherAuth,teacher,provider,new EvidenceSubmissionService(db,audit),options);
using(var bytes=new MemoryStream(letter.Bytes))Check((await teacherUpload.UploadAsync(new(bytes,"اختبار ملف المعلم.pdf",bytes.Length,null,"generation-teacher-file",true))).Status=="Completed","Actual teacher upload reaches its newly provisioned own folder");
var hashes=new List<object>();foreach(var version in await db.StoredFileVersions.AsNoTracking().Where(x=>x.SchoolId==scene.School).ToListAsync()){
 provider.ResetRequestCache();var content=await provider.ContentAsync(scene.School,version.DriveItemId,default);await using var stream=content.Content;using var bytes=new MemoryStream();await stream.CopyToAsync(bytes);var hash=Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
 Check(hash==version.SHA256&&bytes.Length==version.SizeInBytes,"Actual downloaded bytes equal committed immutable hash: "+version.DriveFileName);
 await File.WriteAllBytesAsync(scratch+"/generation-"+version.Id+".pdf",bytes.ToArray());hashes.Add(new{version=version.Id,name=version.DriveFileName,hash,bytes=bytes.Length});
}
await Duties().ProcessBatchAsync(default);await visitProcessor.ProcessBatchAsync();Check(await db.StoredFileVersions.CountAsync(x=>x.SchoolId==scene.School)==5,"Repeated automatic processing creates no duplicate versions or uploads");
Check(!await db.Set<DismissalNoticeBatch>().AnyAsync(x=>x.SchoolId==scene.School)&&!await db.ConversationMessages.AnyAsync(x=>x.SchoolId==scene.School),"Generation, PDF reads and archive processing send no guardian or human/System messages");
Check(before==JsonSerializer.Serialize(await original.SchoolGoogleDrives.AsNoTracking().SingleAsync(x=>x.SchoolId==school.Id)),"Original credentials, provider root, grants and school activation remain unchanged");
var roots=new List<object>();string? pageToken=null;do{var page=await provider.ChildrenAsync(scene.School,root.Id,source.SharedDriveId??"",pageToken,default);pageToken=page.NextPageToken;foreach(var item in page.Files)roots.Add(new{id=item.Id,name=item.Name});}while(pageToken!=null);
await File.WriteAllTextAsync(scratch+"/generation-output.json",JsonSerializer.Serialize(new{at=DateTimeOffset.UtcNow,school=scene.School,root=root.Id,database=new SqlConnectionStringBuilder(fixture.Connection).InitialCatalog,checks,hashes,roots,realDrive=true,uploads=5,sentinelPreserved=fixture.SentinelPreserved,originalSqlReadOnly=true},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Real empty-root generation complete: {checks.Count} checks; five actual file uploads. Fresh SQL and native artifacts retained for bounded cleanup.");
sealed class Flags:IFeatureFlagService{public bool IsVisitsV2Enabled(int? schoolId=null)=>true;}
