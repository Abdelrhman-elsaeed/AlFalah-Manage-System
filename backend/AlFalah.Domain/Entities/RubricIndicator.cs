namespace AlFalah.Domain.Entities;

/// <summary>
/// A performance indicator (مؤشر أداء) within a rubric standard.
/// The V2 prototype defines 66 indicators across 25 standards.
///
/// Indicators are versioned through their parent standard/domain/version chain.
/// Immutable once referenced by visits — copy-on-write creates new rows
/// in any future rubric version. The active V2 rubric is a new version;
/// V1 standards have no indicators and are never modified.
///
/// Phase 2 (Visits V2): Initial creation with exact prototype Arabic texts.
/// </summary>
public class RubricIndicator
{
    public int Id { get; set; }

    /// <summary>FK to the standard this indicator belongs to.</summary>
    public int RubricStandardId { get; set; }

    /// <summary>Stable indicator code within its standard version, e.g. "D1-S1-I1".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Exact Arabic text from the approved prototype, Unicode-safe.</summary>
    public string TextAr { get; set; } = string.Empty;

    /// <summary>Preserves prototype display order within the standard.</summary>
    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public RubricStandard Standard { get; set; } = null!;
}
