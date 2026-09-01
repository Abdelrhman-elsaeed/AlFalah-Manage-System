using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Guardian;
using AlFalah.Application.StudentAffairs.Students;
using AlFalah.Application.StudentAffairs.Students.Handlers;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Guardians.Handlers;

public sealed class GetGuardianStudentSummaryQueryHandler
    : IRequestHandler<GetGuardianStudentSummaryQuery, ApiResponse<GuardianStudentSummaryDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetGuardianStudentSummaryQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<GuardianStudentSummaryDto>> Handle(
        GetGuardianStudentSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<GuardianStudentSummaryDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.GuardianViewLinkedStudents)
            && !_currentUser.IsInRole(RoleNames.Guardian))
        {
            return ApiResponse<GuardianStudentSummaryDto>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
        var summary = await _repository.GetGuardianStudentSummaryAsync(
            schoolId.Value,
            userId,
            query.StudentId,
            today,
            cancellationToken).ConfigureAwait(false);

        if (summary is null)
            return ApiResponse<GuardianStudentSummaryDto>.Fail(StudentHandlerSupport.NotFound);

        return ApiResponse<GuardianStudentSummaryDto>.Success(summary);
    }
}
