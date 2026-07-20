namespace AlFalah.Domain.Enums;

/// <summary>The single, server-calculated state displayed by an evidence-matrix cell.</summary>
public enum EvidenceCellStatus
{
    NotUploaded = 1,
    Uploaded = 2,
    PendingReview = 3,
    Approved = 4,
    Rejected = 5,
    MissingFromDrive = 6
}

public enum EvidenceUploadOperationStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3
}
