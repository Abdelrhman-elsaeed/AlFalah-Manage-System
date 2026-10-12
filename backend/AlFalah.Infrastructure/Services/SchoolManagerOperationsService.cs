using AlFalah.Application.DTOs.Dashboards;
using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs.TeacherContext;
using AlFalah.Domain.Enums;

namespace AlFalah.Infrastructure.Services;

public sealed class SchoolManagerOperationsService(
    ISchoolManagerOperationsRepository repository,
    ICurrentUserService currentUser,
    BellScheduleResolver schedules,
    TeacherContextScheduleOptions clockOptions,
    TimeProvider clock) : ISchoolManagerOperationsService
{
    public async Task<SchoolManagerOperationsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.HasPermission(PermissionNames.DashboardSchoolManager)
            || !currentUser.IsInRole(RoleNames.SchoolManager))
            throw new UnauthorizedAccessException("ليس لديك صلاحية لعرض ملخص تشغيل المدرسة.");
        var schoolId = currentUser.ActiveSchoolId
            ?? throw new UnauthorizedAccessException("يجب اختيار المدرسة أولاً.");
        if (currentUser.UserId is not { } userId
            || !await repository.IsActiveManagerAsync(schoolId, userId, cancellationToken).ConfigureAwait(false))
            throw new UnauthorizedAccessException("لا يوجد إسناد إداري نشط لهذه المدرسة.");

        var now = clock.GetUtcNow();
        var schedule = await schedules.PublishedAsync(schoolId, now, cancellationToken).ConfigureAwait(false);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(schedule?.SchoolTimeZoneId ?? clockOptions.SchoolTimeZoneId);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var date = DateOnly.FromDateTime(local.DateTime);
        var midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var query = new SchoolManagerOperationsQuery(schoolId, date,
            new DateTimeOffset(midnight, zone.GetUtcOffset(midnight)),
            currentUser.HasPermission(PermissionNames.StudentAffairsDashboardSchoolOversight),
            currentUser.HasPermission(PermissionNames.GatePassViewAudit),
            currentUser.HasPermission(PermissionNames.MessagingViewAudit));
        var result = await repository.ReadAsync(query, cancellationToken).ConfigureAwait(false);
        var day = schedule is not null && currentUser.HasPermission(PermissionNames.TimetableView)
            ? new SchoolDayPulseDto(schedule.Name, zone.Id,
                BellScheduleResolver.EffectiveIntervals(schedule, BellScheduleResolver.ToDay(local.DayOfWeek))
                    .Select(x => new SchoolDaySlotDto(x.Name, x.Kind, x.StartLocalTime, x.EndLocalTime)).ToArray())
            : null;
        var browserTimeZone = TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var ianaZone) ? ianaZone : zone.Id;
        return result with { GeneratedAt = now, TimeZone = browserTimeZone,
            SchoolDay = day is null ? null : day with { TimeZone = browserTimeZone } };
    }
}
