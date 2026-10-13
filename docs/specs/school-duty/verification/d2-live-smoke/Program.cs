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
using AlFalah.Tests.Duty;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Fresh guarded LocalDB only. All people, students and guardian links are synthetic.
// No configuration file, operational database, credential, key or external provider is changed.
var fixture=new DutySqlFixture();await fixture.InitializeAsync();
var scene=await new DutyExecutionSqlTests(fixture).Seed();
var officer="d2-officer-"+Guid.NewGuid().ToString("N");
await using(var db=fixture.Db())
{
    foreach(var user in await db.Users.Where(x=>x.Id==scene.Manager||x.Id==scene.Teacher||x.Id==scene.Guardian||x.Id==scene.Replacement).ToListAsync())user.SecurityStamp=Guid.NewGuid().ToString();
    db.UserSignatures.Add(new(){UserId=scene.Guardian,SignatureDrawnData="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jYwoAAAAASUVORK5CYII="});
    if(!await db.Roles.AnyAsync(x=>x.Name==RoleNames.StudentAffairsOfficer))db.Roles.Add(new(){Id=RoleNames.StudentAffairsOfficer,Name=RoleNames.StudentAffairsOfficer,NormalizedName=RoleNames.StudentAffairsOfficer.ToUpperInvariant()});
    db.Users.Add(new(){Id=officer,UserName=officer,FirstName="Synthetic",LastName="Officer",SecurityStamp=Guid.NewGuid().ToString()});
    db.UserSchoolRoles.Add(new(){SchoolId=scene.School,UserId=officer,RoleId=RoleNames.StudentAffairsOfficer});
    db.UserSignatures.Add(new(){UserId=officer,SignatureDrawnData="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jYwoAAAAASUVORK5CYII="});
    await db.SaveChangesAsync();
}
var workspace=Path.GetFullPath(args.FirstOrDefault()??throw new InvalidOperationException("Pass workspace root."));
const string origin="http://127.0.0.1:5388";const string secret="AlFalah-D2-Isolated-Synthetic-Smoke-Key-2026-10-13";
var start=new ProcessStartInfo("dotnet"){WorkingDirectory=Path.Combine(workspace,"backend/AlFalah.Api"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
start.ArgumentList.Add(Path.Combine(start.WorkingDirectory,"bin/Debug/net8.0/AlFalah.Api.dll"));
foreach(var pair in new Dictionary<string,string>{{"ASPNETCORE_ENVIRONMENT","Development"},{"ASPNETCORE_URLS",origin},{"ConnectionStrings__DefaultConnection",fixture.Connection},{"Database__InitializeOnStartup","false"},{"Jwt__Secret",secret},{"Jwt__Issuer","D2-Smoke"},{"Jwt__Audience","D2-Smoke"},{"DataProtection__KeysPath",Path.Combine(Path.GetTempPath(),"AlFalah-D2-Isolated-Smoke-Keys")},{"Logging__LogLevel__Default","Error"},{"Logging__LogLevel__Microsoft","Error"}})start.Environment[pair.Key]=pair.Value;
foreach(var key in new[]{"Enabled","VisitArchiveEnabled","VisitArchiveWorkerEnabled","ExternalWritesEnabled","AdministrationEnabled","ReadModelEnabled","ArchiveWorkerEnabled","ArchiveExternalWritesEnabled"})start.Environment["SchoolFileStorage__"+key]="false";
using var process=Process.Start(start)??throw new InvalidOperationException("Host could not start.");var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
using var client=new HttpClient{BaseAddress=new(origin),Timeout=TimeSpan.FromSeconds(30)};var results=new List<object>();
var jsonOptions=new JsonSerializerOptions(JsonSerializerDefaults.Web);
async Task Token(string id,string role,string[] permissions)
{
    await using var db=fixture.Db();var user=await db.Users.SingleAsync(x=>x.Id==id);
    var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Jwt:Secret",secret},{"Jwt:Issuer","D2-Smoke"},{"Jwt:Audience","D2-Smoke"}}).Build();
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
    Console.WriteLine("Synthetic D2 HTTP host ready.");
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
    await File.WriteAllTextAsync(Path.Combine(workspace,"docs/specs/school-duty/verification/d2-live-http-results.json"),JsonSerializer.Serialize(new{verification="Actual local HTTP/JWT/SQL/hosted worker; isolated synthetic guardians only; no external providers",checks=results},new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine($"D2 live internal workflow passed: {results.Count} checks.");
}
finally{if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}await Task.WhenAll(stdout,stderr);}
