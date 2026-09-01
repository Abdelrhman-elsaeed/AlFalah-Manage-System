using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Application.StudentAffairs.Students;
using AlFalah.Application.StudentAffairs.Students.Handlers;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Classrooms.Handlers;

public sealed class DeleteClassroomCommandHandler
    : IRequestHandler<DeleteClassroomCommand, ApiResponse<bool>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public DeleteClassroomCommandHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<bool>> Handle(
        DeleteClassroomCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<bool>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.StudentEnrollmentManage)
            && !_currentUser.HasPermission(PermissionNames.ClassroomManage)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<bool>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var classroom = await _repository.GetClassroomForUpdateAsync(
            schoolId.Value,
            command.ClassroomId,
            cancellationToken).ConfigureAwait(false);

        if (classroom is null)
            return ApiResponse<bool>.Fail(StudentHandlerSupport.NotFound);

        var hasActiveEnrollments = await _repository.HasActiveClassroomEnrollmentsAsync(
                schoolId.Value,
                classroom.Id,
                cancellationToken).ConfigureAwait(false);

        if (hasActiveEnrollments && !command.Request.ForceDelete)
        {
            return ApiResponse<bool>.Fail("Classroom has active student enrollments. Confirm force deletion to unassign them safely.");
        }

        var now = _timeProvider.GetUtcNow();
        if (hasActiveEnrollments)
        {
            await _repository.UnassignActiveClassroomEnrollmentsAsync(
                schoolId.Value,
                classroom.Id,
                DateOnly.FromDateTime(now.DateTime),
                now,
                userId,
                cancellationToken).ConfigureAwait(false);
        }

        classroom.IsDeleted = true;
        classroom.DeletedAt = now;
        classroom.DeletedByUserId = userId;
        classroom.IsActive = false;

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ApiResponse<bool>.Success(true, "Classroom deleted successfully");
    }
}
