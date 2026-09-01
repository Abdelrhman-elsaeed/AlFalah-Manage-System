using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Students.Handlers;

public sealed class ArchiveStudentCommandHandler
    : IRequestHandler<ArchiveStudentCommand, ApiResponse<bool>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public ArchiveStudentCommandHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<bool>> Handle(
        ArchiveStudentCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<bool>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.StudentArchive)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<bool>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var student = await _repository.GetStudentForUpdateAsync(
            schoolId.Value,
            command.StudentId,
            cancellationToken).ConfigureAwait(false);

        if (student is null)
            return ApiResponse<bool>.Fail(StudentHandlerSupport.NotFound);

        var now = _timeProvider.GetUtcNow();
        student.IsDeleted = true;
        student.DeletedAt = now;
        student.DeletedByUserId = userId;
        student.IsActive = false;

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ApiResponse<bool>.Success(true, "Student archived successfully");
    }
}
