using AlFalah.Application.IntelligentTimetable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

public sealed class CoverageReconciliationWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<CoverageReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var claimScope = scopes.CreateScope();
                var repository = claimScope.ServiceProvider.GetRequiredService<IDailyCoverageRepository>();
                var owner = Guid.NewGuid().ToString("N");
                foreach (var id in await repository.ClaimSignalsAsync(owner, clock.GetUtcNow(), stoppingToken))
                {
                    using var workScope = scopes.CreateScope();
                    var workRepo = workScope.ServiceProvider.GetRequiredService<IDailyCoverageRepository>();
                    try { await workScope.ServiceProvider.GetRequiredService<DailyCoverageReconciler>().ProcessSignalAsync(id, owner, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception ex) { logger.LogWarning(ex, "Coverage reconciliation {Id} will retry", id); await workRepo.FailedSignalAsync(id, owner, clock.GetUtcNow(), ex.Message, stoppingToken); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Coverage reconciliation poll will retry"); }
        }
    }
}
