namespace AlFalah.Domain.Enums.StudentAffairs;

public enum ConversationThreadType
{
    GuardianTeacher = 1,
    GuardianStudentAffairs = 2,
    GuardianSocialWorker = 3
}

public enum ConversationThreadStatus
{
    Open = 1,
    Closed = 2,
    Archived = 3
}

public enum OfficeHoursDisposition
{
    SentImmediately = 1,
    QueuedUntilOfficeHours = 2,
    BypassedForUrgency = 3
}

public enum MessageDeliveryState
{
    Pending = 1,
    Delivered = 2,
    Failed = 3
}

public enum TeacherOfficeHourSource
{
    DerivedFromPublishedTimetable = 1,
    TeacherSelected = 2,
    ManagerOverride = 3
}

public enum StudentTermMetricCode
{
    MorningArrivalDelay = 1,
    PenaltyAbsenceDay = 2,
    SessionDelay = 3,
    AcademicConcern = 4,
    CountableBehaviorIncident = 5,
    ClassroomEntryPermit = 6
}

public enum AutomationTriggerValidity
{
    Satisfied = 1,
    SourceNoLongerSatisfied = 2,
    Reviewed = 3
}

public enum NotificationPriority
{
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum NotificationDeliveryStatus
{
    Pending = 1,
    Processing = 2,
    Delivered = 3,
    Failed = 4,
    Suppressed = 5
}
