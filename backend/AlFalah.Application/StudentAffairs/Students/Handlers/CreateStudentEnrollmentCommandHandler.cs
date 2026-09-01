using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Students.Handlers;

public sealed class CreateStudentEnrollmentCommandHandler
    : IRequestHandler<CreateStudentEnrollmentCommand, ApiResponse<StudentEnrollmentDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateStudentEnrollmentCommandHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<StudentEnrollmentDto>> Handle(
        CreateStudentEnrollmentCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<StudentEnrollmentDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.StudentEnrollmentManage)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<StudentEnrollmentDto>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var student = await _repository.GetStudentForUpdateAsync(
            schoolId.Value,
            command.StudentId,
            cancellationToken).ConfigureAwait(false);

        if (student is null)
            return ApiResponse<StudentEnrollmentDto>.Fail(StudentHandlerSupport.StudentNotFound);

        var req = command.Request;
        var now = _timeProvider.GetUtcNow();

        var enrollment = new StudentEnrollment
        {
            SchoolId = schoolId.Value,
            StudentId = command.StudentId,
            AcademicTermId = req.AcademicTermId,
            ClassroomId = req.ClassroomId,
            RollNumber = req.RollNumber,
            EnrolledOn = req.EnrolledOn,
            Status = StudentEnrollmentStatus.Active,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };

        _repository.AddEnrollment(enrollment);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetEnrollmentDtoAsync(schoolId.Value, enrollment.Id, cancellationToken).ConfigureAwait(false);
        return ApiResponse<StudentEnrollmentDto>.Success(dto!, "Student enrollment created successfully");
    }
}
