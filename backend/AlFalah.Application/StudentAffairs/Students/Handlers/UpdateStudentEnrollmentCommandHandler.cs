using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Enums;
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

        if (!_currentUser.HasPermission(PermissionNames.StudentEnrollmentManage)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
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

        enrollment.Status = req.Status;
        if (req.ClassroomId.HasValue && req.ClassroomId.Value > 0)
            enrollment.ClassroomId = req.ClassroomId.Value;

        if (req.Status == Domain.Enums.StudentAffairs.StudentEnrollmentStatus.Withdrawn
            || req.Status == Domain.Enums.StudentAffairs.StudentEnrollmentStatus.Transferred)
        {
            enrollment.WithdrawnOn = req.EffectiveOn;
        }

        enrollment.UpdatedAt = now;
        enrollment.UpdatedByUserId = userId;

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetEnrollmentDtoAsync(schoolId.Value, enrollment.Id, cancellationToken).ConfigureAwait(false);
        return ApiResponse<StudentEnrollmentDto>.Success(dto!, "Student enrollment updated successfully");
    }
}
