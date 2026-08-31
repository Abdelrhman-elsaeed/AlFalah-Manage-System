using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Automations;

public sealed class StudentAffairsOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StudentAffairsOutboxWorker> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly string _leaseOwner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public StudentAffairsOutboxWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<StudentAffairsOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollInterval = TimeSpan.FromMilliseconds(Math.Clamp(
            configuration.GetValue("StudentAffairsOutbox:PollMilliseconds", 2000), 250, 60000));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<long> ids;
                using (var claimScope = _scopeFactory.CreateScope())
                {
                    var processor = claimScope.ServiceProvider.GetRequiredService<StudentAffairsOutboxProcessor>();
                    ids = await processor.ClaimBatchAsync(_leaseOwner, stoppingToken).ConfigureAwait(false);
                }

                foreach (var id in ids)
                {
                    using var messageScope = _scopeFactory.CreateScope();
                    var processor = messageScope.ServiceProvider.GetRequiredService<StudentAffairsOutboxProcessor>();
                    await processor.ProcessClaimedAsync(id, _leaseOwner, stoppingToken).ConfigureAwait(false);
                }

                if (ids.Count == 0)
                    await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Student Affairs outbox worker iteration failed");
                await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
