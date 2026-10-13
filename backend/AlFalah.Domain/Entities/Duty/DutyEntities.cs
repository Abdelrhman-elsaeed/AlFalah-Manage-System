namespace AlFalah.Domain.Entities.Duty;

public sealed class DutyPlan
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AcademicTermId { get; set; }
    public int? BellScheduleRevisionId { get; set; }
    public string TimeZoneId { get; set; } = "Africa/Cairo";
    public int Revision { get; set; } = 1;
    public string SettingsJson { get; set; } = "{}";
    public DateTimeOffset PublishedAt { get; set; }
    public string PublishedByUserId { get; set; } = "";
    public ICollection<DutyParticipant> Participants { get; set; } = new List<DutyParticipant>();
    public ICollection<DutyClosedDate> Closures { get; set; } = new List<DutyClosedDate>();
}

public sealed class DutyParticipant
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int DutyPlanId { get; set; }
    public int InstructorProfileId { get; set; }
    public string UserId { get; set; } = "";
    public int Position { get; set; }
}

public sealed class DutyClosedDate
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int DutyPlanId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class DutyAssignment
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int DutyPlanId { get; set; }
    public DateOnly Date { get; set; }
    public string OriginalTeacherId { get; set; } = "";
    public string EffectiveTeacherId { get; set; } = "";
    public int Revision { get; set; } = 1;
    public bool IsManuallyProtected { get; set; }
    // D2 will set this when acknowledgement/report activity locks assignment ownership.
    public bool IsLocked { get; set; }
    public bool IsCancelled { get; set; }
    public string? ExceptionCode { get; set; }
}

/// <summary>Append-only evidence. Undo/resolution is another event, never an edited event.</summary>
public sealed class DutyAssignmentChange
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int DutyPlanId { get; set; }
    public int? AssignmentId { get; set; }
    public int? OtherAssignmentId { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly? OtherDate { get; set; }
    public string Kind { get; set; } = "";
    public string BeforeTeacherId { get; set; } = "";
    public string AfterTeacherId { get; set; } = "";
    public string? OtherBeforeTeacherId { get; set; }
    public string? OtherAfterTeacherId { get; set; }
    public int AfterRevision { get; set; }
    public int? OtherAfterRevision { get; set; }
    public int? RelatedChangeId { get; set; }
    public string Reason { get; set; } = "";
    public string ActorUserId { get; set; } = "";
    public string? SettingsSnapshotJson { get; set; }
    public int? AttendanceRecordId { get; set; }
    public DateTimeOffset? AttendanceUpdatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DutyReconciliationOperation
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public DateOnly Date { get; set; }
    public Guid SignalId { get; set; } = Guid.NewGuid();
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorCode { get; set; }
}

public sealed class DutyRequestReceipt
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public Guid RequestId { get; set; }
    public string Digest { get; set; } = "";
    public string ResultJson { get; set; } = "";
}
