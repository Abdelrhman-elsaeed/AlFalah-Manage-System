namespace AlFalah.Application.IntelligentTimetable.DTOs;

public sealed record StandbyDemand(int Day, int Period, int Required);
public sealed record WeeklyStandbySettings(int RequiredPerPeriod = 1,
    IReadOnlyList<StandbyDemand>? Overrides = null, IReadOnlyList<int>? ExcludedTeacherIds = null);
public sealed record StandbyPlacement(int InstructorProfileId, int Day, int Period,
    bool IsLocked = false, string? Source = null);
public sealed record WeeklyStandbyRequest(int Revision, string Mode, WeeklyStandbySettings Settings,
    IReadOnlyList<StandbyPlacement> Placements, string? ProposalId = null);
public sealed record StandbyPeriodDto(int Day, int Period, string Start, string End,
    int Required, int Available, int Allocated);
public sealed record StandbyCellDto(int Day, int Period, bool Eligible, string Kind, string? Reason,
    string? Classroom, string? Subject, bool IsProtected, bool CanStandby);
public sealed record StandbyTeacherDto(int Id, string Name, int TeachingCount, int AvailablePeriods,
    int StandbyCount, int Capacity, IReadOnlyList<StandbyCellDto> Cells);
public sealed record WeeklyStandbyWorkbenchDto(int TimetableId, string Title, int Revision, int? SetupRevision,
    int? BellScheduleRevisionId, bool IsPublished, bool Ready, IReadOnlyList<string> Issues,
    WeeklyStandbySettings Settings, IReadOnlyList<StandbyPeriodDto> Periods,
    IReadOnlyList<StandbyTeacherDto> Teachers, IReadOnlyList<StandbyPlacement> Placements,
    IReadOnlyList<string>? Notices = null);
public sealed record WeeklyStandbyPreviewDto(string ProposalId, string Mode, WeeklyStandbyWorkbenchDto Workbench);
