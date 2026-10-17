using System.ComponentModel.DataAnnotations.Schema;
using AlFalah.Domain.Events;

namespace AlFalah.Domain.Entities;

/// <summary>A dated absence need; approved ownership is always read from the substitution ledger.</summary>
public sealed class DailyCoverageNeed : IHasDomainEvents
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int SchoolTimetableId { get; set; }
    public DateOnly LocalDate { get; set; }
    public int SourceEntryId { get; set; }
    public int OriginalTeacherId { get; set; }
    public string OriginalTeacherName { get; set; } = "";
    public string Classroom { get; set; } = "";
    public string Subject { get; set; } = "";
    public string EntryIdsJson { get; set; } = "[]";
    public string PeriodsJson { get; set; } = "[]";
    public string TimeZoneId { get; set; } = "Africa/Cairo";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string State { get; set; } = "Needed";
    public bool IsRequired { get; set; } = true;
    public bool SubstituteWasAbsent { get; set; }
    public string? ReviewReason { get; set; }
    public int? ActiveSubstitutionId { get; set; }
    public string? AssignedTeacherName { get; set; }
    public string? AssignedUserId { get; set; }
    public TimetableSubstitution? ActiveSubstitution { get; set; }
    public int Revision { get; set; } = 1;
    public string Narrative { get; set; } = "";
    public string Notes { get; set; } = "";
    public string? CorrectionReason { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<CoverageRecord> Records { get; set; } = [];
    private readonly List<IDomainEvent> events = [];
    [NotMapped] public int DomainEventAggregateId => Id;
    [NotMapped] public IReadOnlyCollection<IDomainEvent> DomainEvents => events;
    public void AppendDomainEvent(IDomainEvent domainEvent) => events.Add(domainEvent);
    public void ClearDomainEvents() => events.Clear();
}

/// <summary>Append-only acknowledgement, execution or administrative decision, tied to its actual assignment.</summary>
public sealed class CoverageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int DailyCoverageNeedId { get; set; }
    public DailyCoverageNeed Need { get; set; } = null!;
    public int? SubstitutionId { get; set; }
    public Guid RequestId { get; set; }
    public string ActorUserId { get; set; } = "";
    public string Kind { get; set; } = "";
    public int Revision { get; set; }
    public string Narrative { get; set; } = "";
    public string Notes { get; set; } = "";
    public string? Reason { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public string SnapshotHash { get; set; } = "";
    public byte[]? SignatureBytes { get; set; }
    public string? SignatureHash { get; set; }
    public DateTimeOffset? SignatureUpdatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Independent durable signal, committed with the staff attendance fact.</summary>
public sealed class CoverageReconciliationSignal
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public DateOnly LocalDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public string? LastError { get; set; }
}
