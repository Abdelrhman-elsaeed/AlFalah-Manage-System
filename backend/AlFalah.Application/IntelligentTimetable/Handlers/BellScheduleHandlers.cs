using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Validators.IntelligentTimetable;
using AlFalah.Domain.Entities;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.Handlers;

public sealed class GetBellSchedulesHandler(IBellScheduleRepository repository, ICurrentUserService user)
    : IRequestHandler<GetBellSchedulesQuery, ApiResponse<IReadOnlyList<BellScheduleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<BellScheduleDto>>> Handle(GetBellSchedulesQuery query, CancellationToken ct)
    {
        if (user.ActiveSchoolId is not int schoolId || !TimetableSettingsHandlerSupport.CanView(user))
            return ApiResponse<IReadOnlyList<BellScheduleDto>>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);
        return ApiResponse<IReadOnlyList<BellScheduleDto>>.Success(await repository.ListAsync(schoolId, query.AcademicYearId, query.Semester, ct));
    }
}

public sealed class SaveBellScheduleHandler(IBellScheduleRepository repository, ITimetableSettingsRepository settings,
    ICurrentUserService user, TimeProvider clock)
    : IRequestHandler<SaveBellScheduleCommand, ApiResponse<BellScheduleDto>>
{
    public async Task<ApiResponse<BellScheduleDto>> Handle(SaveBellScheduleCommand command, CancellationToken ct)
    {
        if (user.ActiveSchoolId is not int schoolId || string.IsNullOrWhiteSpace(user.UserId) ||
            !await TimetableSettingsHandlerSupport.CanManageAsync(user, settings, schoolId, ct))
            return ApiResponse<BellScheduleDto>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);
        var request = command.Request;
        var validation = await new SaveBellScheduleValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) return ApiResponse<BellScheduleDto>.Fail(validation.Errors.Select(x => x.ErrorMessage).ToList());
        if (!await settings.AcademicScopeExistsAsync(schoolId, request.AcademicYearId, request.Semester, ct))
            return ApiResponse<BellScheduleDto>.Fail(TimetableSettingsHandlerSupport.InvalidAcademicScope);
        var template = command.TemplateId.HasValue ? await repository.FindAsync(schoolId, command.TemplateId.Value, ct) : null;
        if (command.TemplateId.HasValue && template is null)
            return ApiResponse<BellScheduleDto>.Fail(TimetableSettingsHandlerSupport.NotFound);
        if (template is not null && (template.Revision != request.Revision || template.AcademicYearId != request.AcademicYearId || template.Semester != request.Semester))
            return ApiResponse<BellScheduleDto>.Fail(TimetableSettingsHandlerSupport.ConcurrencyConflict);
        // Older timing clients omit break fields. Preserve the current intervals and validate against them.
        if (template is not null && request.DefaultBreaks is null)
        {
            var current = (await repository.ListAsync(schoolId, template.AcademicYearId, template.Semester, ct)).Single(x => x.Id == template.Id);
            request = request with { DefaultBreaks = current.DefaultBreaks, Days = request.Days.Select(day => {
                var old = current.Days.SingleOrDefault(x => x.Day == day.Day);
                return day with { UsesDefaultBreaks = old?.UsesDefaultBreaks ?? true, Breaks = old?.Breaks };
            }).ToArray() };
        }
        validation = await new SaveBellScheduleValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) return ApiResponse<BellScheduleDto>.Fail(validation.Errors.Select(x => x.ErrorMessage).ToList());
        if (await repository.NameExistsAsync(schoolId, request, command.TemplateId, ct))
            return ApiResponse<BellScheduleDto>.Fail("يوجد قالب توقيت بالاسم نفسه لهذا العام والفصل الدراسي.");
        template ??= new BellScheduleTemplate { SchoolId = schoolId, AcademicYearId = request.AcademicYearId,
            Semester = request.Semester, CreatedAt = clock.GetUtcNow(), CreatedByUserId = user.UserId };
        template.Name = request.Name.Trim();
        template.UpdatedAt = clock.GetUtcNow();
        template.UpdatedByUserId = user.UserId;
        if (command.TemplateId.HasValue) template.Revision++;
        var revision = new BellScheduleRevision { SchoolId = schoolId, Template = template, Revision = template.Revision,
            Name = template.Name, SchoolTimeZoneId = request.SchoolTimeZoneId, CreatedAt = clock.GetUtcNow(), CreatedByUserId = user.UserId };
        revision.Days.Add(BuildDay(0, true, false, request.DefaultPeriods, false, request.DefaultBreaks));
        foreach (var day in request.Days)
            revision.Days.Add(BuildDay(day.Day, day.IsStudyDay, day.UsesDefaultSchedule, day.Periods, day.UsesDefaultBreaks, day.Breaks));
        foreach (var definition in revision.Days.SelectMany(x => x.Breaks))
        { definition.CreatedAt = revision.CreatedAt; definition.CreatedByUserId = user.UserId; }
        if (command.TemplateId.HasValue)
            (await repository.GetDependenciesAsync(schoolId, template.Id, null, ct)).Invalidate(template.UpdatedAt, user.UserId);
        try { await repository.SaveRevisionAsync(template, revision, !command.TemplateId.HasValue, ct); }
        catch (BellScheduleConflictException) { return ApiResponse<BellScheduleDto>.Fail(TimetableSettingsHandlerSupport.ConcurrencyConflict); }
        return ApiResponse<BellScheduleDto>.Success((await repository.GetRevisionAsync(schoolId, revision.Id, ct))!, "تم حفظ جميع تغييرات التوقيت.");
    }

    private static BellScheduleDay BuildDay(int day, bool study, bool inherited, IReadOnlyList<BellPeriodDto> periods,
        bool inheritedBreaks, IReadOnlyList<ScheduleBreakDto>? breaks) => new()
    {
        Day = day, IsStudyDay = study, UsesDefaultSchedule = inherited,
        UsesDefaultBreaks = inheritedBreaks,
        Breaks = (breaks ?? []).Select(x => new ScheduleBreakDefinition { Name = x.Name.Trim(), Category = x.Category,
            Window = new ScheduleBreakWindow { StartLocalTime = x.StartLocalTime, EndLocalTime = x.EndLocalTime } }).ToList(),
        Periods = periods.Select(x => new BellPeriod { Sequence = x.Sequence, DisplayLabel = x.DisplayLabel?.Trim(),
            StartLocalTime = x.StartLocalTime, EndLocalTime = x.EndLocalTime }).ToList()
    };
}

