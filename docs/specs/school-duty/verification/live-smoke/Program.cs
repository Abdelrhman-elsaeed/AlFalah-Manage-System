using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AlFalah.Application.DTOs.Attendance;
using AlFalah.Application.Duty;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.Duty;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

// This harness accepts ONLY the same isolated LocalDB prefix enforced by the SQL fixture.
// Synthetic signing settings and key directory are process overrides; original settings are untouched.
var fixture=new DutySqlFixture();await fixture.InitializeAsync();var scenario=await fixture.Scenario();
await using(var db=fixture.Db())
{
    var users=await db.Users.Where(x=>x.Id==scenario.Manager||x.Id==scenario.Secretary||scenario.Teachers.Contains(x.Id)).ToListAsync();
    foreach(var user in users)user.SecurityStamp=Guid.NewGuid().ToString();
    foreach(var id in scenario.Teachers.Append(scenario.Secretary))db.UserSignatures.Add(new UserSignature{UserId=id,SignatureDrawnData="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jYwoAAAAASUVORK5CYII="});
    await db.SaveChangesAsync();
}
// Resolve from the command's workspace argument rather than guessing a deployed content root.
var workspace=Path.GetFullPath(args.FirstOrDefault()??throw new InvalidOperationException("Pass workspace root."));
var port=5387;var origin=$"http://127.0.0.1:{port}";
const string secret="AlFalah-D1-Isolated-Smoke-Key-Only-2026-10-13";
var start=new ProcessStartInfo("dotnet"){WorkingDirectory=Path.Combine(workspace,"backend/AlFalah.Api"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
start.ArgumentList.Add(Path.Combine(start.WorkingDirectory,"bin/Debug/net8.0/AlFalah.Api.dll"));
var overrides=new Dictionary<string,string>{{"ASPNETCORE_ENVIRONMENT","Development"},{"ASPNETCORE_URLS",origin},{"ConnectionStrings__DefaultConnection",fixture.Connection},{"Database__InitializeOnStartup","false"},{"Jwt__Secret",secret},{"Jwt__Issuer","D1-Smoke"},{"Jwt__Audience","D1-Smoke"},{"DataProtection__KeysPath",Path.Combine(Path.GetTempPath(),"AlFalah-D1-Isolated-Smoke-Keys")},{"SchoolFileStorage__Enabled","false"},{"SchoolFileStorage__VisitArchiveEnabled","false"},{"SchoolFileStorage__VisitArchiveWorkerEnabled","false"},{"SchoolFileStorage__ExternalWritesEnabled","false"},{"Logging__LogLevel__Default","Error"},{"Logging__LogLevel__Microsoft","Error"}};
foreach(var pair in overrides)start.Environment[pair.Key]=pair.Value;
foreach(var key in new[]{"AdministrationEnabled","ReadModelEnabled","ArchiveWorkerEnabled","ArchiveExternalWritesEnabled"})start.Environment["SchoolFileStorage__"+key]="false";
using var process=Process.Start(start)??throw new InvalidOperationException("API process could not start.");
// Drain streams without printing JWTs, SQL parameters, keys or operational configuration.
var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
using var client=new HttpClient{BaseAddress=new Uri(origin),Timeout=TimeSpan.FromSeconds(60)};
var evidence=new List<object>();
async Task Token(string userId,string role,string[] permissions)
{
    await using var db=fixture.Db();var user=await db.Users.SingleAsync(x=>x.Id==userId);
    var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Jwt:Secret",secret},{"Jwt:Issuer","D1-Smoke"},{"Jwt:Audience","D1-Smoke"}}).Build();
    client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",new JwtService(config).GenerateAccessToken(user.Id,user.UserName!,[role],permissions,scenario.School,"ar",user.SecurityStamp!));
}
async Task<JsonElement> Check(HttpResponseMessage response,HttpStatusCode status,string step)
{
    if(response.StatusCode!=status)throw new InvalidOperationException($"{step}: expected {(int)status}, got {(int)response.StatusCode}. {await response.Content.ReadAsStringAsync()}");
    var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();if(!json.TryGetProperty("isSuccess",out _))throw new InvalidOperationException("ApiResponse missing.");
    evidence.Add(new{step,status=(int)status});return json;
}
try
{
    Console.WriteLine("Starting isolated HTTP host; waiting for readiness.");
    for(var attempt=0;attempt<40;attempt++)
    {
        if(process.HasExited)throw new InvalidOperationException("Isolated host exited: "+await stderr);
        try{await client.GetAsync("/api/v1/school-duty/context");break;}
        catch(Exception ex)when(ex is HttpRequestException or TaskCanceledException){await Task.Delay(500);}
    }
    Console.WriteLine("Isolated HTTP host ready; checking manager and teacher contracts.");
    await Token(scenario.Manager,RoleNames.SchoolManager,[DutyPermissions.Manage,DutyPermissions.View]);
    var manager=await Check(await client.GetAsync($"/api/v1/school-duty/context?termId={scenario.Term}"),HttpStatusCode.OK,"Live manager context");
    if(!manager.GetProperty("data").GetProperty("canManage").GetBoolean())throw new InvalidOperationException("Manager capability missing");
    await Token(scenario.Teachers[0],RoleNames.Instructor,[DutyPermissions.View]);
    var teacher=await Check(await client.GetAsync($"/api/v1/school-duty/context?termId={scenario.Term}"),HttpStatusCode.OK,"Live teacher schedule projection");
    if(teacher.GetProperty("data").GetProperty("history").GetArrayLength()!=0||teacher.GetProperty("data").GetProperty("settings").ValueKind!=JsonValueKind.Null)throw new InvalidOperationException("Confidential manager data exposed");
    await Check(await client.PostAsJsonAsync("/api/v1/school-duty/plans/preview",new DutyPreviewRequest(scenario.Settings,1)),HttpStatusCode.Forbidden,"Teacher management denied");
    await Token(scenario.Manager,RoleNames.SchoolManager,[DutyPermissions.Manage,DutyPermissions.View]);
    var deniedAttendance=new SaveAttendanceSheetRequestDto(scenario.Settings.From,[]);
    await Check(await client.PutAsJsonAsync("/api/v1/attendance/sheet",deniedAttendance),HttpStatusCode.Forbidden,"Manager attendance authorization retained");
    await Token(scenario.Secretary,RoleNames.Secretary,[PermissionNames.AttendanceManage,PermissionNames.AttendanceView]);
    var attendance=new SaveAttendanceSheetRequestDto(scenario.Settings.From,scenario.Teachers.Append(scenario.Manager).Select(id=>new SaveAttendanceEntryDto(id,id==scenario.Teachers[0]?AttendanceStatus.Absent:AttendanceStatus.Present,null)).ToArray());
    await Check(await client.PutAsJsonAsync("/api/v1/attendance/sheet",attendance),HttpStatusCode.OK,"Secretary live attendance save");
    // Observe the hosted worker, without any duty GET causing reconciliation.
    var exchanged=false;
    for(var attempt=0;attempt<30;attempt++){await Task.Delay(1000);await using var db=fixture.Db();exchanged=await db.Set<DutyAssignment>().AnyAsync(x=>x.SchoolId==scenario.School&&x.Date==scenario.Settings.From&&x.EffectiveTeacherId==scenario.Teachers[1]);if(exchanged)break;}
    if(!exchanged)throw new InvalidOperationException("Hosted reconciliation did not process durable attendance signal");
    evidence.Add(new{step="Hosted worker exchanges after attendance save without duty page visits",status=200});
    await Token(scenario.Manager,RoleNames.SchoolManager,[DutyPermissions.Manage,DutyPermissions.View]);
    var response=await Check(await client.GetAsync($"/api/v1/school-duty/context?termId={scenario.Term}"),HttpStatusCode.OK,"Live post-reconciliation context");
    var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);var current=response.GetProperty("data").Deserialize<DutyWorkspace>(options)!;
    var change=new DutyChangeRequest(current.Days[0].Id,current.PlanRevision,scenario.Teachers[2],current.Days[2].Id,"Live smoke exchange","Exchange",true,null,Guid.NewGuid());
    await Check(await client.PostAsJsonAsync("/api/v1/school-duty/assignments/change-preview",change),HttpStatusCode.OK,"Live two-date manual review");
    await Check(await client.PostAsJsonAsync("/api/v1/school-duty/assignments/change",change),HttpStatusCode.OK,"Live manual exchange");
    await Check(await client.PostAsJsonAsync("/api/v1/school-duty/assignments/change",change),HttpStatusCode.OK,"Live idempotent retry");
    var conflict=await Check(await client.PostAsJsonAsync("/api/v1/school-duty/assignments/change",change with{RequestId=Guid.NewGuid()}),HttpStatusCode.Conflict,"Stale live revision rejected");
    if(conflict.GetProperty("errorCode").GetString()!="DutyRevisionConflict")throw new InvalidOperationException("Machine error code missing");
    var output=Path.Combine(workspace,"docs/specs/school-duty/verification/live-http-results.json");await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new{verification="Real local HTTP, JWT middleware, SQL Server and hosted worker; synthetic isolated school",checks=evidence},new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine($"Live HTTP/SQL/worker smoke passed: {evidence.Count} checks. Original settings, credentials, keys and database untouched.");
}
finally
{
    if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}
    await Task.WhenAll(stdout,stderr);
}
