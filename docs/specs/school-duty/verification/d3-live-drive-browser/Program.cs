using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Read retained isolated test evidence. No migration, signing, submission or provider write.
var workspace=Path.GetFullPath(args.Single());
var results=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(workspace,"docs/specs/school-duty/verification/d3-live-drive-results.json"))).RootElement;
var catalog=results.GetProperty("isolatedDatabase").GetString()!;
if(!results.GetProperty("liveProvider").GetBoolean()||!catalog.StartsWith("AlFalahDutyTests_D3Drive_",StringComparison.Ordinal))throw new InvalidOperationException("Isolated live evidence required.");
var connection=new SqlConnectionStringBuilder{DataSource="(localdb)\\MSSQLLocalDB",InitialCatalog=catalog,IntegratedSecurity=true,TrustServerCertificate=true}.ConnectionString;
await using var db=new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection).Options);
var school=results.GetProperty("school").GetInt32();var assignment=results.GetProperty("assignment").GetInt32();
if(!await db.Schools.AnyAsync(x=>x.Name=="D1 sentinel")||!(await db.SchoolGoogleDrives.SingleAsync(x=>x.SchoolId==school)).RootFolderId.Equals(results.GetProperty("runRootId").GetString()))throw new InvalidOperationException("Retained isolated SQL/root mismatch.");
var api=Path.Combine(workspace,"backend/AlFalah.Api");var config=new ConfigurationBuilder().SetBasePath(api).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json").Build();
var keys=config["DataProtection:KeysPath"]??"App_Data/DataProtectionKeys";keys=Path.GetFullPath(Path.IsPathRooted(keys)?keys:Path.Combine(api,keys));
const string origin="http://127.0.0.1:5391",secret="D3-Live-Drive-Isolated-Browser-JWT-2026-10-14";
var start=new ProcessStartInfo("dotnet"){WorkingDirectory=api,UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
start.ArgumentList.Add(Path.Combine(api,"bin/Debug/net8.0/AlFalah.Api.dll"));
foreach(var pair in new Dictionary<string,string>{{"ASPNETCORE_ENVIRONMENT","Development"},{"ASPNETCORE_URLS",origin},{"ConnectionStrings__DefaultConnection",connection},{"Database__InitializeOnStartup","false"},{"Jwt__Secret",secret},{"Jwt__Issuer","D3-LiveDrive"},{"Jwt__Audience","D3-LiveDrive"},{"DataProtection__KeysPath",keys},{"Logging__LogLevel__Default","Error"},{"Logging__LogLevel__Microsoft","Error"},{"SchoolDutyArchive__ProcessingEnabled","false"},{"SchoolFileStorage__ArchiveProcessingEnabled","false"}})start.Environment[pair.Key]=pair.Value;
foreach(var flag in new[]{"Enabled","ExternalWritesEnabled","ArchiveExternalWritesEnabled","ArchiveWorkerEnabled","VisitArchiveEnabled","VisitArchiveWorkerEnabled","AdministrationEnabled","ReadModelEnabled"})start.Environment["SchoolFileStorage__"+flag]="false";
using var host=Process.Start(start)??throw new InvalidOperationException("Isolated API did not start");var stdout=host.StandardOutput.ReadToEndAsync();var stderr=host.StandardError.ReadToEndAsync();
try{
 using var http=new HttpClient{BaseAddress=new(origin),Timeout=TimeSpan.FromSeconds(90)};
 for(var attempt=0;attempt<50;attempt++){if(host.HasExited)throw new InvalidOperationException("Isolated host exited");try{await http.GetAsync("/api/v1/school-duty/context");break;}catch(HttpRequestException){await Task.Delay(400);}}
 var jwtConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Jwt:Secret",secret},{"Jwt:Issuer","D3-LiveDrive"},{"Jwt:Audience","D3-LiveDrive"}}).Build();var jwt=new JwtService(jwtConfig);
 var letter=await db.Set<DutySignedDocument>().SingleAsync(x=>x.SchoolId==school&&x.Kind=="Letter");var report=await db.Set<DutySignedDocument>().SingleAsync(x=>x.SchoolId==school&&x.Kind=="Report");
 var users=new List<object>();var checks=new List<object>();
 foreach(var role in new[]{"Instructor","SchoolManager"}){
  var id=role=="Instructor"?letter.AuthorUserId:await db.Schools.Where(x=>x.Id==school).Select(x=>x.ManagerUserId).SingleAsync();var account=await db.Users.SingleAsync(x=>x.Id==id);
  var token=jwt.GenerateAccessToken(id!,account.UserName!,[role],role=="Instructor"?["Duty.View","Timetable.View"]:["Duty.View","Duty.Manage","Timetable.View"],school,"ar",account.SecurityStamp!);
  users.Add(new{role,userId=id,token});http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
  foreach(var doc in new[]{letter,report})foreach(var archived in new[]{false,true}){
   using var response=await http.GetAsync($"/api/v1/school-duty/documents/{doc.Id}/pdf?archived={archived.ToString().ToLowerInvariant()}");response.EnsureSuccessStatusCode();
   var hash=AlFalah.Application.Duty.DutyDocumentIntegrity.Hash(await response.Content.ReadAsByteArrayAsync());var artifact=await db.Set<DutyPdfArtifact>().SingleAsync(x=>x.DocumentId==doc.Id);
   if(hash!=artifact.Hash)throw new InvalidOperationException("Protected HTTP bytes mismatch");checks.Add(new{role,kind=doc.Kind,archived,sha256=hash,status=200});
  }
  foreach(var endpoint in new[]{"archive","dashboard"}){
   using var response=await http.GetAsync("/api/v1/school-duty/"+endpoint);
   var expected=role=="SchoolManager"?200:403;if((int)response.StatusCode!=expected)throw new InvalidOperationException("Administrative read scope mismatch");
   if(expected==200){var payload=JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
    if(endpoint=="archive"&&(payload.GetProperty("pageSize").GetInt32()!=10||!payload.GetProperty("rows").EnumerateArray().Any(x=>x.GetProperty("assignmentId").GetInt32()==assignment)))throw new InvalidOperationException("Actual SQL archive row missing");
    if(endpoint=="dashboard"&&payload.GetProperty("recent").EnumerateArray().Any(x=>DateOnly.Parse(x.GetProperty("date").GetString()!)>=DateOnly.Parse(payload.GetProperty("today").GetString()!)))throw new InvalidOperationException("Dashboard recent range invalid");}
   checks.Add(new{role,kind=endpoint,status=expected});
  }
 }
 var replacement=await db.UserSchoolRoles.Where(x=>x.SchoolId==school&&x.Role.Name=="Instructor"&&x.UserId!=letter.AuthorUserId).Select(x=>x.UserId).SingleAsync();var other=await db.Users.SingleAsync(x=>x.Id==replacement);
 http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",jwt.GenerateAccessToken(other.Id,other.UserName!,["Instructor"],["Duty.View"],school,"ar",other.SecurityStamp!));
 foreach(var archived in new[]{false,true}){using var denial=await http.GetAsync($"/api/v1/school-duty/documents/{letter.Id}/pdf?archived={archived.ToString().ToLowerInvariant()}");if((int)denial.StatusCode!=403)throw new InvalidOperationException("Other teacher document access allowed");checks.Add(new{role="OtherInstructor",kind="Letter",archived,status=403});}
 var teacher=(JsonSerializer.SerializeToElement(users[0])).GetProperty("token").GetString();http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",teacher);
 var plan=await db.Set<DutyPlan>().SingleAsync(x=>x.SchoolId==school);using var roster=await http.GetAsync($"/api/v1/school-duty/plans/roster.pdf?termId={plan.AcademicTermId}&revision={plan.Revision}");roster.EnsureSuccessStatusCode();
 var browserConfig=new{origin,school,assignment,date=(await db.Set<DutyAssignment>().SingleAsync(x=>x.Id==assignment)).Date.ToString("yyyy-MM-dd"),users,
  expectedHash=(await db.Set<DutyPdfArtifact>().SingleAsync(x=>x.DocumentId==report.Id)).Hash,expectedLetterHash=(await db.Set<DutyPdfArtifact>().SingleAsync(x=>x.DocumentId==letter.Id)).Hash,expectedRosterHash=AlFalah.Application.Duty.DutyDocumentIntegrity.Hash(await roster.Content.ReadAsByteArrayAsync()),actualDrive=true};
 var privateFile=Path.Combine(workspace,".audit/duty-organized-20261014/live-drive/browser-private.json");await File.WriteAllTextAsync(privateFile,JsonSerializer.Serialize(browserConfig));
 var browserStart=new ProcessStartInfo("node"){WorkingDirectory=Path.Combine(workspace,"frontend"),UseShellExecute=false,CreateNoWindow=true};browserStart.ArgumentList.Add("scripts/check-duty-documents-live.mjs");browserStart.ArgumentList.Add(privateFile);
 browserStart.Environment["ALFALAH_DUTY_LIVE_EVIDENCE"]=Environment.GetEnvironmentVariable("ALFALAH_DUTY_LIVE_EVIDENCE")??"../docs/specs/school-duty/verification/d3-live-drive-screens";
 using var browser=Process.Start(browserStart)!;await browser.WaitForExitAsync();if(browser.ExitCode!=0)throw new InvalidOperationException("Live browser checks failed.");
 await File.WriteAllTextAsync(Path.Combine(workspace,"docs/specs/school-duty/verification/d3-live-drive-http-results.json"),JsonSerializer.Serialize(new{checks,liveDrive=true,providerWritesDuringReads=0,noMigration=true},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Live protected HTTP checks: {checks.Count}; browser completed.");
}finally{if(!host.HasExited)host.Kill(entireProcessTree:true);await host.WaitForExitAsync();await stdout;var errors=await stderr;if(errors.Length>0)Console.Error.WriteLine("Isolated host diagnostics retained locally; no credentials printed.");}
