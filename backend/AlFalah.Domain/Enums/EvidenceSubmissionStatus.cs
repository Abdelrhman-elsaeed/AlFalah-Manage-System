namespace AlFalah.Domain.Enums;

public enum EvidenceUploadStatus
{
    Pending = 1,
    Uploading = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

public enum EvidenceReviewStatus
{
    NotRequired = 1,
    PendingReview = 2,
    Approved = 3,
    Rejected = 4
}
