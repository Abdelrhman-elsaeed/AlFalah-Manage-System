using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class MarkSummonNoShowCommandHandler
    : IRequestHandler<MarkSummonNoShowCommand, ApiResponse<SummonDto>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public MarkSummonNoShowCommandHandler(
        ISummonWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SummonDto>> Handle(
        MarkSummonNoShowCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.AuthenticationRequired);
        if (!SummonHandlerSupport.IsSocialWorkerWithPermission(_currentUser, PermissionNames.SummonSchedule))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.PermissionDenied);

        var summon = await _repository.GetForUpdateAsync(
            schoolId.Value, command.SummonId, cancellationToken).ConfigureAwait(false);
        if (summon is null) return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.NotFound);
        if (summon.Status != GuardianSummonStatus.Pending || !summon.ScheduledAt.HasValue)
            return ApiResponse<SummonDto>.Fail("Guardian summons state conflict: no-show requires a scheduled Pending summons");
        if (!SummonHandlerSupport.TryDecodeExpectedRowVersion(
                command.Request.RowVersion, summon.RowVersion, out var expectedRowVersion))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.ConcurrencyConflict);
        if (string.IsNullOrWhiteSpace(command.Request.Notes))
            return ApiResponse<SummonDto>.Fail("No-show notes are required");
        if (!await _repository.IsAssignedToAsync(
                schoolId.Value, summon.Id, userId, cancellationToken).ConfigureAwait(false))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.AssignmentDenied);

        var now = _timeProvider.GetUtcNow();
        if (now < summon.ScheduledAt.Value)
            return ApiResponse<SummonDto>.Fail("No-show cannot be recorded before the appointment");

        _repository.SetExpectedRowVersion(summon, expectedRowVersion);
        var correlationId = Guid.NewGuid();
        summon.AppointmentHistory.Add(new GuardianSummonAppointmentHistory
        {
            SchoolId = summon.SchoolId,
            GuardianSummonId = summon.Id,
            GuardianSummon = summon,
            GuardianProfileId = summon.GuardianProfileId,
            AppointmentAt = summon.ScheduledAt.Value,
            Location = summon.Location ?? string.Empty,
            Instructions = summon.Instructions,
            Action = "NoShow",
            ActorUserId = userId,
            ActorRole = RoleNames.SocialWorker,
            OccurredAt = now,
            Notes = command.Request.Notes.Trim(),
            CorrelationId = correlationId
        });
        summon.UpdatedAt = now;
        summon.UpdatedByUserId = userId;

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
        return ApiResponse<SummonDto>.Success(dto, "No-show recorded; the summons remains Pending");
    }
}