public sealed class SelectBellScheduleHandler(IBellScheduleRepository repository, ITimetableSettingsRepository settings,
    ICurrentUserService user, TimeProvider clock) : IRequestHandler<SelectBellScheduleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SelectBellScheduleCommand command, CancellationToken ct)
    {
        if (user.ActiveSchoolId is not int schoolId || !await TimetableSettingsHandlerSupport.CanManageAsync(user, settings, schoolId, ct))
            return ApiResponse<int>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);
        var profile = await settings.GetProfileForUpdateAsync(schoolId, command.ProfileId, ct);
        var template = await repository.FindAsync(schoolId, command.Request.TemplateId, ct);
        if (profile is null || template is null) return ApiResponse<int>.Fail(TimetableSettingsHandlerSupport.NotFound);
        if (profile.Revision != command.Request.ProfileRevision) return ApiResponse<int>.Fail(TimetableSettingsHandlerSupport.ConcurrencyConflict);
        if (profile.AcademicYearId != template.AcademicYearId || profile.Semester != template.Semester || !template.IsActive)
            return ApiResponse<int>.Fail(TimetableSettingsHandlerSupport.InvalidAcademicScope);
        profile.BellScheduleTemplateId = template.Id;
        (await repository.GetDependenciesAsync(schoolId, null, profile.Id, ct)).Invalidate(clock.GetUtcNow(), user.UserId!);
        try { await repository.SelectAsync(profile, template, ct); }
        catch (BellScheduleConflictException) { return ApiResponse<int>.Fail(TimetableSettingsHandlerSupport.ConcurrencyConflict); }
        return ApiResponse<int>.Success(profile.Revision, "تم اختيار قالب التوقيت لملف الإعداد.");
    }
}
