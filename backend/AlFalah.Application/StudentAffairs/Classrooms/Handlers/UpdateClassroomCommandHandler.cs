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

public sealed class UpdateClassroomCommandHandler
    : IRequestHandler<UpdateClassroomCommand, ApiResponse<ClassroomDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public UpdateClassroomCommandHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<ClassroomDto>> Handle(
        UpdateClassroomCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ClassroomDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.StudentEnrollmentManage)
            && !_currentUser.HasPermission(PermissionNames.ClassroomManage)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<ClassroomDto>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var classroom = await _repository.GetClassroomForUpdateAsync(
            schoolId.Value,
            command.ClassroomId,
            cancellationToken).ConfigureAwait(false);

        if (classroom is null)
            return ApiResponse<ClassroomDto>.Fail(StudentHandlerSupport.NotFound);

        var req = command.Request;
        var classLabel = req.ClassLabel.Trim();
        if (await _repository.ClassroomLabelExistsAsync(
                schoolId.Value,
                classroom.AcademicYearId,
                classLabel,
                classroom.Id,
                cancellationToken).ConfigureAwait(false))
        {
            return ApiResponse<ClassroomDto>.Fail("A classroom with the same name already exists in this academic year");
        }

        var now = _timeProvider.GetUtcNow();

        classroom.ClassLabel = classLabel;
        classroom.Section = req.Section.Trim();
        classroom.IsActive = req.IsActive;
        classroom.UpdatedAt = now;
        classroom.UpdatedByUserId = userId;

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetClassroomDtoAsync(schoolId.Value, classroom.Id, cancellationToken).ConfigureAwait(false);
        return ApiResponse<ClassroomDto>.Success(dto!, "Classroom updated successfully");
    }
}
