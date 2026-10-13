using AlFalah.Application.Duty;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

public sealed class DutyReconciliationWorker(IServiceScopeFactory scopes, ILogger<DutyReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IDutyRepository>();
                var processor = scope.ServiceProvider.GetRequiredService<DutyReconciler>();
                foreach (var op in await repo.PendingAsync(stoppingToken))
                {
                    try { await processor.ProcessAsync(op, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception ex) { logger.LogWarning(ex, "Duty reconciliation {OperationId} will retry", op.Id); await processor.FailedAsync(op, stoppingToken); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Duty reconciliation unavailable; next poll will retry"); }
        }
    }
}
