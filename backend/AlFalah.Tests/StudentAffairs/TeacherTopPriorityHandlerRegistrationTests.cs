using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs;
using AlFalah.Application.StudentAffairs.DTOs.Teacher;
using AlFalah.Application.StudentAffairs.TeacherContext;
using AlFalah.Application.StudentAffairs.TeacherContext.Handlers;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using FluentAssertions;
using MediatR;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class TeacherTopPriorityHandlerRegistrationTests
{
    [Fact]
    public void ApplicationAssembly_Contains_TeacherTopPriorityHandler()
    {
        var handlerContract = typeof(IRequestHandler<
            GetTeacherTopPriorityQuery,
            ApiResponse<TeacherTopPriorityDto>>);

        typeof(StudentAffairsAssemblyMarker).Assembly
            .GetTypes()
            .Should()
            .Contain(type => !type.IsAbstract && handlerContract.IsAssignableFrom(type));
    }

    [Fact]
    public async Task Handler_Resolves_Current_Period_And_Maps_Complete_Frontend_Context()
    {
        var repository = new StubTeacherContextRepository
        {
            Snapshot = new TeacherContextSnapshot(
                new TeacherIdentitySnapshot(7, "teacher-user", "E2E Teacher"),
                4,
                new TeacherTimetablePeriodSnapshot(
                    42,
                    2,
                    "Mathematics",
                    new TeacherClassroomSnapshot(9, "E2E-1-A", SchoolStage.Primary, 1, "A")),
                new[]
                {
                    new TeacherRosterStudentSnapshot(
                        11, "E2E-STUDENT-001", "E2E Student", 9, "E2E-1-A", true, null)
                },
                2,
                1)
        };
        var currentUser = new StubCurrentUser(
            "teacher-user",
            18,
            PermissionNames.TeacherQuickActionView,
            PermissionNames.BehaviorCreate,
            PermissionNames.AcademicConcernCreate);
        var schedule = new TeacherContextSchedule(new TeacherContextScheduleOptions
        {
            SchoolTimeZoneId = "Africa/Cairo"
        });
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 9, 1, 5, 10, 0, TimeSpan.Zero));
        var handler = new GetTeacherTopPriorityQueryHandler(
            repository,
            currentUser,
            schedule,
            timeProvider);

        var response = await handler.Handle(new GetTeacherTopPriorityQuery(), CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Data!.Context.CurrentPeriod.Should().NotBeNull();
        response.Data.Context.CurrentPeriod!.TimetableEntryId.Should().Be(42);
        response.Data.Context.CurrentPeriod.Subject.Should().Be("Mathematics");
        response.Data.Context.CurrentPeriod.Classroom.Id.Should().Be(9);
        response.Data.Context.CurrentPeriod.Classroom.Label.Should().Be("E2E-1-A");
        response.Data.Context.Roster.Should().ContainSingle(student => student.Id == 11);
        response.Data.Context.PermittedQuickActions.Should().BeEquivalentTo(
            PermissionNames.BehaviorCreate,
            PermissionNames.AcademicConcernCreate);
        response.Data.PendingGatePassAcknowledgements.Should().Be(2);
        response.Data.PendingEntryPermitAcknowledgements.Should().Be(1);
        repository.Lookup!.SchoolLocalDay.Should().Be(TimetableDay.Tuesday);
        repository.Lookup.CurrentPeriod.Should().Be(2);
        repository.Lookup.AllowOffHoursFallback.Should().BeFalse();
    }

    private sealed class StubTeacherContextRepository : ITeacherContextRepository
    {
        public TeacherContextSnapshot? Snapshot { get; init; }
        public TeacherContextLookup? Lookup { get; private set; }

        public Task<TeacherContextSnapshot?> GetTopPriorityAsync(
            TeacherContextLookup lookup,
            CancellationToken cancellationToken)
        {
            Lookup = lookup;
            return Task.FromResult(Snapshot);
        }
    }

    private sealed class StubCurrentUser(
        string userId,
        int schoolId,
        params string[] permissions) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? Username => "teacher.test";
        public int? ActiveSchoolId => schoolId;
        public string? PreferredLanguage => "en";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == RoleNames.Instructor;
        public bool HasPermission(string permissionName) => permissions.Contains(permissionName);
        public IEnumerable<string> GetRoles() => new[] { RoleNames.Instructor };
        public IEnumerable<string> GetPermissions() => permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
