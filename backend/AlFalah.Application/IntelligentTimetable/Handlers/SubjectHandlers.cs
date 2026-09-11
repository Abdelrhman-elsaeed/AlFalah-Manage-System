using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Validators.IntelligentTimetable;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using FluentValidation;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.Handlers;

public sealed class SubjectHandlers(SubjectService service, ICurrentUserService user) :
    IRequestHandler<GetSubjectsQuery, ApiResponse<SubjectOverviewDto>>,
    IRequestHandler<CreateSubjectCommand, ApiResponse<SubjectDto>>,
    IRequestHandler<UpdateSubjectCommand, ApiResponse<SubjectDto>>,
    IRequestHandler<CreateSubjectRoomCommand, ApiResponse<RoomDto>>,
    IRequestHandler<AllocateSubjectToClassesCommand, ApiResponse<SubjectBulkResult>>,
    IRequestHandler<UpdateSubjectRequirementsCommand, ApiResponse<SubjectBulkResult>>,
    IRequestHandler<RemoveSubjectRequirementCommand, ApiResponse<int>>
{
    public Task<ApiResponse<SubjectOverviewDto>> Handle(GetSubjectsQuery q, CancellationToken ct) =>
        Respond(() => service.GetAsync(user.ActiveSchoolId!.Value, q.SetupId, ct));
    public Task<ApiResponse<SubjectDto>> Handle(CreateSubjectCommand c, CancellationToken ct) =>
        Respond(async () => { await new SaveSubjectValidator().ValidateAndThrowAsync(c.Request, ct);
            return await service.SaveSubjectAsync(user.ActiveSchoolId!.Value, c.SetupId, null, c.Request, user.UserId!, ct); });
    public Task<ApiResponse<SubjectDto>> Handle(UpdateSubjectCommand c, CancellationToken ct) =>
        Respond(async () => { await new SaveSubjectValidator().ValidateAndThrowAsync(c.Request, ct);
            return await service.SaveSubjectAsync(user.ActiveSchoolId!.Value, c.SetupId, c.SubjectId, c.Request, user.UserId!, ct); });
    public Task<ApiResponse<RoomDto>> Handle(CreateSubjectRoomCommand c, CancellationToken ct) =>
        Respond(() => service.CreateRoomAsync(user.ActiveSchoolId!.Value, c.SetupId, c.Request, user.UserId!, ct));
    public Task<ApiResponse<SubjectBulkResult>> Handle(AllocateSubjectToClassesCommand c, CancellationToken ct) =>
        Respond(async () => { await new AllocateSubjectValidator().ValidateAndThrowAsync(c.Request, ct);
            return await service.AllocateAsync(user.ActiveSchoolId!.Value, c.SetupId, c.Request, user.UserId!, ct); });
    public Task<ApiResponse<SubjectBulkResult>> Handle(UpdateSubjectRequirementsCommand c, CancellationToken ct) =>
        Respond(async () => {
            if (c.Request.Rules is null || c.Request.Revision <= 0) throw new ArgumentException("قواعد المادة وإصدار الإعداد مطلوبان.");
            await new SubjectRulesValidator().ValidateAndThrowAsync(c.Request.Rules, ct);
            return await service.UpdateAsync(user.ActiveSchoolId!.Value, c.SetupId, c.RequirementId, c.Request, user.UserId!, ct); });
    public Task<ApiResponse<int>> Handle(RemoveSubjectRequirementCommand c, CancellationToken ct) =>
        Respond(() => service.RemoveAsync(user.ActiveSchoolId!.Value, c.SetupId, c.RequirementId, c.Revision, user.UserId!, ct));
    private async Task<ApiResponse<T>> Respond<T>(Func<Task<T>> action)
    {
        if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.UserId) || !user.ActiveSchoolId.HasValue ||
            TimetableSettingsHandlerSupport.IsExcludedRole(user) || !user.HasPermission(PermissionNames.TimetableManage))
            return ApiResponse<T>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);
        try { return ApiResponse<T>.Success(await action()); }
        catch (ValidationException ex) { return ApiResponse<T>.Fail(ex.Errors.Select(x => x.ErrorMessage).ToList()); }
        catch (KeyNotFoundException) { return ApiResponse<T>.Fail(TimetableSettingsHandlerSupport.NotFound); }
        catch (BellScheduleConflictException) { return ApiResponse<T>.Fail(TimetableSettingsHandlerSupport.ConcurrencyConflict); }
        catch (ArgumentException ex) { return ApiResponse<T>.Fail(ex.Message); }
    }
}
