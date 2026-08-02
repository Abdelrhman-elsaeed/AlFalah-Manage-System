using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>Immutable JSON snapshot of the timetable after a successful change.</summary>
public class SchoolTimetableVersion
{
    public int Id { get; set; }
    public int SchoolTimetableId { get; set; }
    public int VersionNumber { get; set; }
    public TimetableChangeKind ChangeKind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public int? RestoredFromVersionNumber { get; set; }

    public SchoolTimetable SchoolTimetable { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
}
