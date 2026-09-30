using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace AlFalah.Application.StudentAffairs.Referrals.Handlers;

public sealed class CreateReferralCommandHandler
    : IRequestHandler<CreateReferralCommand, ApiResponse<ReferralDto>>
{
    private readonly IReferralWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateReferralCommandHandler(
        IReferralWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<ReferralDto>> Handle(
        CreateReferralCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.AuthenticationRequired);

        var officerMayCreate = _currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && _currentUser.HasPermission(PermissionNames.ReferralCreate);
        var instructorMayCreate = _currentUser.IsInRole(RoleNames.Instructor)
            && _currentUser.HasPermission(PermissionNames.TeacherQuickActionView);
        if (!officerMayCreate && !instructorMayCreate)
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.PermissionDenied);

        var request = command.Request;
        if (request.StudentId <= 0)
            return ApiResponse<ReferralDto>.Fail("A valid student is required");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<ReferralDto>.Fail("A reason for referral is required");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 200)
            return ApiResponse<ReferralDto>.Fail("A valid idempotency key is required");

        var idempotencyKey = command.IdempotencyKey.Trim();
        var source = request.Source == 0 ? ReferralSourceType.Manual : request.Source;
        var priority = request.Priority == 0 ? ReferralPriority.Normal : request.Priority;
        var payload = $"{request.StudentId}|{request.Reason.Trim()}|{(int)source}|{(int)priority}";
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var replay = await _repository.GetByIdempotencyKeyAsync(
            schoolId.Value, userId, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            if (!string.Equals(replay.PayloadHash, payloadHash, StringComparison.Ordinal))
                return ApiResponse<ReferralDto>.Fail("Idempotency key was already used with different referral data");
            var replayDto = await _repository.GetDtoAsync(schoolId.Value, replay.ReferralId, cancellationToken)
                .ConfigureAwait(false);
            return replayDto is null
                ? ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.NotFound)
                : ApiResponse<ReferralDto>.Success(replayDto, "Referral request already processed");
        }

        var now = _timeProvider.GetUtcNow();
        var enrollment = await _repository.GetActiveEnrollmentAsync(
            schoolId.Value,
            request.StudentId,
            DateOnly.FromDateTime(now.DateTime),
            cancellationToken).ConfigureAwait(false);

        if (enrollment is null)
            return ApiResponse<ReferralDto>.Fail("Student does not have an active enrollment in the current term");

        var referral = new StudentReferral
        {
            SchoolId = schoolId.Value,
            StudentId = request.StudentId,
            AcademicTermId = enrollment.AcademicTermId,
            SourceType = source,
            Priority = priority,
            IdempotencyKey = idempotencyKey,
            IdempotencyPayloadHash = payloadHash,
            Status = StudentReferralStatus.Open,
            RecommendedActions = request.Reason.Trim(),
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };

        ReferralHandlerSupport.AppendTransition(
            referral,
            StudentReferralStatus.Open,
            StudentReferralStatus.Open,
            userId,
            officerMayCreate ? RoleNames.StudentAffairsOfficer : RoleNames.Instructor,
            now,
            request.Reason.Trim());

        _repository.Add(referral);
        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ReferralIdempotencyConflictException)
        {
            var winner = await _repository.GetByIdempotencyKeyAsync(
                schoolId.Value, userId, idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (winner is null || !string.Equals(winner.PayloadHash, payloadHash, StringComparison.Ordinal))
                return ApiResponse<ReferralDto>.Fail("Idempotency key was already used with different referral data");
            var winnerDto = await _repository.GetDtoAsync(schoolId.Value, winner.ReferralId, cancellationToken)
                .ConfigureAwait(false);
            return winnerDto is null
                ? ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.NotFound)
                : ApiResponse<ReferralDto>.Success(winnerDto, "Referral request already processed");
        }

        var dto = await _repository.GetDtoAsync(schoolId.Value, referral.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The created referral could not be loaded");

        return ApiResponse<ReferralDto>.Success(dto, "Referral created successfully");
    }
}
