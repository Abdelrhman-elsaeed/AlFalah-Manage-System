using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities.Storage;

public sealed class EvidenceRequirement
{
    public int Id { get; set; }
    public int? SchoolId { get; set; }
    public int? AcademicYearId { get; set; }
    public int TemplateVersion { get; set; } = 1;
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? DomainCode { get; set; }
    public string? StandardCode { get; set; }
    public int? OriginalTaskId { get; set; }
    public EvidenceImportance Importance { get; set; } = EvidenceImportance.Normal;
    public string? ResponsibleUserId { get; set; }
    public string? ResponsibleRole { get; set; }
    public EvidenceFulfillmentPolicy FulfillmentPolicy { get; set; } = EvidenceFulfillmentPolicy.AnyApprovedLink;
    public int MinimumApprovedLinks { get; set; } = 1;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    // Existing S1–S3 task/catalog rows remain optional until explicitly configured.
    public bool IsMandatory { get; set; }
    public string? SourceKey { get; set; }
    public string? SourceSHA256 { get; set; }
    public string? ReferencePath { get; set; }
    public string? CompletionAction { get; set; }
    public string? ImportanceReason { get; set; }
    public string? CandidateTaskCodesJson { get; set; }
    public DateOnly? DueDate { get; set; }
    public string FollowUpStatus { get; set; } = "NotStarted";
    public string? FollowUpNote { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
