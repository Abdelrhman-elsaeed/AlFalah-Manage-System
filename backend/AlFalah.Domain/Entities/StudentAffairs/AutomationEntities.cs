using System.ComponentModel.DataAnnotations;
using AlFalah.Domain.Enums.StudentAffairs;

namespace AlFalah.Domain.Entities.StudentAffairs;

/// <summary>Immutable compiled threshold policy derived from a settings version.</summary>
public sealed class AutomationRuleDefinition
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public int SchoolStudentAffairsSettingsId { get; set; }
    public int Version { get; set; }
    public StudentTermMetricCode MetricCode { get; set; }
    public int Threshold { get; set; }
    public bool RepeatsAtMultiples { get; set; }
    public string PolicySnapshotJson { get; set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset CompiledAt { get; set; } = DateTimeOffset.UtcNow;
    public string CompiledByUserId { get; set; } = string.Empty;

    public School School { get; set; } = null!;
    public SchoolStudentAffairsSettings SchoolStudentAffairsSettings { get; set; } = null!;
    public ApplicationUser CompiledByUser { get; set; } = null!;
}

public sealed class StudentTermMetric : IStudentAffairsMutableEntity, IStudentAffairsConcurrentEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int AcademicTermId { get; set; }
    public StudentTermMetricCode MetricCode { get; set; }
    public int Count { get; set; }
    public DateTimeOffset RecalculatedAt { get; set; } = DateTimeOffset.UtcNow;
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
}

/// <summary>Immutable idempotency and audit proof for one threshold occurrence.</summary>
public sealed class AutomationTriggerLedger
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int AcademicTermId { get; set; }
    public long RuleVersionId { get; set; }
    public int Threshold { get; set; }
    public int OccurrenceNumber { get; set; }
    public int CountSnapshot { get; set; }
    public AutomationTriggerValidity Validity { get; set; } = AutomationTriggerValidity.Satisfied;
    public DateTimeOffset TriggeredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SourceInvalidatedAt { get; set; }
    public string? ReviewNote { get; set; }
    public Guid CorrelationId { get; set; }

    public School School { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public AcademicTerm AcademicTerm { get; set; } = null!;
    public AutomationRuleDefinition RuleVersion { get; set; } = null!;
}

/// <summary>Reliable domain-event envelope written in the business transaction.</summary>
public sealed class OutboxMessage
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset? DeadLetteredAt { get; set; }

    public School School { get; set; } = null!;
}

/// <summary>Idempotency record for an externally delivered message or webhook.</summary>
public sealed class InboxMessage
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public Guid MessageId { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }

    public School School { get; set; } = null!;
}
