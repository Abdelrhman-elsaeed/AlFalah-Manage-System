using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Students.Handlers;

public sealed class GetStudentByIdQueryHandler
    : IRequestHandler<GetStudentByIdQuery, ApiResponse<StudentDetailsDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetStudentByIdQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<StudentDetailsDto>> Handle(
        GetStudentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!StudentHandlerSupport.HasAnyPermission(
                _currentUser,
                PermissionNames.StudentView,
                PermissionNames.GuardianViewLinkedStudents,
                PermissionNames.TeacherQuickActionView,
                PermissionNames.ReferralView,
                PermissionNames.SummonView)
            && !_currentUser.IsInRole(RoleNames.SocialWorker)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
        var details = await _repository.GetStudentDetailsAsync(
            schoolId.Value,
            query.StudentId,
            today,
            cancellationToken).ConfigureAwait(false);

        if (details is null)
            return ApiResponse<StudentDetailsDto>.Fail(StudentHandlerSupport.NotFound);

        return ApiResponse<StudentDetailsDto>.Success(details);
    }
}
