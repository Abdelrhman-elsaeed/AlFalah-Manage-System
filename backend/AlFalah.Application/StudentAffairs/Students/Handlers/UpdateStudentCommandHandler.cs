using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Enums;
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

        if (!_currentUser.HasPermission(PermissionNames.StudentEdit)
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

        student.FirstName = req.FirstName.Trim();
        student.MiddleName = string.IsNullOrWhiteSpace(req.MiddleName) ? null : req.MiddleName.Trim();
        student.LastName = req.LastName.Trim();
        student.NationalId = string.IsNullOrWhiteSpace(req.NationalId) ? null : req.NationalId.Trim();
        student.DateOfBirth = req.DateOfBirth;
        student.Gender = req.Gender;
        student.IsActive = req.IsActive;
        student.UpdatedAt = now;
        student.UpdatedByUserId = userId;

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var details = await _repository.GetStudentDetailsAsync(
            schoolId.Value,
            student.Id,
            today,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<StudentDetailsDto>.Success(details!, "Student updated successfully");
    }
}
