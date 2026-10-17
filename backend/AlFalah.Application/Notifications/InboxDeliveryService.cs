using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Notifications;

public sealed class InboxConnectionService(IInboxRepository repository, IOptions<InboxOptions> options)
{
    public Task<InboxAccess?> AuthorizeAsync(InboxScope scope, string stamp, CancellationToken ct)
        => options.Value.JournalEnabled && options.Value.RealtimeEnabled
            ? repository.GetAccessAsync(scope, stamp, ct) : Task.FromResult<InboxAccess?>(null);
}

public sealed class InboxDeliveryService(IInboxRepository repository, IInboxPublisher publisher,
    ILogger<InboxDeliveryService> logger)
{
    public Task<IReadOnlyList<RealtimeLease>> ClaimAsync(CancellationToken ct) => repository.ClaimAsync(25, ct);
    public Task RevalidateAsync(CancellationToken ct) => publisher.RevalidateConnectionsAsync(ct);
    public Task PruneAsync(CancellationToken ct) => repository.PruneAsync(ct);

    public async Task DeliverAsync(RealtimeLease lease, CancellationToken ct)
    {
        var success = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await publisher.PublishAsync(lease, timeout.Token);
            success = true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        { logger.LogWarning("Inbox delivery failed ({Type})", ex.GetType().Name); }
        await repository.CompleteAsync(lease, success, ct);
    }
}
