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

public sealed class UpdateStudentEnrollmentCommandHandler
    : IRequestHandler<UpdateStudentEnrollmentCommand, ApiResponse<StudentEnrollmentDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public UpdateStudentEnrollmentCommandHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<StudentEnrollmentDto>> Handle(
        UpdateStudentEnrollmentCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<StudentEnrollmentDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.StudentEnrollmentManage))
        {
            return ApiResponse<StudentEnrollmentDto>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var enrollment = await _repository.GetEnrollmentForUpdateAsync(
            schoolId.Value,
            command.StudentId,
            command.EnrollmentId,
            cancellationToken).ConfigureAwait(false);

        if (enrollment is null)
            return ApiResponse<StudentEnrollmentDto>.Fail(StudentHandlerSupport.NotFound);

        var req = command.Request;
        var now = _timeProvider.GetUtcNow();

        if (req.EffectiveOn < enrollment.EnrolledOn)
            return ApiResponse<StudentEnrollmentDto>.Fail("The enrollment effective date cannot precede its start date");

        var requestedClassroomId = req.ClassroomId.GetValueOrDefault(enrollment.ClassroomId);
        var target = await _repository.GetStudentEnrollmentTargetAsync(
            schoolId.Value,
            requestedClassroomId,
            enrollment.AcademicTermId,
            req.EffectiveOn,
            cancellationToken).ConfigureAwait(false);
        if (target is null)
            return ApiResponse<StudentEnrollmentDto>.Fail(
                "The academic term and classroom must belong to the active school and the same academic year");

        if (await _repository.HasOverlappingStudentEnrollmentAsync(
                schoolId.Value,
                command.StudentId,
                req.EffectiveOn,
                req.Status == StudentEnrollmentStatus.Active ? null : req.EffectiveOn,
                enrollment.Id,
                cancellationToken).ConfigureAwait(false))
            return ApiResponse<StudentEnrollmentDto>.Fail("The enrollment change overlaps another enrollment");

        var resultEnrollment = enrollment;
        var classroomChanged = requestedClassroomId != enrollment.ClassroomId;
        if (classroomChanged && req.Status != StudentEnrollmentStatus.Active)
            return ApiResponse<StudentEnrollmentDto>.Fail(
                "Close the historical enrollment without changing its classroom");

        if (classroomChanged)
        {
            enrollment.Status = StudentEnrollmentStatus.Transferred;
            enrollment.WithdrawnOn = req.EffectiveOn;
            enrollment.UpdatedAt = now;
            enrollment.UpdatedByUserId = userId;

            resultEnrollment = new StudentEnrollment
            {
                SchoolId = schoolId.Value,
                StudentId = enrollment.StudentId,
                ClassroomId = target.ClassroomId,
                AcademicTermId = target.AcademicTermId,
                RollNumber = enrollment.RollNumber,
                EnrolledOn = req.EffectiveOn,
                Status = StudentEnrollmentStatus.Active,
                CreatedAt = now,
                CreatedByUserId = userId,
                UpdatedAt = now,
                UpdatedByUserId = userId
            };
            _repository.AddEnrollment(resultEnrollment);
        }
        else
        {
            enrollment.Status = req.Status;
            enrollment.WithdrawnOn = req.Status is StudentEnrollmentStatus.Withdrawn or StudentEnrollmentStatus.Transferred
                ? req.EffectiveOn
                : null;
            enrollment.UpdatedAt = now;
            enrollment.UpdatedByUserId = userId;
        }

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetEnrollmentDtoAsync(schoolId.Value, resultEnrollment.Id, cancellationToken).ConfigureAwait(false);
        return ApiResponse<StudentEnrollmentDto>.Success(dto!, "Student enrollment updated successfully");
    }
}
