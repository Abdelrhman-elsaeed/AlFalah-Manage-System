namespace AlFalah.Domain.Enums;

public enum VisitStatus
{
    Draft = 1,
    Submitted = 2,
    PendingApproval = 3,
    Approved = 4,
    RejectedForChanges = 5,
    Reopened = 6,
    UnderReviewAfterComplaint = 7,
    Cancelled = 8
}
