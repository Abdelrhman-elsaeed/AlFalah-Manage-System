using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Messaging.Handlers;

public sealed class GetStudentGuardianOptionsQueryHandler(
    IMessagingWorkflowRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetStudentGuardianOptionsQuery, ApiResponse<IReadOnlyList<StudentGuardianOptionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<StudentGuardianOptionDto>>> Handle(
        GetStudentGuardianOptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(currentUser.UserId))
            return ApiResponse<IReadOnlyList<StudentGuardianOptionDto>>.Fail(
                "An authenticated user and active school are required");
        if (!currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            || !currentUser.HasPermission(PermissionNames.MessagingStartOfficerGuardian))
            return ApiResponse<IReadOnlyList<StudentGuardianOptionDto>>.Fail(
                "You do not have permission to perform this action");

        var options = await repository.GetStudentGuardianOptionsAsync(
            schoolId, request.StudentId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return ApiResponse<IReadOnlyList<StudentGuardianOptionDto>>.Success(options);
    }
}
