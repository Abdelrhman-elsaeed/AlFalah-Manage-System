using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Students.Handlers;

public sealed class GetStudentAnalyticsProfileQueryHandler
    : IRequestHandler<GetStudentAnalyticsProfileQuery, ApiResponse<StudentAnalyticsProfileDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetStudentAnalyticsProfileQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<StudentAnalyticsProfileDto>> Handle(
        GetStudentAnalyticsProfileQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<StudentAnalyticsProfileDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        // Strict RBAC: Secretary is explicitly blocked. Allowed: Officer, SocialWorker, SchoolManager, MainManager, SuperAdmin
        if (_currentUser.IsInRole(RoleNames.Secretary))
        {
            return ApiResponse<StudentAnalyticsProfileDto>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        if (!StudentHandlerSupport.HasAnyPermission(
                _currentUser,
                PermissionNames.StudentManage,
                PermissionNames.StudentView,
                PermissionNames.AttendanceViewStudents,
                PermissionNames.ReferralView)
            && !_currentUser.IsInRole(RoleNames.SocialWorker)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager)
            && !_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            return ApiResponse<StudentAnalyticsProfileDto>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
        var result = await _repository.GetStudentAnalyticsProfileAsync(
            schoolId.Value,
            query.StudentId,
            today,
            cancellationToken).ConfigureAwait(false);

        if (result is null)
            return ApiResponse<StudentAnalyticsProfileDto>.Fail(StudentHandlerSupport.StudentNotFound);

        return ApiResponse<StudentAnalyticsProfileDto>.Success(result);
    }
}
