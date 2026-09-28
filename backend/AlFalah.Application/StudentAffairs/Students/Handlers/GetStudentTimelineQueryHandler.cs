using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Students.Handlers;

public sealed class GetStudentTimelineQueryHandler
    : IRequestHandler<GetStudentTimelineQuery, ApiResponse<PagedResult<StudentTimelineItemDto>>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetStudentTimelineQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<PagedResult<StudentTimelineItemDto>>> Handle(
        GetStudentTimelineQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<PagedResult<StudentTimelineItemDto>>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!StudentHandlerSupport.HasAnyPermission(
                _currentUser,
                PermissionNames.StudentView,
                PermissionNames.GuardianViewLinkedStudents,
                PermissionNames.TeacherQuickActionView)
            && !_currentUser.IsInRole(RoleNames.SocialWorker)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<PagedResult<StudentTimelineItemDto>>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        if (_currentUser.IsInRole(RoleNames.Guardian))
        {
            var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
            if (!await _repository.IsGuardianLinkedToStudentAsync(
                    schoolId.Value, userId, query.StudentId, today, cancellationToken).ConfigureAwait(false))
                return ApiResponse<PagedResult<StudentTimelineItemDto>>.Fail(StudentHandlerSupport.NotFound);
        }

        var result = await _repository.GetStudentTimelineAsync(
            schoolId.Value,
            query.StudentId,
            query.Query,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<PagedResult<StudentTimelineItemDto>>.Success(result);
    }
}
