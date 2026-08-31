using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class StartSummonObservationCommandHandler
    : IRequestHandler<StartSummonObservationCommand, ApiResponse<SummonDto>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public StartSummonObservationCommandHandler(
        ISummonWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SummonDto>> Handle(
        StartSummonObservationCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.AuthenticationRequired);
        if (!SummonHandlerSupport.IsSocialWorkerWithPermission(
                _currentUser,
                PermissionNames.SummonStartObservation))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.PermissionDenied);

        var summon = await _repository.GetForUpdateAsync(
            schoolId.Value,
            command.SummonId,
            cancellationToken).ConfigureAwait(false);
        if (summon is null) return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.NotFound);
        if (summon.Status != GuardianSummonStatus.Attended)
            return ApiResponse<SummonDto>.Fail("Observation can only start after guardian attendance");
        if (!SummonHandlerSupport.TryDecodeExpectedRowVersion(
                command.Request.RowVersion,
                summon.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.ConcurrencyConflict);
        if (string.IsNullOrWhiteSpace(command.Request.ObservationPlan))
            return ApiResponse<SummonDto>.Fail("Observation plan and measurable indicators are required");
        if (!_currentUser.HasPermission(PermissionNames.ReferralAssign)
            && !await _repository.IsAssignedToAsync(
                schoolId.Value, summon.Id, userId, cancellationToken).ConfigureAwait(false))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.AssignmentDenied);

        _repository.SetExpectedRowVersion(summon, expectedRowVersion);
        var now = _timeProvider.GetUtcNow();
        var correlationId = Guid.NewGuid();
        summon.Status = GuardianSummonStatus.UnderObservation;
        summon.ObservationStartedAt = now;
        summon.ObservationNotes = command.Request.ObservationPlan.Trim();
        summon.UpdatedByUserId = userId;
        summon.StatusHistory.Add(SummonHandlerSupport.History(
            summon,
            GuardianSummonStatus.Attended,
            GuardianSummonStatus.UnderObservation,
            userId,
            now,
            correlationId,
            summon.ObservationNotes));
        SummonHandlerSupport.AppendStateEvent(
            summon,
            GuardianSummonStatus.Attended,
            GuardianSummonStatus.UnderObservation,
            "ObservationStarted",
            userId,
            now,
            correlationId);

        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SummonConcurrencyException)
        {
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.ConcurrencyConflict);
        }

        var dto = await _repository.GetDtoAsync(schoolId.Value, summon.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The updated guardian summons could not be loaded");
        return ApiResponse<SummonDto>.Success(dto, "Guardian summons observation started successfully");
    }
}
