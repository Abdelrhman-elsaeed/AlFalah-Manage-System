namespace AlFalah.Domain.Entities.Notifications;

/// <summary>Sequence allocation is serialized per recipient until the enclosing transaction commits.</summary>
public sealed class RecipientInboxStream
{
    public int SchoolId { get; set; }
    public string UserId { get; set; } = "";
    public long CommittedSequence { get; set; }
    public long MinimumRetainedSequence { get; set; }
}

public sealed class InboxChange
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public string UserId { get; set; } = "";
    public long Sequence { get; set; }
    public Guid EventId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}

/// <summary>Generic realtime invalidation only. Delivered never means read or device delivery.</summary>
public sealed class NotificationDeliveryIntent
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public string UserId { get; set; } = "";
    public string SourceKey { get; set; } = "";
    public string Channel { get; set; } = "Realtime";
    public Guid EventId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public int Attempts { get; set; }
}

public sealed class InboxDeliveryAttempt
{
    public long Id { get; set; }
    public long IntentId { get; set; }
    public Guid LeaseId { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public bool Succeeded { get; set; }
}
