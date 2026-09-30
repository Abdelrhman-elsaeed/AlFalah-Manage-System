using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class GetSummonHistoryQueryHandler
    : IRequestHandler<GetSummonHistoryQuery, ApiResponse<SummonHistoryDto>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetSummonHistoryQueryHandler(
        ISummonWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<SummonHistoryDto>> Handle(
        GetSummonHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SummonHistoryDto>.Fail(SummonHandlerSupport.AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.SocialWorker)
            || !_currentUser.HasPermission(PermissionNames.SummonViewHistory))
            return ApiResponse<SummonHistoryDto>.Fail(SummonHandlerSupport.PermissionDenied);
        if (!await _repository.IsAssignedToAsync(
                schoolId.Value, request.SummonId, userId, cancellationToken).ConfigureAwait(false))
            return ApiResponse<SummonHistoryDto>.Fail(SummonHandlerSupport.NotFound);

        var history = await _repository.GetHistoryAsync(
            schoolId.Value,
            request.SummonId,
            cancellationToken).ConfigureAwait(false);

        if (history is null)
            return ApiResponse<SummonHistoryDto>.Fail(SummonHandlerSupport.NotFound);

        return ApiResponse<SummonHistoryDto>.Success(history);
    }
}
