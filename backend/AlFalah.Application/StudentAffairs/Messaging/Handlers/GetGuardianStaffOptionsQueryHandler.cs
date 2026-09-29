using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Messaging.Handlers;

public sealed class GetGuardianStaffOptionsQueryHandler(
    IMessagingWorkflowRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetGuardianStaffOptionsQuery, ApiResponse<IReadOnlyList<GuardianStaffOptionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<GuardianStaffOptionDto>>> Handle(
        GetGuardianStaffOptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(currentUser.UserId))
            return ApiResponse<IReadOnlyList<GuardianStaffOptionDto>>.Fail(
                "An authenticated user and active school are required");
        if (!currentUser.IsInRole(RoleNames.Guardian)
            || !currentUser.HasPermission(PermissionNames.MessagingStartGuardianAdministration))
            return ApiResponse<IReadOnlyList<GuardianStaffOptionDto>>.Fail(
                "You do not have permission to perform this action");
        var options = await repository.GetGuardianStaffOptionsAsync(
            schoolId, currentUser.UserId, request.StudentId, timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        return ApiResponse<IReadOnlyList<GuardianStaffOptionDto>>.Success(options);
    }
}
