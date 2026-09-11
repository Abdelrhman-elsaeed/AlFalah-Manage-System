using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.DTOs;

public sealed record ScheduleBreakDto(string Name, string? Category, TimeOnly StartLocalTime, TimeOnly EndLocalTime);
public sealed record ScheduleBreakDayDto(int Day, bool UsesDefaultBreaks, IReadOnlyList<ScheduleBreakDto> Breaks);
public sealed record SaveScheduleBreaksRequest(int Revision, IReadOnlyList<ScheduleBreakDto> DefaultBreaks, IReadOnlyList<ScheduleBreakDayDto> Days);
public sealed record GetScheduleBreaksQuery(int TemplateId) : IRequest<ApiResponse<BellScheduleDto>>;
/// <summary>Atomically replaces the editable break collection, including additions and removals.</summary>
public sealed record SaveScheduleBreaksCommand(int TemplateId, SaveScheduleBreaksRequest Request) : IRequest<ApiResponse<BellScheduleDto>>;
public sealed record ScheduleIntervalDto(string Kind, string Name, TimeOnly StartLocalTime, TimeOnly EndLocalTime, int? PeriodSequence = null);
public sealed record GetEffectiveScheduleQuery(int TemplateId, int Day) : IRequest<ApiResponse<IReadOnlyList<ScheduleIntervalDto>>>;
