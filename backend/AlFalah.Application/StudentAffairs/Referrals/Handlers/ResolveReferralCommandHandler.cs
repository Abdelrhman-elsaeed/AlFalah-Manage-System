using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Referrals.Handlers;

public sealed class ResolveReferralCommandHandler
    : IRequestHandler<ResolveReferralCommand, ApiResponse<ReferralDto>>
{
    private readonly IReferralWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public ResolveReferralCommandHandler(
        IReferralWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<ReferralDto>> Handle(
        ResolveReferralCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.ReferralManage))
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.PermissionDenied);

        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.ResolutionNote))
            return ApiResponse<ReferralDto>.Fail("Resolution note is required");

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

        referral.Status = StudentReferralStatus.Resolved;
        referral.ResolutionNotes = request.ResolutionNote.Trim();
        referral.UpdatedAt = now;
        referral.UpdatedByUserId = userId;

        var action = ReferralHandlerSupport.CreateAction(
            referral,
            StudentCaseActionType.Other,
            $"Referral resolved: {request.ResolutionNote.Trim()}",
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

        return ApiResponse<ReferralDto>.Success(dto, "Referral resolved successfully");
    }
}
