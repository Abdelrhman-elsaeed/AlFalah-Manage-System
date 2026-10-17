using AlFalah.Application.StudentAffairs.DTOs.Messaging;

namespace AlFalah.Application.Notifications;

public sealed class InboxOptions
{
    public const string SectionName = "Inbox";
    public bool JournalEnabled { get; set; }
    public bool RealtimeEnabled { get; set; }
    public int RetentionDays { get; set; } = 7;
    public string[] AllowedOrigins { get; set; } = [];
}

public sealed record InboxScope(int SchoolId, string UserId);
public sealed record InboxAccess(bool Messaging, bool Notifications, string Fingerprint);
public sealed record InboxSnapshot(int? UnreadMessages, int? UnreadConversations, int? UnreadNotifications,
    long Sequence, DateTimeOffset AsOfUtc);
public sealed record InboxStateDto(int? UnreadMessages, int? UnreadConversations, int? UnreadNotifications,
    string SyncCursor, DateTimeOffset AsOfUtc, bool RealtimeAvailable);
public sealed record InboxChangeDto(Guid EventId, long Sequence, DateTimeOffset OccurredAtUtc);
public sealed record InboxChangesDto(IReadOnlyList<InboxChangeDto> Items, string NextCursor,
    bool HasMore, bool ResyncRequired);
public sealed record InboxChangesPage(IReadOnlyList<InboxChangeDto> Items, long Sequence,
    bool HasMore, bool ResyncRequired);
public sealed record MessagePosition(DateTimeOffset VisibleAtUtc, long MessageId);
public sealed record MessageWindowRow(ConversationMessageDto Message, DateTimeOffset VisibleAtUtc);
public sealed record MessageWindowDto(IReadOnlyList<ConversationMessageDto> Items,
    bool HasOlder, string? NextBeforeCursor);
public sealed record InboxCursor(int Version, string Kind, int SchoolId, string UserId,
    long Sequence = 0, int ConversationId = 0, MessagePosition? Position = null);
public sealed record RealtimeLease(long Id, Guid EventId, InboxScope Scope, Guid LeaseId,
    DateTimeOffset OccurredAtUtc, int Attempt);

public interface IInboxCursorCodec
{
    string Encode(InboxCursor cursor);
    InboxCursor? Decode(string cursor);
}

public interface IInboxRepository
{
    Task<InboxAccess?> GetAccessAsync(InboxScope scope, string? securityStamp, CancellationToken ct);
    Task<InboxSnapshot> GetStateAsync(InboxScope scope, InboxAccess access, CancellationToken ct);
    Task<InboxChangesPage> GetChangesAsync(InboxScope scope, long sequence, int limit, CancellationToken ct);
    Task<IReadOnlyList<MessageWindowRow>?> GetWindowAsync(InboxScope scope, int conversationId,
        MessagePosition? before, int limit, CancellationToken ct);
    Task<IReadOnlyList<RealtimeLease>> ClaimAsync(int limit, CancellationToken ct);
    Task CompleteAsync(RealtimeLease lease, bool success, CancellationToken ct);
    Task PruneAsync(CancellationToken ct);
}

public interface IInboxPublisher
{
    Task PublishAsync(RealtimeLease lease, CancellationToken ct);
    Task RevalidateConnectionsAsync(CancellationToken ct);
}
