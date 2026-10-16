using AlFalah.Application.IntelligentTimetable;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuestPDF.Infrastructure;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class StatementSqlFactAttribute:FactAttribute
{
    public StatementSqlFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALFALAH_STATEMENT_TEST_CONNECTION")))Skip="Set a fresh isolated AlFalahStatementTests_* LocalDB connection.";}
}
public sealed class TeachingStatementSqlTests
{
    [StatementSqlFact]
    public async Task Fresh_SQL_migration_preserves_allocations_and_scopes_consistent_retained_statements_with_safe_rollback()
    {
        var connection=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ALFALAH_STATEMENT_TEST_CONNECTION"));
        if(!connection.DataSource.StartsWith("(localdb)\\",StringComparison.OrdinalIgnoreCase)||!connection.IntegratedSecurity||!connection.InitialCatalog.StartsWith("AlFalahStatementTests_",StringComparison.Ordinal))throw new InvalidOperationException("Fresh isolated integrated LocalDB required.");
        await using(var master=new SqlConnection(new SqlConnectionStringBuilder(connection.ConnectionString){InitialCatalog="master"}.ConnectionString))
        {
            await master.OpenAsync();await using var query=master.CreateCommand();query.CommandText="SELECT COUNT(*) FROM sys.databases WHERE name=@name";query.Parameters.AddWithValue("@name",connection.InitialCatalog);
            if(Convert.ToInt32(await query.ExecuteScalarAsync())!=0)throw new InvalidOperationException("Existing database rejected before mutation.");
        }
        AlFalahDbContext Db()=>new(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection.ConnectionString).Options);
        await using var db=Db();var migrator=db.GetService<IMigrator>();var previous=db.Database.GetMigrations().TakeWhile(m=>!m.EndsWith("AddTeachingAssignmentStatements")).Last();
        await migrator.MigrateAsync(previous);
        db.Users.AddRange(new(){Id="manager",UserName="statement-manager",FirstName="Synthetic",LastName="Manager",IsActive=true},new(){Id="teacher",UserName="statement-teacher",FirstName="Synthetic",LastName="Teacher",IsActive=true});
        var school=new School{Name="Synthetic statement school",Stage=SchoolStage.Intermediate,City="Test",ManagerUserId="manager"};db.Add(school);
        if(!await db.AcademicYears.AnyAsync(x=>x.Id==1))db.Add(new AcademicYear{Code="statement-test",NameAr="1448 - 1449",StartsOn=new(2026,8,1),EndsOn=new(2027,7,31),IsActive=true});
        await db.SaveChangesAsync();school.Id.Should().Be(1);
        var timing=await BellScheduleTestData.SeedAsync(db,school.Id);
        var instructor=new InstructorProfile{SchoolId=school.Id,UserId="teacher",IsActive=true,SubjectSpecialization="Math"};db.Add(instructor);await db.SaveChangesAsync();
        var teacher=new TeacherTimetableProfile{SchoolId=school.Id,TimetableSetupProfileId=1,InstructorProfileId=instructor.Id,BellScheduleRevisionId=timing.Id,MaximumWeeklyPeriods=24,CreatedByUserId="manager",UpdatedByUserId="manager"};
        var classroom=new Classroom{SchoolId=school.Id,AcademicYearId=1,Stage=SchoolStage.Intermediate,GradeLevel=1,Section="1",SectionNumber=1,ClassLabel="1/1",CreatedByUserId="manager",UpdatedByUserId="manager"};
        var subject=new SubjectDefinition{SchoolId=school.Id,Name="Math",UpdatedByUserId="manager"};db.AddRange(teacher,classroom,subject);await db.SaveChangesAsync();
        var requirement=new ClassSubjectRequirement{SchoolId=school.Id,TimetableSetupProfileId=1,ClassroomId=classroom.Id,SubjectId=subject.Id,IndividualPeriodCount=2,PairedBlockCount=1,UpdatedByUserId="manager"};db.Add(requirement);await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO TeachingAssignments (SchoolId,TimetableSetupProfileId,ClassSubjectRequirementId,Mode,IsDeleted,Revision,CreatedAt,UpdatedAt,UpdatedByUserId) VALUES ({school.Id},1,{requirement.Id},'SingleTeacher',0,1,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET(),'manager')");
        var assignmentId=await db.Set<TeachingAssignment>().Select(x=>x.Id).SingleAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO TeachingAssignmentMembers (SchoolId,TimetableSetupProfileId,TeachingAssignmentId,TeacherTimetableProfileId,AllocatedPeriodCount,AllocatedPairedBlockCount) VALUES ({school.Id},1,{assignmentId},{teacher.Id},4,1)");
        await db.Database.MigrateAsync();await db.Database.MigrateAsync();db.ChangeTracker.Clear();
        var member=await db.Set<TeachingAssignmentMember>().SingleAsync();member.AllocatedPeriodCount.Should().Be(4);member.AllocatedPairedBlockCount.Should().Be(1);member.StatementCategory.Should().BeNull();
        var assignments=new TeachingAssignmentService(new TeachingAssignmentRepository(db),new(new SubjectRepository(db),new BellScheduleRepository(db)));
        var overview=await assignments.SaveAsync(school.Id,1,new(1,[new(requirement.Id,"SingleTeacher",[new(teacher.Id,4,1,"Specialization")])]),"manager",default);
        QuestPDF.Settings.License=LicenseType.Community;var repository=new TeachingStatementRepository(db);var service=new TeachingStatementService(repository,new TeachingStatementRenderer());
        var file=await service.GetAsync(school.Id,1,overview.Revision,"all","manager",default);file.Content.Length.Should().BeGreaterThan(10000);
        var frozen=await db.Set<TeachingAssignmentStatement>().AsNoTracking().SingleAsync();frozen.SnapshotJson.Should().Contain("Specialization");
        await FluentActions.Invoking(()=>service.GetAsync(999,1,overview.Revision,"all","manager",default)).Should().ThrowAsync<KeyNotFoundException>();
        var source=await repository.CaptureAsync(school.Id,1,default);source.Classes.Single().Code.Should().Be("1/1");source.Header.School.Should().Be(school.Name);
        db.ChangeTracker.Clear();var setup=await db.TimetableSetupProfiles.SingleAsync();setup.Revision++;await db.SaveChangesAsync();
        (await service.GetAsync(school.Id,1,overview.Revision,"all","manager",default)).Content.Should().Equal(file.Content);
        await FluentActions.Invoking(()=>service.GetAsync(school.Id,1,overview.Revision,"stage:2","manager",default)).Should().ThrowAsync<BellScheduleConflictException>();
        var altered=await db.Set<TeachingAssignmentStatement>().SingleAsync();altered.FileName="changed";await FluentActions.Invoking(()=>db.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();db.ChangeTracker.Clear();
        var duplicate=new TeachingAssignmentStatement{SchoolId=school.Id,TimetableSetupProfileId=1,Revision=frozen.Revision,Scope=frozen.Scope,RendererVersion=frozen.RendererVersion,SnapshotJson="{}",Pdf=[1],Sha256="bad",FileName="duplicate.pdf"};
        (await repository.StoreAsync(duplicate,default)).Pdf.Should().Equal(file.Content);
        var foreign=new School{Name="Foreign statement",City="Test",Stage=SchoolStage.Intermediate};db.Add(foreign);await db.SaveChangesAsync();
        var forged=new TeachingAssignmentStatement{SchoolId=foreign.Id,TimetableSetupProfileId=1,Revision=99,RendererVersion="test",SnapshotJson="{}",Pdf=[1],Sha256="x",FileName="x"};db.Add(forged);
        await FluentActions.Invoking(()=>db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();db.ChangeTracker.Clear();
        await FluentActions.Invoking(()=>migrator.MigrateAsync(previous)).Should().ThrowAsync<SqlException>();
        (await db.Database.GetAppliedMigrationsAsync()).Last().Should().EndWith("AddTeachingAssignmentStatements");
    }
}
