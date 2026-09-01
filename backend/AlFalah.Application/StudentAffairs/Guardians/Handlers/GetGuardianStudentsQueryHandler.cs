using System;
using System.Collections.Generic;
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

public sealed class GetGuardianStudentsQueryHandler
    : IRequestHandler<GetGuardianStudentsQuery, ApiResponse<IReadOnlyList<GuardianStudentDto>>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetGuardianStudentsQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<IReadOnlyList<GuardianStudentDto>>> Handle(
        GetGuardianStudentsQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<IReadOnlyList<GuardianStudentDto>>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.GuardianViewLinkedStudents)
            && !_currentUser.IsInRole(RoleNames.Guardian))
        {
            return ApiResponse<IReadOnlyList<GuardianStudentDto>>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
        var students = await _repository.GetGuardianStudentsAsync(
            schoolId.Value,
            userId,
            today,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<IReadOnlyList<GuardianStudentDto>>.Success(students);
    }
}
