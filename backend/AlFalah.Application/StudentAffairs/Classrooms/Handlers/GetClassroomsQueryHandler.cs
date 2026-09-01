using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Application.StudentAffairs.Students;
using AlFalah.Application.StudentAffairs.Students.Handlers;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Classrooms.Handlers;

public sealed class GetClassroomsQueryHandler
    : IRequestHandler<GetClassroomsQuery, ApiResponse<PagedResult<ClassroomDto>>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetClassroomsQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<ClassroomDto>>> Handle(
        GetClassroomsQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<PagedResult<ClassroomDto>>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!StudentHandlerSupport.HasAnyPermission(
                _currentUser,
                PermissionNames.StudentView,
                PermissionNames.StudentEnrollmentManage,
                PermissionNames.TeacherQuickActionView)
            && !_currentUser.IsInRole(RoleNames.SocialWorker)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<PagedResult<ClassroomDto>>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var result = await _repository.GetClassroomsAsync(
            schoolId.Value,
            query.Query,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<PagedResult<ClassroomDto>>.Success(result);
    }
}
