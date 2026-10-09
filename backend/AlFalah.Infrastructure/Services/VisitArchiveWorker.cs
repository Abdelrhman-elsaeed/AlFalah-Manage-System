using AlFalah.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlFalah.Infrastructure.Services;

public sealed class VisitArchiveWorker(IServiceScopeFactory scopes, IOptionsMonitor<StorageOptions> options,
    ILogger<VisitArchiveWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastReconcile = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            var flags = options.CurrentValue;
            if (flags.AdministrationEnabled && flags.ReadModelEnabled)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IVisitArchiveProcessor>();
                    await processor.ProcessBatchAsync(stoppingToken);
                    if (DateTimeOffset.UtcNow - lastReconcile > TimeSpan.FromMinutes(15))
                    { await processor.ReconcileAsync(stoppingToken); lastReconcile = DateTimeOffset.UtcNow; }
                }
                catch (Exception) when (!stoppingToken.IsCancellationRequested)
                { logger.LogWarning("Visit archive worker cycle unavailable; durable operations retained for recovery."); }
            }
            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
