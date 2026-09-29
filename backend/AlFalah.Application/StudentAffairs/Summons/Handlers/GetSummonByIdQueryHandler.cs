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

        var dto = await _repository.GetDtoAsync(
            schoolId.Value,
            request.SummonId,
            cancellationToken).ConfigureAwait(false);

        if (dto is null)
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.NotFound);

        return ApiResponse<SummonDto>.Success(dto);
    }
}
