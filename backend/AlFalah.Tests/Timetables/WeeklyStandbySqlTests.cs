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

namespace AlFalah.Tests.Timetables;

public sealed class StandbySqlFactAttribute : FactAttribute
{
    public StandbySqlFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALFALAH_STANDBY_TEST_CONNECTION"))) Skip = "Set a fresh isolated AlFalahStandbyTests_<guid> LocalDB connection."; }
}
public sealed class WeeklyStandbySqlTests
{
    [StandbySqlFact]
    public async Task Fresh_SQL_migration_and_reviewed_apply_preserve_teaching_and_enforce_stale_proposals()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ALFALAH_STANDBY_TEST_CONNECTION"));
        if (!connection.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) || !connection.IntegratedSecurity ||
            !System.Text.RegularExpressions.Regex.IsMatch(connection.InitialCatalog, "^AlFalahStandbyTests_[a-f0-9]{32}$")) throw new InvalidOperationException("Fresh isolated integrated LocalDB required.");
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync(); await using var query = master.CreateCommand(); query.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name=@name"; query.Parameters.AddWithValue("@name", connection.InitialCatalog);
            if (Convert.ToInt32(await query.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("Existing database rejected before mutation.");
        }
        AlFalahDbContext Db() => new(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection.ConnectionString).Options);
        await using var db = Db(); await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        db.Users.AddRange(new() { Id = "manager", UserName = "standby-manager", FirstName = "Synthetic", LastName = "Manager", IsActive = true },
            new() { Id = "teacher", UserName = "standby-teacher", FirstName = "Synthetic", LastName = "Teacher", IsActive = true });
        var school = new School { Name = "Synthetic standby school", Stage = SchoolStage.Intermediate, City = "Test", ManagerUserId = "manager" }; db.Add(school);
        if (!await db.AcademicYears.AnyAsync(x => x.Id == 1)) db.Add(new AcademicYear { Code = "standby-test", NameAr = "2026-2027", StartsOn = new(2026, 8, 1), EndsOn = new(2027, 7, 31), IsActive = true });
        await db.SaveChangesAsync(); school.Id.Should().Be(1);
        var timing = await BellScheduleTestData.SeedAsync(db);
        var instructor = new InstructorProfile { SchoolId = 1, UserId = "teacher", IsActive = true, SubjectSpecialization = "Math" }; db.Add(instructor); await db.SaveChangesAsync();
        var teacher = new TeacherTimetableProfile { SchoolId = 1, TimetableSetupProfileId = 1, InstructorProfileId = instructor.Id, BellScheduleRevisionId = timing.Id, MaximumWeeklyPeriods = 20, CreatedByUserId = "manager", UpdatedByUserId = "manager" };
        var classroom = new Classroom { SchoolId = 1, AcademicYearId = 1, Stage = SchoolStage.Intermediate, GradeLevel = 1, Section = "1", SectionNumber = 1, ClassLabel = "1/1", CreatedByUserId = "manager", UpdatedByUserId = "manager" };
        var subject = new SubjectDefinition { SchoolId = 1, Name = "Math", UpdatedByUserId = "manager" }; db.AddRange(teacher, classroom, subject); await db.SaveChangesAsync();
        var requirement = new ClassSubjectRequirement { SchoolId = 1, TimetableSetupProfileId = 1, ClassroomId = classroom.Id, SubjectId = subject.Id, IndividualPeriodCount = 1, UpdatedByUserId = "manager" }; db.Add(requirement); await db.SaveChangesAsync();
        db.Add(new TeachingAssignment { SchoolId = 1, TimetableSetupProfileId = 1, ClassSubjectRequirementId = requirement.Id, UpdatedByUserId = "manager",
            Members = [new() { SchoolId = 1, TimetableSetupProfileId = 1, TeacherTimetableProfileId = teacher.Id, AllocatedPeriodCount = 1 }] });
        var timetable = new SchoolTimetable { SchoolId = 1, AcademicYearId = 1, Semester = TimetableSemester.First, TimetableSetupProfileId = 1,
            BellScheduleRevisionId = timing.Id, Title = "Synthetic standby timetable", CreatedByUserId = "manager", UpdatedByUserId = "manager",
            Entries = [new() { SchoolId = 1, InstructorProfileId = instructor.Id, Day = TimetableDay.Sunday, Period = 1, EntryType = TimetableEntryType.Lesson,
                ClassroomId = classroom.Id, ClassLabel = "1/1", SubjectId = subject.Id, Subject = "Math", ClassSubjectRequirementId = requirement.Id }] };
        db.Add(timetable); await db.SaveChangesAsync(); var lessonId = timetable.Entries.Single().Id; db.ChangeTracker.Clear();
        var service = WeeklyStandbyTests.Service(db);
        var request = new WeeklyStandbyRequest(1, "Manual", new(1, [new(2, 2, 1)]), [new(instructor.Id, 2, 2, true)]);
        var preview = await service.PreviewAsync(timetable.Id, request, default);
        (await db.SchoolTimetableEntries.CountAsync()).Should().Be(1);
        await service.ApplyAsync(timetable.Id, request with { ProposalId = preview.ProposalId }, default);
        db.ChangeTracker.Clear();
        var rows = await db.SchoolTimetableEntries.OrderBy(x => x.Id).ToArrayAsync();
        rows.Should().HaveCount(2); rows[0].Id.Should().Be(lessonId); rows[0].EntryType.Should().Be(TimetableEntryType.Lesson);
        rows[1].IsStandbyLocked.Should().BeTrue(); rows[1].StandbySource.Should().Be("Manual");
        (await db.SchoolTimetableVersions.CountAsync()).Should().Be(1);
        var next = request with { Revision = 2 };
        preview = await service.PreviewAsync(timetable.Id, next, default); db.ChangeTracker.Clear();
        await using (var other = Db()) { var profile = await other.TeacherTimetableProfiles.SingleAsync(); profile.Revision++; await other.SaveChangesAsync(); }
        await service.Invoking(x => x.ApplyAsync(timetable.Id, next with { ProposalId = preview.ProposalId }, default)).Should().ThrowAsync<BellScheduleConflictException>();
        db.ChangeTracker.Clear();
        (await db.SchoolTimetables.SingleAsync()).Revision.Should().Be(2);
        (await db.SchoolTimetableVersions.CountAsync()).Should().Be(1);
        var user = new WeeklyStandbyTests.User(); var repo = new TimetableReviewRepository(db); var validator = new TimetableValidationEngine();
        await new TimetableReviewService(repo, validator, new(validator), user).PublishAsync(timetable.Id, 2, default);
        db.ChangeTracker.Clear();
        (await db.SchoolTimetables.SingleAsync()).IsPublished.Should().BeTrue();
        (await db.SchoolTimetableEntries.SingleAsync(x => x.EntryType == TimetableEntryType.Standby)).Id.Should().Be(rows[1].Id);
        await WeeklyStandbyTests.Service(db, new(School: 999)).Invoking(x => x.GetAsync(timetable.Id, default)).Should().ThrowAsync<KeyNotFoundException>();
    }
}
