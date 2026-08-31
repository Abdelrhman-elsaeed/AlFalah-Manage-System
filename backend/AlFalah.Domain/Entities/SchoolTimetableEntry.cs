using AlFalah.Domain.Enums;
using AlFalah.Domain.Entities.StudentAffairs;

namespace AlFalah.Domain.Entities;

/// <summary>One teacher slot. Empty timetable cells are intentionally not persisted.</summary>
public class SchoolTimetableEntry
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int SchoolTimetableId { get; set; }
    public int? ClassroomId { get; set; }
    public int InstructorProfileId { get; set; }
    public TimetableDay Day { get; set; }
    public byte Period { get; set; }
    public TimetableEntryType EntryType { get; set; }
    public string? ClassLabel { get; set; }
    public string? Subject { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public SchoolTimetable SchoolTimetable { get; set; } = null!;
    public School School { get; set; } = null!;
    public Classroom? Classroom { get; set; }
    public InstructorProfile InstructorProfile { get; set; } = null!;
}
