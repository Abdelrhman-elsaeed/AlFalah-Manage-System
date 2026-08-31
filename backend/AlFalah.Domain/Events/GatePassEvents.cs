using AlFalah.Domain.Enums.StudentAffairs;

namespace AlFalah.Domain.Events;

public sealed record GatePassRequestedEvent(
    Guid EventId,
    int GatePassId,
    int StudentId,
    int SchoolId,
    int AcademicTermId,
    int GuardianProfileId,
    DateTimeOffset RequestedAt,
    DateTimeOffset RequestedExitAt,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) => this with { GatePassId = aggregateId };
}

public sealed record GatePassApprovedEvent(
    Guid EventId,
    int GatePassId,
    int StudentId,
    int SchoolId,
    int AcademicTermId,
    string ApprovedByUserId,
    DateTimeOffset ApprovedAt,
    DateTimeOffset WindowStartsAt,
    DateTimeOffset WindowEndsAt,
    int ClassroomId,
    int SchoolTimetableId,
    int SchoolTimetableEntryId,
    int InstructorProfileId,
    byte Period,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) => this with { GatePassId = aggregateId };
}

public sealed record GatePassSecurityAcknowledgedEvent(
    Guid EventId,
    int GatePassId,
    int StudentId,
    int SchoolId,
    string AcknowledgedByUserId,
    DateTimeOffset AcknowledgedAt,
    DateTimeOffset WindowEndsAt,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) => this with { GatePassId = aggregateId };
}

public sealed record StudentExitedSchoolEvent(
    Guid EventId,
    int GatePassId,
    int StudentId,
    int SchoolId,
    string RecordedByUserId,
    DateTimeOffset ExitedAt,
    PickupVerificationMethod VerificationMethod,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) => this with { GatePassId = aggregateId };
}
