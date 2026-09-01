using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Students.Handlers;

public sealed class RevokeStudentGuardianCommandHandler
    : IRequestHandler<RevokeStudentGuardianCommand, ApiResponse<bool>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public RevokeStudentGuardianCommandHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<bool>> Handle(
        RevokeStudentGuardianCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<bool>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.GuardianLinkStudent)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<bool>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var link = await _repository.GetGuardianLinkForUpdateAsync(
            schoolId.Value,
            command.StudentId,
            command.LinkId,
            cancellationToken).ConfigureAwait(false);

        if (link is null)
            return ApiResponse<bool>.Fail(StudentHandlerSupport.NotFound);

        var now = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.DateTime);

        link.ValidTo = today;
        link.UpdatedAt = now;
        link.UpdatedByUserId = userId;

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ApiResponse<bool>.Success(true, "Guardian link revoked successfully");
    }
}
