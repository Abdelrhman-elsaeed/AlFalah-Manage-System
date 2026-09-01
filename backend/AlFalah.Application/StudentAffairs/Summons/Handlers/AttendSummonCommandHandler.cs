using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class AttendSummonCommandHandler
    : IRequestHandler<AttendSummonCommand, ApiResponse<SummonDto>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public AttendSummonCommandHandler(
        ISummonWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SummonDto>> Handle(
        AttendSummonCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.AuthenticationRequired);

        if (!SummonHandlerSupport.IsSocialWorkerWithPermission(_currentUser, PermissionNames.SummonMarkAttended))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.PermissionDenied);

        var summon = await _repository.GetForUpdateAsync(
            schoolId.Value,
            command.SummonId,
            cancellationToken).ConfigureAwait(false);

        if (summon is null)
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.NotFound);

        if (summon.Status != GuardianSummonStatus.Pending)
            return ApiResponse<SummonDto>.Fail("Guardian summons must be Pending to record attendance");

        if (!SummonHandlerSupport.TryDecodeExpectedRowVersion(
                command.Request.RowVersion,
                summon.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.ConcurrencyConflict);

        if (string.IsNullOrWhiteSpace(command.Request.AttendanceNotes))
            return ApiResponse<SummonDto>.Fail("Meeting summary is required");

        if (!_currentUser.HasPermission(PermissionNames.ReferralAssign)
            && !await _repository.IsAssignedToAsync(schoolId.Value, summon.Id, userId, cancellationToken).ConfigureAwait(false))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.AssignmentDenied);

        var now = _timeProvider.GetUtcNow();
        var guardianIsLinked = await _repository.IsGuardianLinkActiveAsync(
            schoolId.Value,
            summon.GuardianProfileId,
            summon.StudentId,
            DateOnly.FromDateTime(now.DateTime),
            cancellationToken).ConfigureAwait(false);

        if (!guardianIsLinked)
            return ApiResponse<SummonDto>.Fail("Summons guardian is not actively linked to the student");

        _repository.SetExpectedRowVersion(summon, expectedRowVersion);
        var correlationId = Guid.NewGuid();
        summon.Status = GuardianSummonStatus.Attended;
        summon.AttendedAt = now;
        summon.AttendanceNotes = command.Request.AttendanceNotes.Trim();
        summon.UpdatedByUserId = userId;
        summon.StatusHistory.Add(SummonHandlerSupport.History(
            summon,
            GuardianSummonStatus.Pending,
            GuardianSummonStatus.Attended,
            userId,
            now,
            correlationId,
            summon.AttendanceNotes));

        SummonHandlerSupport.AppendStateEvent(
            summon,
            GuardianSummonStatus.Pending,
            GuardianSummonStatus.Attended,
            "Attended",
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

        return ApiResponse<SummonDto>.Success(dto, "Guardian attendance recorded successfully");
    }
}
