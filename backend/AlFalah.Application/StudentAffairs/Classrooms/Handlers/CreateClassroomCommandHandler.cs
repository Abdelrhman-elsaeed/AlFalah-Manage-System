using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Application.StudentAffairs.Students;
using AlFalah.Application.StudentAffairs.Students.Handlers;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Classrooms.Handlers;

public sealed class CreateClassroomCommandHandler
    : IRequestHandler<CreateClassroomCommand, ApiResponse<ClassroomDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateClassroomCommandHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<ClassroomDto>> Handle(
        CreateClassroomCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ClassroomDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.StudentEnrollmentManage)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<ClassroomDto>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var req = command.Request;
        var now = _timeProvider.GetUtcNow();

        var classroom = new Classroom
        {
            SchoolId = schoolId.Value,
            AcademicYearId = req.AcademicYearId,
            Stage = req.Stage,
            GradeLevel = req.GradeLevel,
            Section = req.Section.Trim(),
            ClassLabel = req.ClassLabel.Trim(),
            IsActive = true,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };

        _repository.AddClassroom(classroom);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetClassroomDtoAsync(schoolId.Value, classroom.Id, cancellationToken).ConfigureAwait(false);
        return ApiResponse<ClassroomDto>.Success(dto!, "Classroom created successfully");
    }
}
