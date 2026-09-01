using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class GetGatePassesQueryHandler
    : IRequestHandler<GetGatePassesQuery, ApiResponse<PagedResult<GatePassDto>>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetGatePassesQueryHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<GatePassDto>>> Handle(
        GetGatePassesQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (schoolId is null || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<PagedResult<GatePassDto>>.Fail(GatePassHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.GatePassView))
            return ApiResponse<PagedResult<GatePassDto>>.Fail(GatePassHandlerSupport.PermissionDenied);

        var result = await _repository.GetGatePassesAsync(
            schoolId.Value,
            request.Query,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<PagedResult<GatePassDto>>.Success(result);
    }
}
