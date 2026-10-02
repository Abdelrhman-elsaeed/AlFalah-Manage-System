using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Application.StudentAffairs.DTOs.Teacher;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.TeacherContext.Handlers;

public sealed class GetTeacherClassroomsQueryHandler
    : IRequestHandler<GetTeacherClassroomsQuery, ApiResponse<IReadOnlyList<ClassroomDto>>>
{
    private const string AuthenticationRequired =
        "An authenticated teacher and active school are required";
    private const string PermissionDenied =
        "You do not have permission to view teacher classrooms";

    private readonly ITeacherContextRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetTeacherClassroomsQueryHandler(
        ITeacherContextRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<ClassroomDto>>> Handle(
        GetTeacherClassroomsQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (!_currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<IReadOnlyList<ClassroomDto>>.Fail(AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.Instructor)
            || !_currentUser.HasPermission(PermissionNames.TeacherQuickActionView))
        {
            return ApiResponse<IReadOnlyList<ClassroomDto>>.Fail(PermissionDenied);
        }

        var classrooms = await _repository.GetAssignedClassroomsAsync(
            schoolId.Value,
            userId,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<IReadOnlyList<ClassroomDto>>.Success(classrooms);
    }
}
