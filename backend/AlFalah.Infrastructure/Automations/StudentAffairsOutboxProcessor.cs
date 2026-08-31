using System.Text.Json;
using System.Text.Json.Serialization;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Automations;

public sealed class StudentAffairsOutboxProcessor
{
    private static readonly string[] SupportedEventTypes =
    {
        typeof(BehaviorIncidentLoggedEvent).FullName!,
        typeof(AcademicConcernLoggedEvent).FullName!,
        typeof(SessionDelayLoggedEvent).FullName!,
        typeof(StudentAbsentRecordedEvent).FullName!,
        typeof(AbsenceExcuseAcceptedEvent).FullName!,
        typeof(MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3).FullName!
    };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly AlFalahDbContext _context;
    private readonly StudentAffairsAutomationRuleEngine _rules;
    private readonly StudentAffairsNotificationDispatcher _notifications;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StudentAffairsOutboxProcessor> _logger;
    private readonly int _batchSize;
    private readonly int _maxAttempts;
    private readonly TimeSpan _leaseDuration;

    public StudentAffairsOutboxProcessor(
        AlFalahDbContext context,
        StudentAffairsAutomationRuleEngine rules,
        StudentAffairsNotificationDispatcher notifications,
        TimeProvider timeProvider,
        IConfiguration configuration,
        ILogger<StudentAffairsOutboxProcessor> logger)
    {
        _context = context;
        _rules = rules;
        _notifications = notifications;
        _timeProvider = timeProvider;
        _logger = logger;
        _batchSize = Math.Clamp(configuration.GetValue("StudentAffairsOutbox:BatchSize", 25), 1, 200);
        _maxAttempts = Math.Clamp(configuration.GetValue("StudentAffairsOutbox:MaxAttempts", 8), 1, 50);
        _leaseDuration = TimeSpan.FromSeconds(Math.Clamp(
            configuration.GetValue("StudentAffairsOutbox:LeaseSeconds", 120), 30, 1800));
    }

    public async Task<IReadOnlyList<long>> ClaimBatchAsync(
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var ids = await _context.OutboxMessages.AsNoTracking()
            .Where(message => message.ProcessedAt == null
                && message.DeadLetteredAt == null
                && SupportedEventTypes.Contains(message.EventType)
                && (message.NextAttemptAt == null || message.NextAttemptAt <= now)
                && (message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now))
            .OrderBy(message => message.OccurredAt)
            .ThenBy(message => message.Id)
            .Select(message => message.Id)
            .Take(_batchSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var claimed = new List<long>(ids.Count);
        foreach (var id in ids)
        {
            var updated = await _context.OutboxMessages
                .Where(message => message.Id == id
                    && message.ProcessedAt == null
                    && message.DeadLetteredAt == null
                    && SupportedEventTypes.Contains(message.EventType)
                    && (message.NextAttemptAt == null || message.NextAttemptAt <= now)
                    && (message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(message => message.LeaseOwner, leaseOwner)
                    .SetProperty(message => message.LeaseExpiresAt, now.Add(_leaseDuration)),
                    cancellationToken).ConfigureAwait(false);
            if (updated == 1) claimed.Add(id);
        }
        return claimed;
    }

    public async Task ProcessClaimedAsync(
        long messageId,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = await _context.OutboxMessages.SingleOrDefaultAsync(item =>
                item.Id == messageId && item.LeaseOwner == leaseOwner && item.ProcessedAt == null,
                cancellationToken).ConfigureAwait(false);
            if (message is null) return;

            var domainEvent = Deserialize(message);
            await _rules.ProcessAsync(domainEvent, cancellationToken).ConfigureAwait(false);
            await _notifications.ProcessAsync(domainEvent, cancellationToken).ConfigureAwait(false);
            message.ProcessedAt = _timeProvider.GetUtcNow();
            message.LeaseOwner = null;
            message.LeaseExpiresAt = null;
            message.LastError = null;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Student Affairs outbox message {MessageId} failed", messageId);
            _context.ChangeTracker.Clear();
            var failed = await _context.OutboxMessages.SingleOrDefaultAsync(item =>
                item.Id == messageId && item.ProcessedAt == null,
                cancellationToken).ConfigureAwait(false);
            if (failed is null) return;
            failed.AttemptCount++;
            failed.LastError = exception.ToString().Length <= 8000
                ? exception.ToString()
                : exception.ToString()[..8000];
            failed.LeaseOwner = null;
            failed.LeaseExpiresAt = null;
            if (failed.AttemptCount >= _maxAttempts)
            {
                failed.DeadLetteredAt = _timeProvider.GetUtcNow();
                failed.NextAttemptAt = null;
            }
            else
            {
                var exponentialSeconds = Math.Min(1800, Math.Pow(2, failed.AttemptCount));
                var jitterMilliseconds = Random.Shared.Next(100, 1000);
                failed.NextAttemptAt = _timeProvider.GetUtcNow()
                    .AddSeconds(exponentialSeconds)
                    .AddMilliseconds(jitterMilliseconds);
            }
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static IDomainEvent Deserialize(OutboxMessage message)
    {
        object? value = message.EventType switch
        {
            var type when type.EndsWith(nameof(BehaviorIncidentLoggedEvent), StringComparison.Ordinal) =>
                JsonSerializer.Deserialize<BehaviorIncidentLoggedEvent>(message.PayloadJson, JsonOptions),
            var type when type.EndsWith(nameof(AcademicConcernLoggedEvent), StringComparison.Ordinal) =>
                JsonSerializer.Deserialize<AcademicConcernLoggedEvent>(message.PayloadJson, JsonOptions),
            var type when type.EndsWith(nameof(SessionDelayLoggedEvent), StringComparison.Ordinal) =>
                JsonSerializer.Deserialize<SessionDelayLoggedEvent>(message.PayloadJson, JsonOptions),
            var type when type.EndsWith(nameof(StudentAbsentRecordedEvent), StringComparison.Ordinal) =>
                JsonSerializer.Deserialize<StudentAbsentRecordedEvent>(message.PayloadJson, JsonOptions),
            var type when type.EndsWith(nameof(AbsenceExcuseAcceptedEvent), StringComparison.Ordinal) =>
                JsonSerializer.Deserialize<AbsenceExcuseAcceptedEvent>(message.PayloadJson, JsonOptions),
            var type when type.EndsWith(nameof(MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3), StringComparison.Ordinal) =>
                JsonSerializer.Deserialize<MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3>(message.PayloadJson, JsonOptions),
            _ => throw new InvalidDataException($"Unsupported Student Affairs outbox event type: {message.EventType}")
        };
        return (IDomainEvent?)value
            ?? throw new InvalidDataException($"Outbox event {message.EventId} could not be deserialized as {message.EventType}");
    }
}
