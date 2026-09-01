using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Referrals.Handlers;

public sealed class AddReferralActionCommandHandler
    : IRequestHandler<AddReferralActionCommand, ApiResponse<ReferralDto>>
{
    private readonly IReferralWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public AddReferralActionCommandHandler(
        IReferralWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<ReferralDto>> Handle(
        AddReferralActionCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.ReferralManage))
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.PermissionDenied);

        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.Description))
            return ApiResponse<ReferralDto>.Fail("Action description is required");

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

        if (!_currentUser.HasPermission(PermissionNames.ReferralAssign)
            && !await _repository.IsAssignedToAsync(schoolId.Value, referral.Id, userId, cancellationToken).ConfigureAwait(false))
        {
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.AssignmentDenied);
        }

        var now = _timeProvider.GetUtcNow();
        _repository.SetExpectedRowVersion(referral, expectedRowVersion);

        if (referral.Status == StudentReferralStatus.Open || referral.Status == StudentReferralStatus.Assigned)
        {
            referral.Status = StudentReferralStatus.InProgress;
        }

        referral.UpdatedAt = now;
        referral.UpdatedByUserId = userId;

        var action = ReferralHandlerSupport.CreateAction(
            referral,
            request.ActionType == 0 ? StudentCaseActionType.Other : request.ActionType,
            request.Description.Trim(),
            userId,
            request.ActionAt ?? now,
            request.Result?.Trim());

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

        return ApiResponse<ReferralDto>.Success(dto, "Action recorded successfully");
    }
}
