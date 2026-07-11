namespace AlFalah.Domain.Entities;

/// <summary>
/// One row per RubricStandard inside a visit's rubric snapshot.
/// All 25 rows are pre-generated at visit creation (scores are null).
/// Phase 4 keeps these mutable ONLY while <c>Visit.Status == Draft</c>;
/// after submit the visit is read-only (Phase 5 introduces reopen).
/// </summary>
public class VisitScore
{
    public int Id { get; set; }

    public int VisitId { get; set; }

    /// <summary>The standard row this score belongs to (its code lives on the RubricStandard row, NOT on this entity, so the snapshot is implicit).</summary>
    public int RubricStandardId { get; set; }

    /// <summary>0..4 from docs/09. Null = not yet scored (only allowed in Draft).</summary>
    public int? Score { get; set; }

    /// <summary>Free-form evidence note (Arabic, optional).</summary>
    public string? EvidenceNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Soft delete — visits soft-delete cascades to scores in service logic,
    // but the columns are here for direct cleanup if ever needed.
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public Visit Visit { get; set; } = null!;
    public RubricStandard RubricStandard { get; set; } = null!;
}