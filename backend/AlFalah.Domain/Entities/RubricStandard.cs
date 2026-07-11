namespace AlFalah.Domain.Entities;

/// <summary>
/// A single evaluation standard (معيار) within a rubric domain.
/// Standards are ordered by SortOrder within their domain.
/// Immutable once superseded — copy-on-write creates new rows for any new version.
/// Phase 4 visits will reference these rows by Id; historical accuracy is preserved.
/// </summary>
public class RubricStandard
{
    public int Id { get; set; }
    public int RubricDomainId { get; set; }

    /// <summary>Short code, e.g. "D1-S1", "D2-S3".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Standard text in Arabic, verbatim from spec.</summary>
    public string TextAr { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public RubricDomain Domain { get; set; } = null!;
}
