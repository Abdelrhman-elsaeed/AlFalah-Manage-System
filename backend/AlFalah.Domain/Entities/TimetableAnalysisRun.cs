namespace AlFalah.Domain.Entities;

/// <summary>Append-only analysis of the exact timetable and input revisions.</summary>
public sealed class TimetableAnalysisRun
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int SchoolTimetableId { get; set; }
    public int TimetableRevision { get; set; }
    public int? SetupRevision { get; set; }
    public int? BellScheduleRevisionId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public int HardViolationCount { get; set; }
    public int WarningCount { get; set; }
    public string AnalyzerVersion { get; set; } = string.Empty;
    public School School { get; set; } = null!;
    public SchoolTimetable SchoolTimetable { get; set; } = null!;
    public BellScheduleRevision? BellScheduleRevision { get; set; }
    public ApplicationUser RequestedByUser { get; set; } = null!;
    public ICollection<TimetableAnalysisFinding> Findings { get; set; } = new List<TimetableAnalysisFinding>();
}
