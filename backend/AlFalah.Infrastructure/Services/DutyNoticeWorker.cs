using AlFalah.Application.Duty;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

public sealed class DutyNoticeWorker(IServiceScopeFactory scopes, ILogger<DutyNoticeWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IDutyExecutionRepository>();
                var processor = scope.ServiceProvider.GetRequiredService<DutyNoticeProcessor>();
                foreach (var pending in await repo.PendingNoticesAsync(stoppingToken)) await processor.ProcessAsync(pending, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogDebug(ex, "Duty notice dispatch will retry"); }
        }
    }
}
