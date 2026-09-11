using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Validators.IntelligentTimetable;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class TimetableSettingsPhase1Tests
{
    [Fact]
    public void Create_validator_rejects_missing_name_and_invalid_scope()
    {
        var validator = new CreateTimetableSetupProfileRequestValidator();

        var result = validator.Validate(new CreateTimetableSetupProfileRequest(
            0,
            (TimetableSemester)99,
            "   "));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(x => x.PropertyName).Should().Contain(
            new[] { "AcademicYearId", "Semester", "Name" });
    }

    [Fact]
    public void Readiness_allows_an_active_classroom_without_students_when_location_is_present()
    {
        var profile = Profile();

        var (steps, warnings) = TimetableSettingsHandlerSupport.BuildReadiness(
            profile,
            new(ActiveClassrooms: 1, ActiveStudents: 0, ClassroomsMissingLocation: 0, ActiveTeachers: 1));

        steps.Single(x => x.Key == "classrooms").Status.Should().Be("complete");
        warnings.Should().NotContain(x => x.Contains("طالب", StringComparison.Ordinal));
    }

    [Fact]
    public void Readiness_marks_classrooms_incomplete_when_any_location_is_missing()
    {
        var (steps, warnings) = TimetableSettingsHandlerSupport.BuildReadiness(
            Profile(),
            new(ActiveClassrooms: 3, ActiveStudents: 20, ClassroomsMissingLocation: 1, ActiveTeachers: 2));

        steps.Single(x => x.Key == "classrooms").Status.Should().Be("incomplete");
        warnings.Should().Contain(x => x.Contains("1 فصل", StringComparison.Ordinal));
    }

    [Fact]
    public void Settings_access_excludes_instructors_even_if_legacy_timetable_view_is_present()
    {
        var currentUser = new TestCurrentUser(RoleNames.Instructor, PermissionNames.TimetableView);

        TimetableSettingsHandlerSupport.CanView(currentUser).Should().BeFalse();
    }

    [Fact]
    public void Ef_model_configures_tenant_key_unique_scope_and_revision_concurrency()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"timetable-settings-{Guid.NewGuid()}")
            .Options;
        using var context = new AlFalahDbContext(options);

        var profile = context.Model.FindEntityType(typeof(TimetableSetupProfile));

        profile.Should().NotBeNull();
        profile!.FindProperty(nameof(TimetableSetupProfile.Revision))!.IsConcurrencyToken.Should().BeTrue();
        profile.FindProperty(nameof(TimetableSetupProfile.Name))!.GetMaxLength().Should().Be(120);
        profile.GetIndexes().Should().Contain(index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { "SchoolId", "AcademicYearId", "Semester", "Name" }));
    }

    [Fact]
    public void Profile_list_query_filters_and_orders_entities_before_projection_to_sql_server()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=AlFalahQueryTranslationTests;Trusted_Connection=True;")
            .Options;
        using var context = new AlFalahDbContext(options);

        var sql = TimetableSettingsRepository.BuildProfilesQuery(
                context.TimetableSetupProfiles.AsNoTracking(),
                schoolId: 18,
                academicYearId: 1,
                TimetableSemester.First)
            .ToQueryString();

        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("[t].[UpdatedAt] DESC");
        sql.Should().Contain("[t].[SchoolId] = @__schoolId_0");
    }

    private static TimetableSetupProfileDto Profile() => new(
        Id: 1,
        SchoolId: 10,
        AcademicYearId: 1,
        AcademicYearName: "2026-2027",
        Semester: 1,
        SemesterLabelAr: "الفصل الدراسي الأول",
        Name: "الجدول الاعتيادي",
        BellScheduleTemplateId: null,
        Status: TimetableSetupStatus.Draft,
        StatusLabelAr: "مسودة",
        Revision: 1,
        UpdatedAt: DateTimeOffset.UtcNow);

    private sealed class TestCurrentUser(string role, params string[] permissions) : ICurrentUserService
    {
        public string? UserId => "user-1";
        public string? Username => "user";
        public int? ActiveSchoolId => 1;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == role;
        public bool HasPermission(string permissionName) => permissions.Contains(permissionName);
        public IEnumerable<string> GetRoles() => new[] { role };
        public IEnumerable<string> GetPermissions() => permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }
}
