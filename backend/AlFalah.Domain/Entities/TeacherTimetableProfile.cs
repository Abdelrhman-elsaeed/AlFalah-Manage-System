namespace AlFalah.Domain.Entities;

public sealed class TeacherTimetableProfile
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int TimetableSetupProfileId { get; set; }
    public int InstructorProfileId { get; set; }
    public int BellScheduleRevisionId { get; set; }
    public string ShortDisplayName { get; set; } = string.Empty;
    public int MaximumWeeklyPeriods { get; set; }
    public bool IsVisiting { get; set; }
    public bool HideFromPrint { get; set; }
    public int Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public TimetableSetupProfile Setup { get; set; } = null!;
    public InstructorProfile Instructor { get; set; } = null!;
    public BellScheduleRevision BellScheduleRevision { get; set; } = null!;
    public ICollection<TeacherAvailabilitySlot> Slots { get; set; } = new List<TeacherAvailabilitySlot>();
}

/// <summary>A real study day and an immutable period identity, never a sequence-only mapping.</summary>
public sealed class TeacherAvailabilitySlot
{
    public int Id { get; set; }
    public int TeacherTimetableProfileId { get; set; }
    public int Day { get; set; }
    public int BellPeriodId { get; set; }
    public bool IsAvailable { get; set; } = true;
    public TeacherTimetableProfile Profile { get; set; } = null!;
    public BellPeriod Period { get; set; } = null!;
}
