using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Referrals.Handlers;

public sealed class GetReferralHistoryQueryHandler
    : IRequestHandler<GetReferralHistoryQuery, ApiResponse<ReferralHistoryDto>>
{
    private readonly IReferralWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetReferralHistoryQueryHandler(
        IReferralWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<ReferralHistoryDto>> Handle(
        GetReferralHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ReferralHistoryDto>.Fail(ReferralHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.SocialWorker)
            || !_currentUser.HasPermission(PermissionNames.ReferralView))
            return ApiResponse<ReferralHistoryDto>.Fail(ReferralHandlerSupport.PermissionDenied);
        if (!await _repository.IsAssignedToAsync(
                schoolId.Value, request.ReferralId, userId, cancellationToken).ConfigureAwait(false))
            return ApiResponse<ReferralHistoryDto>.Fail(ReferralHandlerSupport.NotFound);

        var history = await _repository.GetHistoryAsync(
            schoolId.Value, request.ReferralId, cancellationToken).ConfigureAwait(false);
        return history is null
            ? ApiResponse<ReferralHistoryDto>.Fail(ReferralHandlerSupport.NotFound)
            : ApiResponse<ReferralHistoryDto>.Success(history);
    }
}
