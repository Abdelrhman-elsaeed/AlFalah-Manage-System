using AlFalah.Application.Classrooms;
using AlFalah.Application.DTOs.Teachers;
using AlFalah.Infrastructure.Services;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Tests.TestDoubles;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.DTOs.Timetables;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace AlFalah.Tests.Classrooms;

public sealed class ClassroomSqlFactAttribute : FactAttribute
{
    public ClassroomSqlFactAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALFALAH_CLASSROOM_TEST_CONNECTION"))) Skip="Set a fresh isolated AlFalahClassroomTests_* LocalDB database."; }
}
public sealed class ClassroomCatalogSqlTests
{
    [ClassroomSqlFact]
    public async Task Fresh_SQL_migration_preserves_legacy_and_enforces_catalogue_scope_identity_concurrency_and_rollback_guard()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ALFALAH_CLASSROOM_TEST_CONNECTION"));
        if (!connection.DataSource.StartsWith("(localdb)\\",StringComparison.OrdinalIgnoreCase) || !connection.IntegratedSecurity ||
            !connection.InitialCatalog.StartsWith("AlFalahClassroomTests_",StringComparison.Ordinal)) throw new InvalidOperationException("Fresh isolated LocalDB database required.");
        await using (var master=new SqlConnection(new SqlConnectionStringBuilder(connection.ConnectionString){InitialCatalog="master"}.ConnectionString))
        {
            await master.OpenAsync(); await using var query=master.CreateCommand(); query.CommandText="SELECT COUNT(*) FROM sys.databases WHERE name=@name";
            query.Parameters.AddWithValue("@name",connection.InitialCatalog);
            if (Convert.ToInt32(await query.ExecuteScalarAsync())!=0) throw new InvalidOperationException("Existing databases are rejected before any migration.");
        }
        AlFalahDbContext Db() => new(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection.ConnectionString).Options);
        await using var db=Db(); var migrator=db.GetService<IMigrator>();
        var previous=db.Database.GetMigrations().TakeWhile(m=>!m.EndsWith("AddStructuredClassroomCatalog")).Last();
        await migrator.MigrateAsync(previous);
        db.Users.AddRange(new ApplicationUser{Id="catalog-manager",UserName="catalog-manager",FirstName="Synthetic",LastName="Manager",IsActive=true},
            new ApplicationUser{Id="catalog-teacher",UserName="catalog-teacher",FirstName="Synthetic",LastName="Teacher",IsActive=true});
        db.Roles.AddRange(new(){Id="catalog-role",Name=RoleNames.SchoolManager},new(){Id="catalog-teacher-role",Name=RoleNames.Instructor});
        var school=new School{Name="Synthetic catalogue",City="Test",Stage=SchoolStage.Primary,ManagerUserId="catalog-manager"};
        var foreign=new School{Name="Foreign catalogue",City="Test",Stage=SchoolStage.Primary,ManagerUserId="catalog-manager"};
        var year=new AcademicYear{Code="catalog-test",NameAr="Synthetic year",StartsOn=new(2026,8,1),EndsOn=new(2027,7,31),IsActive=true};
        db.AddRange(school,foreign,year); await db.SaveChangesAsync();
        db.AcademicTerms.Add(new(){SchoolId=school.Id,AcademicYearId=year.Id,Semester=(TimetableSemester)1,IsActive=true,StartsOn=year.StartsOn,EndsOn=year.EndsOn,CreatedByUserId="catalog-manager",UpdatedByUserId="catalog-manager"});
        db.UserSchoolRoles.AddRange(new(){SchoolId=school.Id,UserId="catalog-manager",RoleId="catalog-role"},new(){SchoolId=school.Id,UserId="catalog-teacher",RoleId="catalog-teacher-role"});
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Classrooms (SchoolId,AcademicYearId,Stage,GradeLevel,Section,ClassLabel,PhysicalLocation,IsActive,CreatedAt,UpdatedAt,CreatedByUserId,UpdatedByUserId,IsDeleted) VALUES ({school.Id},{year.Id},1,1,N'A',N'Legacy A',N'Room A',1,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET(),'catalog-manager','catalog-manager',0),({school.Id},{year.Id},1,1,N'٢',N'Legacy numeric',N'Room 2',1,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET(),'catalog-manager','catalog-manager',0)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO InstructorProfiles (UserId,SchoolId,IsActive,CreatedAt,UpdatedAt,IsDeleted) VALUES ('catalog-teacher',{school.Id},1,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET(),0)");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO InstructorClasses (InstructorProfileId,ClassLabel,SortOrder,IsDeleted,CreatedAt,UpdatedAt) SELECT Id,N'Legacy A',0,0,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET() FROM InstructorProfiles WHERE UserId='catalog-teacher'; INSERT INTO InstructorClasses (InstructorProfileId,ClassLabel,SortOrder,IsDeleted,CreatedAt,UpdatedAt) SELECT Id,N'Unknown legacy',1,0,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET() FROM InstructorProfiles WHERE UserId='catalog-teacher'");
        await migrator.MigrateAsync(); await migrator.MigrateAsync(); db.ChangeTracker.Clear();
        db.Database.HasPendingModelChanges().Should().BeFalse();
        var letter=await db.Classrooms.SingleAsync(x=>x.ClassLabel=="Legacy A"); letter.SectionNumber.Should().BeNull(); letter.Section.Should().Be("A");
        var numeric=await db.Classrooms.SingleAsync(x=>x.ClassLabel=="Legacy numeric"); numeric.SectionNumber.Should().Be(2); numeric.ClassLabel.Should().Be("Legacy numeric");
        var links=await db.InstructorClasses.OrderBy(x=>x.SortOrder).ToArrayAsync(); links[0].ClassroomId.Should().Be(letter.Id); links[1].ClassroomId.Should().BeNull();
        links.Should().OnlyContain(x=>x.SchoolId==school.Id && !x.IsDeleted);
        var user=new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,"catalog-manager",school.Id);
        ClassroomCatalogService Service(AlFalahDbContext context) => new(new ClassroomCatalogRepository(context),user);
        // A duplicate in a batch cannot create the other requested section.
        await FluentActions.Invoking(()=>Service(db).CreateAsync(new(year.Id,1,["2","3"],"Room"),default)).Should().ThrowAsync<ArgumentException>();
        (await db.Classrooms.CountAsync()).Should().Be(2);
        await Service(db).CreateAsync(new(year.Id,1,Enumerable.Range(3,100).Select(x=>x.ToString()).ToArray(),"Floor 1"),default);
        await Service(db).CreateAsync(new(year.Id,1,["103","104","105"],"Floor 2"),default);
        var page1=await Service(db).OptionsAsync(year.Id,1,true,default); var page2=await Service(db).OptionsAsync(year.Id,2,true,default);
        page1.Items.Should().HaveCount(100); page1.HasMore.Should().BeTrue(); page2.Items.Should().HaveCount(5); page2.HasMore.Should().BeFalse();
        page1.Items.Select(x=>x.Id).Should().NotIntersectWith(page2.Items.Select(x=>x.Id));
        var teacher=new TeacherDriveHarness.TestCurrentUser(RoleNames.Instructor,"catalog-teacher",school.Id);
        var profile=await db.InstructorProfiles.SingleAsync();
        var teacherService=new TeacherService(db,null!,teacher,new SchoolScopeGuard(db,teacher,NullLogger<SchoolScopeGuard>.Instance),
            new AuditLogWriter(db,new HttpContextAccessor(),NullLogger<AuditLogWriter>.Instance),NullLogger<TeacherService>.Instance,
            new ClassroomCatalogService(new ClassroomCatalogRepository(db),teacher));
        var saved=await teacherService.UpsertTeachingAsync(profile.UserId,new TeacherTeachingUpsertRequest{
            Subject="Synthetic subject",ClassroomIds=[letter.Id,numeric.Id],AcademicYearId=year.Id,TeachingRevision=1});
        saved.ClassroomIds.Should().Equal(letter.Id,numeric.Id); saved.UnresolvedClasses.Should().BeEmpty();
        saved.TeachingRevision.Should().Be(2); profile.TeachingRevision.Should().Be(2);
        (await db.AuditLogs.CountAsync(x=>x.Action=="Classroom.TeacherSelection"&&x.Reason=="Self")).Should().Be(1);
        // Subject-only updates preserve the entire classroom set and revision.
        var partial=await teacherService.UpsertTeachingAsync(profile.UserId,new TeacherTeachingUpsertRequest{Subject="Revised subject"});
        partial.ClassroomIds.Should().Equal(letter.Id,numeric.Id); partial.TeachingRevision.Should().Be(2);
        // Two contexts start from the same revision; the second save cannot overwrite.
        await using(var stale=Db())
        {
            var staleProfile=await stale.InstructorProfiles.SingleAsync();
            await new ClassroomCatalogService(new ClassroomCatalogRepository(stale),teacher).ApplyTeacherSelectionAsync(staleProfile,[],null,year.Id,2,default);
            await new ClassroomCatalogService(new ClassroomCatalogRepository(db),teacher).ApplyTeacherSelectionAsync(profile,[letter.Id],null,year.Id,2,default);
            await db.SaveChangesAsync();
            await FluentActions.Invoking(()=>stale.SaveChangesAsync()).Should().ThrowAsync<DbUpdateConcurrencyException>();
        }
        await VerifyConsumersAsync(db,Service(db),letter,profile,foreign.Id);
        (await new TeacherContextRepository(db).IsClassroomAssignedAsync(school.Id,profile.UserId,letter.Id,default)).Should().BeTrue();
        // Database-level school FK and structural uniqueness survive bypassing service validation.
        await using(var invalid=Db())
        {
            invalid.InstructorClasses.Add(new(){SchoolId=foreign.Id,ClassroomId=numeric.Id,InstructorProfileId=profile.Id,ClassLabel="foreign"});
            await FluentActions.Invoking(()=>invalid.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
        }
        await using(var duplicate=Db())
        {
            duplicate.Classrooms.Add(new(){SchoolId=school.Id,AcademicYearId=year.Id,Stage=SchoolStage.Primary,GradeLevel=1,SectionNumber=1,Section="1",ClassLabel="Different label",PhysicalLocation="Room",CreatedByUserId="catalog-manager",UpdatedByUserId="catalog-manager"});
            await FluentActions.Invoking(()=>duplicate.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
        }
        await FluentActions.Invoking(()=>migrator.MigrateAsync(previous)).Should().ThrowAsync<SqlException>();
        (await db.Database.GetAppliedMigrationsAsync()).Last().Should().Be(db.Database.GetMigrations().Last());
        (await db.InstructorClasses.CountAsync(x=>x.ClassroomId==letter.Id)).Should().Be(1);
    }

    private static async Task VerifyConsumersAsync(AlFalahDbContext db, ClassroomCatalogService catalog,
        Classroom classroom, InstructorProfile teacher, int foreignSchool)
    {
        var template=new BellScheduleTemplate {SchoolId=classroom.SchoolId,AcademicYearId=classroom.AcademicYearId,
            Semester=TimetableSemester.First,Name="Synthetic timing",CreatedByUserId="catalog-manager",UpdatedByUserId="catalog-manager"};
        var timing=new BellScheduleRevision {SchoolId=classroom.SchoolId,Template=template,Revision=1,
            Name=template.Name,SchoolTimeZoneId="Africa/Cairo",CreatedByUserId="catalog-manager"};
        timing.Days.Add(new(){Day=0,IsStudyDay=true,Periods=[new(){Sequence=1,StartLocalTime=new(7,0),EndLocalTime=new(7,45)}]});
        timing.Days.Add(new(){Day=6,IsStudyDay=true,UsesDefaultSchedule=true});
        var timetable=new SchoolTimetable {SchoolId=classroom.SchoolId,AcademicYearId=classroom.AcademicYearId,
            Semester=TimetableSemester.First,Title="Retained legacy",IsPublished=true,BellScheduleRevision=timing,
            CreatedByUserId="catalog-manager",UpdatedByUserId="catalog-manager"};
        var entry=new SchoolTimetableEntry {SchoolId=classroom.SchoolId,SchoolTimetable=timetable,
            InstructorProfileId=teacher.Id,ClassLabel=classroom.ClassLabel,Subject="Legacy subject",Day=TimetableDay.Thursday,
            Period=1,EntryType=TimetableEntryType.Lesson};
        db.Add(entry); await db.SaveChangesAsync();
        var retained=System.Text.Json.JsonSerializer.Serialize(new {entry.Id,entry.ClassLabel,entry.ClassroomId,timetable.Revision});
        var renamed=await catalog.UpdateAsync(classroom.Id,new("1",true,"Moved room",classroom.UpdatedAt.ToString("O")),default);
        renamed.Id.Should().Be(classroom.Id);
        var date=new DateOnly(2026,10,15);
        var repository=new CurrentLessonEntryRepository(db);
        var lessons=await repository.GetEntriesAsync(classroom.SchoolId,timetable.Id,date,TimetableDay.Thursday,1,
            classroom.Id,"forged label",default);
        lessons.Should().ContainSingle(); lessons.Single().ClassroomId.Should().Be(classroom.Id);
        lessons.Single().ClassroomLabel.Should().Be(renamed.Label);
        (await repository.GetEntriesAsync(foreignSchool,timetable.Id,date,TimetableDay.Thursday,1,classroom.Id,entry.ClassLabel,default)).Should().BeEmpty();
        (await repository.GetEntriesAsync(classroom.SchoolId,timetable.Id,date,TimetableDay.Thursday,1,999,entry.ClassLabel,default)).Should().BeEmpty();
        var roster=await new TeacherContextRepository(db).GetPeriodRosterAsync(classroom.SchoolId,teacher.UserId,entry.Id,date,timing.Id,default);
        roster.Should().NotBeNull(); roster!.CurrentPeriod!.Classroom.Id.Should().Be(classroom.Id);
        var gate=await new GatePassWorkflowRepository(db,new BellScheduleRepository(db)).ResolvePublishedTimetableAsync(
            classroom.SchoolId,classroom.AcademicYearId,TimetableSemester.First,classroom.Id,"forged label",
            new(2026,10,15,7,15,0,TimeSpan.FromHours(3)),default);
        gate.Should().NotBeNull(); gate!.InstructorProfileId.Should().Be(teacher.Id);
        var validator=new SubjectAssignmentService(new SubjectRepository(db));
        var schedule=(await new BellScheduleRepository(db).GetRevisionAsync(classroom.SchoolId,timing.Id,default))!;
        TimetableEntryDto Row(string label,int? id=null)=>new(teacher.Id,TimetableDay.Thursday,1,TimetableEntryType.Lesson,label,"Legacy subject",id);
        var normalized=await validator.ValidateAsync(timetable,[Row(entry.ClassLabel!)],schedule,false,default);
        normalized.Single().ClassroomId.Should().Be(classroom.Id); normalized.Single().ClassLabel.Should().Be(renamed.Label);
        (await validator.ValidateAsync(timetable,[Row("forged",classroom.Id)],schedule,false,default)).Single().ClassLabel.Should().Be(renamed.Label);
        await FluentActions.Invoking(()=>validator.ValidateAsync(timetable,[Row("Unknown new class")],schedule,false,default)).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(()=>validator.ValidateAsync(timetable,[Row("forged",999)],schedule,false,default)).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(()=>validator.ValidateAsync(timetable,[Row(entry.ClassLabel!),Row(renamed.Label,classroom.Id)],schedule,false,default)).Should().ThrowAsync<ArgumentException>();
        // Reading and resolving never rewrite the old entry or retained version identity.
        await db.Entry(entry).ReloadAsync(); await db.Entry(timetable).ReloadAsync();
        System.Text.Json.JsonSerializer.Serialize(new {entry.Id,entry.ClassLabel,entry.ClassroomId,timetable.Revision}).Should().Be(retained);
        (await db.ClassroomLabelHistory.CountAsync(x=>x.ClassroomId==classroom.Id)).Should().Be(2);
        // Corrupt legacy collisions fail closed rather than granting either classroom's roster.
        db.Classrooms.Add(new(){SchoolId=classroom.SchoolId,AcademicYearId=classroom.AcademicYearId,Stage=SchoolStage.Primary,
            GradeLevel=2,Section="A",ClassLabel=entry.ClassLabel!,CreatedByUserId="catalog-manager",UpdatedByUserId="catalog-manager"});
        await db.SaveChangesAsync();
        (await repository.GetEntriesAsync(classroom.SchoolId,timetable.Id,date,TimetableDay.Thursday,1,classroom.Id,entry.ClassLabel,default)).Should().BeEmpty();
        (await new TeacherContextRepository(db).GetPeriodRosterAsync(classroom.SchoolId,teacher.UserId,entry.Id,date,timing.Id,default)).Should().BeNull();
        var previousYear=new AcademicYear {Code="consumer-previous",NameAr="Previous",StartsOn=new(2025,8,1),EndsOn=new(2026,7,31)};
        var previousClass=new Classroom {SchoolId=classroom.SchoolId,AcademicYear=previousYear,Stage=SchoolStage.Primary,
            GradeLevel=1,Section="1",ClassLabel=renamed.Label,CreatedByUserId="catalog-manager",UpdatedByUserId="catalog-manager"};
        db.Add(previousClass);await db.SaveChangesAsync();
        db.AcademicTerms.Add(new(){SchoolId=classroom.SchoolId,AcademicYearId=previousYear.Id,Semester=TimetableSemester.First,
            StartsOn=previousYear.StartsOn,EndsOn=previousYear.EndsOn,CreatedByUserId="catalog-manager",UpdatedByUserId="catalog-manager"});
        await db.SaveChangesAsync();
        var directory=new StudentWorkflowRepository(db);
        var current=await directory.GetClassroomsAsync(classroom.SchoolId,new(){CurrentYearOnly=true,PageSize=100},default);
        current.Items.Should().OnlyContain(c=>c.AcademicYearId==classroom.AcademicYearId);current.TotalCount.Should().Be(106);
        var past=await directory.GetClassroomsAsync(classroom.SchoolId,new(){AcademicYearId=previousYear.Id,CurrentYearOnly=true},default);
        past.Items.Should().ContainSingle(c=>c.Id==previousClass.Id);
        (await directory.GetClassroomsAsync(foreignSchool,new(){AcademicYearId=previousYear.Id},default)).Items.Should().BeEmpty();
        var history=await db.ClassroomLabelHistory.FirstAsync(x=>x.ClassroomId==classroom.Id);
        history.Label="attempted rewrite";
        await FluentActions.Invoking(()=>db.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();
        db.Entry(history).State=EntityState.Unchanged;
    }
}
