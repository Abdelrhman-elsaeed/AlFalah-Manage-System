using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Messaging.Handlers;

public sealed class GetGuardianTeacherOptionsQueryHandler(
    IMessagingWorkflowRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetGuardianTeacherOptionsQuery, ApiResponse<IReadOnlyList<GuardianTeacherOptionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<GuardianTeacherOptionDto>>> Handle(
        GetGuardianTeacherOptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(currentUser.UserId))
            return ApiResponse<IReadOnlyList<GuardianTeacherOptionDto>>.Fail("An authenticated user and active school are required");
        if (!currentUser.IsInRole(RoleNames.Guardian)
            || !currentUser.HasPermission(PermissionNames.MessagingStartGuardianTeacher))
            return ApiResponse<IReadOnlyList<GuardianTeacherOptionDto>>.Fail("You do not have permission to perform this action");
        var options = await repository.GetGuardianTeacherOptionsAsync(
            schoolId, currentUser.UserId, request.StudentId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return ApiResponse<IReadOnlyList<GuardianTeacherOptionDto>>.Success(options);
    }
}
