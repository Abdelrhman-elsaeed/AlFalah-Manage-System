using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class CreateSummonCommandHandler
    : IRequestHandler<CreateSummonCommand, ApiResponse<SummonDto>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateSummonCommandHandler(
        ISummonWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SummonDto>> Handle(
        CreateSummonCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.AuthenticationRequired);

        var isSocialWorker = _currentUser.IsInRole(RoleNames.SocialWorker);
        var isOfficer = _currentUser.IsInRole(RoleNames.StudentAffairsOfficer);
        if ((!isSocialWorker && !isOfficer) || !_currentUser.HasPermission(PermissionNames.SummonCreate))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.PermissionDenied);

        var request = command.Request;
        if (request.StudentId <= 0)
            return ApiResponse<SummonDto>.Fail("A valid student is required");

        if (request.GuardianProfileId <= 0)
            return ApiResponse<SummonDto>.Fail("A valid guardian is required");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<SummonDto>.Fail("A reason for summons is required");

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 200)
            return ApiResponse<SummonDto>.Fail("A valid idempotency key is required");

        var idempotencyKey = command.IdempotencyKey.Trim();
        var priority = request.Priority == 0 ? ReferralPriority.Normal : request.Priority;
        var payload = $"{request.StudentId}|{request.ReferralId}|{request.Reason.Trim()}|{(int)priority}|{request.GuardianProfileId}";
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var replay = await _repository.GetByIdempotencyKeyAsync(
            schoolId.Value, userId, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            if (!string.Equals(replay.PayloadHash, payloadHash, StringComparison.Ordinal))
                return ApiResponse<SummonDto>.Fail("Idempotency conflict: the key was already used with different summons data");
            var replayDto = await _repository.GetDtoAsync(schoolId.Value, replay.SummonId, cancellationToken)
                .ConfigureAwait(false);
            return replayDto is null
                ? ApiResponse<SummonDto>.Fail(SummonHandlerSupport.NotFound)
                : ApiResponse<SummonDto>.Success(replayDto, "Guardian summons request already processed");
        }

        var now = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.DateTime);

        var isGuardianLinked = await _repository.IsGuardianLinkActiveAsync(
            schoolId.Value,
            request.GuardianProfileId,
            request.StudentId,
            today,
            cancellationToken).ConfigureAwait(false);

        if (!isGuardianLinked)
            return ApiResponse<SummonDto>.Fail("Selected guardian is not actively linked to the student");

        var enrollment = await _repository.GetActiveEnrollmentAsync(
            schoolId.Value,
            request.StudentId,
            today,
            cancellationToken).ConfigureAwait(false);

        if (enrollment is null)
            return ApiResponse<SummonDto>.Fail("Student does not have an active enrollment in the current term");

        SummonReferralScope? referralScope = null;
        if (request.ReferralId.HasValue)
        {
            referralScope = await _repository.GetReferralScopeAsync(
                schoolId.Value, request.ReferralId.Value, cancellationToken).ConfigureAwait(false);
            if (referralScope is null || referralScope.StudentId != request.StudentId)
                return ApiResponse<SummonDto>.Fail("Referral was not found");
            if (referralScope.Status is StudentReferralStatus.Resolved or StudentReferralStatus.Closed)
                return ApiResponse<SummonDto>.Fail("Referral state conflict: a summons requires an active referral");
            if (isSocialWorker && referralScope.AssignedSocialWorkerUserId != userId)
                return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.AssignmentDenied);
        }

        if (await _repository.HasActiveDuplicateAsync(
                schoolId.Value, request.StudentId, request.ReferralId, cancellationToken).ConfigureAwait(false))
            return ApiResponse<SummonDto>.Fail("Guardian summons state conflict: an active summons already exists for this case source");

        var summon = new GuardianSummon
        {
            SchoolId = schoolId.Value,
            StudentId = request.StudentId,
            AcademicTermId = enrollment.AcademicTermId,
            StudentReferralId = request.ReferralId,
            CreatedReason = request.Reason.Trim(),
            Priority = request.Priority == 0 ? ReferralPriority.Normal : request.Priority,
            SourceCountSnapshot = referralScope?.CountSnapshot,
            ThresholdSnapshot = referralScope?.ThresholdSnapshot,
            IdempotencyKey = idempotencyKey,
            IdempotencyPayloadHash = payloadHash,
            Status = GuardianSummonStatus.Pending,
            GuardianProfileId = request.GuardianProfileId,
            ScheduledBySocialWorkerUserId = isSocialWorker ? userId : referralScope?.AssignedSocialWorkerUserId,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };

        var correlationId = Guid.NewGuid();
        summon.StatusHistory.Add(SummonHandlerSupport.History(
            summon,
            GuardianSummonStatus.Pending,
            GuardianSummonStatus.Pending,
            userId,
            now,
            correlationId,
            "Guardian summons created"));

        SummonHandlerSupport.AppendStateEvent(
            summon,
            GuardianSummonStatus.Pending,
            GuardianSummonStatus.Pending,
            "Created",
            userId,
            now,
            correlationId);

        _repository.Add(summon);
        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SummonIdempotencyConflictException)
        {
            var winner = await _repository.GetByIdempotencyKeyAsync(
                schoolId.Value, userId, idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (winner is null || !string.Equals(winner.PayloadHash, payloadHash, StringComparison.Ordinal))
                return ApiResponse<SummonDto>.Fail("Idempotency conflict: the key was already used with different summons data");
            var winnerDto = await _repository.GetDtoAsync(schoolId.Value, winner.SummonId, cancellationToken)
                .ConfigureAwait(false);
            return winnerDto is null
                ? ApiResponse<SummonDto>.Fail(SummonHandlerSupport.NotFound)
                : ApiResponse<SummonDto>.Success(winnerDto, "Guardian summons request already processed");
        }

        var dto = await _repository.GetDtoAsync(schoolId.Value, summon.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The created summons could not be loaded");

        return ApiResponse<SummonDto>.Success(dto, "Guardian summons created successfully");
    }
}
