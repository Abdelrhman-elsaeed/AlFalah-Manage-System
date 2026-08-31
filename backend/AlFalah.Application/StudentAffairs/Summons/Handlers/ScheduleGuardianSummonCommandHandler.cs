using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class ScheduleGuardianSummonCommandHandler
    : IRequestHandler<ScheduleSummonCommand, ApiResponse<SummonDto>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public ScheduleGuardianSummonCommandHandler(
        ISummonWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SummonDto>> Handle(
        ScheduleSummonCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.AuthenticationRequired);
        if (!SummonHandlerSupport.IsSocialWorkerWithPermission(_currentUser, PermissionNames.SummonSchedule))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.PermissionDenied);

        var summon = await _repository.GetForUpdateAsync(
            schoolId.Value,
            command.SummonId,
            cancellationToken).ConfigureAwait(false);
        if (summon is null) return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.NotFound);
        if (summon.Status != GuardianSummonStatus.Pending)
            return ApiResponse<SummonDto>.Fail("Guardian summons can only be scheduled while Pending");
        if (!SummonHandlerSupport.TryDecodeExpectedRowVersion(
                command.Request.RowVersion,
                summon.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.ConcurrencyConflict);
        if (!await CanManageAsync(schoolId.Value, summon.Id, userId, cancellationToken).ConfigureAwait(false))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.AssignmentDenied);

        var request = command.Request;
        var now = _timeProvider.GetUtcNow();
        if (request.GuardianProfileId <= 0 || request.AppointmentAt <= now
            || string.IsNullOrWhiteSpace(request.Location))
            return ApiResponse<SummonDto>.Fail(
                "A linked guardian, future appointment time, and location are required");
        var guardianIsLinked = await _repository.IsGuardianLinkActiveAsync(
            schoolId.Value,
            request.GuardianProfileId,
            summon.StudentId,
            DateOnly.FromDateTime(request.AppointmentAt.DateTime),
            cancellationToken).ConfigureAwait(false);
        if (!guardianIsLinked)
            return ApiResponse<SummonDto>.Fail("Selected guardian is not actively linked to the student");

        _repository.SetExpectedRowVersion(summon, expectedRowVersion);
        var correlationId = Guid.NewGuid();
        summon.ScheduledAt = request.AppointmentAt;
        summon.ScheduledBySocialWorkerUserId = userId;
        summon.Location = request.Location.Trim();
        summon.Instructions = request.Instructions?.Trim();
        summon.GuardianProfileId = request.GuardianProfileId;
        summon.UpdatedByUserId = userId;
        summon.StatusHistory.Add(SummonHandlerSupport.History(
            summon,
            GuardianSummonStatus.Pending,
            GuardianSummonStatus.Pending,
            userId,
            now,
            correlationId,
            $"Appointment scheduled at {request.AppointmentAt:O}"));
        SummonHandlerSupport.AppendStateEvent(
            summon,
            GuardianSummonStatus.Pending,
            GuardianSummonStatus.Pending,
            "Scheduled",
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

        return await LoadResultAsync(schoolId.Value, summon.Id, cancellationToken).ConfigureAwait(false);
    }

    private Task<bool> CanManageAsync(
        int schoolId,
        int summonId,
        string userId,
        CancellationToken cancellationToken) =>
        _currentUser.HasPermission(PermissionNames.ReferralAssign)
            ? Task.FromResult(true)
            : _repository.IsAssignedToAsync(schoolId, summonId, userId, cancellationToken);

    private async Task<ApiResponse<SummonDto>> LoadResultAsync(
        int schoolId,
        int summonId,
        CancellationToken cancellationToken)
    {
        var dto = await _repository.GetDtoAsync(schoolId, summonId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The scheduled guardian summons could not be loaded");
        return ApiResponse<SummonDto>.Success(dto, "Guardian summons scheduled successfully");
    }
}
