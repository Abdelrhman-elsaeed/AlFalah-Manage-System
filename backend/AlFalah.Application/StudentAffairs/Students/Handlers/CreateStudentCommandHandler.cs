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

        if (!_currentUser.HasPermission(PermissionNames.StudentManage)
            && !_currentUser.HasPermission(PermissionNames.StudentCreate)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var req = command.Request;
        var now = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.DateTime);
        var studentNumber = req.StudentNumber.Trim();
        var identityNumber = req.IdentityNumber.Trim();
        var nationalId = string.IsNullOrWhiteSpace(req.NationalId) ? null : req.NationalId.Trim();

        if (await _repository.StudentNumberExistsAsync(
                schoolId.Value,
                studentNumber,
                null,
                cancellationToken).ConfigureAwait(false))
        {
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.DuplicateStudentNumber);
        }

        if (await _repository.StudentIdentityNumberExistsAsync(
                schoolId.Value,
                identityNumber,
                null,
                cancellationToken).ConfigureAwait(false))
        {
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.DuplicateIdentityNumber);
        }

        if (nationalId is not null && await _repository.StudentNationalIdExistsAsync(
                schoolId.Value,
                nationalId,
                null,
                cancellationToken).ConfigureAwait(false))
        {
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.DuplicateNationalId);
        }

        StudentEnrollmentTarget? enrollmentTarget = null;
        if (req.ClassroomId.HasValue)
        {
            enrollmentTarget = await _repository.GetStudentEnrollmentTargetAsync(
                schoolId.Value,
                req.ClassroomId.Value,
                cancellationToken).ConfigureAwait(false);

            if (enrollmentTarget is null)
                return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.ClassroomNotAvailable);
        }

        var student = new Student
        {
            SchoolId = schoolId.Value,
            StudentNumber = studentNumber,
            IdentityNumber = identityNumber,
            FirstName = req.FirstName.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(req.MiddleName) ? null : req.MiddleName.Trim(),
            LastName = req.LastName.Trim(),
            NationalId = nationalId,
            DateOfBirth = req.DateOfBirth,
            Gender = req.Gender,
            IsActive = true,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };

        if (enrollmentTarget is not null)
        {
            student.Enrollments.Add(new StudentEnrollment
            {
                SchoolId = schoolId.Value,
                AcademicTermId = enrollmentTarget.AcademicTermId,
                ClassroomId = enrollmentTarget.ClassroomId,
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
