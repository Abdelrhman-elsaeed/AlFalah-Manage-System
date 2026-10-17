using System.Collections.Concurrent;
using System.Security.Claims;
using AlFalah.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace AlFalah.Api.Hubs;

[Authorize]
public sealed class InboxHub(InboxConnectionService accessService, InboxConnectionRegistry connections,
    IOptions<InboxOptions> options) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User!;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        var stamp = user.FindFirstValue("security_stamp");
        if (!options.Value.JournalEnabled || !options.Value.RealtimeEnabled || string.IsNullOrEmpty(userId)
            || string.IsNullOrEmpty(stamp) || !int.TryParse(user.FindFirstValue("active_school_id"), out var school))
            throw new HubException("Inbox connection is unavailable");
        var scope = new InboxScope(school, userId);
        var access = await accessService.AuthorizeAsync(scope, stamp, Context.ConnectionAborted);
        if (access is null) throw new HubException("Inbox connection is unavailable");
        connections.Add(new(Context.ConnectionId, scope, stamp, access.Fingerprint, Context.Abort));
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

public sealed record InboxConnection(string Id, InboxScope Scope, string Stamp, string Fingerprint, Action Abort);

public sealed class InboxConnectionRegistry
{
    private readonly ConcurrentDictionary<string, InboxConnection> connections = new();
    public IReadOnlyCollection<InboxConnection> All => connections.Values.ToArray();
    public void Add(InboxConnection connection) => connections[connection.Id] = connection;
    public void Remove(string id) => connections.TryRemove(id, out _);
}

public sealed class SignalRInboxPublisher(IHubContext<InboxHub> hub, InboxConnectionRegistry registry,
    InboxConnectionService accessService) : IInboxPublisher
{
    public async Task PublishAsync(RealtimeLease lease, CancellationToken ct)
    {
        foreach (var connection in registry.All.Where(c => c.Scope == lease.Scope))
        {
            if (!await ValidAsync(connection, ct)) continue;
            await hub.Clients.Client(connection.Id).SendAsync("InboxChanged.v1", new
            {
                contractVersion = 1, eventId = lease.EventId, kind = "inbox-invalidated", occurredAtUtc = lease.OccurredAtUtc
            }, ct);
        }
    }

    public async Task RevalidateConnectionsAsync(CancellationToken ct)
    {
        foreach (var connection in registry.All) await ValidAsync(connection, ct);
    }

    private async Task<bool> ValidAsync(InboxConnection connection, CancellationToken ct)
    {
        var access = await accessService.AuthorizeAsync(connection.Scope, connection.Stamp, ct);
        if (access is not null && access.Fingerprint == connection.Fingerprint) return true;
        registry.Remove(connection.Id); connection.Abort(); return false;
    }
}
