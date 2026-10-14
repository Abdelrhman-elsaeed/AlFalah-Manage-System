using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AlFalah.Application.Duty;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Services;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Tests.TestDoubles;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using AlFalah.Tests.Duty;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Fresh guarded LocalDB only. All people, students and guardian links are synthetic.
// No configuration file, operational database, credential, key or external provider is changed.
var fixture=new DutySqlFixture();await fixture.InitializeAsync();
var scene=await new DutyExecutionSqlTests(fixture).Seed();
QuestPDF.Settings.License=QuestPDF.Infrastructure.LicenseType.Community;
// Publish a longer rotation, then reconcile repeated effective-date absences before signing.
await using(var db=fixture.Db()) {
 var planner=new DutyService(new DutyRepository(db),new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,scene.Manager,scene.School,true));
 var plan=await db.Set<DutyPlan>().SingleAsync(x=>x.SchoolId==scene.School);var settings=DutyRotation.Settings(plan) with {To=scene.Date.AddDays(4),RegenerateTo=scene.Date.AddDays(4)};
 var preview=await planner.PreviewAsync(new(settings,plan.Revision),default);await planner.PublishAsync(new(settings,plan.Revision,preview.Digest,Guid.NewGuid()),default);
}
var scenario=new DutyScenario(fixture,scene.School,scene.Term,scene.Manager,scene.Manager,[scene.Teacher,scene.Replacement],null!);
await scenario.Absence(scene.Teacher,scene.Date);await scenario.Process(scene.Date);
await scenario.Absence(scene.Teacher,scene.Date.AddDays(1));await scenario.Process(scene.Date.AddDays(1));
await using(var db=fixture.Db()) {
 var day=await db.Set<DutyAssignment>().SingleAsync(x=>x.Id==scene.Assignment);
 if(day.EffectiveTeacherId!=scene.Replacement || await db.Set<DutyAssignmentChange>().CountAsync(x=>x.SchoolId==scene.School&&x.Kind=="AutoExchange")!=2)throw new InvalidOperationException("Repeated date exchanges were not reconciled");
 await new DutyExecutionService(new DutyRepository(db),new DutyExecutionRepository(db),new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,scene.Manager,scene.School,true)).TimingExceptionAsync(scene.Assignment,new(day.Revision,new TimeOnly(0,0),"Synthetic timing after repeated absence",Guid.NewGuid()),default);
}
scene=scene with {Teacher=scene.Replacement,Replacement=scene.Teacher};
var officer="d3-officer-"+Guid.NewGuid().ToString("N");
await using(var db=fixture.Db())
{
    foreach(var user in await db.Users.Where(x=>x.Id==scene.Manager||x.Id==scene.Teacher||x.Id==scene.Guardian||x.Id==scene.Replacement).ToListAsync())user.SecurityStamp=Guid.NewGuid().ToString();
    db.UserSignatures.Add(new(){UserId=scene.Guardian,SignatureDrawnData="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jYwoAAAAASUVORK5CYII="});
    if(!await db.Roles.AnyAsync(x=>x.Name==RoleNames.StudentAffairsOfficer))db.Roles.Add(new(){Id=RoleNames.StudentAffairsOfficer,Name=RoleNames.StudentAffairsOfficer,NormalizedName=RoleNames.StudentAffairsOfficer.ToUpperInvariant()});
    db.Users.Add(new(){Id=officer,UserName=officer,FirstName="Synthetic",LastName="Officer",SecurityStamp=Guid.NewGuid().ToString()});
    db.UserSchoolRoles.Add(new(){SchoolId=scene.School,UserId=officer,RoleId=RoleNames.StudentAffairsOfficer});
    db.UserSignatures.Add(new(){UserId=officer,SignatureDrawnData="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jYwoAAAAASUVORK5CYII="});
    foreach(var id in new[]{scene.Teacher,scene.Replacement})
      (await db.UserSignatures.SingleAsync(x=>x.UserId==id)).SignatureDrawnData="data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"TestAssets","Duty","signature-0.png")));
    await db.SaveChangesAsync();
}
var workspace=Path.GetFullPath(args.FirstOrDefault()??throw new InvalidOperationException("Pass workspace root."));
const string origin="http://127.0.0.1:5389";const string secret="AlFalah-D3-Isolated-Synthetic-Smoke-Key-2026-10-13";
var start=new ProcessStartInfo("dotnet"){WorkingDirectory=Path.Combine(workspace,"backend/AlFalah.Api"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
start.ArgumentList.Add(Path.Combine(start.WorkingDirectory,"bin/Debug/net8.0/AlFalah.Api.dll"));
foreach(var pair in new Dictionary<string,string>{{"ASPNETCORE_ENVIRONMENT","Development"},{"ASPNETCORE_URLS",origin},{"ConnectionStrings__DefaultConnection",fixture.Connection},{"Database__InitializeOnStartup","false"},{"Jwt__Secret",secret},{"Jwt__Issuer","D3-Smoke"},{"Jwt__Audience","D3-Smoke"},{"DataProtection__KeysPath",Path.Combine(Path.GetTempPath(),"AlFalah-D3-Isolated-Smoke-Keys")},{"Logging__LogLevel__Default","Error"},{"Logging__LogLevel__Microsoft","Error"}})start.Environment[pair.Key]=pair.Value;
start.Environment["SchoolDutyArchive__ProcessingEnabled"]="false";
start.Environment["SchoolFileStorage__ArchiveProcessingEnabled"]="false";
foreach(var key in new[]{"Enabled","VisitArchiveEnabled","VisitArchiveWorkerEnabled","ExternalWritesEnabled","AdministrationEnabled","ReadModelEnabled","ArchiveWorkerEnabled","ArchiveExternalWritesEnabled"})start.Environment["SchoolFileStorage__"+key]="false";
using var process=Process.Start(start)??throw new InvalidOperationException("Host could not start.");var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
using var client=new HttpClient{BaseAddress=new(origin),Timeout=TimeSpan.FromSeconds(30)};var results=new List<object>{new {step="Actual SQL publication and two effective-date absence exchanges before substitute acknowledgement",status=200}};
var jsonOptions=new JsonSerializerOptions(JsonSerializerDefaults.Web);
async Task Token(string id,string role,string[] permissions)
{
    await using var db=fixture.Db();var user=await db.Users.SingleAsync(x=>x.Id==id);
    var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Jwt:Secret",secret},{"Jwt:Issuer","D3-Smoke"},{"Jwt:Audience","D3-Smoke"}}).Build();
    client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",new JwtService(config).GenerateAccessToken(user.Id,user.UserName!,[role],permissions,scene.School,"ar",user.SecurityStamp!));
}
async Task<JsonElement> Check(HttpResponseMessage response,HttpStatusCode status,string step)
{
    if(response.StatusCode!=status)throw new InvalidOperationException($"{step}: expected {(int)status}, received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    if(!json.TryGetProperty("isSuccess",out _))throw new InvalidOperationException("ApiResponse envelope missing: "+step);
    results.Add(new{step,status=(int)status});return json;
}
string assignment=$"/api/v1/school-duty/assignments/{scene.Assignment}";
try
{
    for(var attempt=0;attempt<40;attempt++)
    {
        if(process.HasExited)throw new InvalidOperationException("Host exited: "+await stderr);
        try{await client.GetAsync("/api/v1/school-duty/context");break;}catch(Exception ex)when(ex is HttpRequestException or TaskCanceledException){await Task.Delay(500);}
    }
    Console.WriteLine("Synthetic D3 HTTP host ready.");
    await Token(scene.Teacher,RoleNames.Instructor,[DutyPermissions.View]);
    await Check(await client.GetAsync("/api/v1/school-duty/archive/enablement"),HttpStatusCode.Forbidden,"Teacher cannot read manager archive settings");
    await Token(scene.Replacement,RoleNames.Instructor,[DutyPermissions.View]);
    await Check(await client.PostAsJsonAsync(assignment+"/acknowledge",new DutySignRequest(1,Guid.NewGuid())),HttpStatusCode.Forbidden,"Other teacher cannot take duty by forging acknowledgement");
    await Token(scene.Teacher,RoleNames.Instructor,[DutyPermissions.View]);
    var execution=(await Check(await client.GetAsync(assignment+"/execution"),HttpStatusCode.OK,"Own execution and unsigned preview")).GetProperty("data").Deserialize<DutyExecution>(jsonOptions)!;
    if(execution.Acknowledged)throw new InvalidOperationException("Opening signed a letter");
    await Check(await client.GetAsync($"/api/v1/school-duty/students?assignmentId={scene.Assignment}&search=Duplicate"),HttpStatusCode.OK,"Actual whole-school roster lookup");
    var sign=new DutySignRequest(execution.AssignmentRevision,Guid.NewGuid());
    await Check(await client.PostAsJsonAsync(assignment+"/acknowledge",sign),HttpStatusCode.OK,"Explicit actual-teacher saved signature acknowledgement");
    var late=new DutyLateRequest(execution.AssignmentRevision,scene.Student,scene.Enrollment,new(0,1),null,"Synthetic pickup action","",Guid.NewGuid());
    var fact=(await Check(await client.PostAsJsonAsync(assignment+"/dismissal-lateness",late),HttpStatusCode.OK,"Explicit fact and durable intent commit before report")).GetProperty("data").Deserialize<DutyLateDto>(jsonOptions)!;
    var replay=(await Check(await client.PostAsJsonAsync(assignment+"/dismissal-lateness",late),HttpStatusCode.OK,"Lost-response fact confirmation replay")).GetProperty("data").Deserialize<DutyLateDto>(jsonOptions)!;
    if(replay.Id!=fact.Id)throw new InvalidOperationException("Replay duplicated a fact");
    for(var attempt=0;attempt<30;attempt++)
    {
        await using var db=fixture.Db();if(await db.Set<DismissalNoticeBatch>().AnyAsync(x=>x.SchoolId==scene.School&&x.State=="Delivered"))break;
        if(attempt==29)throw new InvalidOperationException("Hosted worker did not deliver both surfaces");await Task.Delay(500);
    }
    results.Add(new{step="Real hosted worker persisted dashboard notice and System message without report or human office hours",status=200});
    await Token(scene.Guardian,RoleNames.Guardian,[PermissionNames.GuardianViewLinkedStudents,PermissionNames.NotificationViewOwn,PermissionNames.MessagingViewOwn,PermissionNames.MessagingSend]);
    var notices=(await Check(await client.GetAsync("/api/v1/school-duty/guardian/notices"),HttpStatusCode.OK,"Effective guardian dashboard content and deep link")).GetProperty("data");
    var notification=notices[0].GetProperty("notificationId").GetInt32();
    var route=notices[0].GetProperty("messageRoute").GetString()!;var thread=int.Parse(route.Split('=').Last());
    await Check(await client.PostAsJsonAsync($"/api/v1/school-duty/guardian/notices/{notification}/read",new{}),HttpStatusCode.OK,"Dashboard read receipt");
    await using(var db=fixture.Db())if(await db.MessageReceipts.AnyAsync(x=>x.SchoolId==scene.School&&x.ReadAt!=null))throw new InvalidOperationException("Dashboard read invented message read");
    await Check(await client.GetAsync("/api/v1/conversations"),HttpStatusCode.OK,"Existing Messages inbox and unread thread");
    var messages=(await Check(await client.GetAsync($"/api/v1/conversations/{thread}/messages"),HttpStatusCode.OK,"System provenance in actual Messages projection")).GetProperty("data").GetProperty("items");
    if(messages[0].GetProperty("sender").GetProperty("roleSnapshot").GetString()!="System")throw new InvalidOperationException("Human sender impersonation");
    await Check(await client.PostAsJsonAsync($"/api/v1/conversations/{thread}/read",new{throughMessageId=messages[0].GetProperty("id").GetInt32()}),HttpStatusCode.OK,"Independent actual message read receipt");
    await Check(await client.PostAsJsonAsync($"/api/v1/conversations/{thread}/messages",new{body="Spoof",idempotencyKey="synthetic-spoof",isSystemOrigin=true,senderUserId="system"}),HttpStatusCode.BadRequest,"Browser System-origin forgery rejected");
    await Token(scene.Manager,RoleNames.SchoolManager,[DutyPermissions.Manage,DutyPermissions.View,PermissionNames.StudentView]);
    await Check(await client.GetAsync("/api/v1/school-duty/dismissal-lateness"),HttpStatusCode.OK,"Manager fact names/counts before report submission");
    await Check(await client.GetAsync($"/api/v1/students/{scene.Student}/analytics-profile"),HttpStatusCode.Forbidden,"Existing manager aggregate-only student-record authorization preserved");
    await Token(officer,RoleNames.StudentAffairsOfficer,[PermissionNames.StudentView]);
    await Check(await client.GetAsync($"/api/v1/students/{scene.Student}/timeline"),HttpStatusCode.OK,"Actual authorized student timeline before report submission");
    var profile=(await Check(await client.GetAsync($"/api/v1/students/{scene.Student}/analytics-profile"),HttpStatusCode.OK,"Actual student profile count and retained timeline projection")).GetProperty("data");
    if(profile.GetProperty("totalDismissalLateness").GetInt32()!=1)throw new InvalidOperationException("Student profile count missing");
    await Token(scene.Manager,RoleNames.SchoolManager,[DutyPermissions.Manage,DutyPermissions.View]);
    await Check(await client.PostAsJsonAsync(assignment+"/acknowledge",sign with{RequestId=Guid.NewGuid()}),HttpStatusCode.Forbidden,"Manager cannot sign for teacher");
    await Token(scene.Teacher,RoleNames.Instructor,[DutyPermissions.View]);
    var saved=(await Check(await client.PutAsJsonAsync(assignment+"/report/draft",new DutyDraftRequest(execution.AssignmentRevision,0,new(false,true,[],"Synthetic final report",new(0,2)))),HttpStatusCode.OK,"Actual server draft with timestamp")).GetProperty("data").Deserialize<DutyExecution>(jsonOptions)!;
    var submit=new DutySignRequest(execution.AssignmentRevision,Guid.NewGuid(),saved.Draft!.Version);
    await Check(await client.PostAsJsonAsync(assignment+"/report/submit",submit),HttpStatusCode.OK,"Actual signed local report and D3 archive intent");
    await Check(await client.PostAsJsonAsync(assignment+"/report/submit",submit),HttpStatusCode.OK,"Signed submission lost-response replay");
    await using(var db=fixture.Db())
    {
        if(await db.ConversationMessages.CountAsync(x=>x.SchoolId==scene.School)!=1||await db.Set<DutyArchiveIntent>().CountAsync(x=>x.SchoolId==scene.School)!=2)throw new InvalidOperationException("Duplicate local delivery/archive intent");
    }
    var documents=(await Check(await client.GetAsync(assignment+"/documents"),HttpStatusCode.OK,"Separate locally retained letter and report while external processing disabled")).GetProperty("data").Deserialize<DutyDocumentCollection>(jsonOptions)!;
    if(documents.Documents.Count!=2 || documents.Documents.Any(x=>x.ArchiveState=="Archived"))throw new InvalidOperationException("False archive completion");
    var reportId=documents.Documents.Single(x=>x.Kind=="Report").DocumentId;var letterId=documents.Documents.Single(x=>x.Kind=="Letter").DocumentId;
    async Task<string> Pdf(int id,string step) {
      using var response=await client.GetAsync($"/api/v1/school-duty/documents/{id}/pdf");if(response.StatusCode!=HttpStatusCode.OK)throw new InvalidOperationException(step+await response.Content.ReadAsStringAsync());
      var bytes=await response.Content.ReadAsByteArrayAsync();var hash=DutyDocumentIntegrity.Hash(bytes);
      if(response.Content.Headers.ContentType?.MediaType!="application/pdf" || response.Headers.GetValues("X-Document-SHA256").Single()!=hash || !response.Headers.CacheControl!.NoStore)throw new InvalidOperationException("PDF response integrity headers");
      await using var db=fixture.Db();if((await db.Set<DutyPdfArtifact>().SingleAsync(x=>x.DocumentId==id)).Hash!=hash)throw new InvalidOperationException("HTTP bytes differ from retained artifact");
      results.Add(new{step,status=200,sha256=hash});return hash;
    }
    var retainedHash=await Pdf(reportId,"Actual authenticated HTTP local PDF download and SQL byte hash equality");var retainedLetterHash=await Pdf(letterId,"Letter available independently from report archive progress");
    await Check(await client.GetAsync($"/api/v1/school-duty/documents/{reportId}/pdf?archived=true"),HttpStatusCode.Conflict,"External archive unavailable without false completion");
    await Check(await client.PostAsJsonAsync($"/api/v1/school-duty/documents/{reportId}/archive/retry",new{}),HttpStatusCode.Forbidden,"Teacher retry does not grant archive management");
    await Token(scene.Replacement,RoleNames.Instructor,[DutyPermissions.View]);
    await Check(await client.GetAsync($"/api/v1/school-duty/documents/{reportId}/pdf"),HttpStatusCode.Forbidden,"Original absent teacher cannot read substitute signature/report");
    await Token(scene.Manager,RoleNames.SchoolManager,[DutyPermissions.Manage,DutyPermissions.View]);
    await Pdf(reportId,"Actual authorized school manager PDF read");
    await Check(await client.PostAsJsonAsync($"/api/v1/school-duty/documents/{reportId}/archive/retry",new{}),HttpStatusCode.Conflict,"Manager retry blocked by disconnected/disabled readiness");
    await Check(await client.PutAsJsonAsync("/api/v1/school-duty/archive/enablement",new{enabled=true}),HttpStatusCode.Conflict,"Process-disabled activation is rejected");
    var context=(await Check(await client.GetAsync($"/api/v1/school-duty/context?termId={scene.Term}"),HttpStatusCode.OK,"Final locked schedule context")).GetProperty("data");
    var revision=context.GetProperty("planRevision").GetInt32();
    await Token(scene.Teacher,RoleNames.Instructor,[DutyPermissions.View]);
    string rosterHash;
    using(var roster=await client.GetAsync($"/api/v1/school-duty/plans/roster.pdf?termId={scene.Term}&revision={revision}")) {
      if(roster.StatusCode!=HttpStatusCode.OK || roster.Content.Headers.ContentType?.MediaType!="application/pdf")throw new InvalidOperationException("Schedule-only roster download");
      rosterHash=DutyDocumentIntegrity.Hash(await roster.Content.ReadAsByteArrayAsync());
      results.Add(new{step="Schedule-only authenticated official roster export",status=200,sha256=rosterHash});
    }
    await Check(await client.GetAsync($"/api/v1/school-duty/plans/roster.pdf?termId={scene.Term}&revision={revision-1}"),HttpStatusCode.Conflict,"Stale roster revision rejected");
    // A non-network provider exercises real SQL protected ledger processing; it is explicitly TestVerified.
    var fake=new FakeGoogleDrive().AddFolder("d3-http-synthetic-root","Isolated synthetic root");
    await using(var db=fixture.Db()) {
      db.SchoolGoogleDrives.Add(new(){SchoolId=scene.School,IsEnabled=true,DutyArchiveEnabled=true,ProtectedCredential="synthetic-not-a-credential",RootFolderId="d3-http-synthetic-root"});await db.SaveChangesAsync();
      var repo=new DutyDocumentRepository(db);var provider=new GoogleStorageProvider(fake);var drive=new DutyArchiveDriveService(repo,new StorageRepository(db),new StorageLibraryRepository(db),provider);
      var processor=new DutyArchiveProcessor(repo,new DutyExecutionRepository(db),new DutyPdfRenderer(),drive,provider,Options.Create(new DutyArchiveOptions{ProcessingEnabled=true}),NullLogger<DutyArchiveProcessor>.Instance);
      await processor.ProcessBatchAsync(default);await processor.ProcessBatchAsync(default);db.ChangeTracker.Clear();
      var intents=await db.Set<DutyArchiveIntent>().Where(x=>x.SchoolId==scene.School).ToListAsync();
      if(intents.Any(x=>x.State!="TestVerified") || fake.Uploads.Count!=2 || await db.ConversationMessages.CountAsync(x=>x.SchoolId==scene.School)!=1 || await db.Set<StudentDismissalLateness>().CountAsync(x=>x.SchoolId==scene.School)!=1)throw new InvalidOperationException("Archive/read caused replay or fake completion");
      var byteEvidence=new List<object>();
      foreach(var i in intents) {
        var stored=await fake.DownloadAsync(scene.School,i.ProviderItemId!);await using var content=stored.Content;using var bytes=new MemoryStream();await content.CopyToAsync(bytes);
        var providerHash=DutyDocumentIntegrity.Hash(bytes.ToArray());var artifact=await db.Set<DutyPdfArtifact>().SingleAsync(x=>x.DocumentId==i.DocumentId);
        var version=await db.StoredFileVersions.SingleAsync(x=>x.Id==i.StoredFileVersionId);
        if(providerHash!=artifact.Hash || version.SHA256!=artifact.Hash || version.SizeInBytes!=bytes.Length)throw new InvalidOperationException("Provider-double/ledger byte mismatch");
        var kind=await db.Set<DutySignedDocument>().Where(x=>x.Id==i.DocumentId).Select(x=>x.Kind).SingleAsync();
        byteEvidence.Add(new{kind,retainedHash=artifact.Hash,providerDoubleHash=providerHash,ledgerHash=version.SHA256,bytes=bytes.Length,state=i.State});
      }
      results.Add(new{step="Actual SQL protected ledger with provider DOUBLE: two uploads, retained hash equality, no duplicate facts/notices/signatures; TestVerified only",status=200,byteEvidence});
    }
    await Pdf(reportId,"Local HTTP PDF unchanged after provider-double ledger completion");
    await Check(await client.GetAsync($"/api/v1/school-duty/documents/{reportId}/pdf?archived=true"),HttpStatusCode.Conflict,"Provider-double result remains unavailable as actual Drive delivery");
    // Keep JWT test tokens outside the repository; browser exercises the actual document HTTP APIs.
    var browserConfig=Path.Combine(Path.GetTempPath(),"AlFalah-D3-Browser-"+Guid.NewGuid().ToString("N")+".json");
    var teacherBrowser=new{role=RoleNames.Instructor,token=client.DefaultRequestHeaders.Authorization!.Parameter,userId=scene.Teacher};
    await Token(scene.Manager,RoleNames.SchoolManager,[DutyPermissions.Manage,DutyPermissions.View]);
    var managerBrowser=new{role=RoleNames.SchoolManager,token=client.DefaultRequestHeaders.Authorization!.Parameter,userId=scene.Manager};
    await File.WriteAllTextAsync(browserConfig,JsonSerializer.Serialize(new{origin,users=new[]{teacherBrowser,managerBrowser},assignment=scene.Assignment,date=scene.Date.ToString("yyyy-MM-dd"),reportId,expectedHash=retainedHash,expectedLetterHash=retainedLetterHash,expectedRosterHash=rosterHash,school=scene.School}));
    var node=new ProcessStartInfo("node"){WorkingDirectory=Path.Combine(workspace,"frontend"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};node.ArgumentList.Add("scripts/check-duty-documents-live.mjs");node.ArgumentList.Add(browserConfig);
    using(var browser=Process.Start(node)!) {var output=browser.StandardOutput.ReadToEndAsync();var error=browser.StandardError.ReadToEndAsync();await browser.WaitForExitAsync();Console.WriteLine(await output);if(browser.ExitCode!=0)throw new InvalidOperationException("Live authenticated browser check failed: "+await error);}
    results.Add(new{step="Actual browser authenticated PDF preview/download against live isolated HTTP/JWT/SQL",status=200});
    await File.WriteAllTextAsync(Path.Combine(workspace,"docs/specs/school-duty/verification/d3-live-http-results.json"),JsonSerializer.Serialize(new{verification="Actual local HTTP/JWT/SQL/hosted internal worker and live browser documents; isolated synthetic guardians only; archive provider DOUBLE, no external delivery",checks=results},new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine($"D3 live internal workflow passed: {results.Count} checks.");
}
finally{if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}await Task.WhenAll(stdout,stderr);}
