using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Referrals.Handlers;

public sealed class GetReferralByIdQueryHandler
    : IRequestHandler<GetReferralByIdQuery, ApiResponse<ReferralDto>>
{
    private readonly IReferralWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetReferralByIdQueryHandler(
        IReferralWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<ReferralDto>> Handle(
        GetReferralByIdQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (schoolId is null || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.ReferralView))
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.PermissionDenied);

        var dto = await _repository.GetDtoAsync(
            schoolId.Value,
            request.ReferralId,
            cancellationToken).ConfigureAwait(false);

        if (dto is null)
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.NotFound);

        return ApiResponse<ReferralDto>.Success(dto);
    }
}
