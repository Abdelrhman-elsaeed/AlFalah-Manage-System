namespace AlFalah.Domain.Entities;

/// <summary>
/// One row per rubric domain in the visit's snapshot, persisted alongside
/// <see cref="VisitAnalysis"/>. Carries the average score for that domain
/// plus the snapshot's domain code + Arabic name so the row stays readable
/// even after the rubric is later edited (the snapshot is immutable in Phase 4).
///
/// The domain average is computed as the simple arithmetic mean of the
/// domain's standards' scores — respect the UNEVEN distribution
/// (D1=6 / D2=4 / D3=6 / D4=3 / D5=6), i.e. average over the domain's own
/// standard count, never a fixed /5 divisor.
/// </summary>
public class VisitDomainAverage
{
    public int Id { get; set; }

    public int VisitAnalysisId { get; set; }

    /// <summary>The rubric domain this average belongs to (the snapshot's id).</summary>
    public int RubricDomainId { get; set; }

    /// <summary>Snapshot of the domain code, e.g. "D1".</summary>
    public string DomainCode { get; set; } = string.Empty;

    /// <summary>Snapshot of the domain's Arabic name, verbatim from docs/03/09.</summary>
    public string DomainNameAr { get; set; } = string.Empty;

    /// <summary>Domain average, decimal to preserve precision.</summary>
    public decimal AverageScore { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public VisitAnalysis VisitAnalysis { get; set; } = null!;
    public RubricDomain RubricDomain { get; set; } = null!;
}