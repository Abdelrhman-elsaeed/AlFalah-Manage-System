using AlFalah.Tests.Timetables;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs;
using AlFalah.Application.StudentAffairs.DTOs.Teacher;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
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
    public async Task TeacherClassrooms_Returns_Only_Repository_Assigned_Classrooms()
    {
        var classroom = new ClassroomDto(
            9, "E2E-1-A", SchoolStage.Primary, 1, "A", 4, "2026/2027", true, 20, string.Empty);
        var repository = new StubTeacherContextRepository
        {
            AssignedClassrooms = new[] { classroom }
        };
        var handler = new GetTeacherClassroomsQueryHandler(
            repository,
            new StubCurrentUser("teacher-user", 18, PermissionNames.TeacherQuickActionView));

        var response = await handler.Handle(new GetTeacherClassroomsQuery(), CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Data.Should().ContainSingle().Which.Should().Be(classroom);
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
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 9, 1, 5, 10, 0, TimeSpan.Zero));
        var handler = new GetTeacherTopPriorityQueryHandler(
            repository,
            currentUser,
            new StubCurrentLessonResolver(),
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
        public IReadOnlyList<ClassroomDto> AssignedClassrooms { get; init; } = Array.Empty<ClassroomDto>();

        public Task<IReadOnlyList<ClassroomDto>> GetAssignedClassroomsAsync(
            int schoolId, string teacherUserId, CancellationToken cancellationToken) =>
            Task.FromResult(AssignedClassrooms);

        public Task<bool> IsClassroomAssignedAsync(
            int schoolId, string teacherUserId, int classroomId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<TeacherContextSnapshot?> GetTopPriorityAsync(
            TeacherContextLookup lookup,
            CancellationToken cancellationToken)
        {
            Lookup = lookup;
            return Task.FromResult(Snapshot);
        }

        public Task<TeacherContextSnapshot?> GetPeriodRosterAsync(
            int schoolId,
            string teacherUserId,
            int timetableEntryId,
            DateOnly localDate,
            int? timingRevisionId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Snapshot);
        }

        public Task<IReadOnlyList<TeacherGatePassAcknowledgementDto>> GetPendingGatePassAcknowledgementsAsync(
            int schoolId, string teacherUserId, DateTimeOffset utcNow, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TeacherGatePassAcknowledgementDto>>(
                Enumerable.Range(1, Snapshot?.PendingGatePassAcknowledgements ?? 0)
                    .Select(id => new TeacherGatePassAcknowledgementDto(
                        id,
                        new StudentSummaryDto(id, $"S-{id}", "Student", 9, "E2E-1-A", true, null),
                        new ClassroomSummaryDto(9, "E2E-1-A", "Primary", 1, "A"),
                        utcNow,
                        utcNow.AddMinutes(30),
                        "Reason",
                        "Approved",
                        Convert.ToBase64String(new byte[] { 1 })))
                    .ToArray());

        public Task<IReadOnlyList<TeacherEntryPermitAcknowledgementDto>> GetPendingEntryPermitAcknowledgementsAsync(
            int schoolId, string teacherUserId, DateTimeOffset utcNow, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TeacherEntryPermitAcknowledgementDto>>(
                Enumerable.Range(1, Snapshot?.PendingEntryPermitAcknowledgements ?? 0)
                    .Select(id => new TeacherEntryPermitAcknowledgementDto(
                        id,
                        new StudentSummaryDto(id, $"S-{id}", "Student", 9, "E2E-1-A", true, null),
                        new ClassroomSummaryDto(9, "E2E-1-A", "Primary", 1, "A"),
                        utcNow,
                        utcNow.AddMinutes(30),
                        "Reason",
                        "Issued",
                        Convert.ToBase64String(new byte[] { 1 })))
                    .ToArray());
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

    private sealed class StubCurrentLessonResolver : ICurrentLessonResolver
    {
        private static readonly CurrentLessonResolution Result = new(
            CurrentLessonResolutionKind.ActiveLesson,
            new DateOnly(2026, 9, 1),
            new DateTimeOffset(2026, 9, 1, 8, 10, 0, TimeSpan.FromHours(3)),
            "Africa/Cairo",
            1,
            TimetableSemester.First,
            4,
            1,
            4,
            42,
            2,
            new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 9, 1, 8, 45, 0, TimeSpan.FromHours(3)),
            new CurrentLessonClassroom(9, "E2E-1-A", SchoolStage.Primary, 1, "A"),
            new CurrentLessonInstructor(19, "original-teacher", "Original Teacher"),
            new CurrentLessonInstructor(7, "teacher-user", "E2E Substitute Teacher"),
            77,
            "Active lesson resolved with a date-specific instructor substitution");

        public Task<CurrentLessonResolution> ResolveForClassroomAsync(int schoolId, DateTimeOffset instant, int classroomId, string? classroomLabel, CancellationToken cancellationToken) =>
            Task.FromResult(Result);

        public Task<CurrentLessonResolution> ResolveForInstructorAsync(int schoolId, DateTimeOffset instant, string instructorUserId, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }
}
