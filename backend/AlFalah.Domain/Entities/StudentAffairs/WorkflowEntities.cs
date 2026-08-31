using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;

namespace AlFalah.Domain.Entities.StudentAffairs;

public sealed class ClassroomEntryPermit : IStudentAffairsMutableEntity, IStudentAffairsConcurrentEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int AcademicTermId { get; set; }
    public int ClassroomId { get; set; }
    public string IssuedByStudentAffairsUserId { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public int? SchoolTimetableId { get; set; }
    public int? SchoolTimetableEntryId { get; set; }
    public int? TargetInstructorProfileId { get; set; }
    public ClassroomEntryPermitStatus Status { get; set; } = ClassroomEntryPermitStatus.Issued;
    public string? AcknowledgedByTeacherUserId { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string? RevokedByUserId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public School School { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public AcademicTerm AcademicTerm { get; set; } = null!;
    public Classroom Classroom { get; set; } = null!;
    public ApplicationUser IssuedByStudentAffairsUser { get; set; } = null!;
    public SchoolTimetable? SchoolTimetable { get; set; }
    public SchoolTimetableEntry? SchoolTimetableEntry { get; set; }
    public InstructorProfile? TargetInstructorProfile { get; set; }
}

public sealed class GatePass : IStudentAffairsMutableEntity, IStudentAffairsConcurrentEntity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int AcademicTermId { get; set; }
    public int RequestedByGuardianProfileId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset RequestedExitAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string PickupPersonName { get; set; } = string.Empty;
    public string? PickupRelationship { get; set; }
    public string? PickupIdentityHint { get; set; }
    public GatePassStatus Status { get; set; } = GatePassStatus.Requested;
    public string? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ApprovalNote { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? ApprovedWindowStartsAt { get; set; }
    public DateTimeOffset? ApprovedWindowEndsAt { get; set; }
    public int? CurrentClassroomId { get; set; }
    public int? SchoolTimetableId { get; set; }
    public int? SchoolTimetableEntryId { get; set; }
    public int? CurrentInstructorProfileId { get; set; }
    public byte? CurrentPeriod { get; set; }
    public string? SecurityAcknowledgedByUserId { get; set; }
    public DateTimeOffset? SecurityAcknowledgedAt { get; set; }
    public PickupVerificationMethod? PickupVerificationMethod { get; set; }
    public string? PickupVerificationNote { get; set; }
    public string? ExitGateNote { get; set; }
    public DateTimeOffset? ExitedAt { get; set; }
    public string? ExitRecordedByUserId { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }
    public DateTimeOffset? ExpiredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public School School { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public AcademicTerm AcademicTerm { get; set; } = null!;
    public GuardianProfile RequestedByGuardianProfile { get; set; } = null!;
    public Classroom? CurrentClassroom { get; set; }
    public SchoolTimetable? SchoolTimetable { get; set; }
    public SchoolTimetableEntry? SchoolTimetableEntry { get; set; }
    public InstructorProfile? CurrentInstructorProfile { get; set; }
    public ICollection<GatePassTransition> Transitions { get; set; } = new List<GatePassTransition>();

    [NotMapped] public int DomainEventAggregateId => Id;
    [NotMapped] public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void AppendDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>Immutable audit ledger for every gate-pass state transition.</summary>
public sealed class GatePassTransition
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public int GatePassId { get; set; }
    public GatePassStatus? FromStatus { get; set; }
    public GatePassStatus ToStatus { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
    public Guid CorrelationId { get; set; }
    public string? MetadataJson { get; set; }
    public PickupVerificationMethod? PickupVerificationMethod { get; set; }
    public string? PickupVerificationNote { get; set; }

    public School School { get; set; } = null!;
    public GatePass GatePass { get; set; } = null!;
    public ApplicationUser ActorUser { get; set; } = null!;
}

public sealed class StudentReferral : IStudentAffairsMutableEntity, IStudentAffairsConcurrentEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int AcademicTermId { get; set; }
    public ReferralSourceType SourceType { get; set; }
    public int? SourceEntityId { get; set; }
    public long? RuleTriggerId { get; set; }
    public int? CountSnapshot { get; set; }
    public int? ThresholdSnapshot { get; set; }
    public ReferralPriority Priority { get; set; } = ReferralPriority.Normal;
    public string? AssignedSocialWorkerUserId { get; set; }
    public StudentReferralStatus Status { get; set; } = StudentReferralStatus.Open;
    public string? RecommendedActions { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public School School { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public AcademicTerm AcademicTerm { get; set; } = null!;
    public ApplicationUser? AssignedSocialWorkerUser { get; set; }
    public AutomationTriggerLedger? RuleTrigger { get; set; }
    public ICollection<GuardianSummon> GuardianSummons { get; set; } = new List<GuardianSummon>();
    public ICollection<StudentCaseAction> Actions { get; set; } = new List<StudentCaseAction>();
}

public sealed class GuardianSummon
    : IStudentAffairsMutableEntity, IStudentAffairsConcurrentEntity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int AcademicTermId { get; set; }
    public int? StudentReferralId { get; set; }
    public string CreatedReason { get; set; } = string.Empty;
    public ReferralPriority Priority { get; set; } = ReferralPriority.Normal;
    public int? SourceCountSnapshot { get; set; }
    public int? ThresholdSnapshot { get; set; }
    public GuardianSummonStatus Status { get; set; } = GuardianSummonStatus.Pending;
    public DateTimeOffset? ScheduledAt { get; set; }
    public string? ScheduledBySocialWorkerUserId { get; set; }
    public string? Location { get; set; }
    public string? Instructions { get; set; }
    public int GuardianProfileId { get; set; }
    public DateTimeOffset? GuardianNotifiedAt { get; set; }
    public bool RequiresOfficerReview { get; set; }
    public string? OfficerReviewReason { get; set; }
    public DateTimeOffset? OfficerReviewFlaggedAt { get; set; }
    public DateTimeOffset? OfficerReviewedAt { get; set; }
    public OfficerReviewDecision? OfficerReviewDecision { get; set; }
    public DateTimeOffset? AttendedAt { get; set; }
    public string? AttendanceNotes { get; set; }
    public DateTimeOffset? ObservationStartedAt { get; set; }
    public string? ObservationNotes { get; set; }
    public DateTimeOffset? ImprovedAt { get; set; }
    public string? ImprovementNotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public School School { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public AcademicTerm AcademicTerm { get; set; } = null!;
    public StudentReferral? StudentReferral { get; set; }
    public GuardianProfile GuardianProfile { get; set; } = null!;
    public ICollection<GuardianSummonStatusHistory> StatusHistory { get; set; } = new List<GuardianSummonStatusHistory>();

    [NotMapped] public int DomainEventAggregateId => Id;
    [NotMapped] public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void AppendDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>Immutable history of guardian-summon state changes.</summary>
public sealed class GuardianSummonStatusHistory
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public int GuardianSummonId { get; set; }
    public GuardianSummonStatus? FromStatus { get; set; }
    public GuardianSummonStatus ToStatus { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
    public Guid CorrelationId { get; set; }

    public School School { get; set; } = null!;
    public GuardianSummon GuardianSummon { get; set; } = null!;
    public ApplicationUser ActorUser { get; set; } = null!;
}

public sealed class StudentCaseAction : IStudentAffairsMutableEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentReferralId { get; set; }
    public StudentCaseActionType ActionType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public DateTimeOffset ActionAt { get; set; }
    public string? Result { get; set; }
    public string? AttachmentStorageKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public StudentReferral StudentReferral { get; set; } = null!;
    public ApplicationUser ActorUser { get; set; } = null!;
}
