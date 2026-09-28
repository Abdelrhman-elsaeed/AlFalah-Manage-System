namespace AlFalah.Domain.Events;

public sealed record MessageReleaseDueEvent(
    Guid EventId,
    int SchoolId,
    int ConversationMessageId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) => this with { ConversationMessageId = aggregateId };
}

public sealed record TimetablePublishedEvent(
    Guid EventId,
    int SchoolId,
    int SchoolTimetableId,
    int Revision,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) => this with { SchoolTimetableId = aggregateId };
}
