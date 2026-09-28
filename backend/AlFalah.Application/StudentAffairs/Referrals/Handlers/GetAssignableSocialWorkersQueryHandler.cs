using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Referrals.Handlers;

public sealed class GetAssignableSocialWorkersQueryHandler
    : IRequestHandler<GetAssignableSocialWorkersQuery, ApiResponse<IReadOnlyList<AssignableSocialWorkerDto>>>
{
    private readonly IReferralWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAssignableSocialWorkersQueryHandler(
        IReferralWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<AssignableSocialWorkerDto>>> Handle(
        GetAssignableSocialWorkersQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (!_currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<IReadOnlyList<AssignableSocialWorkerDto>>.Fail(ReferralHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            || !_currentUser.HasPermission(PermissionNames.ReferralAssign))
            return ApiResponse<IReadOnlyList<AssignableSocialWorkerDto>>.Fail(ReferralHandlerSupport.PermissionDenied);

        var workers = await _repository.GetAssignableSocialWorkersAsync(
            schoolId.Value,
            request.Search,
            cancellationToken).ConfigureAwait(false);
        return ApiResponse<IReadOnlyList<AssignableSocialWorkerDto>>.Success(workers);
    }
}
