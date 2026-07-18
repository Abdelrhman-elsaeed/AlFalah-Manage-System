namespace AlFalah.Domain.Entities;

/// <summary>
/// Phase 4 analysis snapshot — written ONCE on submit, IMMUTABLE afterwards
/// (until Phase 5 adds reopen). Carries the overall score, the performance
/// level, the strengths / improvement / priority lists, and the
/// per-domain averages (one row per domain — see <see cref="VisitDomainAverage"/>).
///
/// All thresholds and labels follow docs/09 verbatim and are applied by
/// the analysis engine in the service layer.
/// </summary>
public class VisitAnalysis
{
    public int Id { get; set; }

    public int VisitId { get; set; }

    /// <summary>Equal-weight average of the domain averages, retained for performance-level thresholds.</summary>
    public decimal OverallScore { get; set; }

    /// <summary>Total points earned across the snapshotted standards.</summary>
    public decimal TotalScore { get; set; }

    /// <summary>Maximum possible points for the snapshotted standards (25 × 4 = 100 for the initial rubric).</summary>
    public decimal MaximumScore { get; set; }

    /// <summary>Arabic performance-level label verbatim from docs/09, e.g. "متميز", "جيد جداً".</summary>
    public string PerformanceLevelAr { get; set; } = string.Empty;

    /// <summary>Serialized JSON array of domain snapshots that are strengths (avg &gt;= 3.0). Each entry: { domainCode, domainNameAr, averageScore }.</summary>
    public string StrengthsJson { get; set; } = "[]";

    /// <summary>Serialized JSON array of domain snapshots that need improvement (avg &lt; 2.5).</summary>
    public string ImprovementAreasJson { get; set; } = "[]";

    /// <summary>Serialized JSON array of standard snapshots that are priority (score &lt;= 1.5). Each entry: { domainCode, standardCode, textAr, score }.</summary>
    public string PriorityStandardsJson { get; set; } = "[]";

    /// <summary>When the snapshot was computed (i.e. when the visit was submitted).</summary>
    public DateTimeOffset ComputedAt { get; set; } = DateTimeOffset.UtcNow;

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public Visit Visit { get; set; } = null!;
    public ICollection<VisitDomainAverage> DomainAverages { get; set; } = new List<VisitDomainAverage>();
}
