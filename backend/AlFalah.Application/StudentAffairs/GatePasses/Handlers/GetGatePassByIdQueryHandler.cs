using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class GetGatePassByIdQueryHandler
    : IRequestHandler<GetGatePassByIdQuery, ApiResponse<GatePassDto>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetGatePassByIdQueryHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<GatePassDto>> Handle(
        GetGatePassByIdQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (schoolId is null || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.GatePassView)
            && !_currentUser.HasPermission(PermissionNames.GatePassViewOwn)
            && !_currentUser.HasPermission(PermissionNames.GatePassExecute))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.PermissionDenied);

        var dto = await _repository.GetDtoAsync(
            schoolId.Value,
            request.GatePassId,
            cancellationToken).ConfigureAwait(false);

        if (dto is null)
            return ApiResponse<GatePassDto>.Fail("Gate pass was not found");

        return ApiResponse<GatePassDto>.Success(dto);
    }
}
