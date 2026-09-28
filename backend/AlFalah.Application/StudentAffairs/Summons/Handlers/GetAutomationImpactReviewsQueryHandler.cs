using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class GetAutomationImpactReviewsQueryHandler
    : IRequestHandler<GetAutomationImpactReviewsQuery, ApiResponse<PagedResult<SummonDto>>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAutomationImpactReviewsQueryHandler(
        ISummonWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<SummonDto>>> Handle(
        GetAutomationImpactReviewsQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (!_currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<PagedResult<SummonDto>>.Fail(SummonHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            || !_currentUser.HasPermission(PermissionNames.SummonReviewAutomationImpact))
            return ApiResponse<PagedResult<SummonDto>>.Fail(SummonHandlerSupport.PermissionDenied);

        request.Query.RequiresOfficerReview = true;
        var result = await _repository.GetSummonsAsync(
            schoolId.Value, request.Query, cancellationToken).ConfigureAwait(false);
        return ApiResponse<PagedResult<SummonDto>>.Success(result);
    }
}
