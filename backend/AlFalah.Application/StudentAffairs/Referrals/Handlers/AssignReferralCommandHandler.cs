using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Referrals.Handlers;

public sealed class AssignReferralCommandHandler
    : IRequestHandler<AssignReferralCommand, ApiResponse<ReferralDto>>
{
    private readonly IReferralWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public AssignReferralCommandHandler(
        IReferralWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<ReferralDto>> Handle(
        AssignReferralCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.ReferralAssign))
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.PermissionDenied);

        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.SocialWorkerUserId))
            return ApiResponse<ReferralDto>.Fail("Target social worker is required");

        var isSocialWorker = await _repository.IsSocialWorkerAsync(
            schoolId.Value,
            request.SocialWorkerUserId,
            cancellationToken).ConfigureAwait(false);

        if (!isSocialWorker)
            return ApiResponse<ReferralDto>.Fail("Assigned user is not an active social worker");

        var referral = await _repository.GetForUpdateAsync(
            schoolId.Value,
            command.ReferralId,
            cancellationToken).ConfigureAwait(false);

        if (referral is null)
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.NotFound);

        if (!ReferralHandlerSupport.TryDecodeExpectedRowVersion(
                request.RowVersion,
                referral.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.ConcurrencyConflict);

        var now = _timeProvider.GetUtcNow();
        _repository.SetExpectedRowVersion(referral, expectedRowVersion);

        referral.AssignedSocialWorkerUserId = request.SocialWorkerUserId;
        if (referral.Status == StudentReferralStatus.Open)
        {
            referral.Status = StudentReferralStatus.Assigned;
        }
        referral.UpdatedAt = now;
        referral.UpdatedByUserId = userId;

        var reasonText = string.IsNullOrWhiteSpace(request.Reason)
            ? "Referral assigned to social worker"
            : $"Referral assigned to social worker: {request.Reason.Trim()}";

        var action = ReferralHandlerSupport.CreateAction(
            referral,
            StudentCaseActionType.Other,
            reasonText,
            userId,
            now);

        _repository.AddAction(action);

        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ReferralConcurrencyException)
        {
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.ConcurrencyConflict);
        }

        var dto = await _repository.GetDtoAsync(schoolId.Value, referral.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The updated referral could not be loaded");

        return ApiResponse<ReferralDto>.Success(dto, "Referral assigned successfully");
    }
}
