using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;



using AlFalah.Infrastructure.Services;
using AlFalah.Infrastructure.Notifications;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Attendance;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using AlFalah.Domain.Events;

namespace AlFalah.Tests.Timetables;

public sealed class RegisterSqlFactAttribute : FactAttribute
{
 public RegisterSqlFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALFALAH_REGISTER_TEST_CONNECTION")))Skip="Requires fresh isolated AlFalahRegisterTests_<guid> LocalDB.";}
}
public sealed class CoverageRegisterSqlTests
{
 [RegisterSqlFact]
 public async Task Fresh_SQL_weekly_to_absence_execution_and_retained_register_is_scoped_atomic_and_replay_safe()
 {
        var cs = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ALFALAH_REGISTER_TEST_CONNECTION"));
        if (!cs.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) || !cs.IntegratedSecurity ||
            !System.Text.RegularExpressions.Regex.IsMatch(cs.InitialCatalog, "^AlFalahRegisterTests_[a-f0-9]{32}$")) throw new InvalidOperationException("Fresh isolated integrated LocalDB required.");
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(cs.ConnectionString) { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync(); await using var command = master.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name=@name"; command.Parameters.AddWithValue("@name", cs.InitialCatalog);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("An existing database is not an allowed test destination.");
        }
        AlFalahDbContext Db() => new(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(cs.ConnectionString).Options);
        await using var db = Db(); await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        db.Users.AddRange(new() { Id = "manager", UserName = "coverage-manager", FirstName = "Synthetic", LastName = "Manager", IsActive = true },
            new() { Id = "teacher1", UserName = "coverage-original", FirstName = "Synthetic", LastName = "Original", IsActive = true },
            new() { Id = "teacher2", UserName = "coverage-replacement", FirstName = "Synthetic", LastName = "Replacement", IsActive = true });
        db.Add(new School { Name = "Synthetic coverage school", Stage = SchoolStage.Intermediate, City = "Test", ManagerUserId = "manager" });
        if (!await db.AcademicYears.AnyAsync(x => x.Id == 1)) db.Add(new AcademicYear { Code = "coverage-test", NameAr = "2026-2027", StartsOn = new(2026, 8, 1), EndsOn = new(2027, 7, 31), IsActive = true });
        await db.SaveChangesAsync(); var timing = await BellScheduleTestData.SeedAsync(db);
        var year = await db.AcademicYears.SingleAsync(x => x.Id == 1); year.StartsOn = new(2026, 8, 1); year.EndsOn = new(2027, 7, 31);
        var original = new InstructorProfile { SchoolId = 1, UserId = "teacher1", IsActive = true, SubjectSpecialization = "Math" };
        var substitute = new InstructorProfile { SchoolId = 1, UserId = "teacher2", IsActive = true, SubjectSpecialization = "Math" };
        db.AddRange(original, substitute); await db.SaveChangesAsync();
        var teachers = new[] { original, substitute }.Select(i => new TeacherTimetableProfile { SchoolId = 1, TimetableSetupProfileId = 1, InstructorProfileId = i.Id, BellScheduleRevisionId = timing.Id, MaximumWeeklyPeriods = 20, CreatedByUserId = "manager", UpdatedByUserId = "manager" }).ToArray();
        var classroom = new Classroom { SchoolId = 1, AcademicYearId = 1, Stage = SchoolStage.Intermediate, GradeLevel = 1, Section = "1", SectionNumber = 1, ClassLabel = "1/1", CreatedByUserId = "manager", UpdatedByUserId = "manager" };
        var subject = new SubjectDefinition { SchoolId = 1, Name = "Math", UpdatedByUserId = "manager" }; db.AddRange(teachers); db.AddRange(classroom, subject); await db.SaveChangesAsync();
        var requirement = new ClassSubjectRequirement { SchoolId = 1, TimetableSetupProfileId = 1, ClassroomId = classroom.Id, SubjectId = subject.Id, IndividualPeriodCount = 2, UpdatedByUserId = "manager" }; db.Add(requirement); await db.SaveChangesAsync();
        db.Add(new TeachingAssignment { SchoolId = 1, TimetableSetupProfileId = 1, ClassSubjectRequirementId = requirement.Id, UpdatedByUserId = "manager",
            Members = [new() { SchoolId = 1, TimetableSetupProfileId = 1, TeacherTimetableProfileId = teachers[0].Id, AllocatedPeriodCount = 2 }] });
        var timetable = new SchoolTimetable { SchoolId = 1, AcademicYearId = 1, Semester = TimetableSemester.First, TimetableSetupProfileId = 1,
            BellScheduleRevisionId = timing.Id, Title = "Synthetic coverage timetable", IsPublished = false, CreatedByUserId = "manager", UpdatedByUserId = "manager" };
        foreach (var p in new[] { 1, 2 })
        {
            timetable.Entries.Add(new() { SchoolId = 1, InstructorProfileId = original.Id, Day = TimetableDay.Sunday, Period = p, EntryType = TimetableEntryType.Lesson, ClassroomId = classroom.Id, ClassLabel = "1/1", SubjectId = subject.Id, Subject = "Math", ClassSubjectRequirementId = requirement.Id });
        }
        db.Add(timetable); db.Add(new AttendanceRecord { SchoolId = 1, UserId = "teacher1", AttendanceDate = DailyCoverageTests.Date, Status = AttendanceStatus.Absent, RecordedByUserId = "manager" });
        db.Add(new UserSignature { UserId = "teacher2", SignatureDrawnData = "data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"TestAssets","Duty","signature-0.png"))) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();

        QuestPDF.Settings.License=QuestPDF.Infrastructure.LicenseType.Community;
        var weekly=WeeklyStandbyTests.Service(db);
        var manual=new WeeklyStandbyRequest(timetable.Revision,"Manual",new(0,[new(2,1,1),new(2,2,1)]),[new(substitute.Id,2,1,true),new(substitute.Id,2,2)]);
        var preview=await weekly.PreviewAsync(timetable.Id,manual,default);
        var work=await weekly.ApplyAsync(timetable.Id,manual with{ProposalId=preview.ProposalId},default);
        var fair=new WeeklyStandbyRequest(work.Revision,"Fair",work.Settings,work.Placements);
        preview=await weekly.PreviewAsync(timetable.Id,fair,default);
        work=await weekly.ApplyAsync(timetable.Id,fair with{ProposalId=preview.ProposalId},default);
        work.Placements.Should().Contain(p=>p.InstructorProfileId==substitute.Id&&p.Period==1&&p.IsLocked);
        var repo=new TimetableSubstitutionRepository(db);var validator=new TimetableValidationEngine();
        var review=new TimetableReviewService(repo,validator,new(validator),new DailyCoverageTests.User());
        var analysis=await review.EvaluateAsync(timetable.Id,default);analysis.HardViolationCount.Should().Be(0);
        foreach(var finding in analysis.Findings.Where(f=>f.Severity==AlFalah.Domain.Enums.ViolationSeverity.Warning&&!f.IsOverridden))await review.OverrideAsync(finding.Id,"Synthetic reviewed publication warning",default);
        await review.PublishAsync(timetable.Id,work.Revision,default);db.ChangeTracker.Clear();
        foreach(var (uid,roleName) in new[]{("manager",RoleNames.SchoolManager),("teacher1",RoleNames.Instructor),("teacher2",RoleNames.Instructor),("secretary",RoleNames.Secretary)}){
            if(uid=="secretary")db.Users.Add(new(){Id=uid,UserName=uid,FirstName="Synthetic",LastName="Secretary",IsActive=true});
            var role=await db.Roles.SingleOrDefaultAsync(r=>r.Name==roleName);if(role==null){role=new(){Id=roleName,Name=roleName};db.Add(role);await db.SaveChangesAsync();}
            db.UserSchoolRoles.Add(new(){SchoolId=1,UserId=uid,RoleId=role.Id,IsActive=true});
        }
        await db.SaveChangesAsync();var secretary=new DailyCoverageTests.User("secretary",RoleNames.Secretary);
        var attendance=new AttendanceService(db,secretary,new SchoolScopeGuard(db,secretary,NullLogger<SchoolScopeGuard>.Instance));
        var sheet=new SaveAttendanceSheetRequestDto(DailyCoverageTests.Date,[new("manager",AttendanceStatus.Present,null),new("teacher1",AttendanceStatus.Absent,null),new("teacher2",AttendanceStatus.Present,null)]);
        await attendance.SaveSheetAsync(sheet);await attendance.SaveSheetAsync(sheet);
        (await db.Set<CoverageReconciliationSignal>().CountAsync()).Should().Be(1);
        var clock=new DailyCoverageTests.Clock();var signals=await repo.ClaimSignalsAsync("register-test",clock.Now,default);
        await new DailyCoverageReconciler(repo,clock).ProcessSignalAsync(signals.Single(),"register-test",default);
        var coverage=DailyCoverageTests.Services(db,clock).Coverage;var board=await coverage.BoardAsync(DailyCoverageTests.Date,default);board.Needs.Should().HaveCount(2);
        var choice=new CoveragePreviewRequest(DailyCoverageTests.Date,"Manual",[new(board.Needs[0].Id,substitute.Id)],clock.Now.AddMinutes(4));
        var daily=await coverage.PreviewAsync(choice,default);daily.Assignments.Should().ContainSingle();
        choice=choice with{Mode="Fair"};daily=await coverage.PreviewAsync(choice,default);daily.Assignments.Should().HaveCount(2);
        board=await coverage.ApproveAsync(new(Guid.NewGuid(),choice,daily.ProposalId,"Synthetic reviewed coverage warning"),default);
        var dispatcher=new StudentAffairsNotificationDispatcher(db,clock);
        foreach(var message in await db.OutboxMessages.Where(o=>o.EventType.EndsWith(nameof(TeacherTimetableChangedEvent))).ToListAsync()){
            var changed=JsonSerializer.Deserialize<TeacherTimetableChangedEvent>(message.PayloadJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            await dispatcher.ProcessAsync(changed,default);await db.SaveChangesAsync();await dispatcher.ProcessAsync(changed,default);await db.SaveChangesAsync();
        }
        (await db.Notifications.CountAsync(n=>n.UserId=="teacher2"&&n.RelatedEntityType==nameof(DailyCoverageNeed))).Should().Be(2);
        CoverageRegisterService Register(AlFalahDbContext context,DailyCoverageTests.User? user=null){var r=new TimetableSubstitutionRepository(context);return new(r,new CoverageRegisterRenderer(),new(r,user??new()),clock);}
        var register=Register(db);var current=await register.ContextAsync(DailyCoverageTests.Date,1,10,default);
        var pending=await register.IssueAsync(new(current.Date,current.SourceRevision!),default);pending.IsComplete.Should().BeFalse();
        var pendingBytes=await register.ContentAsync(pending.Id,default);
        var teacher=DailyCoverageTests.Services(db,clock,DailyCoverageTests.Teacher).Execution;
        foreach(var n in board.Needs){
            var detail=await teacher.ActAsync(n.Id,"Acknowledgement",new(Guid.NewGuid(),n.Revision,n.SubstitutionId!.Value),default);
            detail=await teacher.SaveDraftAsync(n.Id,new(detail.Need.Revision,n.SubstitutionId.Value,"مراجعة مع الطلاب", ""),default);
            await teacher.ActAsync(n.Id,"Execution",new(Guid.NewGuid(),detail.Need.Revision,n.SubstitutionId.Value),default);
        }
        current=await register.ContextAsync(current.Date,1,10,default);current.IsComplete.Should().BeTrue();
        var complete=await register.IssueAsync(new(current.Date,current.SourceRevision!),default);complete.Revision.Should().Be(2);complete.IsComplete.Should().BeTrue();
        (await register.IssueAsync(new(current.Date,current.SourceRevision!),default)).Id.Should().Be(complete.Id);
        (await register.ContentAsync(pending.Id,default)).Bytes.Should().Equal(pendingBytes.Bytes);
        await Register(db,DailyCoverageTests.Teacher).Invoking(s=>s.ContentAsync(complete.Id,default)).Should().ThrowAsync<UnauthorizedAccessException>();
        await Register(db,new(School:999)).Invoking(s=>s.ContentAsync(complete.Id,default)).Should().ThrowAsync<KeyNotFoundException>();
        var school=await db.Schools.SingleAsync();school.Name="Renamed synthetic school";await db.SaveChangesAsync();
        current=await register.ContextAsync(current.Date,1,10,default);var issue=new IssueCoverageRegisterRequest(current.Date,current.SourceRevision!);db.ChangeTracker.Clear();
        async Task<int?> Compete(){await using var context=Db();try{return(await Register(context).IssueAsync(issue,default)).Id;}catch(BellScheduleConflictException){return null;}}
        var racers=await Task.WhenAll(Compete(),Compete());racers.Where(id=>id!=null).Distinct().Should().ContainSingle();
        (await db.Set<CoverageRegisterArtifact>().CountAsync(a=>a.SourceRevision==issue.SourceRevision)).Should().Be(1);
        var before=await register.ContentAsync(complete.Id,default);before.Bytes.Take(5).Should().Equal("%PDF-"u8.ToArray());
        var old=await db.Set<CoverageRegisterArtifact>().SingleAsync(a=>a.Id==pending.Id);old.SnapshotJson="tamper";
        await db.Invoking(d=>d.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();
 }
}
