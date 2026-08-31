namespace AlFalah.Domain.Enums.StudentAffairs;

public enum StudentAttendanceStatus
{
    Present = 1,
    Absent = 2,
    AbsentExcused = 3
}

public enum StudentAttendanceSource
{
    SecretaryRoster = 1,
    Correction = 2
}

public enum AbsenceExcuseType
{
    Medical = 1,
    Family = 2,
    Official = 3,
    Other = 4
}

public enum AbsenceExcuseStatus
{
    Pending = 1,
    Accepted = 2,
    Rejected = 3
}

public enum GuardianNotificationStatus
{
    Pending = 1,
    Queued = 2,
    Delivered = 3,
    Failed = 4,
    Suppressed = 5
}

public enum GuardianDispatchDecision
{
    PendingOfficerDecision = 1,
    Approved = 2,
    Suppressed = 3
}

public enum BehaviorSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum NoorAbsenceCorrectionBatchStatus
{
    Created = 1,
    Exported = 2,
    Failed = 3
}
