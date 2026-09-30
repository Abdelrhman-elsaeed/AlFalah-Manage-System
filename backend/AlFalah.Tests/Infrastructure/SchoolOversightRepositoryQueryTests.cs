using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Infrastructure;

public sealed class SchoolOversightRepositoryQueryTests
{
    [Fact]
    public async Task Oversight_executes_relationally_and_aggregates_only_active_same_school_daily_facts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation("Arabic_CI_AS", (left, right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
        var options = new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlite(connection).Options;
        await using var db = new SqliteAlFalahDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

        var date = new DateOnly(2026, 9, 30);
        const int yearId = 901;
        var year = new AcademicYear { Id = yearId, Code = "W9-2026", NameAr = "2026", StartsOn = date.AddMonths(-1), EndsOn = date.AddMonths(8), IsActive = true };
        db.Schools.AddRange(
            new School { Id = 42, Name = "School A", City = "Cairo", Stage = SchoolStage.Primary },
            new School { Id = 43, Name = "School B", City = "Cairo", Stage = SchoolStage.Primary });
        db.Users.Add(new ApplicationUser { Id = "seed", UserName = "seed", FirstName = "Seed", LastName = "User", IsActive = true });
        db.AcademicYears.Add(year);
        db.AcademicTerms.AddRange(
            new AcademicTerm { Id = 1, SchoolId = 42, AcademicYearId = yearId, Semester = TimetableSemester.First, StartsOn = date.AddDays(-10), EndsOn = date.AddDays(30), IsActive = true },
            new AcademicTerm { Id = 2, SchoolId = 43, AcademicYearId = yearId, Semester = TimetableSemester.First, StartsOn = date.AddDays(-10), EndsOn = date.AddDays(30), IsActive = true });
        db.Classrooms.AddRange(
            new Classroom { Id = 1, SchoolId = 42, AcademicYearId = yearId, Stage = SchoolStage.Primary, GradeLevel = 1, Section = "A", ClassLabel = "1/A", PhysicalLocation = "1", IsActive = true },
            new Classroom { Id = 2, SchoolId = 42, AcademicYearId = yearId, Stage = SchoolStage.Primary, GradeLevel = 1, Section = "B", ClassLabel = "1/B", PhysicalLocation = "2", IsActive = true },
            new Classroom { Id = 3, SchoolId = 43, AcademicYearId = yearId, Stage = SchoolStage.Primary, GradeLevel = 1, Section = "C", ClassLabel = "1/C", PhysicalLocation = "3", IsActive = true });
        db.Students.AddRange(
            Student(1, 42, "S1"), Student(2, 42, "S2"), Student(3, 42, "S3"), Student(4, 43, "S4"));
        db.StudentEnrollments.AddRange(
            Enrollment(1, 42, 1, 1, 1, date), Enrollment(2, 42, 2, 1, 1, date),
            Enrollment(3, 42, 3, 2, 1, date), Enrollment(4, 43, 4, 3, 2, date));
        db.DailyStudentAttendances.AddRange(
            Attendance(1, 42, 1, 1, 1, date, StudentAttendanceStatus.Present),
            Attendance(2, 42, 2, 1, 1, date, StudentAttendanceStatus.Absent),
            Attendance(3, 42, 3, 2, 1, date, StudentAttendanceStatus.AbsentExcused),
            Attendance(4, 43, 4, 3, 2, date, StudentAttendanceStatus.Absent),
            Attendance(5, 42, 1, 1, 1, date.AddDays(-1), StudentAttendanceStatus.Absent));
        await db.SaveChangesAsync();

        var generatedAt = new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.Zero);
        var result = await new StudentWorkflowRepository(db)
            .GetSchoolOversightDashboardAsync(42, date, generatedAt, CancellationToken.None);

        result.Present.Should().Be(1);
        result.Absent.Should().Be(1);
        result.AbsentExcused.Should().Be(1);
        result.ByClassroom.Select(row => row.ClassLabel).Should().Equal("1/A", "1/B");
        result.ByClassroom.Sum(row => row.Present + row.Absent + row.AbsentExcused).Should().Be(3);
        result.GeneratedAt.Should().Be(generatedAt);
    }

    private static Student Student(int id, int schoolId, string number) => new()
    { Id = id, SchoolId = schoolId, StudentNumber = number, IdentityNumber = $"ID-{id}", FirstName = "Student", LastName = id.ToString(), IsActive = true };
    private static StudentEnrollment Enrollment(int id, int schoolId, int studentId, int classroomId, int termId, DateOnly date) => new()
    { Id = id, SchoolId = schoolId, StudentId = studentId, ClassroomId = classroomId, AcademicTermId = termId, EnrolledOn = date.AddDays(-5), Status = StudentEnrollmentStatus.Active };
    private static DailyStudentAttendance Attendance(int id, int schoolId, int studentId, int classroomId, int termId, DateOnly date, StudentAttendanceStatus status) => new()
    { Id = id, SchoolId = schoolId, StudentId = studentId, ClassroomId = classroomId, AcademicTermId = termId, AttendanceDate = date, Status = status, RecordedByUserId = "seed", Source = StudentAttendanceSource.SecretaryRoster };

    private sealed class SqliteAlFalahDbContext(DbContextOptions<AlFalahDbContext> options) : AlFalahDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            foreach (var entity in builder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                if (property.GetColumnType()?.Contains("max", StringComparison.OrdinalIgnoreCase) == true)
                    property.SetColumnType("TEXT");
            builder.Entity<DailyStudentAttendance>().Property(row => row.RowVersion)
                .ValueGeneratedNever().IsConcurrencyToken();
        }
    }
}
