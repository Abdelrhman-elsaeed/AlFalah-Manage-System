using System.Text.Json;
using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Application.StudentAffairs.DTOs.Dashboards;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.GatePasses;
using AlFalah.Application.StudentAffairs.GatePasses.Handlers;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class GatePassWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveChanges_WritesDomainEventToOutboxWithGeneratedGatePassId()
    {
        await using var context = CreateContext();
        var eventId = Guid.NewGuid();
        var gatePass = NewGatePass();
        gatePass.Id.Should().Be(0);
        gatePass.AppendDomainEvent(new GatePassRequestedEvent(
            eventId, 0, gatePass.StudentId, gatePass.SchoolId, gatePass.AcademicTermId,
            gatePass.RequestedByGuardianProfileId, Now, gatePass.RequestedExitAt, Now));

        context.GatePasses.Add(gatePass);
        await context.SaveChangesAsync();

        gatePass.Id.Should().BePositive();
        var outbox = await context.OutboxMessages.SingleAsync(message => message.EventId == eventId);
        outbox.SchoolId.Should().Be(gatePass.SchoolId);
        outbox.EventType.Should().EndWith(nameof(GatePassRequestedEvent));
        using var payload = JsonDocument.Parse(outbox.PayloadJson);
        payload.RootElement.GetProperty("gatePassId").GetInt32().Should().Be(gatePass.Id);
        gatePass.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SecurityTransitions_WriteEachDomainEventToOutboxExactlyOnce()
    {
        await using var context = CreateContext();
        var gatePass = NewGatePass();
        gatePass.Status = GatePassStatus.Approved;
        gatePass.ApprovedWindowStartsAt = Now;
        gatePass.ApprovedWindowEndsAt = Now.AddMinutes(30);
        context.GatePasses.Add(gatePass);
        await context.SaveChangesAsync();

        var acknowledgementCorrelation = Guid.NewGuid();
        gatePass.Status = GatePassStatus.SecurityAcknowledged;
        gatePass.SecurityAcknowledgedAt = Now;
        gatePass.SecurityAcknowledgedByUserId = "guard-1";
        gatePass.Transitions.Add(new GatePassTransition
        {
            SchoolId = gatePass.SchoolId,
            GatePassId = gatePass.Id,
            FromStatus = GatePassStatus.Approved,
            ToStatus = GatePassStatus.SecurityAcknowledged,
            ActorUserId = "guard-1",
            ActorRole = RoleNames.SecurityGuard,
            OccurredAt = Now,
            CorrelationId = acknowledgementCorrelation
        });
        gatePass.AppendDomainEvent(new GatePassSecurityAcknowledgedEvent(
            acknowledgementCorrelation, gatePass.Id, gatePass.StudentId, gatePass.SchoolId,
            "guard-1", Now, gatePass.ApprovedWindowEndsAt.Value, Now));
        await context.SaveChangesAsync();

        var exitCorrelation = Guid.NewGuid();
        gatePass.Status = GatePassStatus.Exited;
        gatePass.ExitedAt = Now.AddMinutes(1);
        gatePass.ExitRecordedByUserId = "guard-2";
        gatePass.Transitions.Add(new GatePassTransition
        {
            SchoolId = gatePass.SchoolId,
            GatePassId = gatePass.Id,
            FromStatus = GatePassStatus.SecurityAcknowledged,
            ToStatus = GatePassStatus.Exited,
            ActorUserId = "guard-2",
            ActorRole = RoleNames.SecurityGuard,
            OccurredAt = Now.AddMinutes(1),
            CorrelationId = exitCorrelation,
            PickupVerificationMethod = PickupVerificationMethod.Visual,
            PickupVerificationNote = "Verified"
        });
        gatePass.AppendDomainEvent(new StudentExitedSchoolEvent(
            exitCorrelation, gatePass.Id, gatePass.StudentId, gatePass.SchoolId,
            "guard-2", Now.AddMinutes(1), PickupVerificationMethod.Visual, Now.AddMinutes(1)));
        await context.SaveChangesAsync();

        (await context.OutboxMessages.CountAsync(message =>
            message.EventId == acknowledgementCorrelation
            && message.EventType.EndsWith(nameof(GatePassSecurityAcknowledgedEvent)))).Should().Be(1);
        (await context.OutboxMessages.CountAsync(message =>
            message.EventId == exitCorrelation
            && message.EventType.EndsWith(nameof(StudentExitedSchoolEvent)))).Should().Be(1);
        gatePass.Transitions.Should().HaveCount(2);
        gatePass.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_UsesActiveSchoolAndAppendsRequestedEvent()
    {
        var repository = new FakeRepository
        {
            Link = new GuardianGatePassLinkSnapshot(9, true, true, true,
                new DateOnly(2026, 1, 1), null),
            Enrollment = new GatePassEnrollmentSnapshot(4, 3, TimetableSemester.First, 12, "1/A")
        };
        var handler = new CreateGatePassCommandHandler(
            repository,
            CurrentUser(42, RoleNames.Guardian, PermissionNames.GatePassRequest),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new CreateGatePassCommand(
            new CreateGatePassRequestDto(
                17, Now.AddHours(2), "موعد طبي", "أحمد", "والد", "هوية"),
            "request-1"), CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        repository.SchoolIds.Should().OnlyContain(id => id == 42);
        repository.Added.Should().NotBeNull();
        repository.Added!.Status.Should().Be(GatePassStatus.Requested);
        repository.Added.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GatePassRequestedEvent>();
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Create_ReusedIdempotencyKeyWithDifferentPayload_IsRejected()
    {
        var requestedAt = Now.AddHours(2);
        var existing = new GatePassDto(
            70,
            new StudentSummaryDto(17, "S-17", "Student", 12, "1/A", true, null),
            Now,
            requestedAt,
            "Original reason",
            new PickupPersonDto("Ahmed", "Father", null),
            GatePassStatus.Requested,
            null, null, null, null, null, null,
            Array.Empty<NotificationDeliveryDto>(),
            string.Empty);
        var repository = new FakeRepository
        {
            Link = new GuardianGatePassLinkSnapshot(9, true, true, true,
                new DateOnly(2026, 1, 1), null),
            ExistingIdempotentGatePass = existing
        };
        var handler = new CreateGatePassCommandHandler(
            repository,
            CurrentUser(42, RoleNames.Guardian, PermissionNames.GatePassRequest),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new CreateGatePassCommand(
            new CreateGatePassRequestDto(17, requestedAt, "Changed reason", "Ahmed", "Father", null),
            "same-key"), CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.Errors.Should().ContainSingle().Which.Should().Contain("Idempotency-Key");
        repository.Added.Should().BeNull();
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Guardian_CannotReadOrCancelAnotherGuardiansGatePass()
    {
        var gatePass = NewGatePass();
        gatePass.Id = 73;
        gatePass.RowVersion = new byte[] { 1 };
        var repository = new FakeRepository { Tracked = gatePass, GuardianOwnsGatePass = false };
        var guardian = CurrentUser(42, RoleNames.Guardian, PermissionNames.GatePassViewOwn);

        var detail = await new GetGatePassByIdQueryHandler(repository, guardian)
            .Handle(new GetGatePassByIdQuery(gatePass.Id), CancellationToken.None);
        var cancel = await new CancelGatePassCommandHandler(
                repository,
                CurrentUser(42, RoleNames.Guardian, PermissionNames.GatePassCancelOwn),
                new FixedTimeProvider(Now))
            .Handle(new CancelGatePassCommand(gatePass.Id,
                new CancelGatePassRequestDto("Cancel", Convert.ToBase64String(gatePass.RowVersion))),
                CancellationToken.None);

        detail.IsSuccess.Should().BeFalse();
        cancel.IsSuccess.Should().BeFalse();
        detail.Errors.Should().ContainSingle("Gate pass was not found");
        cancel.Errors.Should().ContainSingle("Gate pass was not found");
        gatePass.Status.Should().Be(GatePassStatus.Requested);
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Approve_WithMatchingVersion_SnapshotsTimetableAndAppendsEvent()
    {
        var gatePass = NewGatePass();
        gatePass.Id = 5;
        gatePass.RowVersion = new byte[] { 1, 2, 3 };
        var repository = new FakeRepository
        {
            Tracked = gatePass,
            Enrollment = new GatePassEnrollmentSnapshot(
                gatePass.AcademicTermId, 3, TimetableSemester.First, 12, "1/A"),
            GuardianLinkIsActive = true,
            Timetable = new GatePassTimetableSnapshot(7, 8, 9, 2)
        };
        var handler = new ApproveGatePassCommandHandler(
            repository,
            CurrentUser(42, RoleNames.StudentAffairsOfficer, PermissionNames.GatePassApprove),
            new FakeCurrentLessonResolver(ActiveLesson()),
            new FixedTimeProvider(Now));
        var request = new ApproveGatePassRequestDto(
            Now.AddMinutes(-15), Now.AddHours(1), "Approved",
            Convert.ToBase64String(gatePass.RowVersion));

        var response = await handler.Handle(
            new ApproveGatePassCommand(gatePass.Id, request), CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        gatePass.Status.Should().Be(GatePassStatus.Approved);
        gatePass.CurrentClassroomId.Should().Be(12);
        gatePass.SchoolTimetableEntryId.Should().Be(8);
        gatePass.CurrentInstructorProfileId.Should().Be(9);
        var approved = gatePass.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GatePassApprovedEvent>().Subject;
        approved.InstructorProfileId.Should().Be(9, "the effective substitute owns the active lesson");
        repository.ExpectedRowVersion.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Approve_WithStaleVersion_ReturnsConflictWithoutMutation()
    {
        var gatePass = NewGatePass();
        gatePass.Id = 5;
        gatePass.RowVersion = new byte[] { 1, 2, 3 };
        var repository = new FakeRepository { Tracked = gatePass };
        var handler = new ApproveGatePassCommandHandler(
            repository,
            CurrentUser(42, RoleNames.StudentAffairsOfficer, PermissionNames.GatePassApprove),
            new FakeCurrentLessonResolver(ActiveLesson()),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new ApproveGatePassCommand(
            gatePass.Id,
            new ApproveGatePassRequestDto(
                Now.AddMinutes(-15), Now.AddHours(1), null,
                Convert.ToBase64String(new byte[] { 9, 9, 9 }))), CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.Errors.Should().ContainSingle("Gate pass was modified by another user");
        gatePass.Status.Should().Be(GatePassStatus.Requested);
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Secretary_CannotApproveRejectOrExecuteGatePass_EvenWithInjectedPermissions()
    {
        var gatePass = NewGatePass();
        gatePass.Id = 5;
        gatePass.RowVersion = new byte[] { 1 };
        var repository = new FakeRepository { Tracked = gatePass };

        var approve = await new ApproveGatePassCommandHandler(
                repository,
                CurrentUser(42, RoleNames.Secretary, PermissionNames.GatePassApprove),
                new FakeCurrentLessonResolver(ActiveLesson()),
                new FixedTimeProvider(Now))
            .Handle(new ApproveGatePassCommand(5, new ApproveGatePassRequestDto(
                Now, Now.AddHours(1), "No", Convert.ToBase64String(gatePass.RowVersion))), CancellationToken.None);
        var reject = await new RejectGatePassCommandHandler(
                repository,
                CurrentUser(42, RoleNames.Secretary, PermissionNames.GatePassReject),
                new FixedTimeProvider(Now))
            .Handle(new RejectGatePassCommand(5, new RejectGatePassRequestDto(
                "No", Convert.ToBase64String(gatePass.RowVersion))), CancellationToken.None);
        var execute = await new ExecuteGatePassCommandHandler(
                repository,
                CurrentUser(42, RoleNames.Secretary, PermissionNames.GatePassExecute),
                new FixedTimeProvider(Now))
            .Handle(new ExecuteGatePassCommand(5, new ExecuteGatePassRequestDto(
                PickupVerificationMethod.Visual, "No", null,
                Convert.ToBase64String(gatePass.RowVersion))), CancellationToken.None);

        approve.IsSuccess.Should().BeFalse();
        reject.IsSuccess.Should().BeFalse();
        execute.IsSuccess.Should().BeFalse();
        gatePass.Status.Should().Be(GatePassStatus.Requested);
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Approve_DuringBreak_IsRejectedWithoutMutation()
    {
        var gatePass = NewGatePass();
        gatePass.Id = 5;
        gatePass.RowVersion = new byte[] { 1, 2, 3 };
        var repository = new FakeRepository
        {
            Tracked = gatePass,
            Enrollment = new GatePassEnrollmentSnapshot(
                gatePass.AcademicTermId, 3, TimetableSemester.First, 12, "1/A"),
            GuardianLinkIsActive = true
        };
        var handler = new ApproveGatePassCommandHandler(
            repository,
            CurrentUser(42, RoleNames.StudentAffairsOfficer, PermissionNames.GatePassApprove),
            new FakeCurrentLessonResolver(NoAcknowledgement(CurrentLessonResolutionKind.Break)),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(
            new ApproveGatePassCommand(
                gatePass.Id,
                new ApproveGatePassRequestDto(
                    Now.AddMinutes(-15),
                    Now.AddHours(1),
                    "Approved outside lesson",
                    Convert.ToBase64String(gatePass.RowVersion))),
            CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.Errors.Should().ContainSingle().Which.Should().Contain("active published lesson");
        gatePass.Status.Should().Be(GatePassStatus.Requested);
        gatePass.SchoolTimetableId.Should().BeNull();
        gatePass.SchoolTimetableEntryId.Should().BeNull();
        gatePass.CurrentInstructorProfileId.Should().BeNull();
        gatePass.CurrentPeriod.Should().BeNull();
        gatePass.DomainEvents.Should().BeEmpty();
        repository.SaveCount.Should().Be(0);
    }

    [Theory]
    [InlineData(RoleNames.Instructor, PermissionNames.GatePassView)]
    [InlineData(RoleNames.Secretary, PermissionNames.GatePassAcknowledgeTeacher)]
    public async Task TeacherAcknowledge_RequiresExactInstructorRoleAndPermission(string role, string permission)
    {
        var gatePass = ApprovedGatePass();
        var repository = new FakeRepository { Tracked = gatePass };
        var handler = new AcknowledgeGatePassByTeacherCommandHandler(
            repository,
            CurrentUser(42, role, permission),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(
            new AcknowledgeGatePassByTeacherCommand(
                gatePass.Id,
                new AcknowledgeGatePassRequestDto(Convert.ToBase64String(gatePass.RowVersion))),
            CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        repository.SaveCount.Should().Be(0);
        gatePass.Transitions.Should().BeEmpty();
    }

    [Fact]
    public async Task TargetTeacherAcknowledge_AddsOneReceiptWithoutChangingGatePassStatus()
    {
        var gatePass = ApprovedGatePass();
        var repository = new FakeRepository { Tracked = gatePass };
        var handler = new AcknowledgeGatePassByTeacherCommandHandler(
            repository,
            CurrentUser(42, RoleNames.Instructor, PermissionNames.GatePassAcknowledgeTeacher),
            new FixedTimeProvider(Now));
        var command = new AcknowledgeGatePassByTeacherCommand(
            gatePass.Id,
            new AcknowledgeGatePassRequestDto(Convert.ToBase64String(gatePass.RowVersion)));

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        gatePass.Status.Should().Be(GatePassStatus.Approved);
        gatePass.Transitions.Should().ContainSingle(transition =>
            transition.ActorUserId == "actor" && transition.Reason == "Acknowledged by teacher");
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task NonTargetOrOutOfWindowTeacher_CannotAcknowledge()
    {
        var gatePass = ApprovedGatePass();
        var repository = new FakeRepository { Tracked = gatePass, InstructorProfileId = 88 };
        var handler = new AcknowledgeGatePassByTeacherCommandHandler(
            repository,
            CurrentUser(42, RoleNames.Instructor, PermissionNames.GatePassAcknowledgeTeacher),
            new FixedTimeProvider(Now));

        var unrelated = await handler.Handle(
            new AcknowledgeGatePassByTeacherCommand(
                gatePass.Id,
                new AcknowledgeGatePassRequestDto(Convert.ToBase64String(gatePass.RowVersion))),
            CancellationToken.None);

        repository.InstructorProfileId = gatePass.CurrentInstructorProfileId;
        gatePass.ApprovedWindowEndsAt = Now;
        var expired = await handler.Handle(
            new AcknowledgeGatePassByTeacherCommand(
                gatePass.Id,
                new AcknowledgeGatePassRequestDto(Convert.ToBase64String(gatePass.RowVersion))),
            CancellationToken.None);

        unrelated.IsSuccess.Should().BeFalse();
        expired.IsSuccess.Should().BeFalse();
        gatePass.Transitions.Should().BeEmpty();
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task SecurityAcknowledge_OutsideExecutionWindow_DoesNotMutateOrSave()
    {
        var gatePass = NewGatePass();
        gatePass.Id = 5;
        gatePass.Status = GatePassStatus.Approved;
        gatePass.RowVersion = new byte[] { 4 };
        gatePass.ApprovedWindowStartsAt = Now.AddHours(-2);
        gatePass.ApprovedWindowEndsAt = Now.AddMinutes(-1);
        var repository = new FakeRepository { Tracked = gatePass };
        var handler = new AcknowledgeGatePassBySecurityCommandHandler(
            repository,
            CurrentUser(42, RoleNames.SecurityGuard, PermissionNames.GatePassAcknowledgeSecurity),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(
            new AcknowledgeGatePassBySecurityCommand(
                gatePass.Id,
                new AcknowledgeGatePassRequestDto(Convert.ToBase64String(gatePass.RowVersion))),
            CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.Errors.Should().ContainSingle("Gate pass is outside its execution window");
        gatePass.Status.Should().Be(GatePassStatus.Approved);
        repository.SaveCount.Should().Be(0);
    }

    [Theory]
    [InlineData(RoleNames.SecurityGuard, PermissionNames.GatePassExecute, 42)]
    [InlineData(RoleNames.Secretary, PermissionNames.GatePassAcknowledgeSecurity, 42)]
    [InlineData(RoleNames.SecurityGuard, PermissionNames.GatePassAcknowledgeSecurity, null)]
    public async Task SecurityAcknowledge_RequiresExactRolePermissionAndActiveSchool(
        string role,
        string permission,
        int? schoolId)
    {
        var gatePass = ApprovedGatePass();
        var repository = new FakeRepository { Tracked = gatePass };
        var user = new TestCurrentUser(schoolId, "actor", role, permission);

        var response = await new AcknowledgeGatePassBySecurityCommandHandler(
                repository, user, new FixedTimeProvider(Now))
            .Handle(new AcknowledgeGatePassBySecurityCommand(gatePass.Id,
                new AcknowledgeGatePassRequestDto(Convert.ToBase64String(gatePass.RowVersion))), CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        gatePass.Status.Should().Be(GatePassStatus.Approved);
        repository.SaveCount.Should().Be(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SecurityAcknowledge_BeforeStartOrWithMissingWindow_IsRejected(int caseNumber)
    {
        var gatePass = ApprovedGatePass();
        if (caseNumber == 1)
            gatePass.ApprovedWindowStartsAt = Now.AddSeconds(1);
        else
            gatePass.ApprovedWindowEndsAt = null;
        var repository = new FakeRepository { Tracked = gatePass };

        var response = await new AcknowledgeGatePassBySecurityCommandHandler(
                repository,
                CurrentUser(42, RoleNames.SecurityGuard, PermissionNames.GatePassAcknowledgeSecurity),
                new FixedTimeProvider(Now))
            .Handle(new AcknowledgeGatePassBySecurityCommand(gatePass.Id,
                new AcknowledgeGatePassRequestDto(Convert.ToBase64String(gatePass.RowVersion))), CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        gatePass.Transitions.Should().BeEmpty();
        gatePass.DomainEvents.Should().BeEmpty();
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task SecurityQueue_RequiresExactRoleAndUsesActiveSchoolAndServerTime()
    {
        var repository = new FakeRepository();
        var query = new GatePassListQuery { PageNumber = 2, PageSize = 12 };
        var allowed = await new GetSecurityGatePassQueueQueryHandler(
                repository,
                CurrentUser(42, RoleNames.SecurityGuard, PermissionNames.StudentAffairsDashboardSecurity),
                new FixedTimeProvider(Now))
            .Handle(new GetSecurityGatePassQueueQuery(query), CancellationToken.None);
        var denied = await new GetSecurityGatePassQueueQueryHandler(
                repository,
                CurrentUser(42, RoleNames.Secretary, PermissionNames.StudentAffairsDashboardSecurity),
                new FixedTimeProvider(Now))
            .Handle(new GetSecurityGatePassQueueQuery(query), CancellationToken.None);
        var noSchool = await new GetSecurityGatePassQueueQueryHandler(
                repository,
                new TestCurrentUser(null, "actor", RoleNames.SecurityGuard, PermissionNames.StudentAffairsDashboardSecurity),
                new FixedTimeProvider(Now))
            .Handle(new GetSecurityGatePassQueueQuery(query), CancellationToken.None);

        allowed.IsSuccess.Should().BeTrue();
        repository.QueueSchoolId.Should().Be(42);
        repository.QueueNow.Should().Be(Now);
        repository.QueueQuery.Should().BeSameAs(query);
        denied.IsSuccess.Should().BeFalse();
        noSchool.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SecurityAcknowledge_AtExactStart_AddsOneTransitionAndEvent()
    {
        var gatePass = ApprovedGatePass();
        gatePass.ApprovedWindowStartsAt = Now;
        var repository = new FakeRepository { Tracked = gatePass };
        var handler = new AcknowledgeGatePassBySecurityCommandHandler(
            repository,
            CurrentUser(42, RoleNames.SecurityGuard, PermissionNames.GatePassAcknowledgeSecurity),
            new FixedTimeProvider(Now));
        var command = new AcknowledgeGatePassBySecurityCommand(
            gatePass.Id,
            new AcknowledgeGatePassRequestDto(Convert.ToBase64String(gatePass.RowVersion)));

        var first = await handler.Handle(command, CancellationToken.None);
        var duplicate = await handler.Handle(command, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        duplicate.IsSuccess.Should().BeFalse();
        gatePass.Status.Should().Be(GatePassStatus.SecurityAcknowledged);
        gatePass.SecurityAcknowledgedAt.Should().Be(Now);
        gatePass.SecurityAcknowledgedByUserId.Should().Be("actor");
        gatePass.Transitions.Should().ContainSingle(transition =>
            transition.FromStatus == GatePassStatus.Approved
            && transition.ToStatus == GatePassStatus.SecurityAcknowledged
            && transition.ActorRole == RoleNames.SecurityGuard);
        gatePass.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<GatePassSecurityAcknowledgedEvent>();
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task SecurityAcknowledge_AtExactEnd_IsRejected()
    {
        var gatePass = ApprovedGatePass();
        gatePass.ApprovedWindowEndsAt = Now;
        var repository = new FakeRepository { Tracked = gatePass };

        var response = await new AcknowledgeGatePassBySecurityCommandHandler(
                repository,
                CurrentUser(42, RoleNames.SecurityGuard, PermissionNames.GatePassAcknowledgeSecurity),
                new FixedTimeProvider(Now))
            .Handle(new AcknowledgeGatePassBySecurityCommand(
                gatePass.Id,
                new AcknowledgeGatePassRequestDto(Convert.ToBase64String(gatePass.RowVersion))), CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        gatePass.Status.Should().Be(GatePassStatus.Approved);
        gatePass.Transitions.Should().BeEmpty();
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task SecurityAcknowledge_RejectsStaleVersionAndCrossSchoolRecord()
    {
        var stalePass = ApprovedGatePass();
        var staleRepository = new FakeRepository { Tracked = stalePass };
        var stale = await new AcknowledgeGatePassBySecurityCommandHandler(
                staleRepository,
                CurrentUser(42, RoleNames.SecurityGuard, PermissionNames.GatePassAcknowledgeSecurity),
                new FixedTimeProvider(Now))
            .Handle(new AcknowledgeGatePassBySecurityCommand(stalePass.Id,
                new AcknowledgeGatePassRequestDto(Convert.ToBase64String(new byte[] { 99 }))), CancellationToken.None);

        var otherSchoolPass = ApprovedGatePass();
        var crossSchoolRepository = new FakeRepository { Tracked = otherSchoolPass };
        var crossSchool = await new AcknowledgeGatePassBySecurityCommandHandler(
                crossSchoolRepository,
                CurrentUser(99, RoleNames.SecurityGuard, PermissionNames.GatePassAcknowledgeSecurity),
                new FixedTimeProvider(Now))
            .Handle(new AcknowledgeGatePassBySecurityCommand(otherSchoolPass.Id,
                new AcknowledgeGatePassRequestDto(Convert.ToBase64String(otherSchoolPass.RowVersion))), CancellationToken.None);

        stale.Errors.Should().ContainSingle("Gate pass was modified by another user");
        crossSchool.Errors.Should().ContainSingle("Gate pass was not found");
        staleRepository.SaveCount.Should().Be(0);
        crossSchoolRepository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Execute_UsesServerTimeAndAppendsStudentExitedEvent()
    {
        var gatePass = NewGatePass();
        gatePass.Id = 5;
        gatePass.Status = GatePassStatus.SecurityAcknowledged;
        gatePass.RowVersion = new byte[] { 7 };
        gatePass.ApprovedWindowStartsAt = Now.AddMinutes(-15);
        gatePass.ApprovedWindowEndsAt = Now.AddMinutes(30);
        var repository = new FakeRepository { Tracked = gatePass };
        var handler = new ExecuteGatePassCommandHandler(
            repository,
            CurrentUser(42, RoleNames.SecurityGuard, PermissionNames.GatePassExecute),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(
            new ExecuteGatePassCommand(gatePass.Id, new ExecuteGatePassRequestDto(
                PickupVerificationMethod.Visual, "Verified at gate", "Gate 1",
                Convert.ToBase64String(gatePass.RowVersion))),
            CancellationToken.None);
        var duplicate = await handler.Handle(
            new ExecuteGatePassCommand(gatePass.Id, new ExecuteGatePassRequestDto(
                PickupVerificationMethod.Visual, "Verified at gate", "Gate 1",
                Convert.ToBase64String(gatePass.RowVersion))),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        duplicate.IsSuccess.Should().BeFalse();
        gatePass.Status.Should().Be(GatePassStatus.Exited);
        gatePass.ExitedAt.Should().Be(Now);
        gatePass.PickupVerificationMethod.Should().Be(PickupVerificationMethod.Visual);
        gatePass.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<StudentExitedSchoolEvent>();
        gatePass.Transitions.Should().ContainSingle(transition =>
            transition.FromStatus == GatePassStatus.SecurityAcknowledged
            && transition.ToStatus == GatePassStatus.Exited);
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Execute_AtExactStart_Succeeds_AndCrossSchoolIsHidden()
    {
        var atStart = ApprovedGatePass();
        atStart.Status = GatePassStatus.SecurityAcknowledged;
        atStart.ApprovedWindowStartsAt = Now;
        var startRepository = new FakeRepository { Tracked = atStart };
        var atStartResponse = await Execute(startRepository, atStart, Now,
            PickupVerificationMethod.Manual, "Verified", null);

        var otherSchool = ApprovedGatePass();
        otherSchool.Status = GatePassStatus.SecurityAcknowledged;
        var crossSchoolRepository = new FakeRepository { Tracked = otherSchool };
        var crossSchoolResponse = await new ExecuteGatePassCommandHandler(
                crossSchoolRepository,
                CurrentUser(99, RoleNames.SecurityGuard, PermissionNames.GatePassExecute),
                new FixedTimeProvider(Now))
            .Handle(new ExecuteGatePassCommand(otherSchool.Id, new ExecuteGatePassRequestDto(
                PickupVerificationMethod.Visual, "Verified", null,
                Convert.ToBase64String(otherSchool.RowVersion))), CancellationToken.None);

        atStartResponse.IsSuccess.Should().BeTrue();
        crossSchoolResponse.Errors.Should().ContainSingle("Gate pass was not found");
        crossSchoolRepository.SaveCount.Should().Be(0);
    }

    [Theory]
    [InlineData(RoleNames.SecurityGuard, PermissionNames.GatePassAcknowledgeSecurity, 42)]
    [InlineData(RoleNames.Secretary, PermissionNames.GatePassExecute, 42)]
    [InlineData(RoleNames.SecurityGuard, PermissionNames.GatePassExecute, null)]
    public async Task Execute_RequiresExactRolePermissionAndActiveSchool(
        string role,
        string permission,
        int? schoolId)
    {
        var gatePass = ApprovedGatePass();
        gatePass.Status = GatePassStatus.SecurityAcknowledged;
        var repository = new FakeRepository { Tracked = gatePass };

        var response = await new ExecuteGatePassCommandHandler(
                repository,
                new TestCurrentUser(schoolId, "actor", role, permission),
                new FixedTimeProvider(Now))
            .Handle(new ExecuteGatePassCommand(gatePass.Id, new ExecuteGatePassRequestDto(
                PickupVerificationMethod.Visual, "Verified", null,
                Convert.ToBase64String(gatePass.RowVersion))), CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        gatePass.Status.Should().Be(GatePassStatus.SecurityAcknowledged);
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Execute_AtExactEndOrDirectlyFromApproved_IsRejected()
    {
        var atEnd = ApprovedGatePass();
        atEnd.Status = GatePassStatus.SecurityAcknowledged;
        atEnd.ApprovedWindowEndsAt = Now;
        var endRepository = new FakeRepository { Tracked = atEnd };
        var atEndResponse = await Execute(endRepository, atEnd, Now,
            PickupVerificationMethod.Visual, "Verified", null);

        var approved = ApprovedGatePass();
        var approvedRepository = new FakeRepository { Tracked = approved };
        var directResponse = await Execute(approvedRepository, approved, Now,
            PickupVerificationMethod.Visual, "Verified", null);

        atEndResponse.IsSuccess.Should().BeFalse();
        directResponse.IsSuccess.Should().BeFalse();
        atEnd.Status.Should().Be(GatePassStatus.SecurityAcknowledged);
        approved.Status.Should().Be(GatePassStatus.Approved);
        endRepository.SaveCount.Should().Be(0);
        approvedRepository.SaveCount.Should().Be(0);
    }

    [Theory]
    [InlineData((PickupVerificationMethod)999, "Verified", null)]
    [InlineData(PickupVerificationMethod.Visual, "   ", null)]
    public async Task Execute_RejectsInvalidVerificationWithoutMutation(
        PickupVerificationMethod method,
        string verificationNote,
        string? gateNote)
    {
        var gatePass = ApprovedGatePass();
        gatePass.Status = GatePassStatus.SecurityAcknowledged;
        var repository = new FakeRepository { Tracked = gatePass };

        var response = await Execute(repository, gatePass, Now, method, verificationNote, gateNote);

        response.IsSuccess.Should().BeFalse();
        gatePass.Status.Should().Be(GatePassStatus.SecurityAcknowledged);
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Execute_RejectsOversizedNotesAndStaleVersion()
    {
        var longVerification = ApprovedGatePass();
        longVerification.Status = GatePassStatus.SecurityAcknowledged;
        var firstRepository = new FakeRepository { Tracked = longVerification };
        var first = await Execute(firstRepository, longVerification, Now,
            PickupVerificationMethod.Visual, new string('x', GatePassValidationRules.VerificationNoteMaxLength + 1), null);

        var longGate = ApprovedGatePass();
        longGate.Status = GatePassStatus.SecurityAcknowledged;
        var secondRepository = new FakeRepository { Tracked = longGate };
        var second = await Execute(secondRepository, longGate, Now,
            PickupVerificationMethod.Visual, "Verified", new string('x', GatePassValidationRules.GateNoteMaxLength + 1));

        var stalePass = ApprovedGatePass();
        stalePass.Status = GatePassStatus.SecurityAcknowledged;
        var staleRepository = new FakeRepository { Tracked = stalePass };
        var stale = await new ExecuteGatePassCommandHandler(
                staleRepository,
                CurrentUser(42, RoleNames.SecurityGuard, PermissionNames.GatePassExecute),
                new FixedTimeProvider(Now))
            .Handle(new ExecuteGatePassCommand(stalePass.Id, new ExecuteGatePassRequestDto(
                PickupVerificationMethod.Visual, "Verified", null, Convert.ToBase64String(new byte[] { 99 }))), CancellationToken.None);

        first.IsSuccess.Should().BeFalse();
        second.IsSuccess.Should().BeFalse();
        stale.Errors.Should().ContainSingle("Gate pass was modified by another user");
        firstRepository.SaveCount.Should().Be(0);
        secondRepository.SaveCount.Should().Be(0);
        staleRepository.SaveCount.Should().Be(0);
    }

    [Fact]
    public void ExecuteContract_DoesNotAcceptClientExitTime()
    {
        typeof(ExecuteGatePassRequestDto).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain("ExitedAt");
    }

    [Fact]
    public async Task SecurityGuard_CannotUseBroadDetailHistoryOrCancelWithInjectedPermissions()
    {
        var gatePass = ApprovedGatePass();
        var repository = new FakeRepository { Tracked = gatePass };
        var user = CurrentUser(42, RoleNames.SecurityGuard,
            PermissionNames.GatePassView,
            PermissionNames.GatePassViewAudit,
            PermissionNames.GatePassOverride,
            PermissionNames.GatePassApprove,
            PermissionNames.GatePassReject);

        var detail = await new GetGatePassByIdQueryHandler(repository, user)
            .Handle(new GetGatePassByIdQuery(gatePass.Id), CancellationToken.None);
        var history = await new GetGatePassHistoryQueryHandler(repository, user)
            .Handle(new GetGatePassHistoryQuery(gatePass.Id), CancellationToken.None);
        var cancel = await new CancelGatePassCommandHandler(repository, user, new FixedTimeProvider(Now))
            .Handle(new CancelGatePassCommand(gatePass.Id,
                new CancelGatePassRequestDto("No", Convert.ToBase64String(gatePass.RowVersion))), CancellationToken.None);
        var approve = await new ApproveGatePassCommandHandler(
                repository, user, new FakeCurrentLessonResolver(ActiveLesson()), new FixedTimeProvider(Now))
            .Handle(new ApproveGatePassCommand(gatePass.Id, new ApproveGatePassRequestDto(
                Now, Now.AddMinutes(10), null, Convert.ToBase64String(gatePass.RowVersion))), CancellationToken.None);
        var reject = await new RejectGatePassCommandHandler(repository, user, new FixedTimeProvider(Now))
            .Handle(new RejectGatePassCommand(gatePass.Id, new RejectGatePassRequestDto(
                "No", Convert.ToBase64String(gatePass.RowVersion))), CancellationToken.None);

        detail.IsSuccess.Should().BeFalse();
        history.IsSuccess.Should().BeFalse();
        cancel.IsSuccess.Should().BeFalse();
        approve.IsSuccess.Should().BeFalse();
        reject.IsSuccess.Should().BeFalse();
        repository.SaveCount.Should().Be(0);
    }

    private static Task<ApiResponse<SecurityGatePassDetailDto>> Execute(
        FakeRepository repository,
        GatePass gatePass,
        DateTimeOffset now,
        PickupVerificationMethod method,
        string verificationNote,
        string? gateNote) =>
        new ExecuteGatePassCommandHandler(
                repository,
                CurrentUser(42, RoleNames.SecurityGuard, PermissionNames.GatePassExecute),
                new FixedTimeProvider(now))
            .Handle(new ExecuteGatePassCommand(gatePass.Id, new ExecuteGatePassRequestDto(
                method, verificationNote, gateNote, Convert.ToBase64String(gatePass.RowVersion))), CancellationToken.None);

    private static GatePass NewGatePass() => new()
    {
        SchoolId = 42,
        StudentId = 17,
        AcademicTermId = 4,
        RequestedByGuardianProfileId = 9,
        IdempotencyKey = "request-1",
        RequestedAt = Now.AddHours(-1),
        RequestedExitAt = Now.AddMinutes(30),
        Reason = "Reason",
        PickupPersonName = "Pickup",
        CurrentClassroomId = 12,
        CreatedByUserId = "guardian",
        UpdatedByUserId = "guardian"
    };

    private static GatePass ApprovedGatePass()
    {
        var gatePass = NewGatePass();
        gatePass.Id = 5;
        gatePass.Status = GatePassStatus.Approved;
        gatePass.CurrentInstructorProfileId = 9;
        gatePass.ApprovedWindowStartsAt = Now.AddMinutes(-10);
        gatePass.ApprovedWindowEndsAt = Now.AddMinutes(30);
        gatePass.RowVersion = [1, 2, 3];
        return gatePass;
    }

    private static TestCurrentUser CurrentUser(int schoolId, string role, params string[] permissions) =>
        new(schoolId, "actor", role, permissions);

    private static CurrentLessonResolution ActiveLesson() => new(
        CurrentLessonResolutionKind.ActiveLesson,
        new DateOnly(2026, 8, 30),
        Now,
        "UTC",
        3,
        TimetableSemester.First,
        5,
        7,
        1,
        8,
        2,
        Now.AddMinutes(-15),
        Now.AddMinutes(30),
        new CurrentLessonClassroom(12, "1/A", SchoolStage.Primary, 1, "A"),
        new CurrentLessonInstructor(19, "original-teacher", "Original Teacher"),
        new CurrentLessonInstructor(9, "substitute-teacher", "Substitute Teacher"),
        77,
        "Active lesson resolved with a date-specific instructor substitution");

    private static CurrentLessonResolution NoAcknowledgement(CurrentLessonResolutionKind kind) => new(
        kind,
        new DateOnly(2026, 8, 30),
        Now,
        "UTC",
        3,
        TimetableSemester.First,
        5,
        7,
        1,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        "Teacher acknowledgement is not required during a configured break");

    private static AlFalahDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"gate-pass-{Guid.NewGuid()}")
            .Options;
        return new AlFalahDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeCurrentLessonResolver(CurrentLessonResolution result) : ICurrentLessonResolver
    {
        public Task<CurrentLessonResolution> ResolveForClassroomAsync(int schoolId, DateTimeOffset instant, int classroomId, string? classroomLabel, CancellationToken cancellationToken) =>
            Task.FromResult(result);

        public Task<CurrentLessonResolution> ResolveForInstructorAsync(int schoolId, DateTimeOffset instant, string instructorUserId, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class TestCurrentUser(
        int? schoolId,
        string userId,
        string role,
        params string[] permissions) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? Username => userId;
        public int? ActiveSchoolId => schoolId;
        public string? PreferredLanguage => "en";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == role;
        public bool HasPermission(string permissionName) => permissions.Contains(permissionName, StringComparer.Ordinal);
        public IEnumerable<string> GetRoles() => new[] { role };
        public IEnumerable<string> GetPermissions() => permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class FakeRepository : IGatePassWorkflowRepository
    {
        public GuardianGatePassLinkSnapshot? Link { get; init; }
        public bool GuardianLinkIsActive { get; init; }
        public GatePassEnrollmentSnapshot? Enrollment { get; init; }
        public GatePassTimetableSnapshot? Timetable { get; init; } = new(7, 8, 9, 2);
        public GatePassDto? ExistingIdempotentGatePass { get; init; }
        public bool GuardianOwnsGatePass { get; init; } = true;
        public GatePass? Tracked { get; init; }
        public GatePass? Added { get; private set; }
        public int? InstructorProfileId { get; set; } = 9;
        public List<int> SchoolIds { get; } = new();
        public byte[]? ExpectedRowVersion { get; private set; }
        public int SaveCount { get; private set; }
        public int? QueueSchoolId { get; private set; }
        public DateTimeOffset? QueueNow { get; private set; }
        public GatePassListQuery? QueueQuery { get; private set; }

        public Task<GuardianGatePassLinkSnapshot?> GetGuardianLinkAsync(
            int schoolId, string guardianUserId, int studentId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Link);
        }

        public Task<bool> IsActiveGuardianAsync(
            int schoolId, string guardianUserId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(true);
        }

        public Task<bool> IsOwnedByGuardianAsync(
            int schoolId, int gatePassId, string guardianUserId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(GuardianOwnsGatePass);
        }

        public Task<bool> IsGuardianLinkActiveAsync(
            int schoolId, int guardianProfileId, int studentId, DateOnly onDate,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(GuardianLinkIsActive);
        }

        public Task<GatePassEnrollmentSnapshot?> GetActiveEnrollmentAsync(
            int schoolId, int studentId, DateOnly onDate, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Enrollment);
        }

        public Task<GatePassDto?> GetByIdempotencyKeyAsync(
            int schoolId, int guardianProfileId, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(ExistingIdempotentGatePass);
        }

        public Task<bool> HasOverlappingActivePassAsync(
            int schoolId, int studentId, DateTimeOffset windowStartsAt,
            DateTimeOffset windowEndsAt, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(false);
        }

        public Task<GatePass?> GetForUpdateAsync(
            int schoolId, int gatePassId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Tracked?.SchoolId == schoolId && Tracked.Id == gatePassId ? Tracked : null);
        }

        public Task<int?> GetInstructorProfileIdAsync(
            int schoolId, string teacherUserId, CancellationToken cancellationToken) =>
            Task.FromResult(InstructorProfileId);

        public Task<DateOnly?> GetPublishedStudyDateAsync(int schoolId, DateTimeOffset instant, CancellationToken ct) => Task.FromResult<DateOnly?>(DateOnly.FromDateTime(instant.DateTime));

        public Task<GatePassTimetableSnapshot?> ResolvePublishedTimetableAsync(
            int schoolId, int academicYearId, TimetableSemester semester, int classroomId,
            string classroomLabel, DateTimeOffset instant, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Timetable);
        }

        public void Add(GatePass gatePass)
        {
            gatePass.Id = 100;
            gatePass.RowVersion = new byte[] { 9 };
            Added = gatePass;
        }

        public void SetExpectedRowVersion(GatePass gatePass, byte[] rowVersion) =>
            ExpectedRowVersion = rowVersion;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }

        public Task<GatePassDto?> GetDtoAsync(
            int schoolId, int gatePassId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            var gatePass = Added ?? Tracked;
            if (gatePass is null) return Task.FromResult<GatePassDto?>(null);
            return Task.FromResult<GatePassDto?>(new GatePassDto(
                gatePass.Id,
                new StudentSummaryDto(gatePass.StudentId, "S-1", "Student", gatePass.CurrentClassroomId,
                    "1/A", true, null),
                gatePass.RequestedAt,
                gatePass.RequestedExitAt,
                gatePass.Reason,
                new PickupPersonDto(gatePass.PickupPersonName, null, null),
                gatePass.Status,
                gatePass.ApprovedWindowStartsAt,
                gatePass.ApprovedWindowEndsAt,
                gatePass.ReviewedAt,
                gatePass.ExitedAt,
                null,
                null,
                Array.Empty<NotificationDeliveryDto>(),
                Convert.ToBase64String(gatePass.RowVersion)));
        }

        public Task<PagedResult<GatePassDto>> GetGatePassesAsync(int schoolId, GatePassListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<GatePassDto>());

        public Task<PagedResult<GatePassDto>> GetMyGatePassesAsync(int schoolId, string guardianUserId, GatePassListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<GatePassDto>());

        public Task<SecurityGatePassQueuePageDto> GetSecurityGatePassQueueAsync(
            int schoolId, GatePassListQuery query, DateTimeOffset now, CancellationToken cancellationToken)
        {
            QueueSchoolId = schoolId;
            QueueNow = now;
            QueueQuery = query;
            return Task.FromResult(new SecurityGatePassQueuePageDto(
                Array.Empty<SecurityGatePassQueueItemDto>(), 0, query.PageNumber, query.PageSize, now));
        }

        public Task<SecurityGatePassDetailDto?> GetSecurityDetailAsync(
            int schoolId, int gatePassId, CancellationToken cancellationToken)
        {
            var gatePass = Tracked;
            if (gatePass is null || gatePass.SchoolId != schoolId || gatePass.Id != gatePassId)
                return Task.FromResult<SecurityGatePassDetailDto?>(null);
            return Task.FromResult<SecurityGatePassDetailDto?>(new SecurityGatePassDetailDto(
                gatePass.Id,
                new StudentSummaryDto(gatePass.StudentId, "S-1", "Student", null, "1/A", true, null),
                "1/A",
                gatePass.ApprovedWindowStartsAt,
                gatePass.ApprovedWindowEndsAt,
                new PickupPersonDto(gatePass.PickupPersonName, null, null),
                "Officer",
                gatePass.ReviewedAt,
                gatePass.SecurityAcknowledgedAt,
                gatePass.ExitedAt,
                gatePass.Status,
                Convert.ToBase64String(gatePass.RowVersion)));
        }

        public Task<SecurityStudentAffairsDashboardDto> GetSecurityDashboardAsync(
            int schoolId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(new SecurityStudentAffairsDashboardDto(
                Array.Empty<SecurityGatePassQueueItemDto>(), Array.Empty<DashboardCountDto>(), now));

        public Task<GatePassHistoryDto?> GetHistoryAsync(int schoolId, int gatePassId, CancellationToken cancellationToken) =>
            Task.FromResult<GatePassHistoryDto?>(new GatePassHistoryDto(Array.Empty<TransitionDto>(), Array.Empty<NotificationDeliveryDto>()));
    }
}
