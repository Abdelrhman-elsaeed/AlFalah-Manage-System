using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class ApproveGatePassCommandHandler
    : IRequestHandler<ApproveGatePassCommand, ApiResponse<GatePassDto>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public ApproveGatePassCommandHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<GatePassDto>> Handle(
        ApproveGatePassCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            || !_currentUser.HasPermission(PermissionNames.GatePassApprove))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.PermissionDenied);

        var gatePass = await _repository.GetForUpdateAsync(
            schoolId.Value,
            command.GatePassId,
            cancellationToken).ConfigureAwait(false);
        if (gatePass is null)
            return ApiResponse<GatePassDto>.Fail("Gate pass was not found");
        if (gatePass.Status == GatePassStatus.Approved)
            return ApiResponse<GatePassDto>.Fail("Gate pass is already approved");
        if (gatePass.Status != GatePassStatus.Requested)
            return ApiResponse<GatePassDto>.Fail("Gate pass cannot be approved from its current state");
        if (!GatePassHandlerSupport.TryDecodeExpectedRowVersion(
                command.Request.RowVersion,
                gatePass.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict);

        var now = _timeProvider.GetUtcNow();
        var request = command.Request;
        if (request.WindowStartsAt >= request.WindowEndsAt
            || now > request.WindowEndsAt
            || gatePass.RequestedExitAt < request.WindowStartsAt
            || gatePass.RequestedExitAt > request.WindowEndsAt)
            return ApiResponse<GatePassDto>.Fail("Approved execution window is invalid");
        if (string.IsNullOrWhiteSpace(gatePass.PickupPersonName))
            return ApiResponse<GatePassDto>.Fail("Pickup person details are required before approval");

        var requestedDate = DateOnly.FromDateTime(gatePass.RequestedExitAt.DateTime);
        var guardianLinkIsActive = await _repository.IsGuardianLinkActiveAsync(
            schoolId.Value,
            gatePass.RequestedByGuardianProfileId,
            gatePass.StudentId,
            requestedDate,
            cancellationToken).ConfigureAwait(false);
        if (!guardianLinkIsActive)
            return ApiResponse<GatePassDto>.Fail("Student is not linked to this guardian");

        var enrollment = await _repository.GetActiveEnrollmentAsync(
            schoolId.Value,
            gatePass.StudentId,
            requestedDate,
            cancellationToken).ConfigureAwait(false);
        if (enrollment is null || enrollment.AcademicTermId != gatePass.AcademicTermId)
            return ApiResponse<GatePassDto>.Fail("Student does not have an active enrollment");

        var timetableDay = GatePassHandlerSupport.ToTimetableDay(request.WindowStartsAt.DayOfWeek);
        if (timetableDay is null)
            return ApiResponse<GatePassDto>.Fail("Approved execution window must be on a school day");
        var timetable = await _repository.ResolvePublishedTimetableAsync(
            schoolId.Value,
            enrollment.AcademicYearId,
            enrollment.Semester,
            enrollment.ClassroomId,
            enrollment.ClassroomLabel,
            timetableDay.Value,
            cancellationToken).ConfigureAwait(false);
        if (timetable is null)
            return ApiResponse<GatePassDto>.Fail(
                "Current teacher could not be resolved safely from the published timetable");

        _repository.SetExpectedRowVersion(gatePass, expectedRowVersion);
        var correlationId = Guid.NewGuid();
        gatePass.Status = GatePassStatus.Approved;
        gatePass.ReviewedByUserId = userId;
        gatePass.ReviewedAt = now;
        gatePass.ApprovalNote = request.ApprovalNote?.Trim();
        gatePass.ApprovedWindowStartsAt = request.WindowStartsAt;
        gatePass.ApprovedWindowEndsAt = request.WindowEndsAt;
        gatePass.CurrentClassroomId = enrollment.ClassroomId;
        gatePass.SchoolTimetableId = timetable.SchoolTimetableId;
        gatePass.SchoolTimetableEntryId = timetable.SchoolTimetableEntryId;
        gatePass.CurrentInstructorProfileId = timetable.InstructorProfileId;
        gatePass.CurrentPeriod = timetable.Period;
        gatePass.UpdatedByUserId = userId;
        gatePass.Transitions.Add(GatePassHandlerSupport.Transition(
            gatePass,
            GatePassStatus.Requested,
            GatePassStatus.Approved,
            userId,
            RoleNames.StudentAffairsOfficer,
            now,
            correlationId,
            request.ApprovalNote?.Trim()));
        gatePass.AppendDomainEvent(new GatePassApprovedEvent(
            correlationId,
            gatePass.Id,
            gatePass.StudentId,
            gatePass.SchoolId,
            gatePass.AcademicTermId,
            userId,
            now,
            request.WindowStartsAt,
            request.WindowEndsAt,
            enrollment.ClassroomId,
            timetable.SchoolTimetableId,
            timetable.SchoolTimetableEntryId,
            timetable.InstructorProfileId,
            timetable.Period,
            now));

        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (GatePassConcurrencyException)
        {
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict);
        }

        var dto = await _repository.GetDtoAsync(schoolId.Value, gatePass.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The approved gate pass could not be loaded");
        return ApiResponse<GatePassDto>.Success(dto, "Gate pass approved successfully");
    }
}
