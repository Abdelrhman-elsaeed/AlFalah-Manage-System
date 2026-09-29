using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class GetSecurityGatePassByIdQueryHandler
    : IRequestHandler<GetSecurityGatePassByIdQuery, ApiResponse<SecurityGatePassDetailDto>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetSecurityGatePassByIdQueryHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<SecurityGatePassDetailDto>> Handle(
        GetSecurityGatePassByIdQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (schoolId is null || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<SecurityGatePassDetailDto>.Fail(GatePassHandlerSupport.AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.SecurityGuard)
            || (!_currentUser.HasPermission(PermissionNames.StudentAffairsDashboardSecurity)
                && !_currentUser.HasPermission(PermissionNames.GatePassAcknowledgeSecurity)
                && !_currentUser.HasPermission(PermissionNames.GatePassExecute)))
            return ApiResponse<SecurityGatePassDetailDto>.Fail(GatePassHandlerSupport.PermissionDenied);

        var detail = await _repository.GetSecurityDetailAsync(
            schoolId.Value,
            request.GatePassId,
            cancellationToken).ConfigureAwait(false);

        return detail is null
            ? ApiResponse<SecurityGatePassDetailDto>.Fail("Gate pass was not found")
            : ApiResponse<SecurityGatePassDetailDto>.Success(detail);
    }
}
