using AlFalah.Application.Classrooms;
using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Application.StudentAffairs.Students;
using AlFalah.Application.StudentAffairs.Students.Handlers;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;
namespace AlFalah.Application.StudentAffairs.Classrooms.Handlers;
// Compatibility endpoint: all writes use the same catalogue policy.
public sealed class UpdateClassroomCommandHandler(
    IStudentWorkflowRepository repository, ICurrentUserService currentUser,
    IClassroomCatalogService catalog) : IRequestHandler<UpdateClassroomCommand, ApiResponse<ClassroomDto>>
{
    public async Task<ApiResponse<ClassroomDto>> Handle(UpdateClassroomCommand command, CancellationToken cancellationToken)
    {
        var school = currentUser.ActiveSchoolId;
        if (school is null || string.IsNullOrWhiteSpace(currentUser.UserId))
            return ApiResponse<ClassroomDto>.Fail(StudentHandlerSupport.AuthenticationRequired);
        if (!currentUser.HasPermission(PermissionNames.StudentEnrollmentManage) && !currentUser.HasPermission(PermissionNames.ClassroomManage))
            return ApiResponse<ClassroomDto>.Fail(StudentHandlerSupport.PermissionDenied);
        try
        {
            var row = await repository.GetClassroomForUpdateAsync(school.Value, command.ClassroomId, cancellationToken);
            if (row is null) return ApiResponse<ClassroomDto>.Fail(StudentHandlerSupport.NotFound);
            var request = command.Request;
            await catalog.UpdateAsync(row.Id, new(request.Section, request.IsActive, request.PhysicalLocation,
                string.IsNullOrEmpty(request.RowVersion) ? row.UpdatedAt.ToString("O") : request.RowVersion), cancellationToken);
            var id = row.Id;
            return ApiResponse<ClassroomDto>.Success((await repository.GetClassroomDtoAsync(school.Value, id, cancellationToken))!);
        }
        catch (ArgumentException ex) { return ApiResponse<ClassroomDto>.Fail(ex.Message); }
        catch (ClassroomCatalogConflictException ex) { return ApiResponse<ClassroomDto>.Fail(ex.Message); }
        catch (UnauthorizedSchoolAccessException) { return ApiResponse<ClassroomDto>.Fail(StudentHandlerSupport.PermissionDenied); }
    }
}
