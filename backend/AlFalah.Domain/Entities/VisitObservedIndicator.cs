namespace AlFalah.Domain.Entities;

/// <summary>
/// Records a single observed/checked indicator for a visit score.
/// Only indicators that were selected (checked) have rows; unselected
/// indicators are inferred from the rubric snapshot.
///
/// The <see cref="IndicatorTextArSnapshot"/> is captured at observation time
/// so the report remains historically accurate even if the rubric is later
/// edited (copy-on-write versioning).
///
/// Phase 2 (Visits V2): Bridge entity between VisitScore and RubricIndicator.
/// </summary>
public class VisitObservedIndicator
{
    public int Id { get; set; }

    /// <summary>The score row this observation belongs to (one standard's score within a visit).</summary>
    public int VisitScoreId { get; set; }

    /// <summary>The rubric indicator that was observed/checked.</summary>
    public int RubricIndicatorId { get; set; }

    /// <summary>Immutable snapshot of the indicator's Arabic text at observation time.</summary>
    public string IndicatorTextArSnapshot { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public VisitScore VisitScore { get; set; } = null!;
    public RubricIndicator RubricIndicator { get; set; } = null!;
}
