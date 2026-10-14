using System.Text.Json;
using AlFalah.Application.Duty;
using AlFalah.Application.DTOs.Attendance;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Duty;
public sealed class DutySqlFactAttribute : FactAttribute
{
    public DutySqlFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALFALAH_DUTY_TEST_CONNECTION"))) Skip="Requires explicitly isolated AlFalahDutyTests_* LocalDB."; }
}
[CollectionDefinition("Duty SQL",DisableParallelization=true)] public sealed class DutySqlCollection : ICollectionFixture<DutySqlFixture>;
public sealed class DutySqlFixture : IAsyncLifetime
{
    public string Connection { get; private set; }="";
    public bool SentinelPreserved { get; private set; }
    public AlFalahDbContext Db() => new(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(Connection).Options);
    public async Task InitializeAsync()
    {
        var value=Environment.GetEnvironmentVariable("ALFALAH_DUTY_TEST_CONNECTION");if(value is null)return;
        var b=new SqlConnectionStringBuilder(value);
        if(!b.DataSource.StartsWith("(localdb)\\",StringComparison.OrdinalIgnoreCase)||!b.InitialCatalog.StartsWith("AlFalahDutyTests_",StringComparison.Ordinal)||!b.IntegratedSecurity)throw new InvalidOperationException("Isolated LocalDB required.");
        Connection=b.ConnectionString;
        var masterBuilder=new SqlConnectionStringBuilder(Connection){InitialCatalog="master"};
        await using(var master=new SqlConnection(masterBuilder.ConnectionString))
        {
            await master.OpenAsync();
            await using var check=master.CreateCommand();
            check.CommandText="SELECT COUNT(*) FROM sys.databases WHERE name=@name";
            check.Parameters.AddWithValue("@name",b.InitialCatalog);
            if(Convert.ToInt32(await check.ExecuteScalarAsync())!=0)throw new InvalidOperationException("Choose a fresh isolated database; tests do not reset existing databases.");
        }
        await using var db=Db();
        await db.GetService<IMigrator>().MigrateAsync("20261013071807_AddTimetableChangeAuditDetails");
        if(await db.Schools.AnyAsync())throw new InvalidOperationException("Choose a fresh isolated database; tests do not reset existing databases.");
        db.Schools.Add(new School{Name="D1 sentinel",City="Sentinel",Stage=SchoolStage.Primary});await db.SaveChangesAsync();
        var school=await db.Schools.SingleAsync();
        // Seed the pre-D1 schema without using a current model that includes future additive columns.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchoolGoogleDrives (SchoolId,CredentialType,SchoolGoogleEmail,ProtectedCredential,RootFolderId,RootFolderDisplayName,IsEnabled,ConnectedAtUtc,UpdatedAtUtc) VALUES ({school.Id},1,'','ciphertext-sentinel','root-sentinel','',1,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET())");
        await db.Database.MigrateAsync();await db.Database.MigrateAsync();db.ChangeTracker.Clear();
        SentinelPreserved=await db.Schools.AnyAsync(x=>x.Id==school.Id&&x.Name=="D1 sentinel")&&await db.SchoolGoogleDrives.AnyAsync(x=>x.SchoolId==school.Id&&x.ProtectedCredential=="ciphertext-sentinel"&&x.RootFolderId=="root-sentinel");
        foreach(var role in new[]{RoleNames.SchoolManager,RoleNames.Instructor,RoleNames.Secretary,RoleNames.Moderator})db.Roles.Add(new ApplicationRole{Id=role,Name=role,NormalizedName=role.ToUpperInvariant()});
        await db.SaveChangesAsync();
        foreach(var p in await db.Permissions.Where(x=>x.Group=="Duty").ToListAsync())
        {
            db.RolePermissions.Add(new RolePermission{RoleId=RoleNames.SchoolManager,PermissionId=p.Id});
            if(p.Name==DutyPermissions.View)db.RolePermissions.Add(new RolePermission{RoleId=RoleNames.Instructor,PermissionId=p.Id});
        }
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync()=>Task.CompletedTask; // Keep evidence; never drop/reset implicitly.
    public async Task<DutyScenario> Scenario(int days=5)
    {
        await using var db=Db();var tag=Guid.NewGuid().ToString("N");var ids=new[]{"A","B","C"}.Select(x=>tag+x).ToArray();var manager=tag+"M";var secretary=tag+"S";
        foreach(var id in ids.Append(manager).Append(secretary))db.Users.Add(new ApplicationUser{Id=id,UserName=id,FirstName=id[^1].ToString(),LastName="Duty"});
        var school=new School{Name="Duty "+tag,City="Test",Stage=SchoolStage.Primary,ManagerUserId=manager};db.Add(school);
        var year=new AcademicYear{Code=tag,NameAr="2030",StartsOn=new(2030,1,1),EndsOn=new(2030,12,31),IsActive=true};db.Add(year);await db.SaveChangesAsync();
        var term=new AcademicTerm{SchoolId=school.Id,AcademicYearId=year.Id,Semester=TimetableSemester.First,StartsOn=new(2030,1,6),EndsOn=new(2030,2,6),IsActive=true,CreatedByUserId=manager,UpdatedByUserId=manager};db.Add(term);
        foreach(var id in ids){db.InstructorProfiles.Add(new InstructorProfile{SchoolId=school.Id,UserId=id});db.UserSchoolRoles.Add(new UserSchoolRole{SchoolId=school.Id,UserId=id,RoleId=RoleNames.Instructor});}
        db.UserSchoolRoles.AddRange(new UserSchoolRole{SchoolId=school.Id,UserId=manager,RoleId=RoleNames.SchoolManager},new UserSchoolRole{SchoolId=school.Id,UserId=secretary,RoleId=RoleNames.Secretary});await db.SaveChangesAsync();
        var template=new BellScheduleTemplate{SchoolId=school.Id,AcademicYearId=year.Id,Semester=TimetableSemester.First,Name="Synthetic duty calendar",CreatedByUserId=manager,UpdatedByUserId=manager};db.Add(template);await db.SaveChangesAsync();
        db.Add(new BellScheduleRevision{SchoolId=school.Id,BellScheduleTemplateId=template.Id,Revision=1,Name="Synthetic calendar",SchoolTimeZoneId="Africa/Cairo",CreatedByUserId=manager,
            Days=Enumerable.Range(1,7).Select(day=>new BellScheduleDay{Day=day,IsStudyDay=true}).ToList()});
        db.Add(new TimetableSetupProfile{SchoolId=school.Id,AcademicYearId=year.Id,Semester=TimetableSemester.First,Name="Synthetic duty setup",BellScheduleTemplateId=template.Id,CreatedByUserId=manager,UpdatedByUserId=manager});await db.SaveChangesAsync();
        var scenario=new DutyScenario(this,school.Id,term.Id,manager,secretary,ids,DutyTests.Settings(term.Id,ids,days));
        await using var publishDb=Db();var service=scenario.Service(publishDb);var preview=await service.PreviewAsync(new(scenario.Settings,0),default);
        await service.PublishAsync(new(scenario.Settings,0,preview.Digest,Guid.NewGuid()),default);return scenario;
    }
}
public sealed record DutyScenario(DutySqlFixture Fixture,int School,int Term,string Manager,string Secretary,string[] Teachers,DutySettings Settings)
{
    public DutyService Service(AlFalahDbContext db,string? user=null,int? school=null,string role=RoleNames.SchoolManager)=>new(new DutyRepository(db),new TeacherDriveHarness.TestCurrentUser(role,user??Manager,school??School,true));
    public async Task Absence(string teacher,DateOnly date,AttendanceStatus status=AttendanceStatus.Absent)
    {
        await using var db=Fixture.Db();var old=await db.AttendanceRecords.SingleOrDefaultAsync(x=>x.SchoolId==School&&x.UserId==teacher&&x.AttendanceDate==date);
        if(old is null)db.Add(new AttendanceRecord{SchoolId=School,UserId=teacher,AttendanceDate=date,Status=status,RecordedByUserId=Secretary});
        else{old.Status=status;old.UpdatedAt=DateTimeOffset.UtcNow;}
        db.Add(new DutyReconciliationOperation{SchoolId=School,Date=date});await db.SaveChangesAsync();
    }
    public async Task Process(DateOnly date)
    {
        await using var db=Fixture.Db();var op=await db.Set<DutyReconciliationOperation>().Where(x=>x.SchoolId==School&&x.Date==date&&x.CompletedAt==null).OrderByDescending(x=>x.Id).FirstAsync();
        var p=new DutyReconciler(new DutyRepository(db));await p.ProcessAsync(op,default);await p.ProcessAsync(op,default);
    }
}
[Collection("Duty SQL")]
public sealed class DutySqlTests(DutySqlFixture fixture)
{
    private static async Task<int> Calendar(AlFalahDbContext db, DutyScenario s, params int[] days)
    {
        var setup=await db.TimetableSetupProfiles.SingleAsync(x=>x.SchoolId==s.School);
        var template=await db.Set<BellScheduleTemplate>().SingleAsync(x=>x.Id==setup.BellScheduleTemplateId);
        template.Revision++;
        var revision=new BellScheduleRevision{SchoolId=s.School,BellScheduleTemplateId=template.Id,Revision=template.Revision,Name="Revised synthetic calendar",SchoolTimeZoneId="Africa/Cairo",CreatedByUserId=s.Manager,
            Days=Enumerable.Range(1,7).Select(day=>new BellScheduleDay{Day=day,IsStudyDay=days.Contains((day+5)%7)}).ToList()};
        db.Add(revision);await db.SaveChangesAsync();return revision.Id;
    }
    [DutySqlFact] public async Task All_teachers_are_default_and_server_uses_study_days_instead_of_request_weekdays()
    {
        var s=await fixture.Scenario(20);await using var db=fixture.Db();var revision=await Calendar(db,s,0,1,2,3,4);
        var service=s.Service(db);var w=await service.WorkspaceAsync(s.Term,default);w.SuggestedWeekdays.Should().Equal(0,1,2,3,4);
        var settings=s.Settings with{TeacherIds=[],PermanentlyExcludedTeacherIds=[],Weekdays=[5,6],AcceptAttendanceCalendarMismatch=false};
        var p=await service.PreviewAsync(new(settings,w.PlanRevision),default);
        p.Days.Where(x=>x.TeacherId.Length>0).Should().OnlyContain(x=>(int)x.Date.DayOfWeek<=4);
        p.Fairness.Select(x=>x.UserId).Should().BeEquivalentTo(s.Teachers);
        await service.PublishAsync(new(settings,w.PlanRevision,p.Digest,Guid.NewGuid()),default);
        var current=await service.WorkspaceAsync(s.Term,default);current.Settings!.TeacherIds.Should().BeEquivalentTo(s.Teachers);
        current.Settings.Weekdays.Should().Equal(0,1,2,3,4);current.Settings.PermanentlyExcludedTeacherIds.Should().BeEmpty();
        (await db.Set<DutyPlan>().SingleAsync(x=>x.SchoolId==s.School)).BellScheduleRevisionId.Should().Be(revision);
    }
    [DutySqlFact] public async Task Explicit_exclusions_include_unmentioned_active_teachers_and_restore_without_losing_dated_exclusion()
    {
        var s=await fixture.Scenario(10);await using var db=fixture.Db();var service=s.Service(db);var w=await service.WorkspaceAsync(s.Term,default);
        var settings=s.Settings with{TeacherIds=[s.Teachers[0]],PermanentlyExcludedTeacherIds=[s.Teachers[1]],Exclusions=[new(s.Teachers[1],s.Settings.From,s.Settings.From,"Temporary retained reason")]};
        var p=await service.PreviewAsync(new(settings,w.PlanRevision),default);p.Fairness.Select(x=>x.UserId).Should().BeEquivalentTo(s.Teachers[0],s.Teachers[2]);
        await service.PublishAsync(new(settings,w.PlanRevision,p.Digest,Guid.NewGuid()),default);
        w=await service.WorkspaceAsync(s.Term,default);w.Settings!.PermanentlyExcludedTeacherIds.Should().Equal(s.Teachers[1]);
        var restored=w.Settings with{PermanentlyExcludedTeacherIds=[]};p=await service.PreviewAsync(new(restored,w.PlanRevision),default);
        p.Fairness.Select(x=>x.UserId).Should().BeEquivalentTo(s.Teachers);p.Days.First().TeacherId.Should().NotBe(s.Teachers[1]);
        await service.PublishAsync(new(restored,w.PlanRevision,p.Digest,Guid.NewGuid()),default);
        (await service.WorkspaceAsync(s.Term,default)).Settings!.Exclusions.Should().ContainSingle();
        (await db.Set<DutyAssignmentChange>().CountAsync(x=>x.SchoolId==s.School&&x.Kind=="Publish")).Should().Be(3);
    }
    [DutySqlFact] public async Task Changing_timing_revision_after_preview_is_conflict_even_with_identical_days()
    {
        var s=await fixture.Scenario();await using var db=fixture.Db();var service=s.Service(db);var w=await service.WorkspaceAsync(s.Term,default);
        var p=await service.PreviewAsync(new(s.Settings,w.PlanRevision),default);await Calendar(db,s,0,1,2,3,4,5,6);
        await FluentActions.Awaiting(()=>service.PublishAsync(new(s.Settings,w.PlanRevision,p.Digest,Guid.NewGuid()),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==409);
        (await db.Set<DutyPlan>().SingleAsync(x=>x.SchoolId==s.School)).Revision.Should().Be(w.PlanRevision);
    }
    [DutySqlFact] public async Task Missing_timetable_calendar_requires_setup_and_cannot_mutate_existing_plan()
    {
        var s=await fixture.Scenario();await using var db=fixture.Db();var setup=await db.TimetableSetupProfiles.SingleAsync(x=>x.SchoolId==s.School);setup.BellScheduleTemplateId=null;await db.SaveChangesAsync();
        var service=s.Service(db);var w=await service.WorkspaceAsync(s.Term,default);w.SuggestedWeekdays.Should().BeEmpty();
        await FluentActions.Awaiting(()=>service.PreviewAsync(new(s.Settings,w.PlanRevision),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyStudyCalendarRequired");
        (await db.Set<DutyAssignment>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(5);
    }
    [DutySqlFact] public async Task Foreign_duplicate_and_all_teacher_permanent_exclusions_are_denied()
    {
        var s=await fixture.Scenario();var other=await fixture.Scenario();await using var db=fixture.Db();var service=s.Service(db);var w=await service.WorkspaceAsync(s.Term,default);
        foreach(var excluded in new[]{new[]{other.Teachers[0]},new[]{s.Manager},new[]{s.Teachers[0],s.Teachers[0]},s.Teachers})
            await FluentActions.Awaiting(()=>service.PreviewAsync(new(s.Settings with{PermanentlyExcludedTeacherIds=excluded},w.PlanRevision),default)).Should().ThrowAsync<DutyException>();
        (await db.Set<DutyPlan>().SingleAsync(x=>x.SchoolId==s.School)).Revision.Should().Be(w.PlanRevision);
    }
    [DutySqlFact] public async Task Published_timetable_calendar_precedes_setup_and_ignores_clones_and_invalid_timing()
    {
        var s=await fixture.Scenario();await using var db=fixture.Db();var term=await db.AcademicTerms.SingleAsync(x=>x.Id==s.Term);
        var original=(await db.Set<DutyPlan>().SingleAsync(x=>x.SchoolId==s.School)).BellScheduleRevisionId;
        var revised=await Calendar(db,s,0,2,4);
        SchoolTimetable Timetable(int revision,bool clone=false,bool invalid=false)=>new(){SchoolId=s.School,AcademicYearId=term.AcademicYearId,Semester=term.Semester,Title="Synthetic timetable",BellScheduleRevisionId=revision,IsPublished=true,PublishedAt=DateTimeOffset.UtcNow,CreatedByUserId=s.Manager,UpdatedByUserId=s.Manager,PublishedByUserId=s.Manager,IsClone=clone,TimingsRequireRevalidation=invalid};
        db.Add(Timetable(original!.Value));await db.SaveChangesAsync();
        var invalid=Timetable(revised,invalid:true);invalid.TimetableSetupProfileId=(await db.TimetableSetupProfiles.SingleAsync(x=>x.SchoolId==s.School)).Id;
        db.Add(Timetable(revised,clone:true));db.Add(invalid);await db.SaveChangesAsync();
        var timing=await new DutyRepository(db).TimingAsync(s.School,new(s.Term,term.AcademicYearId,"2030",(int)term.Semester,term.StartsOn,term.EndsOn),default);
        timing.RevisionId.Should().Be(original);timing.Weekdays.Should().Equal(0,1,2,3,4,5,6);
    }
    [DutySqlFact] public async Task Existing_database_is_rejected_before_any_migration_or_data_change()
    {
        var scenario=await fixture.Scenario();
        await using var db=fixture.Db();
        var migrations=(await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var assignments=await db.Set<DutyAssignment>().CountAsync();
        var history=await db.Set<DutyAssignmentChange>().CountAsync();
        var retry=new DutySqlFixture();
        await FluentActions.Invoking(()=>retry.InitializeAsync()).Should().ThrowAsync<InvalidOperationException>().WithMessage("Choose a fresh isolated database*");
        (await db.Database.GetAppliedMigrationsAsync()).Should().Equal(migrations);
        (await db.Set<DutyAssignment>().CountAsync()).Should().Be(assignments);
        (await db.Set<DutyAssignmentChange>().CountAsync()).Should().Be(history);
        (await db.Set<DutyAssignment>().CountAsync(x=>x.SchoolId==scenario.School)).Should().Be(5);
    }
    [DutySqlFact] public async Task Additive_migration_twice_preserves_sentinel_credentials_and_catalog()
    {fixture.SentinelPreserved.Should().BeTrue();await using var db=fixture.Db();(await db.Permissions.CountAsync(x=>x.Group=="Duty")).Should().Be(2);}
    [DutySqlFact] public async Task Repeated_absence_and_correction_preserve_originals_and_do_not_duplicate_notifications()
    {
        var s=await fixture.Scenario();await s.Absence(s.Teachers[0],s.Settings.From);await s.Process(s.Settings.From);
        await using(var db=fixture.Db())
        {
            var days=await db.Set<DutyAssignment>().Where(x=>x.SchoolId==s.School).OrderBy(x=>x.Date).ToListAsync();days.Take(3).Select(x=>x.EffectiveTeacherId).Should().Equal(s.Teachers[1],s.Teachers[0],s.Teachers[2]);
            (await db.Set<DutyAssignmentChange>().CountAsync(x=>x.SchoolId==s.School&&x.Kind=="AutoExchange")).Should().Be(1);
            (await db.Notifications.CountAsync(x=>x.SchoolId==s.School&&x.DeduplicationKey!.StartsWith("duty:"+days[0].Id+":"))).Should().Be(2);
        }
        await s.Absence(s.Teachers[0],s.Settings.From.AddDays(1));await s.Process(s.Settings.From.AddDays(1));
        await using(var db=fixture.Db())
        {
            var days=await db.Set<DutyAssignment>().Where(x=>x.SchoolId==s.School).OrderBy(x=>x.Date).ToListAsync();days.Take(3).Select(x=>x.EffectiveTeacherId).Should().Equal(s.Teachers[1],s.Teachers[2],s.Teachers[0]);
            var w=await s.Service(db).WorkspaceAsync(s.Term,default);w.Fairness.Single(x=>x.UserId==s.Teachers[0]).PendingCompensation.Should().Be(1);
        }
        await s.Absence(s.Teachers[0],s.Settings.From,AttendanceStatus.Present);await s.Process(s.Settings.From);
        await using(var db=fixture.Db()) (await db.Set<DutyAssignment>().SingleAsync(x=>x.SchoolId==s.School&&x.Date==s.Settings.From)).ExceptionCode.Should().Be("AttendanceCorrectionReview");
    }
    [DutySqlFact] public async Task Exact_correction_undo_is_atomic_and_idempotent()
    {
        var s=await fixture.Scenario();await s.Absence(s.Teachers[0],s.Settings.From);await s.Process(s.Settings.From);
        await s.Absence(s.Teachers[0],s.Settings.From,AttendanceStatus.Present);await s.Process(s.Settings.From);
        await using var db=fixture.Db();(await db.Set<DutyAssignment>().Where(x=>x.SchoolId==s.School).OrderBy(x=>x.Date).Select(x=>x.EffectiveTeacherId).Take(3).ToListAsync()).Should().Equal(s.Teachers);
        (await db.Set<DutyAssignmentChange>().CountAsync(x=>x.SchoolId==s.School&&x.Kind=="Undo")).Should().Be(1);
    }
    [DutySqlFact] public async Task Secretary_save_persists_signal_with_facts_and_same_sheet_is_a_noop()
    {
        var s=await fixture.Scenario();await using var db=fixture.Db();var user=new TeacherDriveHarness.TestCurrentUser(RoleNames.Secretary,s.Secretary,s.School,true);
        var service=new AttendanceService(db,user,new SchoolScopeGuard(db,user,NullLogger<SchoolScopeGuard>.Instance));
        var request=new SaveAttendanceSheetRequestDto(s.Settings.From,s.Teachers.Append(s.Manager).Select(id=>new SaveAttendanceEntryDto(id,id==s.Teachers[0]?AttendanceStatus.Absent:AttendanceStatus.Present,null)).ToArray());
        var count=await db.Set<DutyReconciliationOperation>().CountAsync(x=>x.SchoolId==s.School);await service.SaveSheetAsync(request);await service.SaveSheetAsync(request);
        (await db.Set<DutyReconciliationOperation>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(count+1);
        (await db.AttendanceRecords.CountAsync(x=>x.SchoolId==s.School)).Should().Be(4);
        var corrected=request with{Entries=request.Entries.Select(x=>x with{Status=AttendanceStatus.Present}).ToArray()};await service.SaveSheetAsync(corrected);
        (await db.Set<DutyReconciliationOperation>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(count+2);
        var manager=new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,s.Manager,s.School,true);
        var denied=new AttendanceService(db,manager,new SchoolScopeGuard(db,manager,NullLogger<SchoolScopeGuard>.Instance));await FluentActions.Awaiting(()=>denied.SaveSheetAsync(request)).Should().ThrowAsync<UnauthorizedAccessException>();
    }
    [DutySqlFact] public async Task Manual_pair_is_protected_revision_checked_and_retry_returns_same_receipt()
    {
        var s=await fixture.Scenario();await using var db=fixture.Db();var service=s.Service(db);var w=await service.WorkspaceAsync(s.Term,default);
        var request=new DutyChangeRequest(w.Days[0].Id,w.PlanRevision,s.Teachers[1],w.Days[1].Id,"Emergency exchange","Exchange",false,null,Guid.NewGuid());
        await FluentActions.Awaiting(()=>service.ChangeAsync(request with{Mode="Replace"},default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyInvalidChange");
        await FluentActions.Awaiting(()=>service.ChangeAsync(request with{Mode="Unprotect",OtherAssignmentId=null,ResolveChangeId=42},default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyInvalidChange");
        var preview=await service.ChangePreviewAsync(request,default);preview.Days.Should().HaveCount(2);var result=await service.ChangeAsync(request,default);
        (await service.ChangeAsync(request,default)).Should().Be(result);
        await FluentActions.Awaiting(()=>service.ChangeAsync(request with{Reason="Different"},default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==409);
        await s.Absence(s.Teachers[1],s.Settings.From);await s.Process(s.Settings.From);
        var current=await service.WorkspaceAsync(s.Term,default);current.Days[0].TeacherId.Should().Be(s.Teachers[1]);current.Days[0].Protected.Should().BeTrue();current.Days[0].ExceptionCode.Should().Be("ManualAbsenceReview");
    }
    [DutySqlFact] public async Task Teacher_confidentiality_actual_manager_and_school_isolation_are_enforced_live()
    {
        var s=await fixture.Scenario();var other=await fixture.Scenario();await using var db=fixture.Db();
        var teacher=s.Service(db,s.Teachers[0],role:RoleNames.Instructor);var w=await teacher.WorkspaceAsync(s.Term,default);
        w.CanManage.Should().BeFalse();w.Settings.Should().BeNull();w.History.Should().BeEmpty();w.Fairness.Should().BeEmpty();w.Teachers.Should().BeEmpty();w.Days.Should().OnlyContain(x=>x.Attendance==null&&x.ExceptionCode==null);
        foreach(var id in s.Teachers)await s.Absence(id,s.Settings.From);await s.Process(s.Settings.From);
        var privateException=await teacher.WorkspaceAsync(s.Term,default);privateException.Days.Should().OnlyContain(x=>x.Attendance==null&&x.ExceptionCode==null&&x.ChangeKind!="NoEligibleSubstitute");
        await FluentActions.Awaiting(()=>teacher.PreviewAsync(new(s.Settings,1),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
        await FluentActions.Awaiting(()=>s.Service(db).WorkspaceAsync(other.Term,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==404);
        await FluentActions.Awaiting(()=>s.Service(db,s.Manager,other.School).WorkspaceAsync(other.Term,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
        var school=await db.Schools.SingleAsync(x=>x.Id==s.School);school.ManagerUserId=null;await db.SaveChangesAsync();
        await FluentActions.Awaiting(()=>s.Service(db).WorkspaceAsync(s.Term,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
    }
    [DutySqlFact] public async Task Failed_mid_exchange_rolls_back_both_rows_and_history_and_retry_remains_durable()
    {
        var s=await fixture.Scenario();await using var db=fixture.Db();var repo=new DutyRepository(db);
        await FluentActions.Awaiting(()=>repo.AtomicAsync(s.School,async()=>
        {
            var state=await repo.LoadAsync(s.School,default);state.Assignments[0].EffectiveTeacherId=s.Teachers[1];await repo.SaveAsync(default);
            if(state.Assignments.Count>0)throw new InvalidOperationException("Injected failure after first SQL write");return true;
        },default)).Should().ThrowAsync<InvalidOperationException>();
        var rows=await db.Set<DutyAssignment>().Where(x=>x.SchoolId==s.School).OrderBy(x=>x.Date).ToListAsync();rows.Take(3).Select(x=>x.EffectiveTeacherId).Should().Equal(s.Teachers);
        var op=await db.Set<DutyReconciliationOperation>().FirstAsync(x=>x.SchoolId==s.School);await new DutyReconciler(repo).FailedAsync(op,default);
        var persisted=await db.Set<DutyReconciliationOperation>().AsNoTracking().SingleAsync(x=>x.Id==op.Id);persisted.CompletedAt.Should().BeNull();persisted.Attempts.Should().Be(1);persisted.NextAttemptAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }
    [DutySqlFact] public async Task Simultaneous_manual_and_automatic_changes_commit_one_valid_pair()
    {
        var s=await fixture.Scenario();await s.Absence(s.Teachers[0],s.Settings.From);
        await using var read=fixture.Db();var w=await s.Service(read).WorkspaceAsync(s.Term,default);
        var request=new DutyChangeRequest(w.Days[0].Id,w.PlanRevision,s.Teachers[2],w.Days[2].Id,"Race exchange","Exchange",true,null,Guid.NewGuid());
        var manual=Task.Run(async()=>{await using var db=fixture.Db();try{await s.Service(db).ChangeAsync(request,default);return true;}catch(DutyException e)when(e.Status==409){return false;}});
        var automatic=Task.Run(()=>s.Process(s.Settings.From));await Task.WhenAll(manual,automatic);
        await using var verify=fixture.Db();var rows=await verify.Set<DutyAssignment>().Where(x=>x.SchoolId==s.School).OrderBy(x=>x.Date).ToListAsync();
        rows.Take(3).Select(x=>x.EffectiveTeacherId).Should().Equal(await manual ? new[]{s.Teachers[2],s.Teachers[1],s.Teachers[0]} : new[]{s.Teachers[1],s.Teachers[0],s.Teachers[2]});
        (await verify.Set<DutyAssignmentChange>().CountAsync(x=>x.SchoolId==s.School&&(x.Kind=="AutoExchange"||x.Kind=="Exchange"))).Should().Be(1);
    }
    [DutySqlFact] public async Task Future_regeneration_retains_exchange_and_renewal_retains_unresolved_compensation()
    {
        var s=await fixture.Scenario(1);await s.Absence(s.Teachers[0],s.Settings.From);await s.Process(s.Settings.From);
        await using var db=fixture.Db();var service=s.Service(db);var w=await service.WorkspaceAsync(s.Term,default);w.History.Should().ContainSingle(x=>x.Unresolved);
        var updated=s.Settings with{TeacherIds=s.Teachers.Reverse().ToArray()};var p=await service.PreviewAsync(new(updated,w.PlanRevision),default);p.Days.Single().Preserved.Should().BeTrue();
        await service.PublishAsync(new(updated,w.PlanRevision,p.Digest,Guid.NewGuid()),default);
        var year=await db.AcademicTerms.Where(x=>x.Id==s.Term).Select(x=>x.AcademicYearId).SingleAsync();var term=new AcademicTerm{SchoolId=s.School,AcademicYearId=year,Semester=TimetableSemester.Second,StartsOn=new(2030,3,1),EndsOn=new(2030,3,5),CreatedByUserId=s.Manager,UpdatedByUserId=s.Manager};db.Add(term);await db.SaveChangesAsync();
        var calendar=new BellScheduleTemplate{SchoolId=s.School,AcademicYearId=year,Semester=term.Semester,Name="Synthetic renewal calendar",CreatedByUserId=s.Manager,UpdatedByUserId=s.Manager};db.Add(calendar);await db.SaveChangesAsync();
        db.Add(new BellScheduleRevision{SchoolId=s.School,BellScheduleTemplateId=calendar.Id,Revision=1,Name="Renewal calendar",SchoolTimeZoneId="Africa/Cairo",CreatedByUserId=s.Manager,Days=Enumerable.Range(1,7).Select(day=>new BellScheduleDay{Day=day,IsStudyDay=true}).ToList()});
        db.Add(new TimetableSetupProfile{SchoolId=s.School,AcademicYearId=year,Semester=term.Semester,Name="Renewal setup",BellScheduleTemplateId=calendar.Id,CreatedByUserId=s.Manager,UpdatedByUserId=s.Manager});await db.SaveChangesAsync();
        var renewal=updated with{TermId=term.Id,From=term.StartsOn,To=term.EndsOn,RegenerateFrom=term.StartsOn,RegenerateTo=term.EndsOn};var preview=await service.PreviewAsync(new(renewal,0),default);await service.PublishAsync(new(renewal,0,preview.Digest,Guid.NewGuid()),default);
        var renewed=await service.WorkspaceAsync(term.Id,default);var debt=renewed.History.Single(x=>x.Unresolved);debt.BeforeTeacherId.Should().Be(s.Teachers[0]);
        var change=new DutyChangeRequest(renewed.Days[0].Id,renewed.PlanRevision,s.Teachers[0],null,"Carry compensation forward","ResolveCompensation",false,debt.Id,Guid.NewGuid());await service.ChangeAsync(change,default);
        (await service.WorkspaceAsync(term.Id,default)).History.Should().NotContain(x=>x.Unresolved);
    }
    [DutySqlFact] public async Task Database_rejects_duplicate_dates_cross_school_links_and_history_mutation()
    {
        var a=await fixture.Scenario();var b=await fixture.Scenario();await using var db=fixture.Db();var day=await db.Set<DutyAssignment>().FirstAsync(x=>x.SchoolId==a.School);
        db.Add(new DutyAssignment{SchoolId=a.School,DutyPlanId=day.DutyPlanId,Date=day.Date,OriginalTeacherId=a.Teachers[0],EffectiveTeacherId=a.Teachers[0]});await FluentActions.Awaiting(()=>db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();db.ChangeTracker.Clear();
        db.Add(new DutyAssignment{SchoolId=b.School,DutyPlanId=day.DutyPlanId,Date=day.Date.AddDays(30),OriginalTeacherId=b.Teachers[0],EffectiveTeacherId=b.Teachers[0]});await FluentActions.Awaiting(()=>db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();db.ChangeTracker.Clear();
        db.Add(new DutyAssignment{SchoolId=a.School,DutyPlanId=day.DutyPlanId,Date=day.Date.AddDays(30),OriginalTeacherId=a.Teachers[0],EffectiveTeacherId=b.Teachers[0]});await FluentActions.Awaiting(()=>db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();db.ChangeTracker.Clear();
        var change=await db.Set<DutyAssignmentChange>().FirstAsync(x=>x.SchoolId==a.School);change.Reason="rewrite";await FluentActions.Awaiting(()=>db.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();db.ChangeTracker.Clear();
        await FluentActions.Awaiting(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE DutyAssignmentChanges SET Reason=N'rewrite' WHERE Id={change.Id}")).Should().ThrowAsync<SqlException>();
    }
    [DutySqlFact] public async Task Attendance_and_signal_commit_together_when_signal_insert_fails()
    {
        var s=await fixture.Scenario();await using var db=fixture.Db();var trigger="TR_DutySignalFailure_"+s.School;
        // Only a SQL-generated integer forms this test-only DDL identifier; no external input.
        var createTrigger="CREATE TRIGGER "+trigger+" ON DutyReconciliationOperations AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE SchoolId="+s.School+") THROW 51002, 'Injected duty signal failure', 1; END";
        await db.Database.ExecuteSqlRawAsync(createTrigger);
        try
        {
            var user=new TeacherDriveHarness.TestCurrentUser(RoleNames.Secretary,s.Secretary,s.School,true);
            var service=new AttendanceService(db,user,new SchoolScopeGuard(db,user,NullLogger<SchoolScopeGuard>.Instance));
            var request=new SaveAttendanceSheetRequestDto(s.Settings.From,s.Teachers.Append(s.Manager).Select(id=>new SaveAttendanceEntryDto(id,AttendanceStatus.Absent,null)).ToArray());
            await FluentActions.Awaiting(()=>service.SaveSheetAsync(request)).Should().ThrowAsync<DbUpdateException>();db.ChangeTracker.Clear();
            (await db.AttendanceRecords.CountAsync(x=>x.SchoolId==s.School)).Should().Be(0);
        }
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER "+trigger);}
    }
    [DutySqlFact] public async Task Selected_pool_date_exclusions_closures_and_regeneration_are_previewed_before_publication()
    {
        var s=await fixture.Scenario(20);await using var db=fixture.Db();var service=s.Service(db);var w=await service.WorkspaceAsync(s.Term,default);
        var settings=s.Settings with {TeacherIds=[s.Teachers[0],s.Teachers[2]],Exclusions=[new(s.Teachers[0],s.Settings.From,s.Settings.From,"Temporary duty exclusion")],Closures=[new(s.Settings.From.AddDays(1),s.Settings.From.AddDays(1),"Closure")]};
        var p=await service.PreviewAsync(new(settings,w.PlanRevision),default);p.Days.First().TeacherId.Should().Be(s.Teachers[2]);p.Days.Should().NotContain(x=>x.TeacherId==s.Teachers[1]);p.Days.Single(x=>x.Date==settings.From.AddDays(1)).TeacherId.Should().BeEmpty();
        var request=new DutyPublishRequest(settings,w.PlanRevision,p.Digest,Guid.NewGuid());var result=await service.PublishAsync(request,default);(await service.PublishAsync(request,default)).Should().Be(result);
        var current=await service.WorkspaceAsync(s.Term,default);current.Days.Should().HaveCount(19);current.Days.Should().NotContain(x=>x.Date==settings.From.AddDays(1));
        (await db.Notifications.CountAsync(x=>x.SchoolId==s.School&&x.UserId==s.Teachers[1]&&x.DeduplicationKey!.StartsWith("duty:publish:"))).Should().Be(2);
        // A changed generated effective owner does not become a compensation lock.
        var restored=settings with{Exclusions=[]};var restoredPreview=await service.PreviewAsync(new(restored,current.PlanRevision),default);restoredPreview.Days.First().Preserved.Should().BeFalse();
        await FluentActions.Awaiting(()=>service.PreviewAsync(new(settings with{TeacherIds=[s.Teachers[0],"forged"]},current.PlanRevision),default)).Should().ThrowAsync<DutyException>();
        await FluentActions.Awaiting(()=>service.PreviewAsync(new(settings with{AcceptAttendanceCalendarMismatch=false},current.PlanRevision),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyAttendanceCalendarMismatch");
        await FluentActions.Awaiting(()=>service.PublishAsync(request with{RequestId=Guid.NewGuid(),ExpectedRevision=current.PlanRevision,PreviewDigest="stale"},default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==409);
    }
    [DutySqlFact] public async Task An_already_processed_future_absence_is_requeued_when_exchange_changes_its_effective_owner()
    {
        var s=await fixture.Scenario();var future=s.Settings.From.AddDays(1);
        await s.Absence(s.Teachers[0],future);
        while(true)
        {
            await using var read=fixture.Db();if(!await read.Set<DutyReconciliationOperation>().AnyAsync(x=>x.SchoolId==s.School&&x.Date==future&&x.CompletedAt==null))break;
            await s.Process(future);
        }
        await s.Absence(s.Teachers[0],s.Settings.From);await s.Process(s.Settings.From);
        await using(var db=fixture.Db())(await db.Set<DutyReconciliationOperation>().CountAsync(x=>x.SchoolId==s.School&&x.Date==future&&x.CompletedAt==null)).Should().Be(1);
        await s.Process(future);
        await using(var db=fixture.Db())(await db.Set<DutyAssignment>().Where(x=>x.SchoolId==s.School).OrderBy(x=>x.Date).Take(3).Select(x=>x.EffectiveTeacherId).ToListAsync()).Should().Equal(s.Teachers[1],s.Teachers[2],s.Teachers[0]);
    }
    [DutySqlFact] public async Task Duplicate_teacher_names_remain_distinct_in_review_and_fairness_uses_identity()
    {
        var s=await fixture.Scenario();await using var db=fixture.Db();
        var duplicates=await db.Users.Where(x=>x.Id==s.Teachers[0]||x.Id==s.Teachers[1]).ToListAsync();foreach(var u in duplicates){u.FirstName="Same";u.LastName="Name";}await db.SaveChangesAsync();
        var service=s.Service(db);var w=await service.WorkspaceAsync(s.Term,default);w.Teachers.Select(x=>x.Name).Distinct().Should().HaveCount(3);
        var request=new DutyChangeRequest(w.Days[0].Id,w.PlanRevision,s.Teachers[1],null,"Replacement review","Replace",false,null,Guid.NewGuid());var p=await service.ChangePreviewAsync(request,default);
        p.Fairness.Single(x=>x.UserId==s.Teachers[0]).Effective.Should().Be(w.Fairness.Single(x=>x.UserId==s.Teachers[0]).Effective-1);
        p.Fairness.Single(x=>x.UserId==s.Teachers[1]).Effective.Should().Be(w.Fairness.Single(x=>x.UserId==s.Teachers[1]).Effective+1);
    }
    [DutySqlFact] public async Task Teacher_transfer_preserves_history_and_allows_explicit_removal_of_ineligible_selection()
    {
        var s=await fixture.Scenario();var destination=await fixture.Scenario();await using var db=fixture.Db();
        var original=await db.Set<DutyAssignment>().Where(x=>x.SchoolId==s.School).OrderBy(x=>x.Date).FirstAsync();
        // Existing SchoolId is an EF alternate-key member; the established SQL transfer
        // seam must remain possible without duty history imposing a new FK restriction.
        await db.InstructorProfiles.Where(x=>x.UserId==s.Teachers[0]).ExecuteUpdateAsync(update=>update.SetProperty(x=>x.SchoolId,destination.School));
        var membership=await db.UserSchoolRoles.SingleAsync(x=>x.SchoolId==s.School&&x.UserId==s.Teachers[0]);membership.IsActive=false;
        db.UserSchoolRoles.Add(new UserSchoolRole{SchoolId=destination.School,UserId=s.Teachers[0],RoleId=RoleNames.Instructor});await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var service=s.Service(db);var w=await service.WorkspaceAsync(s.Term,default);w.Teachers.Single(x=>x.UserId==s.Teachers[0]).IsEligible.Should().BeFalse();
        w.Days.First().OriginalTeacherId.Should().Be(original.OriginalTeacherId);w.Days.First().ExceptionCode.Should().Be("EligibilityReview");
        await FluentActions.Awaiting(()=>s.Service(db,s.Teachers[0],role:RoleNames.Instructor).WorkspaceAsync(s.Term,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
        var removal=s.Settings with{TeacherIds=s.Teachers.Skip(1).ToArray()};var p=await service.PreviewAsync(new(removal,w.PlanRevision),default);await service.PublishAsync(new(removal,w.PlanRevision,p.Digest,Guid.NewGuid()),default);
        var current=await service.WorkspaceAsync(s.Term,default);current.Days.Should().NotContain(x=>x.TeacherId==s.Teachers[0]);current.Days.First().OriginalTeacherId.Should().Be(original.OriginalTeacherId);
        var history=await db.Set<DutyAssignmentChange>().Where(x=>x.SchoolId==s.School&&x.Kind=="Publish").OrderBy(x=>x.Id).FirstAsync();history.SettingsSnapshotJson.Should().Contain(s.Teachers[0]);
    }
}
