using AlFalah.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

public sealed class EvidenceReconciliationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EvidenceReconciliationBackgroundService> _logger;

    public EvidenceReconciliationBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration,
        ILogger<EvidenceReconciliationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = Math.Clamp(_configuration.GetValue<int?>("TeacherEvidence:ReconciliationMinutes") ?? 30, 5, 1_440);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<IEvidenceReconciliationService>();
                var changed = await reconciler.ReconcileAsync(stoppingToken);
                if (changed > 0) _logger.LogInformation("Evidence reconciliation updated {Count} submission(s).", changed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Evidence reconciliation failed; it will retry on the next interval.");
            }
        }
    }
}
