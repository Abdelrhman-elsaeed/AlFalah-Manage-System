using AlFalah.Domain.Entities.Notifications;

namespace AlFalah.Application.Notifications;

public sealed record CenterQuery(string? Before = null, string? Category = null, bool Unread = false, int Limit = 30);
public sealed record CenterPosition(DateTimeOffset At, Guid Id, int School, string User, string? Category, bool Unread);
public sealed record CenterItem(Guid Id, string Category, string Title, string Preview, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc, string TaskState, string SourceType, int SourceId);
public sealed record CenterPage(IReadOnlyList<CenterItem> Items, string? NextCursor);
public sealed record CenterState(int UnreadCount, bool Available);
public sealed record CenterDetail(string Title, string Preview, string State, string SourceType, int SourceId,
    IReadOnlyList<CenterDetailRow> Rows, CenterTarget? Action = null);
public sealed record CenterDetailRow(string Label, string Value);
public sealed record CenterTarget(bool Available, string? Route = null, IReadOnlyDictionary<string, string>? Query = null,
    string? Title = null, string? State = null);
public sealed record CenterAudience(string Kind, string Permission, string? UserId = null);
public sealed record CenterFact(int SchoolId, string Key, string Category, string SourceType, int SourceId,
    string Title, string Preview, IReadOnlyList<CenterAudience> Audiences, int? StudentId = null,
    DateOnly? Date = null, DateTimeOffset? Expires = null, int? NotificationId = null, int? MessageId = null);
public sealed record SourceChange(object Source, bool Added, IReadOnlySet<string> Changed);

public interface ICenterRepository
{
    Task<bool> HasAccessAsync(InboxScope scope, CancellationToken ct);
    Task<IReadOnlyList<PersonalNotification>> ListAsync(InboxScope scope, CenterQuery query, CenterPosition? before, CancellationToken ct);
    Task<int> CountAsync(InboxScope scope, CancellationToken ct);
    Task<PersonalNotification?> FindAsync(InboxScope scope, Guid id, CancellationToken ct);
    Task<bool> ReadAsync(InboxScope scope, Guid? id, CancellationToken ct);
    Task<string> TaskStateAsync(PersonalNotification entry, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, string>> TaskStatesAsync(IReadOnlyList<PersonalNotification> entries, CancellationToken ct);
    Task<CenterTarget> TargetAsync(PersonalNotification entry, CancellationToken ct);
    Task<CenterDetail> DetailAsync(PersonalNotification entry, CancellationToken ct);
}

public sealed class WebPushOptions
{
    public bool RegistrationEnabled { get; set; }
    public bool DispatchEnabled { get; set; }
    public bool PwaUiEnabled { get; set; }
    public string VapidKeyId { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";
    public string Subject { get; set; } = "";
    public string[] AllowedProviderHosts { get; set; } = [];
}
public sealed record PushKeys(string P256dh, string Auth);
public sealed record PushMaterial(string Endpoint, PushKeys Keys);
public sealed record RegisterPush(Guid InstallationId, string VapidKeyId, PushMaterial Subscription);
public sealed record PushDevice(Guid Id, Guid InstallationId, int BindingVersion, DateTimeOffset ConfirmedAtUtc);
public sealed record PushConfig(bool Enabled, bool PwaUiEnabled, string? PublicKey, string? VapidKeyId);
public sealed record PushLease(long Id, Guid LeaseId, Guid EntryId, Guid SubscriptionId, int BindingVersion, int Attempt, DateTimeOffset ExpiresAtUtc);
public sealed record PushResult(string State, int? HttpStatus = null, TimeSpan? RetryAfter = null);
public interface IPushRepository
{
    Task<PushDevice?> RegisterAsync(InboxScope scope, RegisterPush request, CancellationToken ct);
    Task<IReadOnlyList<PushDevice>> DevicesAsync(InboxScope scope, CancellationToken ct);
    Task RevokeAsync(InboxScope scope, Guid id, CancellationToken ct);
    Task<IReadOnlyList<PushLease>> ClaimAsync(CancellationToken ct);
    Task<(PersonalNotification Entry, PushMaterial Material)?> EligibleAsync(PushLease lease, CancellationToken ct);
    Task CompleteAsync(PushLease lease, PushResult result, CancellationToken ct);
    Task CancelDisabledAsync(CancellationToken ct);
}
public interface IPushEndpointPolicy
{
    bool IsValid(PushMaterial material);
}
public interface IWebPushClient
{
    Task<PushResult> SendAsync(PushMaterial material, Guid entryId, DateTimeOffset expires, CancellationToken ct);
}
