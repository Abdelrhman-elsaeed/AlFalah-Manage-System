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

public sealed class UpdateStudentCommandHandler
    : IRequestHandler<UpdateStudentCommand, ApiResponse<StudentDetailsDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public UpdateStudentCommandHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<StudentDetailsDto>> Handle(
        UpdateStudentCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.StudentManage)
            && !_currentUser.HasPermission(PermissionNames.StudentEdit)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var student = await _repository.GetStudentForUpdateAsync(
            schoolId.Value,
            command.StudentId,
            cancellationToken).ConfigureAwait(false);

        if (student is null)
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.NotFound);

        var req = command.Request;
        var now = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.DateTime);
        var studentNumber = req.StudentNumber.Trim();
        var identityNumber = req.IdentityNumber.Trim();
        var nationalId = string.IsNullOrWhiteSpace(req.NationalId) ? null : req.NationalId.Trim();

        if (await _repository.StudentNumberExistsAsync(
                schoolId.Value,
                studentNumber,
                student.Id,
                cancellationToken).ConfigureAwait(false))
        {
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.DuplicateStudentNumber);
        }

        if (await _repository.StudentIdentityNumberExistsAsync(
                schoolId.Value,
                identityNumber,
                student.Id,
                cancellationToken).ConfigureAwait(false))
        {
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.DuplicateIdentityNumber);
        }

        if (nationalId is not null && await _repository.StudentNationalIdExistsAsync(
                schoolId.Value,
                nationalId,
                student.Id,
                cancellationToken).ConfigureAwait(false))
        {
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.DuplicateNationalId);
        }

        var requestedClassroomId = req.IsActive ? req.ClassroomId : null;
        StudentEnrollmentTarget? enrollmentTarget = null;
        if (requestedClassroomId.HasValue)
        {
            enrollmentTarget = await _repository.GetStudentEnrollmentTargetAsync(
                schoolId.Value,
                requestedClassroomId.Value,
                cancellationToken).ConfigureAwait(false);

            if (enrollmentTarget is null)
                return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.ClassroomNotAvailable);
        }

        var activeEnrollment = await _repository.GetActiveStudentEnrollmentForUpdateAsync(
            schoolId.Value,
            student.Id,
            cancellationToken).ConfigureAwait(false);

        student.StudentNumber = studentNumber;
        student.IdentityNumber = identityNumber;
        student.FirstName = req.FirstName.Trim();
        student.MiddleName = string.IsNullOrWhiteSpace(req.MiddleName) ? null : req.MiddleName.Trim();
        student.LastName = req.LastName.Trim();
        student.NationalId = nationalId;
        student.DateOfBirth = req.DateOfBirth;
        student.Gender = req.Gender;
        student.IsActive = req.IsActive;
        student.UpdatedAt = now;
        student.UpdatedByUserId = userId;

        if (activeEnrollment is not null && enrollmentTarget is null)
        {
            CloseEnrollment(activeEnrollment, StudentEnrollmentStatus.Withdrawn, today, now, userId);
        }
        else if (enrollmentTarget is not null
                 && activeEnrollment is not null
                 && activeEnrollment.ClassroomId == enrollmentTarget.ClassroomId
                 && activeEnrollment.AcademicTermId == enrollmentTarget.AcademicTermId)
        {
            activeEnrollment.RollNumber = req.RollNumber;
            activeEnrollment.UpdatedAt = now;
            activeEnrollment.UpdatedByUserId = userId;
        }
        else if (enrollmentTarget is not null)
        {
            if (activeEnrollment is not null)
                CloseEnrollment(activeEnrollment, StudentEnrollmentStatus.Transferred, today, now, userId);

            _repository.AddEnrollment(new StudentEnrollment
            {
                SchoolId = schoolId.Value,
                StudentId = student.Id,
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

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var details = await _repository.GetStudentDetailsAsync(
            schoolId.Value,
            student.Id,
            today,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<StudentDetailsDto>.Success(details!, "Student updated successfully");
    }

    private static void CloseEnrollment(
        StudentEnrollment enrollment,
        StudentEnrollmentStatus status,
        DateOnly effectiveOn,
        DateTimeOffset changedAt,
        string changedByUserId)
    {
        enrollment.Status = status;
        enrollment.WithdrawnOn = effectiveOn < enrollment.EnrolledOn
            ? enrollment.EnrolledOn
            : effectiveOn;
        enrollment.UpdatedAt = changedAt;
        enrollment.UpdatedByUserId = changedByUserId;
    }
}
