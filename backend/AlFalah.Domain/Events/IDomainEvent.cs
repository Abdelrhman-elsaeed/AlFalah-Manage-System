using MediatR;

namespace AlFalah.Domain.Events;

/// <summary>
/// Immutable business fact captured for reliable publication through the outbox.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    int SchoolId { get; }
    DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// Supplies the database-generated aggregate id before outbox serialization.
    /// Existing aggregate events simply receive their already-known id.
    /// </summary>
    IDomainEvent WithAggregateId(int aggregateId);
}

public interface IHasDomainEvents
{
    int DomainEventAggregateId { get; }
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
