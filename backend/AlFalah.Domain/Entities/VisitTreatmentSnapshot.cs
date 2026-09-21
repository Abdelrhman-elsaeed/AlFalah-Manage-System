using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// A treatment plan recommendation item owned by a V2 visit.
/// Replaces the standalone ImprovementPlan module for V2 visits (Decision #7).
///
/// Generated automatically for domains with percentage &lt; 65% using the
/// approved template texts, then editable by the evaluator (add/remove/modify).
/// Persisted in the database so changes survive reload and appear in the
/// official server-side PDF report.
///
/// Phase 2 (Visits V2): Initial entity creation.
/// </summary>
public class VisitTreatmentSnapshot
{
    public int Id { get; set; }

    /// <summary>The visit this treatment recommendation belongs to.</summary>
    public int VisitId { get; set; }

    /// <summary>
    /// The rubric domain this recommendation targets.
    /// Null for custom/manual goals not tied to a specific domain.
    /// </summary>
    public int? RubricDomainId { get; set; }

    /// <summary>Snapshot of the domain's Arabic name for report stability.</summary>
    public string DomainNameArSnapshot { get; set; } = string.Empty;

    /// <summary>The improvement goal text (required).</summary>
    public string Goal { get; set; } = string.Empty;

    /// <summary>Recommended actions/steps (required, multiline).</summary>
    public string Actions { get; set; } = string.Empty;

    /// <summary>Success indicators/criteria (required).</summary>
    public string SuccessIndicators { get; set; } = string.Empty;

    /// <summary>Whether this item was auto-generated or manually added.</summary>
    public TreatmentSource Source { get; set; } = TreatmentSource.Generated;

    /// <summary>Display order in the report.</summary>
    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public Visit Visit { get; set; } = null!;
    public RubricDomain? RubricDomain { get; set; }
}
