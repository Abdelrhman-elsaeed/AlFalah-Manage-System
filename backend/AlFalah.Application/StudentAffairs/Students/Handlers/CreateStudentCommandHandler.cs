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

public sealed class CreateStudentCommandHandler
    : IRequestHandler<CreateStudentCommand, ApiResponse<StudentDetailsDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateStudentCommandHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<StudentDetailsDto>> Handle(
        CreateStudentCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.StudentCreate)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var req = command.Request;
        var now = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.DateTime);

        var student = new Student
        {
            SchoolId = schoolId.Value,
            StudentNumber = req.StudentNumber.Trim(),
            FirstName = req.FirstName.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(req.MiddleName) ? null : req.MiddleName.Trim(),
            LastName = req.LastName.Trim(),
            NationalId = string.IsNullOrWhiteSpace(req.NationalId) ? null : req.NationalId.Trim(),
            DateOfBirth = req.DateOfBirth,
            Gender = req.Gender,
            IsActive = true,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };

        if (req.InitialAcademicTermId > 0 && req.InitialClassroomId > 0)
        {
            student.Enrollments.Add(new StudentEnrollment
            {
                SchoolId = schoolId.Value,
                AcademicTermId = req.InitialAcademicTermId,
                ClassroomId = req.InitialClassroomId,
                RollNumber = req.RollNumber,
                EnrolledOn = today,
                Status = StudentEnrollmentStatus.Active,
                CreatedAt = now,
                CreatedByUserId = userId,
                UpdatedAt = now,
                UpdatedByUserId = userId
            });
        }

        _repository.AddStudent(student);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var details = await _repository.GetStudentDetailsAsync(
            schoolId.Value,
            student.Id,
            today,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<StudentDetailsDto>.Success(details!, "Student created successfully");
    }
}
