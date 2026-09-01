using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Application.StudentAffairs.Students;
using AlFalah.Application.StudentAffairs.Students.Handlers;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Classrooms.Handlers;

public sealed class GetClassroomAcademicYearsQueryHandler
    : IRequestHandler<GetClassroomAcademicYearsQuery, ApiResponse<IReadOnlyList<ClassroomAcademicYearDto>>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetClassroomAcademicYearsQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<ClassroomAcademicYearDto>>> Handle(
        GetClassroomAcademicYearsQuery query,
        CancellationToken cancellationToken)
    {
        if (_currentUser.ActiveSchoolId is null || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<IReadOnlyList<ClassroomAcademicYearDto>>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.ClassroomManage)
            && !_currentUser.HasPermission(PermissionNames.StudentEnrollmentManage)
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<IReadOnlyList<ClassroomAcademicYearDto>>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var years = await _repository.GetClassroomAcademicYearsAsync(cancellationToken).ConfigureAwait(false);
        return ApiResponse<IReadOnlyList<ClassroomAcademicYearDto>>.Success(years);
    }
}
