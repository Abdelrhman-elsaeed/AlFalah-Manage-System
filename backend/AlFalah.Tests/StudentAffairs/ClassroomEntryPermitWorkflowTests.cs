using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Permits;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.Permits;
using AlFalah.Application.StudentAffairs.Permits.Handlers;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class ClassroomEntryPermitWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 6, 15, 0, TimeSpan.Zero);

    [Fact]
    public async Task OfficerWithPermission_CreatesLivePermitFromCanonicalLessonSnapshot()
    {
        var repository = new FakeRepository
        {
            Enrollment = new ClassroomEntryPermitEnrollmentSnapshot(4, 1, TimetableSemester.First, 12)
        };
        var handler = new CreateClassroomEntryPermitCommandHandler(
            repository,
            new ScheduleRepository([Candidate()]),
            new LessonResolver(ActiveLesson()),
            User(RoleNames.StudentAffairsOfficer, PermissionNames.ClassroomEntryPermitIssue),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new CreateClassroomEntryPermitCommand(
            new CreateClassroomEntryPermitRequestDto(17, "Late arrival", Now.AddMinutes(-5), Now.AddMinutes(20))),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        repository.Added.Should().NotBeNull();
        repository.Added!.SchoolTimetableId.Should().Be(20);
        repository.Added.SchoolTimetableEntryId.Should().Be(21);
        repository.Added.TargetInstructorProfileId.Should().Be(7);
        repository.Added.ClassroomId.Should().Be(12);
        repository.Added.IssuedAt.Should().Be(Now);
        repository.Added.Status.Should().Be(ClassroomEntryPermitStatus.Issued);
        repository.Added.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ClassroomEntryPermitIssuedEvent>();
    }

    [Theory]
    [InlineData(RoleNames.StudentAffairsOfficer, false)]
    [InlineData(RoleNames.Secretary, true)]
    [InlineData(RoleNames.Instructor, true)]
    [InlineData(RoleNames.SchoolManager, true)]
    public async Task Create_RequiresExactOfficerRoleAndPermission(string role, bool hasPermission)
    {
        var permissions = hasPermission ? new[] { PermissionNames.ClassroomEntryPermitIssue } : Array.Empty<string>();
        var repository = new FakeRepository();
        var handler = new CreateClassroomEntryPermitCommandHandler(
            repository,
            new ScheduleRepository([]),
            new LessonResolver(ActiveLesson()),
            User(role, permissions),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new CreateClassroomEntryPermitCommand(
            new CreateClassroomEntryPermitRequestDto(17, "Reason", Now.AddMinutes(-1), Now.AddMinutes(10))),
            CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        repository.Added.Should().BeNull();
    }

    [Theory]
    [InlineData(CurrentLessonResolutionKind.Break)]
    [InlineData(CurrentLessonResolutionKind.Gap)]
    [InlineData(CurrentLessonResolutionKind.OutsideSchoolHours)]
    [InlineData(CurrentLessonResolutionKind.NonStudyDay)]
    [InlineData(CurrentLessonResolutionKind.NoPublishedSchedule)]
    [InlineData(CurrentLessonResolutionKind.AmbiguousPublishedSchedule)]
    public async Task Create_FailsClosedWithoutAnActiveCanonicalLesson(CurrentLessonResolutionKind kind)
    {
        var repository = new FakeRepository
        {
            Enrollment = new ClassroomEntryPermitEnrollmentSnapshot(4, 1, TimetableSemester.First, 12)
        };
        var handler = new CreateClassroomEntryPermitCommandHandler(
            repository,
            new ScheduleRepository([Candidate()]),
            new LessonResolver(ActiveLesson() with { Kind = kind, ResolutionReason = kind.ToString() }),
            User(RoleNames.StudentAffairsOfficer, PermissionNames.ClassroomEntryPermitIssue),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new CreateClassroomEntryPermitCommand(
            new CreateClassroomEntryPermitRequestDto(17, "Reason", Now.AddMinutes(-1), Now.AddMinutes(10))),
            CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        repository.SaveCount.Should().Be(0);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(6, 5)]
    [InlineData(-5, -1)]
    public async Task Create_RejectsInvalidOrNonCurrentValidityWindow(int fromMinutes, int untilMinutes)
    {
        var repository = new FakeRepository();
        var handler = new CreateClassroomEntryPermitCommandHandler(
            repository,
            new ScheduleRepository([Candidate()]),
            new LessonResolver(ActiveLesson()),
            User(RoleNames.StudentAffairsOfficer, PermissionNames.ClassroomEntryPermitIssue),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new CreateClassroomEntryPermitCommand(
            new CreateClassroomEntryPermitRequestDto(
                17, "Reason", Now.AddMinutes(fromMinutes), Now.AddMinutes(untilMinutes))),
            CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        repository.Added.Should().BeNull();
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task EquivalentCreate_ReturnsExistingPermitWithoutDuplicateEffects()
    {
        var existing = Permit();
        var repository = new FakeRepository
        {
            Enrollment = new ClassroomEntryPermitEnrollmentSnapshot(4, 1, TimetableSemester.First, 12),
            Tracked = existing,
            EquivalentPermitId = existing.Id
        };
        var handler = new CreateClassroomEntryPermitCommandHandler(
            repository,
            new ScheduleRepository([Candidate()]),
            new LessonResolver(ActiveLesson()),
            User(RoleNames.StudentAffairsOfficer, PermissionNames.ClassroomEntryPermitIssue),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new CreateClassroomEntryPermitCommand(
            new CreateClassroomEntryPermitRequestDto(17, "Retry", existing.ValidFrom, existing.ValidUntil)),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        response.Data!.Id.Should().Be(existing.Id);
        repository.Added.Should().BeNull();
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task OnlySnapshottedTargetInstructorCanAcknowledge_AndStaleVersionDoesNotMutate()
    {
        var permit = Permit();
        var repository = new FakeRepository { Tracked = permit, InstructorProfileId = 7 };
        var handler = new AcknowledgeClassroomEntryPermitCommandHandler(
            repository,
            User(RoleNames.Instructor, PermissionNames.ClassroomEntryPermitAcknowledge),
            new FixedTimeProvider(Now));

        var stale = await handler.Handle(new AcknowledgeClassroomEntryPermitCommand(
            permit.Id, new AcknowledgeClassroomEntryPermitRequestDto(Convert.ToBase64String([9]))),
            CancellationToken.None);
        stale.IsSuccess.Should().BeFalse();
        repository.SaveCount.Should().Be(0);
        permit.Status.Should().Be(ClassroomEntryPermitStatus.Issued);

        var success = await handler.Handle(new AcknowledgeClassroomEntryPermitCommand(
            permit.Id, new AcknowledgeClassroomEntryPermitRequestDto(Convert.ToBase64String(permit.RowVersion))),
            CancellationToken.None);
        success.IsSuccess.Should().BeTrue();
        permit.Status.Should().Be(ClassroomEntryPermitStatus.AcknowledgedByTeacher);
        permit.AcknowledgedByTeacherUserId.Should().Be("actor");
        permit.AcknowledgedAt.Should().Be(Now);
        repository.SaveCount.Should().Be(1);

        permit.Status = ClassroomEntryPermitStatus.Issued;
        repository.InstructorProfileId = 99;
        var unrelated = await handler.Handle(new AcknowledgeClassroomEntryPermitCommand(
            permit.Id, new AcknowledgeClassroomEntryPermitRequestDto(Convert.ToBase64String(permit.RowVersion))),
            CancellationToken.None);
        unrelated.IsSuccess.Should().BeFalse();
        repository.SaveCount.Should().Be(1);
    }

    [Theory]
    [InlineData(RoleNames.Instructor, PermissionNames.ClassroomEntryPermitView)]
    [InlineData(RoleNames.Secretary, PermissionNames.ClassroomEntryPermitAcknowledge)]
    public async Task Acknowledge_RequiresExactInstructorRoleAndPermission(string role, string permission)
    {
        var permit = Permit();
        var repository = new FakeRepository { Tracked = permit };
        var handler = new AcknowledgeClassroomEntryPermitCommandHandler(
            repository,
            User(role, permission),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new AcknowledgeClassroomEntryPermitCommand(
            permit.Id, new AcknowledgeClassroomEntryPermitRequestDto(Convert.ToBase64String(permit.RowVersion))),
            CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        permit.Status.Should().Be(ClassroomEntryPermitStatus.Issued);
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Acknowledge_IsIdempotent_AndRejectsExpiredOrRevokedPermits()
    {
        var permit = Permit();
        var repository = new FakeRepository { Tracked = permit };
        var handler = new AcknowledgeClassroomEntryPermitCommandHandler(
            repository,
            User(RoleNames.Instructor, PermissionNames.ClassroomEntryPermitAcknowledge),
            new FixedTimeProvider(Now));
        var command = new AcknowledgeClassroomEntryPermitCommand(
            permit.Id, new AcknowledgeClassroomEntryPermitRequestDto(Convert.ToBase64String(permit.RowVersion)));

        var first = await handler.Handle(command, CancellationToken.None);
        var duplicate = await handler.Handle(command, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        duplicate.IsSuccess.Should().BeTrue();
        repository.SaveCount.Should().Be(1);

        permit.Status = ClassroomEntryPermitStatus.Revoked;
        permit.AcknowledgedByTeacherUserId = null;
        permit.AcknowledgedAt = null;
        var revoked = await handler.Handle(command, CancellationToken.None);
        permit.Status = ClassroomEntryPermitStatus.Issued;
        permit.ValidUntil = Now;
        var expired = await handler.Handle(command, CancellationToken.None);

        revoked.IsSuccess.Should().BeFalse();
        expired.IsSuccess.Should().BeFalse();
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task OfficerRevoke_RecordsServerActorTimeAndReason()
    {
        var permit = Permit();
        var repository = new FakeRepository { Tracked = permit };
        var handler = new RevokeClassroomEntryPermitCommandHandler(
            repository,
            User(RoleNames.StudentAffairsOfficer, PermissionNames.ClassroomEntryPermitRevoke),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new RevokeClassroomEntryPermitCommand(
            permit.Id,
            new RevokeClassroomEntryPermitRequestDto("Entered already", Convert.ToBase64String(permit.RowVersion))),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        permit.Status.Should().Be(ClassroomEntryPermitStatus.Revoked);
        permit.RevokedByUserId.Should().Be("actor");
        permit.RevokedAt.Should().Be(Now);
        permit.RevocationReason.Should().Be("Entered already");
    }

    [Theory]
    [InlineData(RoleNames.Instructor, ClassroomEntryPermitViewerScope.Instructor)]
    [InlineData(RoleNames.Guardian, ClassroomEntryPermitViewerScope.Guardian)]
    [InlineData(RoleNames.StudentAffairsOfficer, ClassroomEntryPermitViewerScope.Officer)]
    public async Task List_DelegatesServerSideObjectScope(string role, ClassroomEntryPermitViewerScope expectedScope)
    {
        var repository = new FakeRepository();
        var handler = new GetClassroomEntryPermitsQueryHandler(
            repository,
            User(role, PermissionNames.ClassroomEntryPermitView),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(
            new GetClassroomEntryPermitsQuery(new ClassroomEntryPermitListQuery()),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        repository.LastViewerScope.Should().Be(expectedScope);
    }

    private static CurrentLessonResolution ActiveLesson() => new(
        CurrentLessonResolutionKind.ActiveLesson,
        new DateOnly(2026, 9, 28),
        new DateTimeOffset(2026, 9, 28, 9, 15, 0, TimeSpan.FromHours(3)),
        "Africa/Cairo",
        1,
        TimetableSemester.First,
        5,
        20,
        3,
        21,
        2,
        Now.AddMinutes(-15),
        Now.AddMinutes(30),
        new CurrentLessonClassroom(12, "1/A", SchoolStage.Primary, 1, "A"),
        new CurrentLessonInstructor(6, "original", "Original"),
        new CurrentLessonInstructor(7, "actor", "Substitute"),
        77,
        "Active substitution lesson");

    private static PublishedBellScheduleCandidate Candidate() => new(20, 3, new BellScheduleDto(
        2,
        5,
        1,
        1,
        TimetableSemester.First,
        "Reference schedule",
        1,
        "Africa/Cairo",
        [new BellPeriodDto(2, "Period 2", new TimeOnly(9, 0), new TimeOnly(9, 45))],
        Enumerable.Range(1, 7).Select(day => new BellScheduleDayDto(day, true, true, [])).ToArray(),
        [],
        []));

    private static ClassroomEntryPermit Permit() => new()
    {
        Id = 5,
        SchoolId = 42,
        StudentId = 17,
        AcademicTermId = 4,
        ClassroomId = 12,
        IssuedByStudentAffairsUserId = "officer",
        IssuedAt = Now.AddMinutes(-5),
        Reason = "Reason",
        ValidFrom = Now.AddMinutes(-5),
        ValidUntil = Now.AddMinutes(20),
        SchoolTimetableId = 20,
        SchoolTimetableEntryId = 21,
        TargetInstructorProfileId = 7,
        Status = ClassroomEntryPermitStatus.Issued,
        RowVersion = [1, 2, 3],
        CreatedByUserId = "officer",
        UpdatedByUserId = "officer"
    };

    private static TestCurrentUser User(string role, params string[] permissions) =>
        new(role, permissions);

    private sealed class TestCurrentUser(string role, string[] permissions) : ICurrentUserService
    {
        public string? UserId => "actor";
        public string? Username => "actor.test";
        public int? ActiveSchoolId => 42;
        public string? PreferredLanguage => "en";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == role;
        public bool HasPermission(string permissionName) => permissions.Contains(permissionName);
        public IEnumerable<string> GetRoles() => [role];
        public IEnumerable<string> GetPermissions() => permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class LessonResolver(CurrentLessonResolution resolution) : ICurrentLessonResolver
    {
        public Task<CurrentLessonResolution> ResolveForClassroomAsync(
            int schoolId, DateTimeOffset instant, int classroomId, string? classroomLabel,
            CancellationToken cancellationToken) => Task.FromResult(resolution);
        public Task<CurrentLessonResolution> ResolveForInstructorAsync(
            int schoolId, DateTimeOffset instant, string instructorUserId,
            CancellationToken cancellationToken) => Task.FromResult(resolution);
    }

    private sealed class ScheduleRepository(IReadOnlyList<PublishedBellScheduleCandidate> candidates)
        : IBellScheduleRepository
    {
        public Task<IReadOnlyList<PublishedBellScheduleCandidate>> GetPublishedCandidatesAsync(
            int schoolId, DateTimeOffset instant, CancellationToken ct) => Task.FromResult(candidates);
        public Task<BellScheduleDto?> GetPublishedAsync(int schoolId, DateTimeOffset instant, CancellationToken ct) =>
            Task.FromResult(candidates.Count == 1 ? candidates[0].Schedule : null);
        public Task<IReadOnlyList<BellScheduleDto>> ListAsync(int schoolId, int yearId, TimetableSemester semester, CancellationToken ct) => throw new NotSupportedException();
        public Task<BellScheduleTemplate?> FindAsync(int schoolId, int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> NameExistsAsync(int schoolId, SaveBellScheduleRequest request, int? exceptId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BellScheduleDependencies> GetDependenciesAsync(int schoolId, int? templateId, int? profileId, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveRevisionAsync(BellScheduleTemplate template, BellScheduleRevision revision, bool isNew, CancellationToken ct) => throw new NotSupportedException();
        public Task SelectAsync(TimetableSetupProfile profile, BellScheduleTemplate template, CancellationToken ct) => throw new NotSupportedException();
        public Task<BellScheduleDto?> GetRevisionAsync(int schoolId, int revisionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BellScheduleDto?> GetSelectedAsync(int schoolId, int yearId, TimetableSemester semester, int? profileId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRepository : IClassroomEntryPermitWorkflowRepository
    {
        public ClassroomEntryPermitEnrollmentSnapshot? Enrollment { get; init; }
        public ClassroomEntryPermit? Tracked { get; init; }
        public ClassroomEntryPermit? Added { get; private set; }
        public int? InstructorProfileId { get; set; } = 7;
        public int? EquivalentPermitId { get; init; }
        public int SaveCount { get; private set; }
        public ClassroomEntryPermitViewerScope? LastViewerScope { get; private set; }

        public Task<ClassroomEntryPermitEnrollmentSnapshot?> GetActiveEnrollmentAsync(
            int schoolId, int studentId, int? classroomId, int academicYearId, TimetableSemester semester,
            DateOnly onDate, CancellationToken cancellationToken) => Task.FromResult(Enrollment);
        public Task<int?> FindEquivalentActivePermitIdAsync(
            int schoolId, int studentId, int timetableEntryId, DateTimeOffset validFrom,
            DateTimeOffset validUntil, CancellationToken cancellationToken) => Task.FromResult(EquivalentPermitId);
        public Task<ClassroomEntryPermit?> GetForUpdateAsync(
            int schoolId, int permitId, CancellationToken cancellationToken) => Task.FromResult(Tracked);
        public Task<int?> GetInstructorProfileIdAsync(
            int schoolId, string teacherUserId, CancellationToken cancellationToken) => Task.FromResult(InstructorProfileId);
        public Task<ClassroomEntryPermitSaveResult> SaveNewPermitAsync(
            ClassroomEntryPermit permit, CancellationToken cancellationToken)
        {
            if (EquivalentPermitId is { } existingId)
                return Task.FromResult(new ClassroomEntryPermitSaveResult(existingId, false));
            Add(permit);
            SaveCount++;
            return Task.FromResult(new ClassroomEntryPermitSaveResult(permit.Id, true));
        }
        public void Add(ClassroomEntryPermit permit)
        {
            permit.Id = 5;
            permit.RowVersion = [1, 2, 3];
            Added = permit;
        }
        public void SetExpectedRowVersion(ClassroomEntryPermit permit, byte[] rowVersion) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
        public Task<ClassroomEntryPermitDto?> GetDtoAsync(
            int schoolId, int permitId, ClassroomEntryPermitViewerScope scope, string userId,
            DateTimeOffset utcNow, CancellationToken cancellationToken)
        {
            LastViewerScope = scope;
            var permit = Added ?? Tracked ?? Permit();
            return Task.FromResult<ClassroomEntryPermitDto?>(Dto(permit, utcNow));
        }
        public Task<PagedResult<ClassroomEntryPermitDto>> GetPermitsAsync(
            int schoolId, ClassroomEntryPermitListQuery query, ClassroomEntryPermitViewerScope scope,
            string userId, DateTimeOffset utcNow, CancellationToken cancellationToken)
        {
            LastViewerScope = scope;
            return Task.FromResult(new PagedResult<ClassroomEntryPermitDto>());
        }

        private static ClassroomEntryPermitDto Dto(ClassroomEntryPermit permit, DateTimeOffset now) => new(
            permit.Id,
            new StudentSummaryDto(permit.StudentId, "ST-17", "Student", permit.ClassroomId, "1/A", true, null),
            permit.Reason,
            permit.IssuedAt,
            permit.ValidFrom,
            permit.ValidUntil,
            permit.SchoolTimetableEntryId,
            new ClassroomSummaryDto(permit.ClassroomId, "1/A", "Primary", 1, "A"),
            new ActorSummaryDto("actor", "Teacher", RoleNames.Instructor),
            permit.ValidUntil <= now ? ClassroomEntryPermitStatus.Expired : permit.Status,
            permit.AcknowledgedByTeacherUserId is null ? null : new ActorSummaryDto("actor", "Teacher", RoleNames.Instructor),
            permit.AcknowledgedAt,
            null,
            new MetricBadgeDto(StudentTermMetricCode.ClassroomEntryPermit, 1, 1, 5, "None", permit.IssuedAt, now),
            Convert.ToBase64String(permit.RowVersion));
    }
}
