using AlFalah.Domain.Entities.StudentAffairs;

namespace AlFalah.Domain.Entities;

public sealed class SubjectDefinition
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#2563eb";
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public int Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
}

/// <summary>A teaching room, distinct from the geographic SchoolLocation catalog.</summary>
public sealed class TimetableRoom
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
}

public sealed class ClassSubjectRequirement
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int TimetableSetupProfileId { get; set; }
    public int ClassroomId { get; set; }
    public int SubjectId { get; set; }
    public int IndividualPeriodCount { get; set; }
    public int PairedBlockCount { get; set; }
    public int TotalWeeklyPeriods => IndividualPeriodCount + 2 * PairedBlockCount;
    /// <summary>None, Early, or Late. Always a soft preference.</summary>
    public string TimePreference { get; set; } = "None";
    public int? EarliestPeriodSequence { get; set; }
    public int? LatestPreferredPeriodSequence { get; set; }
    public int Revision { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public TimetableSetupProfile Setup { get; set; } = null!;
    public Classroom Classroom { get; set; } = null!;
    public SubjectDefinition Subject { get; set; } = null!;
    public ICollection<ClassSubjectAllowedDay> AllowedDays { get; set; } = new List<ClassSubjectAllowedDay>();
    public ICollection<ClassSubjectFixedSlot> FixedSlots { get; set; } = new List<ClassSubjectFixedSlot>();
    public ICollection<SubjectRoomRequirement> Rooms { get; set; } = new List<SubjectRoomRequirement>();
}

public sealed class ClassSubjectAllowedDay
{
    public int Id { get; set; }
    public int ClassSubjectRequirementId { get; set; }
    public int Day { get; set; }
}

public sealed class ClassSubjectFixedSlot
{
    public int Id { get; set; }
    public int ClassSubjectRequirementId { get; set; }
    public int Day { get; set; }
    public int Period { get; set; }
}

public sealed class SubjectRoomRequirement
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int ClassSubjectRequirementId { get; set; }
    public int RoomId { get; set; }
    public bool IsPreferred { get; set; }
    public TimetableRoom Room { get; set; } = null!;
}
