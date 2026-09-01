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

public sealed class ArchiveClassroomCommandHandler
    : IRequestHandler<ArchiveClassroomCommand, ApiResponse<bool>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public ArchiveClassroomCommandHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<bool>> Handle(
        ArchiveClassroomCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<bool>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.StudentEnrollmentManage)
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

        var now = _timeProvider.GetUtcNow();
        classroom.IsDeleted = true;
        classroom.DeletedAt = now;
        classroom.DeletedByUserId = userId;
        classroom.IsActive = false;

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ApiResponse<bool>.Success(true, "Classroom archived successfully");
    }
}
