using AlFalah.Domain.Enums.StudentAffairs;

namespace AlFalah.Domain.Events;

public sealed record BehaviorIncidentLoggedEvent(
    Guid EventId,
    int BehaviorIncidentId,
    int StudentId,
    int SchoolId,
    int AcademicTermId,
    int? ClassroomId,
    int SchoolTimetableId,
    int SchoolTimetableEntryId,
    byte Period,
    string CategoryCode,
    BehaviorSeverity Severity,
    DateTimeOffset IncidentOccurredAt,
    int? ReportedByInstructorProfileId,
    string? ReportedByStaffUserId,
    GuardianDispatchDecision GuardianDispatchDecision,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) =>
        this with { BehaviorIncidentId = aggregateId };
}

public sealed record AcademicConcernLoggedEvent(
    Guid EventId,
    int AcademicConcernId,
    int StudentId,
    int SchoolId,
    int AcademicTermId,
    int? ClassroomId,
    int? SchoolTimetableEntryId,
    string Category,
    DateTimeOffset ConcernOccurredAt,
    int ReportedByInstructorProfileId,
    GuardianDispatchDecision GuardianDispatchDecision,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) =>
        this with { AcademicConcernId = aggregateId };
}

public sealed record SessionDelayLoggedEvent(
    Guid EventId,
    int SessionDelayId,
    int StudentId,
    int SchoolId,
    int AcademicTermId,
    int ClassroomId,
    int SchoolTimetableId,
    int SchoolTimetableEntryId,
    byte Period,
    DateTimeOffset DelayOccurredAt,
    int? DelayMinutes,
    int ReportedByInstructorProfileId,
    GuardianNotificationStatus GuardianNotificationStatus,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) =>
        this with { SessionDelayId = aggregateId };
}

public sealed record GuardianSummonCreatedEvent(
    Guid EventId,
    int GuardianSummonId,
    int StudentId,
    int SchoolId,
    int AcademicTermId,
    int? StudentReferralId,
    int GuardianProfileId,
    GuardianSummonStatus Status,
    ReferralPriority Priority,
    int? SourceCountSnapshot,
    int? ThresholdSnapshot,
    string CreatedByUserId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) =>
        this with { GuardianSummonId = aggregateId };
}

/// <summary>
/// Primitive snapshot for a summons lifecycle action. Scheduling intentionally records
/// Pending-to-Pending because appointment history is separate from the four business states.
/// </summary>
public sealed record GP9jdFE6bJJJBXm548MTsCQvpLk7RqkKB7(
    Guid EventId,
    int GuardianSummonId,
    int StudentId,
    int SchoolId,
    int AcademicTermId,
    int GuardianProfileId,
    GuardianSummonStatus FromStatus,
    GuardianSummonStatus ToStatus,
    string Action,
    string ActorUserId,
    DateTimeOffset ActionAt,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? AttendedAt,
    DateTimeOffset? ObservationStartedAt,
    DateTimeOffset? ImprovedAt,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) =>
        this with { GuardianSummonId = aggregateId };
}
