using System.Text.Json;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Behaviors;
using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Application.StudentAffairs.Summons;
using AlFalah.Application.StudentAffairs.Summons.Handlers;
using AlFalah.Application.StudentAffairs.TeacherActions;
using AlFalah.Application.StudentAffairs.TeacherActions.Handlers;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SummonTransitionHandler = AlFalah.Application.StudentAffairs.Summons.Handlers.MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3;

namespace AlFalah.Tests.StudentAffairs;

public sealed class TeacherActionsAndSummonsWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TeacherQuickActions_UseScopedTimetableAndAppendOnlyFactEvents()
    {
        var repository = new FakeTeacherActionRepository
        {
            Scope = new TeacherActionScopeSnapshot(7, 4, 12, 20, 21, 2)
        };
        var time = new FixedTimeProvider(Now);

        var behaviorResponse = await new CreateBehaviorIncidentCommandHandler(
            repository,
            CurrentUser(RoleNames.Instructor, PermissionNames.BehaviorCreate),
            time).Handle(new CreateBehaviorIncidentCommand(new CreateBehaviorIncidentRequestDto(
                17, 21, "Conduct", BehaviorSeverity.Medium, "Incident", Now, "Class", "Warning")),
                CancellationToken.None);
        var concernResponse = await new CreateAcademicConcernCommandHandler(
            repository,
            CurrentUser(RoleNames.Instructor, PermissionNames.AcademicConcernCreate),
            time).Handle(new CreateAcademicConcernCommand(new CreateAcademicConcernRequestDto(
                17, 21, "Progress", "Concern", Now)), CancellationToken.None);
        var delayResponse = await new CreateSessionDelayCommandHandler(
            repository,
            CurrentUser(RoleNames.Instructor, PermissionNames.SessionDelayCreate),
            time).Handle(new CreateSessionDelayCommand(new CreateSessionDelayRequestDto(
                17, 21, Now, 5, "Late")), CancellationToken.None);

        behaviorResponse.IsSuccess.Should().BeTrue();
        concernResponse.IsSuccess.Should().BeTrue();
        delayResponse.IsSuccess.Should().BeTrue();
        repository.SchoolIds.Should().OnlyContain(id => id == 42);
        repository.SaveCount.Should().Be(3);
        repository.Behavior!.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BehaviorIncidentLoggedEvent>();
        repository.Concern!.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AcademicConcernLoggedEvent>();
        repository.Delay!.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SessionDelayLoggedEvent>();
        repository.Behavior.GuardianDispatchDecision.Should()
            .Be(GuardianDispatchDecision.PendingOfficerDecision);
        repository.Delay.GuardianNotificationStatus.Should().Be(GuardianNotificationStatus.Pending);
    }

    [Fact]
    public async Task TeacherQuickAction_WhenTimetableScopeCannotBeProven_DoesNotSave()
    {
        var repository = new FakeTeacherActionRepository();
        var handler = new CreateSessionDelayCommandHandler(
            repository,
            CurrentUser(RoleNames.Instructor, PermissionNames.SessionDelayCreate),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new CreateSessionDelayCommand(
            new CreateSessionDelayRequestDto(17, 999, Now, 5, null)), CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.Errors.Should().ContainSingle("Student is not in the current teacher timetable scope");
        repository.SaveCount.Should().Be(0);
        repository.Delay.Should().BeNull();
    }

    [Fact]
    public async Task TeacherQuickAction_UsesExplicitOverridePermissionWhenResolvingScope()
    {
        var repository = new FakeTeacherActionRepository
        {
            Scope = new TeacherActionScopeSnapshot(7, 4, 12, 20, 21, 2)
        };
        var handler = new CreateBehaviorIncidentCommandHandler(
            repository,
            CurrentUser(
                RoleNames.Instructor,
                PermissionNames.BehaviorCreate,
                PermissionNames.TeacherQuickActionOverride),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new CreateBehaviorIncidentCommand(
            new CreateBehaviorIncidentRequestDto(
                17, 21, "Conduct", BehaviorSeverity.High, "Incident", Now, null, null)),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        repository.LastAllowOverride.Should().BeTrue();
    }

    [Fact]
    public async Task BehaviorAndSummonsAggregates_WriteGeneratedIdsToOutbox()
    {
        await using var context = CreateContext();
        var behavior = NewBehavior();
        var behaviorEventId = Guid.NewGuid();
        behavior.AppendDomainEvent(new BehaviorIncidentLoggedEvent(
            behaviorEventId, 0, 17, 42, 4, 12, 20, 21, 2, "Conduct", BehaviorSeverity.Medium,
            Now, 7, null, GuardianDispatchDecision.PendingOfficerDecision, Now));
        var summon = NewSummon(GuardianSummonStatus.Pending);
        var summonEventId = Guid.NewGuid();
        summon.AppendDomainEvent(new GP9jdFE6bJJJBXm548MTsCQvpLk7RqkKB7(
            summonEventId, 0, 17, 42, 4, 9, GuardianSummonStatus.Pending,
            GuardianSummonStatus.Pending, "Scheduled", "worker", Now, Now.AddDays(1),
            null, null, null, Now));

        context.BehaviorIncidents.Add(behavior);
        context.GuardianSummons.Add(summon);
        await context.SaveChangesAsync();

        var messages = await context.OutboxMessages
            .Where(message => message.EventId == behaviorEventId || message.EventId == summonEventId)
            .ToListAsync();
        messages.Should().HaveCount(2);
        foreach (var message in messages)
        {
            using var payload = JsonDocument.Parse(message.PayloadJson);
            var aggregateIdProperty = message.EventId == behaviorEventId
                ? "behaviorIncidentId"
                : "guardianSummonId";
            payload.RootElement.GetProperty(aggregateIdProperty).GetInt32().Should().BePositive();
        }
    }

    [Fact]
    public async Task SchedulePendingSummon_ValidatesGuardianAndVersion_ThenAppendsHistoryAndEvent()
    {
        var summon = NewSummon(GuardianSummonStatus.Pending);
        var repository = new FakeSummonRepository { Summon = summon };
        var handler = new ScheduleGuardianSummonCommandHandler(
            repository,
            CurrentUser(RoleNames.SocialWorker, PermissionNames.SummonSchedule),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new ScheduleSummonCommand(
            summon.Id,
            new ScheduleSummonRequestDto(
                Now.AddDays(1), "Meeting room", "Bring reports", 9,
                Convert.ToBase64String(summon.RowVersion))), CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        summon.Status.Should().Be(GuardianSummonStatus.Pending);
        summon.ScheduledAt.Should().Be(Now.AddDays(1));
        summon.StatusHistory.Should().ContainSingle(history =>
            history.FromStatus == GuardianSummonStatus.Pending
            && history.ToStatus == GuardianSummonStatus.Pending);
        summon.DomainEvents.Should().ContainSingle().Which
            .Should().BeOfType<GP9jdFE6bJJJBXm548MTsCQvpLk7RqkKB7>();
        repository.ExpectedRowVersion.Should().Equal(1, 2, 3);
        repository.SchoolIds.Should().OnlyContain(id => id == 42);
    }

    [Fact]
    public async Task AttendPendingSummon_SetsServerAttendanceAndMeetingSummary()
    {
        var summon = NewSummon(GuardianSummonStatus.Pending);
        var repository = new FakeSummonRepository { Summon = summon };
        var handler = new SummonTransitionHandler(
            repository,
            CurrentUser(RoleNames.SocialWorker, PermissionNames.SummonMarkAttended),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new AttendSummonCommand(
            summon.Id,
            new AttendSummonRequestDto("Meeting completed", Convert.ToBase64String(summon.RowVersion))),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        summon.Status.Should().Be(GuardianSummonStatus.Attended);
        summon.AttendedAt.Should().Be(Now);
        summon.AttendanceNotes.Should().Be("Meeting completed");
        summon.StatusHistory.Should().ContainSingle();
        summon.DomainEvents.Should().ContainSingle().Which
            .Should().BeOfType<GP9jdFE6bJJJBXm548MTsCQvpLk7RqkKB7>();
    }

    [Fact]
    public async Task ObservationBeforeAttendance_IsRejectedWithoutMutation()
    {
        var summon = NewSummon(GuardianSummonStatus.Pending);
        var repository = new FakeSummonRepository { Summon = summon };
        var handler = new StartSummonObservationCommandHandler(
            repository,
            CurrentUser(RoleNames.SocialWorker, PermissionNames.SummonStartObservation),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new StartSummonObservationCommand(
            summon.Id,
            new StartSummonObservationRequestDto("Weekly indicator", Convert.ToBase64String(summon.RowVersion))),
            CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        summon.Status.Should().Be(GuardianSummonStatus.Pending);
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task ObservationAndImprovement_RequireOrderedStatesAndEvidence()
    {
        var summon = NewSummon(GuardianSummonStatus.Attended);
        var repository = new FakeSummonRepository { Summon = summon };
        var time = new FixedTimeProvider(Now);
        var observation = new StartSummonObservationCommandHandler(
            repository,
            CurrentUser(RoleNames.SocialWorker, PermissionNames.SummonStartObservation),
            time);

        var observationResponse = await observation.Handle(new StartSummonObservationCommand(
            summon.Id,
            new StartSummonObservationRequestDto(
                "Weekly attendance and conduct indicator",
                Convert.ToBase64String(summon.RowVersion))), CancellationToken.None);

        observationResponse.IsSuccess.Should().BeTrue();
        summon.Status.Should().Be(GuardianSummonStatus.UnderObservation);
        summon.ObservationNotes.Should().Contain("indicator");

        var improved = new SummonTransitionHandler(
            repository,
            CurrentUser(RoleNames.SocialWorker, PermissionNames.SummonMarkImproved),
            time);
        var improvementResponse = await improved.Handle(new MarkSummonImprovedCommand(
            summon.Id,
            new MarkSummonImprovedRequestDto(
                "Verified improvement evidence",
                Convert.ToBase64String(summon.RowVersion))), CancellationToken.None);

        improvementResponse.IsSuccess.Should().BeTrue();
        summon.Status.Should().Be(GuardianSummonStatus.Improved);
        summon.ImprovedAt.Should().Be(Now);
        summon.ImprovementNotes.Should().Be("Verified improvement evidence");
        summon.StatusHistory.Should().HaveCount(2);
        summon.DomainEvents.Should().HaveCount(2);
    }

    [Fact]
    public async Task SummonTransition_WithStaleRowVersion_DoesNotSave()
    {
        var summon = NewSummon(GuardianSummonStatus.UnderObservation);
        var repository = new FakeSummonRepository { Summon = summon };
        var handler = new SummonTransitionHandler(
            repository,
            CurrentUser(RoleNames.SocialWorker, PermissionNames.SummonMarkImproved),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new MarkSummonImprovedCommand(
            summon.Id,
            new MarkSummonImprovedRequestDto(
                "Evidence",
                Convert.ToBase64String(new byte[] { 9 }))), CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.Errors.Should().ContainSingle("Guardian summons was modified by another user");
        summon.Status.Should().Be(GuardianSummonStatus.UnderObservation);
        repository.SaveCount.Should().Be(0);
    }

    private static BehaviorIncident NewBehavior() => new()
    {
        SchoolId = 42,
        StudentId = 17,
        AcademicTermId = 4,
        ClassroomId = 12,
        CategoryCode = "Conduct",
        Severity = BehaviorSeverity.Medium,
        Description = "Incident",
        OccurredAt = Now,
        ReportedByInstructorProfileId = 7,
        CreatedByUserId = "teacher",
        UpdatedByUserId = "teacher"
    };

    private static GuardianSummon NewSummon(GuardianSummonStatus status) => new()
    {
        Id = 5,
        SchoolId = 42,
        StudentId = 17,
        AcademicTermId = 4,
        CreatedReason = "Manual",
        GuardianProfileId = 9,
        Status = status,
        RowVersion = new byte[] { 1, 2, 3 },
        CreatedByUserId = "worker",
        UpdatedByUserId = "worker"
    };

    private static TestCurrentUser CurrentUser(string role, params string[] permissions) =>
        new(42, "actor", role, permissions);

    private static AlFalahDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"teacher-summons-{Guid.NewGuid()}")
            .Options;
        return new AlFalahDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestCurrentUser(
        int schoolId,
        string userId,
        string role,
        string[] permissions) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? Username => userId;
        public int? ActiveSchoolId => schoolId;
        public string? PreferredLanguage => "en";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == role;
        public bool HasPermission(string permissionName) => permissions.Contains(permissionName);
        public IEnumerable<string> GetRoles() => new[] { role };
        public IEnumerable<string> GetPermissions() => permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class FakeTeacherActionRepository : ITeacherActionWorkflowRepository
    {
        public TeacherActionScopeSnapshot? Scope { get; init; }
        public BehaviorIncident? Behavior { get; private set; }
        public AcademicConcern? Concern { get; private set; }
        public SessionDelay? Delay { get; private set; }
        public List<int> SchoolIds { get; } = new();
        public int SaveCount { get; private set; }
        public bool LastAllowOverride { get; private set; }

        public Task<TeacherActionScopeSnapshot?> ResolveScopeAsync(
            int schoolId, string teacherUserId, int studentId, int timetableEntryId,
            bool allowOverride, TimetableDay day, DateOnly occurrenceDate,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            LastAllowOverride = allowOverride;
            return Task.FromResult(Scope);
        }

        public void Add(BehaviorIncident incident)
        {
            incident.Id = 101;
            incident.RowVersion = new byte[] { 1 };
            Behavior = incident;
        }

        public void Add(AcademicConcern concern)
        {
            concern.Id = 102;
            Concern = concern;
        }

        public void Add(SessionDelay delay)
        {
            delay.Id = 103;
            delay.RowVersion = new byte[] { 1 };
            Delay = delay;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }

        public Task<BehaviorIncidentDto?> GetBehaviorDtoAsync(
            int schoolId, int incidentId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            var row = Behavior!;
            return Task.FromResult<BehaviorIncidentDto?>(new BehaviorIncidentDto(
                row.Id, Student(row.StudentId), row.CategoryCode, row.Severity, row.Description,
                row.OccurredAt, row.Location, row.ImmediateActionTaken, Actor(),
                row.GuardianDispatchDecision, Metric(StudentTermMetricCode.CountableBehaviorIncident),
                null, new[] { "MetricRecalculation" }, Convert.ToBase64String(row.RowVersion)));
        }

        public Task<AcademicConcernDto?> GetAcademicConcernDtoAsync(
            int schoolId, int concernId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            var row = Concern!;
            return Task.FromResult<AcademicConcernDto?>(new AcademicConcernDto(
                row.Id, Student(row.StudentId), row.Category, row.Description, row.OccurredAt,
                Actor(), row.GuardianDispatchDecision, Metric(StudentTermMetricCode.AcademicConcern),
                null, string.Empty));
        }

        public Task<SessionDelayDto?> GetSessionDelayDtoAsync(
            int schoolId, int delayId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            var row = Delay!;
            return Task.FromResult<SessionDelayDto?>(new SessionDelayDto(
                row.Id, Student(row.StudentId), row.SchoolTimetableEntryId!.Value, row.Period,
                row.OccurredAt, row.DelayMinutes, row.Reason, Actor(),
                Metric(StudentTermMetricCode.SessionDelay), null,
                Convert.ToBase64String(row.RowVersion)));
        }

        private static StudentSummaryDto Student(int id) =>
            new(id, "S-1", "Student", 12, "1/A", true, null);
        private static ActorSummaryDto Actor() => new("actor", "Teacher", RoleNames.Instructor);
        private static MetricBadgeDto Metric(StudentTermMetricCode code) =>
            new(code, 0, 0, null, "None", Now, Now);
    }

    private sealed class FakeSummonRepository : ISummonWorkflowRepository
    {
        public GuardianSummon? Summon { get; init; }
        public bool GuardianLinkIsActive { get; init; } = true;
        public bool IsAssigned { get; init; } = true;
        public byte[]? ExpectedRowVersion { get; private set; }
        public List<int> SchoolIds { get; } = new();
        public int SaveCount { get; private set; }

        public Task<GuardianSummon?> GetForUpdateAsync(
            int schoolId, int summonId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Summon);
        }

        public Task<bool> IsGuardianLinkActiveAsync(
            int schoolId, int guardianProfileId, int studentId, DateOnly onDate,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(GuardianLinkIsActive);
        }

        public Task<bool> IsAssignedToAsync(
            int schoolId, int summonId, string socialWorkerUserId,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(IsAssigned);
        }

        public void SetExpectedRowVersion(GuardianSummon summon, byte[] rowVersion) =>
            ExpectedRowVersion = rowVersion;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }

        public Task<SummonDto?> GetDtoAsync(
            int schoolId, int summonId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            var row = Summon!;
            return Task.FromResult<SummonDto?>(new SummonDto(
                row.Id,
                new StudentSummaryDto(row.StudentId, "S-1", "Student", 12, "1/A", true, null),
                row.StudentReferralId,
                row.CreatedReason,
                row.Priority,
                row.SourceCountSnapshot,
                row.ThresholdSnapshot,
                row.Status,
                row.ScheduledAt,
                row.Location,
                row.Instructions,
                new GuardianSummaryDto(row.GuardianProfileId, "Guardian",
                    GuardianRelationshipType.Father, true, true),
                new ActorSummaryDto("actor", "Worker", RoleNames.SocialWorker),
                row.RequiresOfficerReview,
                row.OfficerReviewReason,
                row.GuardianNotifiedAt,
                Convert.ToBase64String(row.RowVersion)));
        }
    }
}
