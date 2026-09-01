using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Students.Handlers;

public sealed class GetStudentsQueryHandler
    : IRequestHandler<GetStudentsQuery, ApiResponse<PagedResult<StudentListItemDto>>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetStudentsQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<PagedResult<StudentListItemDto>>> Handle(
        GetStudentsQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<PagedResult<StudentListItemDto>>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!StudentHandlerSupport.HasAnyPermission(
                _currentUser,
                PermissionNames.StudentView,
                PermissionNames.TeacherQuickActionView,
                PermissionNames.AttendanceViewStudents,
                PermissionNames.ReferralView,
                PermissionNames.SummonView)
            && !_currentUser.IsInRole(RoleNames.SocialWorker)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<PagedResult<StudentListItemDto>>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
        var result = await _repository.GetStudentsAsync(
            schoolId.Value,
            query.Query,
            today,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<PagedResult<StudentListItemDto>>.Success(result);
    }
}
