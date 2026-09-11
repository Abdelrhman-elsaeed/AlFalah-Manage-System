using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Validators.IntelligentTimetable;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.Handlers;

public sealed class GetScheduleBreaksHandler(IBellScheduleRepository repository, ICurrentUserService user)
    : IRequestHandler<GetScheduleBreaksQuery, ApiResponse<BellScheduleDto>>
{
    public async Task<ApiResponse<BellScheduleDto>> Handle(GetScheduleBreaksQuery query, CancellationToken ct)
    {
        if (user.ActiveSchoolId is not int schoolId || !TimetableSettingsHandlerSupport.CanView(user))
            return ApiResponse<BellScheduleDto>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);
        var template = await repository.FindAsync(schoolId, query.TemplateId, ct);
        if (template is null || !template.IsActive) return ApiResponse<BellScheduleDto>.Fail(TimetableSettingsHandlerSupport.NotFound);
        var schedule = (await repository.ListAsync(schoolId, template.AcademicYearId, template.Semester, ct)).SingleOrDefault(x => x.Id == template.Id);
        return schedule is null ? ApiResponse<BellScheduleDto>.Fail(TimetableSettingsHandlerSupport.NotFound) : ApiResponse<BellScheduleDto>.Success(schedule);
    }
}

public sealed class SaveScheduleBreaksHandler(IMediator mediator, ICurrentUserService user, ITimetableSettingsRepository settings)
    : IRequestHandler<SaveScheduleBreaksCommand, ApiResponse<BellScheduleDto>>
{
    public async Task<ApiResponse<BellScheduleDto>> Handle(SaveScheduleBreaksCommand command, CancellationToken ct)
    {
        if (user.ActiveSchoolId is not int schoolId || !await TimetableSettingsHandlerSupport.CanManageAsync(user, settings, schoolId, ct))
            return ApiResponse<BellScheduleDto>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);
        var validation = await new SaveScheduleBreaksValidator().ValidateAsync(command.Request, ct);
        if (!validation.IsValid) return ApiResponse<BellScheduleDto>.Fail(validation.Errors.Select(x => x.ErrorMessage).ToList());
        var response = await mediator.Send(new GetScheduleBreaksQuery(command.TemplateId), ct);
        if (!response.IsSuccess) return response;
        var current = response.Data!;
        var changes = command.Request.Days.ToDictionary(x => x.Day);
        var request = new SaveBellScheduleRequest(current.AcademicYearId, current.Semester, current.Name,
            command.Request.Revision, current.SchoolTimeZoneId, current.DefaultPeriods,
            current.Days.Select(day => day with { UsesDefaultBreaks = changes[day.Day].UsesDefaultBreaks, Breaks = changes[day.Day].Breaks }).ToArray(),
            command.Request.DefaultBreaks);
        // The same aggregate validator and optimistic revision token protect both write paths.
        return await mediator.Send(new SaveBellScheduleCommand(command.TemplateId, request), ct);
    }
}

public sealed class GetEffectiveScheduleHandler(IMediator mediator)
    : IRequestHandler<GetEffectiveScheduleQuery, ApiResponse<IReadOnlyList<ScheduleIntervalDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<ScheduleIntervalDto>>> Handle(GetEffectiveScheduleQuery query, CancellationToken ct)
    {
        if (query.Day is < 1 or > 7) return ApiResponse<IReadOnlyList<ScheduleIntervalDto>>.Fail("يوم الأسبوع غير صالح.");
        var response = await mediator.Send(new GetScheduleBreaksQuery(query.TemplateId), ct);
        return response.IsSuccess
            ? ApiResponse<IReadOnlyList<ScheduleIntervalDto>>.Success(BellScheduleResolver.EffectiveIntervals(response.Data!, (TimetableDay)query.Day))
            : ApiResponse<IReadOnlyList<ScheduleIntervalDto>>.Fail(response.Errors.ToList());
    }
}
