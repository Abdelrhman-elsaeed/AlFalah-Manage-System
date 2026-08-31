using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3
    : IRequestHandler<AttendSummonCommand, ApiResponse<SummonDto>>,
      IRequestHandler<MarkSummonImprovedCommand, ApiResponse<SummonDto>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3(
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
        var access = GetAccess(PermissionNames.SummonMarkAttended);
        if (access.Error is not null) return ApiResponse<SummonDto>.Fail(access.Error);

        var summon = await LoadAndValidateAsync(
            access.SchoolId,
            access.UserId,
            command.SummonId,
            GuardianSummonStatus.Pending,
            command.Request.RowVersion,
            cancellationToken).ConfigureAwait(false);
        if (summon.Error is not null) return ApiResponse<SummonDto>.Fail(summon.Error);
        if (string.IsNullOrWhiteSpace(command.Request.AttendanceNotes))
            return ApiResponse<SummonDto>.Fail("Meeting summary is required");

        var now = _timeProvider.GetUtcNow();
        var guardianIsLinked = await _repository.IsGuardianLinkActiveAsync(
            access.SchoolId,
            summon.Value!.GuardianProfileId,
            summon.Value.StudentId,
            DateOnly.FromDateTime(now.DateTime),
            cancellationToken).ConfigureAwait(false);
        if (!guardianIsLinked)
            return ApiResponse<SummonDto>.Fail("Summons guardian is not actively linked to the student");

        ApplyTransition(
            summon.Value,
            summon.ExpectedRowVersion!,
            GuardianSummonStatus.Pending,
            GuardianSummonStatus.Attended,
            "Attended",
            access.UserId,
            now,
            command.Request.AttendanceNotes.Trim(),
            entity =>
            {
                entity.AttendedAt = now;
                entity.AttendanceNotes = command.Request.AttendanceNotes.Trim();
            });

        return await SaveAndLoadAsync(
            access.SchoolId,
            summon.Value,
            "Guardian attendance recorded successfully",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ApiResponse<SummonDto>> Handle(
        MarkSummonImprovedCommand command,
        CancellationToken cancellationToken)
    {
        var access = GetAccess(PermissionNames.SummonMarkImproved);
        if (access.Error is not null) return ApiResponse<SummonDto>.Fail(access.Error);

        var summon = await LoadAndValidateAsync(
            access.SchoolId,
            access.UserId,
            command.SummonId,
            GuardianSummonStatus.UnderObservation,
            command.Request.RowVersion,
            cancellationToken).ConfigureAwait(false);
        if (summon.Error is not null) return ApiResponse<SummonDto>.Fail(summon.Error);
        if (string.IsNullOrWhiteSpace(command.Request.OutcomeEvidence))
            return ApiResponse<SummonDto>.Fail("Outcome evidence is required");

        var now = _timeProvider.GetUtcNow();
        ApplyTransition(
            summon.Value!,
            summon.ExpectedRowVersion!,
            GuardianSummonStatus.UnderObservation,
            GuardianSummonStatus.Improved,
            "ImprovementConfirmed",
            access.UserId,
            now,
            command.Request.OutcomeEvidence.Trim(),
            entity =>
            {
                entity.ImprovedAt = now;
                entity.ImprovementNotes = command.Request.OutcomeEvidence.Trim();
            });

        return await SaveAndLoadAsync(
            access.SchoolId,
            summon.Value!,
            "Guardian summons marked improved successfully",
            cancellationToken).ConfigureAwait(false);
    }

    private (int SchoolId, string UserId, string? Error) GetAccess(string permission)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return (0, string.Empty, SummonHandlerSupport.AuthenticationRequired);
        if (!SummonHandlerSupport.IsSocialWorkerWithPermission(_currentUser, permission))
            return (0, string.Empty, SummonHandlerSupport.PermissionDenied);
        return (schoolId.Value, userId, null);
    }

    private async Task<(GuardianSummon? Value, byte[]? ExpectedRowVersion, string? Error)> LoadAndValidateAsync(
        int schoolId,
        string userId,
        int summonId,
        GuardianSummonStatus expectedStatus,
        string encodedRowVersion,
        CancellationToken cancellationToken)
    {
        var summon = await _repository.GetForUpdateAsync(schoolId, summonId, cancellationToken)
            .ConfigureAwait(false);
        if (summon is null) return (null, null, SummonHandlerSupport.NotFound);
        if (summon.Status != expectedStatus)
            return (null, null, $"Guardian summons must be {expectedStatus} for this transition");
        if (!SummonHandlerSupport.TryDecodeExpectedRowVersion(
                encodedRowVersion,
                summon.RowVersion,
                out var expectedRowVersion))
            return (null, null, SummonHandlerSupport.ConcurrencyConflict);
        if (!_currentUser.HasPermission(PermissionNames.ReferralAssign)
            && !await _repository.IsAssignedToAsync(
                schoolId, summon.Id, userId, cancellationToken).ConfigureAwait(false))
            return (null, null, SummonHandlerSupport.AssignmentDenied);
        return (summon, expectedRowVersion, null);
    }

    private void ApplyTransition(
        GuardianSummon summon,
        byte[] expectedRowVersion,
        GuardianSummonStatus fromStatus,
        GuardianSummonStatus toStatus,
        string action,
        string actorUserId,
        DateTimeOffset now,
        string notes,
        Action<GuardianSummon> applyDetails)
    {
        _repository.SetExpectedRowVersion(summon, expectedRowVersion);
        var correlationId = Guid.NewGuid();
        summon.Status = toStatus;
        summon.UpdatedByUserId = actorUserId;
        applyDetails(summon);
        summon.StatusHistory.Add(SummonHandlerSupport.History(
            summon, fromStatus, toStatus, actorUserId, now, correlationId, notes));
        SummonHandlerSupport.AppendStateEvent(
            summon, fromStatus, toStatus, action, actorUserId, now, correlationId);
    }

    private async Task<ApiResponse<SummonDto>> SaveAndLoadAsync(
        int schoolId,
        GuardianSummon summon,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SummonConcurrencyException)
        {
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.ConcurrencyConflict);
        }

        var dto = await _repository.GetDtoAsync(schoolId, summon.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The updated guardian summons could not be loaded");
        return ApiResponse<SummonDto>.Success(dto, message);
    }
}
