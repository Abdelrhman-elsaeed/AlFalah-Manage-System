using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>The current editable timetable for one school, academic year and semester.</summary>
public class SchoolTimetable
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AcademicYearId { get; set; }
    public TimetableSemester Semester { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedByUserId { get; set; }
    public int Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public AcademicYear AcademicYear { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser UpdatedByUser { get; set; } = null!;
    public ApplicationUser? PublishedByUser { get; set; }
    public ICollection<SchoolTimetableEntry> Entries { get; set; } = new List<SchoolTimetableEntry>();
    public ICollection<SchoolTimetableVersion> Versions { get; set; } = new List<SchoolTimetableVersion>();
}
