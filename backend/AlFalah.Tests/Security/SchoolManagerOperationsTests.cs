using System.Security.Claims;
using AlFalah.Application.DTOs.Dashboards;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs.TeacherContext;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Security;

public sealed class SchoolManagerOperationsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 22, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Overview_keeps_unrecorded_students_and_deduplicates_staff_roles_inside_one_school()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var result = await Service(context, 1, RoleNames.SchoolManager, oversight: true).GetAsync();

        result.Date.Should().Be(new DateOnly(2026, 10, 12)); // Riyadh, not the UTC date.
        result.Students!.Counts.Should().Be(new AttendanceCountsDto(2, 1, 0, 0));
        result.Students.Counts.Unrecorded.Should().Be(1);
        result.Students.Counts.AttendanceRate.Should().Be(100m);
        result.Students.Counts.RecordingRate.Should().Be(50m);
        result.Students.Classrooms.Should().ContainSingle().Which.Label.Should().Be("الأول أ");
        result.Staff.Counts.Total.Should().Be(2); // Manager and teacher, despite two teacher assignments.
        result.Staff.Counts.Present.Should().Be(1);
        result.Staff.Counts.Unrecorded.Should().Be(1);
        result.Summons!.Pending.Should().Be(1);
        result.Summons.Appointments.Should().ContainSingle().Which.StudentName.Should().Be("طالب مسجل");
        result.Activities.Should().NotContain(x => x.Title.Contains("مدرسة أخرى"));
        result.GatePasses.Should().BeNull();
        result.Conversations.Should().BeNull();
    }

    [Theory]
    [InlineData(2, RoleNames.SchoolManager)]
    [InlineData(1, RoleNames.Instructor)]
    public async Task Dashboard_permission_does_not_allow_another_school_or_another_role(int schoolId, string role)
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var action = () => Service(context, schoolId, role, oversight: true).GetAsync();
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Revoked_school_assignment_is_rejected_even_with_a_still_valid_claim()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        (await context.UserSchoolRoles.SingleAsync(x => x.UserId == "manager")).IsActive = false;
        await context.SaveChangesAsync();
        var action = () => Service(context, 1, RoleNames.SchoolManager, oversight: true).GetAsync();
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Student_oversight_permission_is_required_and_private_case_fields_are_not_in_the_contract()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var result = await Service(context, 1, RoleNames.SchoolManager, oversight: false).GetAsync();
        result.Students.Should().BeNull();
        result.Summons.Should().BeNull();
        result.Activities.Should().NotContain(x => x.Category == "summons" || x.Category == "students");
        typeof(SummonAppointmentDto).GetProperties().Select(x => x.Name).Should()
            .NotContain(new[] { "CreatedReason", "GuardianProfileId", "Instructions", "ObservationNotes", "AttendanceNotes" });
    }

    private static AlFalahDbContext CreateContext() => new(new DbContextOptionsBuilder<AlFalahDbContext>()
        .UseInMemoryDatabase("manager-operations-" + Guid.NewGuid()).Options);

    private static SchoolManagerOperationsService Service(AlFalahDbContext context, int schoolId, string role, bool oversight)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "manager"), new(ClaimTypes.Role, role),
            new("active_school_id", schoolId.ToString()), new("permission", PermissionNames.DashboardSchoolManager) };
        if (oversight) claims.Add(new("permission", PermissionNames.StudentAffairsDashboardSchoolOversight));
        var current = new CurrentUserService(new HttpContextAccessor { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } });
        return new(new SchoolManagerOperationsRepository(context), current,
            new BellScheduleResolver(new BellScheduleRepository(context)),
            new TeacherContextScheduleOptions { SchoolTimeZoneId = "Asia/Riyadh" }, new FixedClock());
    }

    private static async Task SeedAsync(AlFalahDbContext context)
    {
        var date = new DateOnly(2026, 10, 12);
        var school = new School { Id = 1, Name = "مدرسة الاختبار", City = "مكة" };
        var other = new School { Id = 2, Name = "مدرسة أخرى", City = "جدة" };
        var manager = new ApplicationUser { Id = "manager", UserName = "manager", FirstName = "مدير", LastName = "المدرسة" };
        var teacher = new ApplicationUser { Id = "teacher", UserName = "teacher", FirstName = "معلم", LastName = "مسجل" };
        var managerRole = new ApplicationRole { Id = "manager-role", Name = RoleNames.SchoolManager };
        var teacherRole = new ApplicationRole { Id = "teacher-role", Name = RoleNames.Instructor };
        var moderatorRole = new ApplicationRole { Id = "moderator-role", Name = RoleNames.Moderator };
        var term = new AcademicTerm { Id = 1, SchoolId = 1, AcademicYearId = 1, IsActive = true,
            StartsOn = date.AddDays(-30), EndsOn = date.AddDays(30) };
        var otherTerm = new AcademicTerm { Id = 2, SchoolId = 2, AcademicYearId = 1, IsActive = true,
            StartsOn = date.AddDays(-30), EndsOn = date.AddDays(30) };
        var classroom = new Classroom { Id = 1, SchoolId = 1, AcademicYearId = 1, ClassLabel = "الأول أ" };
        var otherClass = new Classroom { Id = 2, SchoolId = 2, AcademicYearId = 1, ClassLabel = "مدرسة أخرى" };
        context.AddRange(school, other, manager, teacher, managerRole, teacherRole, moderatorRole, term, otherTerm, classroom, otherClass,
            new UserSchoolRole { UserId = manager.Id, SchoolId = 1, RoleId = managerRole.Id },
            new UserSchoolRole { UserId = teacher.Id, SchoolId = 1, RoleId = teacherRole.Id },
            new UserSchoolRole { UserId = teacher.Id, SchoolId = 1, RoleId = moderatorRole.Id },
            new AttendanceRecord { SchoolId = 1, UserId = teacher.Id, AttendanceDate = date, Status = AttendanceStatus.Present });
        for (var id = 1; id <= 3; id++)
        {
            var schoolId = id == 3 ? 2 : 1;
            context.AddRange(new Student { Id = id, SchoolId = schoolId, StudentNumber = id.ToString(), IdentityNumber = id.ToString(),
                FirstName = id == 3 ? "مدرسة أخرى" : "طالب", LastName = id == 1 ? "مسجل" : "غير مرصود" },
                new StudentEnrollment { StudentId = id, SchoolId = schoolId, ClassroomId = schoolId, AcademicTermId = schoolId,
                    EnrolledOn = date.AddDays(-10) });
        }
        context.AddRange(new DailyStudentAttendance { StudentId = 1, SchoolId = 1, ClassroomId = 1, AcademicTermId = 1,
                AttendanceDate = date, Status = StudentAttendanceStatus.Present },
            new DailyStudentAttendance { StudentId = 3, SchoolId = 2, ClassroomId = 2, AcademicTermId = 2,
                AttendanceDate = date, Status = StudentAttendanceStatus.Absent },
            new GuardianSummon { StudentId = 1, SchoolId = 1, AcademicTermId = 1, ScheduledAt = Now.AddHours(10), CreatedReason = "private reason" },
            new GuardianSummon { StudentId = 3, SchoolId = 2, AcademicTermId = 2, ScheduledAt = Now.AddHours(10), CreatedReason = "other-school private reason" });
        await context.SaveChangesAsync();
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
