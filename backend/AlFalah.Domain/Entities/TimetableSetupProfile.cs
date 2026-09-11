using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// A named, school-scoped timetable setup for one academic year and semester.
/// Readiness is calculated from source records and is intentionally not persisted here.
/// </summary>
public sealed class TimetableSetupProfile
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AcademicYearId { get; set; }
    public TimetableSemester Semester { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? BellScheduleTemplateId { get; set; }
    public TimetableSetupStatus Status { get; set; } = TimetableSetupStatus.Draft;
    public int Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public BellScheduleTemplate? BellScheduleTemplate { get; set; }
    public School School { get; set; } = null!;
    public AcademicYear AcademicYear { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser UpdatedByUser { get; set; } = null!;
    public ApplicationUser? DeletedByUser { get; set; }
    public ICollection<SchoolTimetable> Timetables { get; set; } = new List<SchoolTimetable>();
}
