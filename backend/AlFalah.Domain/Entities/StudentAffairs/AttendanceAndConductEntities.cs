using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;

namespace AlFalah.Domain.Entities.StudentAffairs;

public sealed class DailyStudentAttendance
    : IStudentAffairsMutableEntity, IStudentAffairsConcurrentEntity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int AcademicTermId { get; set; }
    public int ClassroomId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public StudentAttendanceStatus Status { get; set; }
    public AbsenceExcuseStatus? ExcuseStatus { get; set; }
    public DateTimeOffset? ArrivedAfterAttendanceRecordedAt { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public StudentAttendanceSource Source { get; set; }
    public string? CorrectionReason { get; set; }
    public string? CorrectedByUserId { get; set; }
    public DateTimeOffset? CorrectedAt { get; set; }
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
    public ApplicationUser RecordedByUser { get; set; } = null!;
    public ApplicationUser? CorrectedByUser { get; set; }
    public ICollection<AbsenceExcuse> Excuses { get; set; } = new List<AbsenceExcuse>();

    [NotMapped] public int DomainEventAggregateId => Id;
    [NotMapped] public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void AppendDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public sealed class AbsenceExcuse
    : IStudentAffairsMutableEntity, IStudentAffairsConcurrentEntity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int DailyStudentAttendanceId { get; set; }
    public int GuardianProfileId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public AbsenceExcuseType ExcuseType { get; set; }
    public string? GuardianNotes { get; set; }
    public AbsenceExcuseStatus Status { get; set; } = AbsenceExcuseStatus.Pending;
    public string? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewReason { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public School School { get; set; } = null!;
    public DailyStudentAttendance DailyStudentAttendance { get; set; } = null!;
    public GuardianProfile GuardianProfile { get; set; } = null!;
    public ApplicationUser? ReviewedByUser { get; set; }
    public ICollection<AbsenceExcuseAttachment> Attachments { get; set; } = new List<AbsenceExcuseAttachment>();

    [NotMapped] public int DomainEventAggregateId => Id;
    [NotMapped] public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void AppendDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public sealed class AbsenceExcuseAttachment : IStudentAffairsMutableEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AbsenceExcuseId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string StorageProvider { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string UploadedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public AbsenceExcuse AbsenceExcuse { get; set; } = null!;
    public ApplicationUser UploadedByUser { get; set; } = null!;
}

public sealed class MorningArrivalDelay : IStudentAffairsMutableEntity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int AcademicTermId { get; set; }
    public DateTimeOffset ArrivalAt { get; set; }
    public DateOnly SchoolLocalDate { get; set; }
    public TimeOnly CutoffTimeSnapshot { get; set; }
    public int DelayMinutes { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? ReasonProvidedByGuardianAt { get; set; }
    public string NotificationPolicySnapshot { get; set; } = "ImmediateGuardian";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public AcademicTerm AcademicTerm { get; set; } = null!;

    [NotMapped] public int DomainEventAggregateId => Id;
    [NotMapped] public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void AppendDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public sealed class SessionDelay
    : IStudentAffairsMutableEntity, IStudentAffairsConcurrentEntity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int AcademicTermId { get; set; }
    public int ClassroomId { get; set; }
    public int? SchoolTimetableId { get; set; }
    public int? SchoolTimetableEntryId { get; set; }
    public byte Period { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int? DelayMinutes { get; set; }
    public string? Reason { get; set; }
    public int ReportedByInstructorProfileId { get; set; }
    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;
    public GuardianNotificationStatus GuardianNotificationStatus { get; set; } = GuardianNotificationStatus.Pending;
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
    public SchoolTimetable? SchoolTimetable { get; set; }
    public SchoolTimetableEntry? SchoolTimetableEntry { get; set; }
    public InstructorProfile ReportedByInstructorProfile { get; set; } = null!;

    [NotMapped] public int DomainEventAggregateId => Id;
    [NotMapped] public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void AppendDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public sealed class AcademicConcern : IStudentAffairsMutableEntity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int AcademicTermId { get; set; }
    public int? ClassroomId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public int ReportedByInstructorProfileId { get; set; }
    public int? SchoolTimetableEntryId { get; set; }
    public GuardianDispatchDecision GuardianDispatchDecision { get; set; } = GuardianDispatchDecision.PendingOfficerDecision;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public AcademicTerm AcademicTerm { get; set; } = null!;
    public Classroom? Classroom { get; set; }
    public InstructorProfile ReportedByInstructorProfile { get; set; } = null!;
    public SchoolTimetableEntry? SchoolTimetableEntry { get; set; }

    [NotMapped] public int DomainEventAggregateId => Id;
    [NotMapped] public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void AppendDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public sealed class BehaviorIncident
    : IStudentAffairsMutableEntity, IStudentAffairsConcurrentEntity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int AcademicTermId { get; set; }
    public int? ClassroomId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public BehaviorSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string? Location { get; set; }
    public int? ReportedByInstructorProfileId { get; set; }
    public string? ReportedByStaffUserId { get; set; }
    public string? ImmediateActionTaken { get; set; }
    public GuardianDispatchDecision GuardianDispatchDecision { get; set; } = GuardianDispatchDecision.PendingOfficerDecision;
    public bool IsUpheld { get; set; } = true;
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
    public Classroom? Classroom { get; set; }
    public InstructorProfile? ReportedByInstructorProfile { get; set; }
    public ApplicationUser? ReportedByStaffUser { get; set; }

    [NotMapped] public int DomainEventAggregateId => Id;
    [NotMapped] public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void AppendDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public sealed class StudentRecognition : IStudentAffairsMutableEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int AcademicTermId { get; set; }
    public int? ClassroomId { get; set; }
    public string RecognitionType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset RecognizedAt { get; set; }
    public int ReportedByInstructorProfileId { get; set; }
    public GuardianNotificationStatus GuardianNotificationStatus { get; set; } = GuardianNotificationStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public AcademicTerm AcademicTerm { get; set; } = null!;
    public Classroom? Classroom { get; set; }
    public InstructorProfile ReportedByInstructorProfile { get; set; } = null!;
}

/// <summary>Immutable metadata and row snapshots for one Noor weekly correction export.</summary>
public sealed class NoorAbsenceCorrectionBatch : IStudentAffairsMutableEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public DateOnly WeekStartsOn { get; set; }
    public DateOnly WeekEndsOn { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public NoorAbsenceCorrectionBatchStatus Status { get; set; } = NoorAbsenceCorrectionBatchStatus.Created;
    public int RowCount { get; set; }
    public string? FileName { get; set; }
    public string? Sha256 { get; set; }
    public DateTimeOffset? ExportedAt { get; set; }
    public string? ExportedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public ApplicationUser? ExportedByUser { get; set; }
    public ICollection<NoorAbsenceCorrectionBatchItem> Items { get; set; } = new List<NoorAbsenceCorrectionBatchItem>();
}

/// <summary>Point-in-time copy of an accepted excused absence included in a Noor export.</summary>
public sealed class NoorAbsenceCorrectionBatchItem
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public int BatchId { get; set; }
    public int DailyStudentAttendanceId { get; set; }
    public int StudentId { get; set; }
    public string StudentNameSnapshot { get; set; } = string.Empty;
    public string NationalIdSnapshot { get; set; } = string.Empty;
    public DateOnly AttendanceDate { get; set; }
    public AbsenceExcuseStatus ExcuseStatusSnapshot { get; set; }

    public School School { get; set; } = null!;
    public NoorAbsenceCorrectionBatch Batch { get; set; } = null!;
    public DailyStudentAttendance DailyStudentAttendance { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
