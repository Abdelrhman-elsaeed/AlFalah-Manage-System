namespace AlFalah.Domain.Entities.Notifications;

/// <summary>Recipient projection. Source workflows, task state and legacy read receipts stay authoritative.</summary>
public sealed class PersonalNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SchoolId { get; set; }
    public string UserId { get; set; } = "";
    public string SourceKey { get; set; } = "";
    public string Category { get; set; } = "";
    public string SourceType { get; set; } = "";
    public int SourceId { get; set; }
    public int? StudentId { get; set; }
    public string Audience { get; set; } = "";
    public string Permission { get; set; } = "";
    public int? NotificationId { get; set; }
    public int? MessageId { get; set; }
    public string Title { get; set; } = "";
    public string Preview { get; set; } = "";
    public DateOnly? LocalDate { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public string PushDisposition { get; set; } = "Disabled";
}

/// <summary>Explicit complete-sheet signal, staged only after full roster validation.</summary>
public sealed class AttendanceCompletion
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int? ClassroomId { get; set; }
    public DateOnly Date { get; set; }
    public int RosterCount { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; }
}

public sealed class BrowserPushSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SchoolId { get; set; }
    public string UserId { get; set; } = "";
    public Guid InstallationId { get; set; }
    public string EndpointHash { get; set; } = "";
    public string ProtectedMaterial { get; set; } = "";
    public string VapidKeyId { get; set; } = "";
    public int BindingVersion { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ConfirmedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}

public sealed class BrowserPushTarget
{
    public long Id { get; set; }
    public Guid EntryId { get; set; }
    public PersonalNotification Entry { get; set; } = null!;
    public Guid SubscriptionId { get; set; }
    public BrowserPushSubscription Subscription { get; set; } = null!;
    public int BindingVersion { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public int Attempts { get; set; }
    public string State { get; set; } = "Pending";
    public int? LastHttpStatus { get; set; }
    public DateTimeOffset? ProviderAcceptedAtUtc { get; set; }
}

public sealed class BrowserPushAttempt
{
    public long Id { get; set; }
    public long TargetId { get; set; }
    public BrowserPushTarget Target { get; set; } = null!;
    public Guid LeaseId { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public string State { get; set; } = "";
    public int? HttpStatus { get; set; }
}
