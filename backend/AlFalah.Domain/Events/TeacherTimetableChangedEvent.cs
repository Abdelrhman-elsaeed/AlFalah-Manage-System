namespace AlFalah.Domain.Events;

public sealed record TeacherTimetableChangedEvent(Guid EventId, int SchoolId, int SubstitutionId,
    int SchoolTimetableId, int Revision, DateOnly LocalDate, string Kind, string[] TeacherUserIds,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) => this with { SubstitutionId = aggregateId };
}
