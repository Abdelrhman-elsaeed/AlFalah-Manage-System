using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Students.Handlers;

public sealed class GetStudentGuardiansQueryHandler
    : IRequestHandler<GetStudentGuardiansQuery, ApiResponse<IReadOnlyList<StudentGuardianLinkDto>>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetStudentGuardiansQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<IReadOnlyList<StudentGuardianLinkDto>>> Handle(
        GetStudentGuardiansQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<IReadOnlyList<StudentGuardianLinkDto>>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!StudentHandlerSupport.HasAnyPermission(
                _currentUser,
                PermissionNames.GuardianView,
                PermissionNames.StudentView,
                PermissionNames.SummonView,
                PermissionNames.SummonSchedule,
                PermissionNames.SummonMarkAttended,
                PermissionNames.ReferralView,
                PermissionNames.AttendanceViewStudents,
                PermissionNames.GatePassView,
                PermissionNames.GuardianViewLinkedStudents)
            && !_currentUser.IsInRole(RoleNames.SocialWorker)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<IReadOnlyList<StudentGuardianLinkDto>>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
        var guardians = await _repository.GetStudentGuardiansAsync(
            schoolId.Value,
            query.StudentId,
            today,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<IReadOnlyList<StudentGuardianLinkDto>>.Success(guardians);
    }
}
