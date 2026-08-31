using AlFalah.Domain.Enums.StudentAffairs;

namespace AlFalah.Domain.Events;

public sealed record StudentAbsentRecordedEvent(
    Guid EventId,
    int DailyStudentAttendanceId,
    int StudentId,
    int SchoolId,
    int AcademicTermId,
    int ClassroomId,
    DateOnly AttendanceDate,
    string RecordedByUserId,
    DateTimeOffset RecordedAt,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) =>
        this with { DailyStudentAttendanceId = aggregateId };
}

public sealed record MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3(
    Guid EventId,
    int MorningArrivalDelayId,
    int StudentId,
    int SchoolId,
    int AcademicTermId,
    DateTimeOffset ArrivalAt,
    DateOnly SchoolLocalDate,
    TimeOnly CutoffTimeSnapshot,
    int DelayMinutes,
    string NotificationPolicySnapshot,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) =>
        this with { MorningArrivalDelayId = aggregateId };
}

public sealed record AcbXX3KgvqD7B8Y4WjCu6yNx1Prfu5cNHz(
    Guid EventId,
    int AbsenceExcuseId,
    int DailyStudentAttendanceId,
    int StudentId,
    int SchoolId,
    int AcademicTermId,
    int GuardianProfileId,
    AbsenceExcuseType ExcuseType,
    DateTimeOffset SubmittedAt,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) =>
        this with { AbsenceExcuseId = aggregateId };
}

public sealed record AbsenceExcuseAcceptedEvent(
    Guid EventId,
    int AbsenceExcuseId,
    int DailyStudentAttendanceId,
    int StudentId,
    int SchoolId,
    int AcademicTermId,
    string AcceptedByUserId,
    DateTimeOffset AcceptedAt,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public IDomainEvent WithAggregateId(int aggregateId) =>
        this with { AbsenceExcuseId = aggregateId };
}
