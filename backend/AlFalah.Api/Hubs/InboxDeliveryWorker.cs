using AlFalah.Application.Notifications;
using Microsoft.Extensions.Options;

namespace AlFalah.Api.Hubs;

public sealed class InboxDeliveryWorker(IServiceScopeFactory scopes, IOptions<InboxOptions> options,
    ILogger<InboxDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ticks = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (options.Value.JournalEnabled)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<InboxDeliveryService>();
                    if (ticks % 5 == 0) await service.RevalidateAsync(stoppingToken);
                    await Parallel.ForEachAsync(await service.ClaimAsync(stoppingToken),
                        new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = stoppingToken }, async (lease, token) =>
                    {
                        await using var deliveryScope = scopes.CreateAsyncScope();
                        await deliveryScope.ServiceProvider.GetRequiredService<InboxDeliveryService>().DeliverAsync(lease, token);
                    });
                    if (ticks % 60 == 0) await service.PruneAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Inbox worker will retry ({Type})", ex.GetType().Name); }
            ticks++;
            try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
