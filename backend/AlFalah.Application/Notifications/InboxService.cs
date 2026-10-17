using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Notifications;

public sealed class InboxService(IInboxRepository repository, IInboxCursorCodec cursors,
    ICurrentUserService currentUser, IOptions<InboxOptions> options)
{
    private InboxScope? Scope => currentUser.ActiveSchoolId is { } school && currentUser.UserId is { Length: > 0 } user
        ? new(school, user) : null;

    public async Task<ApiResponse<InboxStateDto>> StateAsync(CancellationToken ct)
    {
        if (Scope is not { } scope || await repository.GetAccessAsync(scope, null, ct) is not { } access)
            return ApiResponse<InboxStateDto>.Fail("Inbox access is unavailable");
        var state = await repository.GetStateAsync(scope, access, ct);
        return ApiResponse<InboxStateDto>.Success(new(state.UnreadMessages, state.UnreadConversations,
            state.UnreadNotifications, Encode(scope, state.Sequence), state.AsOfUtc,
            options.Value.JournalEnabled && options.Value.RealtimeEnabled));
    }

    public async Task<ApiResponse<InboxChangesDto>> ChangesAsync(string after, int limit, CancellationToken ct)
    {
        if (Scope is not { } scope || await repository.GetAccessAsync(scope, null, ct) is null)
            return ApiResponse<InboxChangesDto>.Fail("Inbox access is unavailable");
        var cursor = Parse(after, scope, "sync");
        if (cursor is null) return ApiResponse<InboxChangesDto>.Fail("Invalid inbox cursor; reload inbox state");
        var page = await repository.GetChangesAsync(scope, cursor.Sequence, Math.Clamp(limit, 1, 100), ct);
        return ApiResponse<InboxChangesDto>.Success(new(page.Items, Encode(scope, page.Sequence),
            page.HasMore, page.ResyncRequired));
    }

    public async Task<ApiResponse<MessageWindowDto>> WindowAsync(int conversationId, string? before, int limit, CancellationToken ct, int? at = null)
    {
        if (Scope is not { } scope || await repository.GetAccessAsync(scope, null, ct) is not { Messaging: true })
            return ApiResponse<MessageWindowDto>.Fail("You do not have permission to view messages");
        InboxCursor? cursor = null;
        if (before is not null)
        {
            cursor = Parse(before, scope, "history");
            if (cursor?.ConversationId != conversationId || cursor.Position is null)
                return ApiResponse<MessageWindowDto>.Fail("Invalid conversation cursor");
        }
        limit = Math.Clamp(limit, 1, 100);
        var position = cursor?.Position;
        if (at is { } messageId)
        {
            if (before is not null || await repository.GetPositionAsync(scope, conversationId, messageId, ct) is not { } anchor)
                return ApiResponse<MessageWindowDto>.Fail("Message was not found");
            position = anchor;
        }
        var rows = await repository.GetWindowAsync(scope, conversationId, position, limit + 1, ct);
        if (rows is null) return ApiResponse<MessageWindowDto>.Fail("Conversation was not found");
        var hasOlder = rows.Count > limit;
        var selected = rows.Take(limit).Reverse().ToArray();
        var oldest = selected.FirstOrDefault();
        var next = hasOlder && oldest is not null ? cursors.Encode(new(1, "history", scope.SchoolId,
            scope.UserId, ConversationId: conversationId, Position: new(oldest.VisibleAtUtc, oldest.Message.Id))) : null;
        return ApiResponse<MessageWindowDto>.Success(new(selected.Select(r => r.Message).ToArray(), hasOlder, next));
    }

    private string Encode(InboxScope scope, long sequence) => cursors.Encode(new(1, "sync", scope.SchoolId, scope.UserId, sequence));
    private InboxCursor? Parse(string raw, InboxScope scope, string kind)
    {
        var cursor = raw.Length <= 4096 ? cursors.Decode(raw) : null;
        return cursor is { Version: 1 } && cursor.Kind == kind && cursor.SchoolId == scope.SchoolId
            && cursor.UserId == scope.UserId ? cursor : null;
    }
}
