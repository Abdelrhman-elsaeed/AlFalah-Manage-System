namespace AlFalah.Domain.Entities;

public sealed class TeachingAssignment
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int TimetableSetupProfileId { get; set; }
    public int ClassSubjectRequirementId { get; set; }
    public string Mode { get; set; } = "SingleTeacher";
    public bool IsDeleted { get; set; }
    public int Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public ClassSubjectRequirement Requirement { get; set; } = null!;
    public ICollection<TeachingAssignmentMember> Members { get; set; } = new List<TeachingAssignmentMember>();
}

public sealed class TeachingAssignmentMember
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int TimetableSetupProfileId { get; set; }
    public int TeachingAssignmentId { get; set; }
    public int TeacherTimetableProfileId { get; set; }
    public int AllocatedPeriodCount { get; set; }
    /// <summary>Whole double periods owned by this teacher; included in AllocatedPeriodCount.</summary>
    public int AllocatedPairedBlockCount { get; set; }
    public TeachingAssignment Assignment { get; set; } = null!;
    public TeacherTimetableProfile Teacher { get; set; } = null!;
}
