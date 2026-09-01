using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class GetMySummonsQueryHandler
    : IRequestHandler<GetMySummonsQuery, ApiResponse<PagedResult<SummonDto>>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMySummonsQueryHandler(
        ISummonWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<SummonDto>>> Handle(
        GetMySummonsQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<PagedResult<SummonDto>>.Fail(SummonHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.GuardianViewLinkedStudents))
            return ApiResponse<PagedResult<SummonDto>>.Fail(SummonHandlerSupport.PermissionDenied);

        var result = await _repository.GetMySummonsAsync(
            schoolId.Value,
            userId,
            request.Query,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<PagedResult<SummonDto>>.Success(result);
    }
}
