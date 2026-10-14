namespace AlFalah.Domain.Entities.Duty;

public sealed class DutyReportDraft
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AssignmentId { get; set; }
    public int AssignmentRevision { get; set; }
    public string AuthorUserId { get; set; } = "";
    public int Version { get; set; } = 1;
    public string ContentJson { get; set; } = "{}";
    public string? CorrectionReason { get; set; }
    public int? PreviousDocumentId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}

/// <summary>Immutable, locally committed evidence. D3 consumes the separate archive intent.</summary>
public sealed class DutySignedDocument
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AssignmentId { get; set; }
    public int AssignmentRevision { get; set; }
    public string AuthorUserId { get; set; } = "";
    public string Kind { get; set; } = "";
    public int? DraftId { get; set; }
    public int? PreviousDocumentId { get; set; }
    public string? CorrectionReason { get; set; }
    public string TemplateVersion { get; set; } = "school-duty/source-2026-v1";
    public string SnapshotJson { get; set; } = "{}";
    public byte[] SignatureBytes { get; set; } = [];
    public string SignatureHash { get; set; } = "";
    public string SnapshotHash { get; set; } = "";
    public DateTimeOffset SignedAt { get; set; }
}

public sealed class DutyArchiveIntent
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int DocumentId { get; set; }
    public string State { get; set; } = "PendingD3";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? OperationIdentity { get; set; }
    public int? ArtifactId { get; set; }
    public string? ProviderItemId { get; set; }
    public string? DriveId { get; set; }
    public string? SchoolRootItemId { get; set; }
    public int? FolderId { get; set; }
    public string? FolderItemId { get; set; }
    public int? StoredFileVersionId { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public int Attempts { get; set; }
    public string? ErrorCode { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class StudentDismissalLateness
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int EnrollmentId { get; set; }
    public int ClassroomId { get; set; }
    public int AcademicTermId { get; set; }
    public DateOnly DutyDate { get; set; }
    public int AssignmentId { get; set; }
    public int AssignmentRevision { get; set; }
    public string AuthorUserId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Classroom { get; set; } = "";
    public TimeOnly ObservedAt { get; set; }
    public TimeOnly DismissalAt { get; set; }
    public int? BellScheduleRevisionId { get; set; }
    public string TimeZoneId { get; set; } = "";
    public TimeOnly? DepartureAt { get; set; }
    public string Action { get; set; } = "";
    public string Notes { get; set; } = "";
    public int Revision { get; set; } = 1;
    public bool IsRetracted { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public sealed class DismissalLatenessRevision
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int FactId { get; set; }
    public int Revision { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public string Reason { get; set; } = "";
    public string ActorUserId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Durable recipient resolution intent, committed atomically with the fact revision.</summary>
public sealed class DismissalNoticeBatch
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int FactId { get; set; }
    public int Revision { get; set; }
    public string Content { get; set; } = "";
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DismissalNoticeRecipient
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int BatchId { get; set; }
    public string RecipientUserId { get; set; } = "";
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public int? NotificationId { get; set; }
    public int? MessageId { get; set; }
    public int? ThreadId { get; set; }
    public string DashboardState { get; set; } = "Pending";
    public string MessageState { get; set; } = "Pending";
    public string? ErrorCode { get; set; }
}
