using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AlFalah.Application.Duty;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.Duty;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Fresh guarded synthetic SQL only. No operational grants/configuration/provider writes.
var workspace=Path.GetFullPath(args.Single());var fixture=new DutySqlFixture();await fixture.InitializeAsync();
if(string.IsNullOrEmpty(fixture.Connection)||!fixture.SentinelPreserved)throw new InvalidOperationException("Fresh guarded SQL and sentinel required.");
var scene=await new DutyExecutionSqlTests(fixture).Seed();await using var db=fixture.Db();
foreach(var id in new[]{scene.Manager,scene.Replacement}){var account=await db.Users.SingleAsync(x=>x.Id==id);account.SecurityStamp=Guid.NewGuid().ToString("N");}
db.UserRoles.Add(new IdentityUserRole<string>{UserId=scene.Manager,RoleId=RoleNames.SchoolManager});db.UserRoles.Add(new IdentityUserRole<string>{UserId=scene.Replacement,RoleId=RoleNames.Moderator});
db.UserRoles.Add(new IdentityUserRole<string>{UserId=scene.Replacement,RoleId=RoleNames.Instructor});db.UserSchoolRoles.Add(new(){SchoolId=scene.School,UserId=scene.Replacement,RoleId=RoleNames.Moderator});await db.SaveChangesAsync();
const string origin="http://127.0.0.1:5392",secret="Duty-Delegation-Isolated-Synthetic-JWT-2026-10-14";
var api=Path.Combine(workspace,"backend/AlFalah.Api");var start=new ProcessStartInfo("dotnet"){WorkingDirectory=api,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("--roll-forward");start.ArgumentList.Add("Major");start.ArgumentList.Add(Path.Combine(api,"bin/Debug/net8.0/AlFalah.Api.dll"));
foreach(var pair in new Dictionary<string,string>{{"ASPNETCORE_ENVIRONMENT","Development"},{"ASPNETCORE_URLS",origin},{"ConnectionStrings__DefaultConnection",fixture.Connection},{"Database__InitializeOnStartup","false"},{"Jwt__Secret",secret},{"Jwt__Issuer","DutyDelegation"},{"Jwt__Audience","DutyDelegation"},{"SchoolDutyArchive__ProcessingEnabled","false"},{"SchoolFileStorage__ArchiveProcessingEnabled","false"},{"SchoolFileStorage__ExternalWritesEnabled","false"},{"Logging__LogLevel__Default","Error"},{"Logging__LogLevel__Microsoft","Error"}})start.Environment[pair.Key]=pair.Value;
using var host=Process.Start(start)!;var stdout=host.StandardOutput.ReadToEndAsync();var stderr=host.StandardError.ReadToEndAsync();
try{
 using var http=new HttpClient{BaseAddress=new(origin),Timeout=TimeSpan.FromSeconds(30)};
 for(var attempt=0;attempt<60;attempt++){if(host.HasExited)throw new InvalidOperationException("Isolated host exited.");try{await http.GetAsync("/api/v1/school-duty/dashboard");break;}catch(HttpRequestException){await Task.Delay(300);}}
 var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Jwt:Secret",secret},{"Jwt:Issuer","DutyDelegation"},{"Jwt:Audience","DutyDelegation"}}).Build();var jwt=new JwtService(config);var checks=new List<object>();
 async Task Token(string user,string[] roles){var account=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==user);http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",jwt.GenerateAccessToken(user,account.UserName!,roles,[],scene.School,"ar",account.SecurityStamp!));}
 async Task<JsonElement> Get(string path,int expected=200){using var response=await http.GetAsync("/api/v1/"+path);if((int)response.StatusCode!=expected)throw new InvalidOperationException(path+" expected "+expected+" got "+(int)response.StatusCode);checks.Add(new{path,status=expected});return expected==200?JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone():default;}
 async Task Put(string user,bool enabled,DateTimeOffset? version){using var response=await http.PutAsJsonAsync("/api/v1/school-duty/administrators/"+user,new DutyAdministratorChange(enabled,version));response.EnsureSuccessStatusCode();checks.Add(new{path="administrators",enabled,status=200});}
 await Token(scene.Manager,[RoleNames.SchoolManager]);var before=await Get("school-duty/administrators");if(!before.EnumerateArray().Any(x=>x.GetProperty("userId").GetString()==scene.Replacement))throw new InvalidOperationException("Candidate missing");
 await Put(scene.Replacement,true,null);
 await Token(scene.Replacement,[RoleNames.Moderator,RoleNames.Instructor]);var identity=await Get("auth/me");if(!identity.GetProperty("permissions").EnumerateArray().Any(x=>x.GetString()==DutyPermissions.Manage)||identity.GetProperty("permissions").EnumerateArray().Any(x=>x.GetString()=="Storage.ManageSchool"))throw new InvalidOperationException("Identity permission scope invalid");
 var context=await Get("school-duty/context");if(!context.GetProperty("canManage").GetBoolean()||context.GetProperty("canDelegate").GetBoolean())throw new InvalidOperationException("Delegated authority invalid");
 await Get("school-duty/dashboard");await Get("school-duty/archive");await Get("school-duty/archive/enablement");await Get("school-duty/administrators",403);
 var settings=context.GetProperty("settings");using var previewResponse=await http.PostAsJsonAsync("/api/v1/school-duty/plans/preview",new{settings,expectedRevision=context.GetProperty("planRevision").GetInt32()});previewResponse.EnsureSuccessStatusCode();var preview=JsonDocument.Parse(await previewResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
 using var publishResponse=await http.PostAsJsonAsync("/api/v1/school-duty/plans/publish",new{settings,expectedRevision=context.GetProperty("planRevision").GetInt32(),previewDigest=preview.GetProperty("digest").GetString(),requestId=Guid.NewGuid()});publishResponse.EnsureSuccessStatusCode();checks.Add(new{path="plans/preview+publish",status=200});
 using var signing=await http.PostAsJsonAsync($"/api/v1/school-duty/assignments/{scene.Assignment}/acknowledge",new DutySignRequest(1,Guid.NewGuid()));if((int)signing.StatusCode!=403)throw new InvalidOperationException("Delegation must not allow another teacher signature");checks.Add(new{path="other-teacher signing",status=403});
 await Token(scene.Manager,[RoleNames.SchoolManager]);var grants=await Get("school-duty/administrators");var version=grants.EnumerateArray().Single(x=>x.GetProperty("userId").GetString()==scene.Replacement).GetProperty("version").GetDateTimeOffset();await Put(scene.Replacement,false,version);
 await Token(scene.Replacement,[RoleNames.Moderator,RoleNames.Instructor]);var revoked=await Get("auth/me");if(revoked.GetProperty("permissions").EnumerateArray().Any(x=>x.GetString()==DutyPermissions.Manage))throw new InvalidOperationException("Revoked identity retained authority");await Get("school-duty/dashboard",403);await Get("school-duty/archive",403);await Get("school-duty/administrators",403);
 var audits=await db.AuditLogs.CountAsync(x=>x.SchoolId==scene.School&&x.EntityName=="DutyAdministrator");if(audits!=2)throw new InvalidOperationException("Audit count invalid");
 await File.WriteAllTextAsync(Path.Combine(workspace,"docs/specs/school-duty/verification/delegation-http-results.json"),JsonSerializer.Serialize(new{at=DateTimeOffset.UtcNow,isolatedSql=true,freshGuard=true,sentinelPreserved=fixture.SentinelPreserved,checks,audits,providerWrites=0,operationalGrants=0,existingSchema=true},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Actual isolated authenticated HTTP delegation checks passed: {checks.Count}; audit rows {audits}.");
}finally{if(!host.HasExited)host.Kill(entireProcessTree:true);await host.WaitForExitAsync();await stdout;await stderr;}
