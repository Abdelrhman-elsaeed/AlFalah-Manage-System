namespace AlFalah.Domain.Events;

public sealed record ClassroomEntryPermitIssuedEvent(
    Guid EventId,
    int ClassroomEntryPermitId,
    int StudentId,
    int SchoolId,
    int AcademicTermId,
    int ClassroomId,
    int SchoolTimetableId,
    int SchoolTimetableEntryId,
    int TargetInstructorProfileId,
    string TargetInstructorUserId,
    DateTimeOffset IssuedAt,
    DateTimeOffset ValidUntil,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) =>
        this with { ClassroomEntryPermitId = aggregateId };
}
