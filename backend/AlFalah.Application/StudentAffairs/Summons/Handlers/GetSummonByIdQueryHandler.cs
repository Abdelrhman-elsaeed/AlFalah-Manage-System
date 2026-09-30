using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class GetSummonByIdQueryHandler
    : IRequestHandler<GetSummonByIdQuery, ApiResponse<SummonDto>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetSummonByIdQueryHandler(
        ISummonWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<SummonDto>> Handle(
        GetSummonByIdQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.SummonView))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.PermissionDenied);

        var isOfficer = _currentUser.IsInRole(RoleNames.StudentAffairsOfficer);
        var isSocialWorker = _currentUser.IsInRole(RoleNames.SocialWorker);
        if (!isOfficer && !isSocialWorker)
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.PermissionDenied);
        if (isSocialWorker && !await _repository.IsAssignedToAsync(
                schoolId.Value, request.SummonId, userId, cancellationToken).ConfigureAwait(false))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.NotFound);

        var dto = await _repository.GetDtoAsync(
            schoolId.Value,
            request.SummonId,
            cancellationToken).ConfigureAwait(false);

        if (dto is null)
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.NotFound);

        if (isOfficer)
            dto = dto with
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
            };

        return ApiResponse<SummonDto>.Success(dto);
    }
}
