using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
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
    private readonly ISchoolLocalDateResolver _schoolLocalDateResolver;

    public GetGuardianStudentsQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        ISchoolLocalDateResolver schoolLocalDateResolver)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _schoolLocalDateResolver = schoolLocalDateResolver;
    }

    public async Task<ApiResponse<IReadOnlyList<GuardianStudentDto>>> Handle(
        GetGuardianStudentsQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<IReadOnlyList<GuardianStudentDto>>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.Guardian)
            || !_currentUser.HasPermission(PermissionNames.GuardianViewLinkedStudents))
        {
            return ApiResponse<IReadOnlyList<GuardianStudentDto>>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        if (!await _repository.IsActiveGuardianProfileAsync(
                schoolId.Value,
                userId,
                cancellationToken).ConfigureAwait(false))
            return ApiResponse<IReadOnlyList<GuardianStudentDto>>.Fail(StudentHandlerSupport.NotFound);

        var today = await _schoolLocalDateResolver.ResolveAsync(
            schoolId.Value,
            _timeProvider.GetUtcNow(),
            cancellationToken).ConfigureAwait(false);
        if (today is null)
            return ApiResponse<IReadOnlyList<GuardianStudentDto>>.Fail("School local date could not be resolved");
        var students = await _repository.GetGuardianStudentsAsync(
            schoolId.Value,
            userId,
            today.Value,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<IReadOnlyList<GuardianStudentDto>>.Success(students);
    }
}
