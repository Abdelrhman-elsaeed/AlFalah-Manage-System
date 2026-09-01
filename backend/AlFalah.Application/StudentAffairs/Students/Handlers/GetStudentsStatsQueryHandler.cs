using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Students.Handlers;

public sealed class GetStudentsStatsQueryHandler
    : IRequestHandler<GetStudentsStatsQuery, ApiResponse<StudentStatsPageResult>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetStudentsStatsQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<StudentStatsPageResult>> Handle(
        GetStudentsStatsQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<StudentStatsPageResult>.Fail(StudentHandlerSupport.AuthenticationRequired);

        // Strict RBAC: Secretary is explicitly blocked. Allowed: Officer, SocialWorker, SchoolManager, MainManager, SuperAdmin
        if (_currentUser.IsInRole(RoleNames.Secretary))
        {
            return ApiResponse<StudentStatsPageResult>.Fail(StudentHandlerSupport.PermissionDenied);
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
            return ApiResponse<StudentStatsPageResult>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
        var result = await _repository.GetStudentsStatsAsync(
            schoolId.Value,
            query.Query,
            today,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<StudentStatsPageResult>.Success(result);
    }
}
