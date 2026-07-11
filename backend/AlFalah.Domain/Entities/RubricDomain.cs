namespace AlFalah.Domain.Entities;

/// <summary>
/// A domain (محور) within a rubric version. Each domain groups related standards.
/// Domains are ordered by SortOrder within their version.
/// Immutable once a version is superseded — copy-on-write creates new rows in the new version.
/// </summary>
public class RubricDomain
{
    public int Id { get; set; }
    public int RubricVersionId { get; set; }

    /// <summary>Short code, e.g. "D1", "D2".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Domain name in Arabic, verbatim from spec.</summary>
    public string NameAr { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public RubricVersion Version { get; set; } = null!;
    public ICollection<RubricStandard> Standards { get; set; } = new List<RubricStandard>();
}
