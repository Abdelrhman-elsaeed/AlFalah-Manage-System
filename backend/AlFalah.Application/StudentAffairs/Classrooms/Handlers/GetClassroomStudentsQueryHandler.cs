using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.Students;
using AlFalah.Application.StudentAffairs.Students.Handlers;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Classrooms.Handlers;

public sealed class GetClassroomStudentsQueryHandler
    : IRequestHandler<GetClassroomStudentsQuery, ApiResponse<IReadOnlyList<StudentSummaryDto>>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetClassroomStudentsQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<IReadOnlyList<StudentSummaryDto>>> Handle(
        GetClassroomStudentsQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<IReadOnlyList<StudentSummaryDto>>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!StudentHandlerSupport.HasAnyPermission(
                _currentUser,
                PermissionNames.StudentView,
                PermissionNames.TeacherQuickActionView,
                PermissionNames.AttendanceViewStudents)
            && !_currentUser.IsInRole(RoleNames.Instructor)
            && !_currentUser.IsInRole(RoleNames.SocialWorker)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<IReadOnlyList<StudentSummaryDto>>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
        var students = await _repository.GetClassroomStudentsAsync(
            schoolId.Value,
            query.ClassroomId,
            query.AcademicTermId,
            today,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<IReadOnlyList<StudentSummaryDto>>.Success(students);
    }
}
