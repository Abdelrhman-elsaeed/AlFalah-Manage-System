using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.Handlers;

public sealed class TeachingAssignmentHandlers(TeachingAssignmentService service, ICurrentUserService user) :
    IRequestHandler<GetTeachingAssignmentsQuery, ApiResponse<TeachingAssignmentsOverview>>,
    IRequestHandler<SearchTeachingTeachersQuery, ApiResponse<TeachingTeacherPage>>,
    IRequestHandler<SaveTeachingAssignmentsCommand, ApiResponse<TeachingAssignmentsOverview>>,
    IRequestHandler<UnassignTeachingSubjectCommand, ApiResponse<TeachingAssignmentsOverview>>
{
    public Task<ApiResponse<TeachingAssignmentsOverview>> Handle(GetTeachingAssignmentsQuery q, CancellationToken ct) =>
        Respond(() => service.GetAsync(user.ActiveSchoolId!.Value, q.SetupId, ct));
    public Task<ApiResponse<TeachingTeacherPage>> Handle(SearchTeachingTeachersQuery q, CancellationToken ct) =>
        Respond(() => service.SearchAsync(user.ActiveSchoolId!.Value, q.SetupId, q.Search, ct));
    public Task<ApiResponse<TeachingAssignmentsOverview>> Handle(SaveTeachingAssignmentsCommand c, CancellationToken ct) =>
        Respond(() => service.SaveAsync(user.ActiveSchoolId!.Value, c.SetupId, c.Request, user.UserId!, ct));
    public Task<ApiResponse<TeachingAssignmentsOverview>> Handle(UnassignTeachingSubjectCommand c, CancellationToken ct) =>
        Respond(() => service.SaveAsync(user.ActiveSchoolId!.Value, c.SetupId, new(c.Revision, [new(c.RequirementId, "SingleTeacher", [])]), user.UserId!, ct));
    private async Task<ApiResponse<T>> Respond<T>(Func<Task<T>> action)
    {
        if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.UserId) || !user.ActiveSchoolId.HasValue ||
            TimetableSettingsHandlerSupport.IsExcludedRole(user) || !user.HasPermission(PermissionNames.TimetableManage))
            return ApiResponse<T>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);
        try { return ApiResponse<T>.Success(await action()); }
        catch (KeyNotFoundException) { return ApiResponse<T>.Fail(TimetableSettingsHandlerSupport.NotFound); }
        catch (BellScheduleConflictException) { return ApiResponse<T>.Fail(TimetableSettingsHandlerSupport.ConcurrencyConflict); }
        catch (ArgumentException ex) { return ApiResponse<T>.Fail(ex.Message); }
    }
}
