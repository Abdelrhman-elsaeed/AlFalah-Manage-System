namespace AlFalah.Domain.Events;

public sealed record CoverageAttentionEvent(Guid EventId, int SchoolId, int NeedId, string Kind,
    string? TeacherUserId, string Message, DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) => this with { NeedId = aggregateId };
}
