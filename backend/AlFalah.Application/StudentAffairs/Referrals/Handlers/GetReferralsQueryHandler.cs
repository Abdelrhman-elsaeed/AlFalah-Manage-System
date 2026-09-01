using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Referrals.Handlers;

public sealed class GetReferralsQueryHandler
    : IRequestHandler<GetReferralsQuery, ApiResponse<PagedResult<ReferralDto>>>
{
    private readonly IReferralWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetReferralsQueryHandler(
        IReferralWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<ReferralDto>>> Handle(
        GetReferralsQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (schoolId is null || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<PagedResult<ReferralDto>>.Fail(ReferralHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.ReferralView))
            return ApiResponse<PagedResult<ReferralDto>>.Fail(ReferralHandlerSupport.PermissionDenied);

        var result = await _repository.GetReferralsAsync(
            schoolId.Value,
            request.Query,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<PagedResult<ReferralDto>>.Success(result);
    }
}
