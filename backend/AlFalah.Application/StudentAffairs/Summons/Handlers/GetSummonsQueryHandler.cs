using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class GetSummonsQueryHandler
    : IRequestHandler<GetSummonsQuery, ApiResponse<PagedResult<SummonDto>>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetSummonsQueryHandler(
        ISummonWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<SummonDto>>> Handle(
        GetSummonsQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (schoolId is null || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<PagedResult<SummonDto>>.Fail(SummonHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.SummonView))
            return ApiResponse<PagedResult<SummonDto>>.Fail(SummonHandlerSupport.PermissionDenied);

        var isOfficer = _currentUser.IsInRole(RoleNames.StudentAffairsOfficer);
        var isSocialWorker = _currentUser.IsInRole(RoleNames.SocialWorker);
        if (!isOfficer && !isSocialWorker)
            return ApiResponse<PagedResult<SummonDto>>.Fail(SummonHandlerSupport.PermissionDenied);

        if (isSocialWorker)
            request.Query.AssignedWorkerUserId = _currentUser.UserId;

        var result = await _repository.GetSummonsAsync(
            schoolId.Value,
            request.Query,
            cancellationToken).ConfigureAwait(false);

        if (isOfficer)
        {
            result.Items = result.Items.Select(item => item with
            {
                ObservationGoals = null,
                ObservationStartDate = null,
                ObservationReviewDate = null,
                ObservationEndDate = null,
                ObservationResponsibleStaffUserId = null,
                ObservationIndicators = null,
                ObservationNotes = null,
                OutcomeEvidence = null,
                OutcomeVerificationDetails = null
            }).ToList();
        }

        return ApiResponse<PagedResult<SummonDto>>.Success(result);
    }
}
