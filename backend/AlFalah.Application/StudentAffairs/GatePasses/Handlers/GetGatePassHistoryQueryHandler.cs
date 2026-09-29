using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class GetGatePassHistoryQueryHandler
    : IRequestHandler<GetGatePassHistoryQuery, ApiResponse<GatePassHistoryDto>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetGatePassHistoryQueryHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<GatePassHistoryDto>> Handle(
        GetGatePassHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<GatePassHistoryDto>.Fail(GatePassHandlerSupport.AuthenticationRequired);

        var isGuardian = _currentUser.IsInRole(RoleNames.Guardian);
        if (isGuardian
            ? !_currentUser.HasPermission(PermissionNames.GatePassViewOwn)
            : !_currentUser.HasPermission(PermissionNames.GatePassViewAudit)
                && !_currentUser.HasPermission(PermissionNames.GatePassView))
            return ApiResponse<GatePassHistoryDto>.Fail(GatePassHandlerSupport.PermissionDenied);

        if (isGuardian
            && (!await _repository.IsActiveGuardianAsync(schoolId.Value, userId, cancellationToken).ConfigureAwait(false)
                || !await _repository.IsOwnedByGuardianAsync(
                    schoolId.Value, request.GatePassId, userId, cancellationToken).ConfigureAwait(false)))
            return ApiResponse<GatePassHistoryDto>.Fail("Gate pass history was not found");

        var history = await _repository.GetHistoryAsync(
            schoolId.Value,
            request.GatePassId,
            cancellationToken).ConfigureAwait(false);

        if (history is null)
            return ApiResponse<GatePassHistoryDto>.Fail("Gate pass history was not found");

        return ApiResponse<GatePassHistoryDto>.Success(history);
    }
}
