using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class GetMyGatePassesQueryHandler
    : IRequestHandler<GetMyGatePassesQuery, ApiResponse<PagedResult<GatePassDto>>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMyGatePassesQueryHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<GatePassDto>>> Handle(
        GetMyGatePassesQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<PagedResult<GatePassDto>>.Fail(GatePassHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.GatePassViewOwn))
            return ApiResponse<PagedResult<GatePassDto>>.Fail(GatePassHandlerSupport.PermissionDenied);

        var result = await _repository.GetMyGatePassesAsync(
            schoolId.Value,
            userId,
            request.Query,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<PagedResult<GatePassDto>>.Success(result);
    }
}
