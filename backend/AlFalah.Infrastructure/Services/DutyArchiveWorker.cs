using AlFalah.Application.Duty;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

public sealed class DutyArchiveWorker(IServiceScopeFactory scopes,ILogger<DutyArchiveWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(15));
        while(await timer.WaitForNextTickAsync(stoppingToken))
        {
            try {using var scope=scopes.CreateScope();var processor=scope.ServiceProvider.GetRequiredService<DutyArchiveProcessor>();
                await processor.ProcessBatchAsync(stoppingToken);await processor.ReconcileAsync(stoppingToken);}
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch {logger.LogWarning("Duty document processing unavailable; retained signing evidence is unchanged.");}
        }
    }
}
