using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.DTOs;

public sealed record BellPeriodDto(int Sequence, string? DisplayLabel, TimeOnly StartLocalTime, TimeOnly EndLocalTime);
public sealed record BellScheduleDayDto(int Day, bool IsStudyDay, bool UsesDefaultSchedule, IReadOnlyList<BellPeriodDto> Periods,
    bool UsesDefaultBreaks = true, IReadOnlyList<ScheduleBreakDto>? Breaks = null);
public sealed record BellScheduleDto(int Id, int RevisionId, int SchoolId, int AcademicYearId,
    TimetableSemester Semester, string Name, int Revision, string SchoolTimeZoneId,
    IReadOnlyList<BellPeriodDto> DefaultPeriods, IReadOnlyList<BellScheduleDayDto> Days,
    IReadOnlyList<int> SelectedByProfileIds, IReadOnlyList<ScheduleBreakDto>? DefaultBreaks = null);
public sealed record SaveBellScheduleRequest(int AcademicYearId, TimetableSemester Semester, string Name,
    int Revision, string SchoolTimeZoneId, IReadOnlyList<BellPeriodDto> DefaultPeriods,
    IReadOnlyList<BellScheduleDayDto> Days, IReadOnlyList<ScheduleBreakDto>? DefaultBreaks = null);
public sealed record SelectBellScheduleRequest(int TemplateId, int ProfileRevision);
public sealed record GetBellSchedulesQuery(int AcademicYearId, TimetableSemester Semester) : IRequest<ApiResponse<IReadOnlyList<BellScheduleDto>>>;
public sealed record SaveBellScheduleCommand(int? TemplateId, SaveBellScheduleRequest Request) : IRequest<ApiResponse<BellScheduleDto>>;
public sealed record SelectBellScheduleCommand(int ProfileId, SelectBellScheduleRequest Request) : IRequest<ApiResponse<int>>;
