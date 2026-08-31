namespace AlFalah.Domain.Enums.StudentAffairs;

public enum ClassroomEntryPermitStatus
{
    Issued = 1,
    AcknowledgedByTeacher = 2,
    Expired = 3,
    Revoked = 4
}

public enum GatePassStatus
{
    Requested = 1,
    Approved = 2,
    Rejected = 3,
    SecurityAcknowledged = 4,
    Exited = 5,
    Cancelled = 6,
    Expired = 7
}

public enum PickupVerificationMethod
{
    Visual = 1,
    Manual = 2,
    GuardianScreenshot = 3
}

public enum ReferralSourceType
{
    MorningDelay = 1,
    SessionDelay = 2,
    AcademicConcern = 3,
    Behavior = 4,
    Absence = 5,
    RepeatedEntryPermit = 6,
    Manual = 7
}

public enum ReferralPriority
{
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum StudentReferralStatus
{
    Open = 1,
    Assigned = 2,
    InProgress = 3,
    Resolved = 4,
    Closed = 5
}

public enum GuardianSummonStatus
{
    Pending = 1,
    Attended = 2,
    UnderObservation = 3,
    Improved = 4
}

public enum OfficerReviewDecision
{
    Retain = 1,
    Cancel = 2,
    Close = 3
}

public enum StudentCaseActionType
{
    CounselingSession = 1,
    GuardianSummon = 2,
    GradeDeductionRecommendation = 3,
    SuspensionRecommendation = 4,
    ChildRightsCommitteeReferral = 5,
    Other = 6
}
