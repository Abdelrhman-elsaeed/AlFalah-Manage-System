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
public sealed class CreateClassroomCommandHandler(
    IStudentWorkflowRepository repository, ICurrentUserService currentUser,
    IClassroomCatalogService catalog) : IRequestHandler<CreateClassroomCommand, ApiResponse<ClassroomDto>>
{
    public async Task<ApiResponse<ClassroomDto>> Handle(CreateClassroomCommand command, CancellationToken cancellationToken)
    {
        var school = currentUser.ActiveSchoolId;
        if (school is null || string.IsNullOrWhiteSpace(currentUser.UserId))
            return ApiResponse<ClassroomDto>.Fail(StudentHandlerSupport.AuthenticationRequired);
        if (!currentUser.HasPermission(PermissionNames.StudentEnrollmentManage) && !currentUser.HasPermission(PermissionNames.ClassroomManage))
            return ApiResponse<ClassroomDto>.Fail(StudentHandlerSupport.PermissionDenied);
        try
        {
            var request = command.Request;
            var context = await catalog.OptionsAsync(request.AcademicYearId, 1, true, cancellationToken);
            if (context.Context.Stage != request.Stage) throw new ArgumentException("Stage must match the active school.");
            var created = await catalog.CreateAsync(new(request.AcademicYearId, request.GradeLevel, [request.Section], request.PhysicalLocation), cancellationToken);
            var id = created.Single().Id;
            return ApiResponse<ClassroomDto>.Success((await repository.GetClassroomDtoAsync(school.Value, id, cancellationToken))!);
        }
        catch (ArgumentException ex) { return ApiResponse<ClassroomDto>.Fail(ex.Message); }
        catch (ClassroomCatalogConflictException ex) { return ApiResponse<ClassroomDto>.Fail(ex.Message); }
        catch (UnauthorizedSchoolAccessException) { return ApiResponse<ClassroomDto>.Fail(StudentHandlerSupport.PermissionDenied); }
    }
}
