namespace AlFalah.Domain.Enums;

/// <summary>
/// Indicates the source/origin of a treatment plan recommendation item.
/// Used by <see cref="AlFalah.Domain.Entities.VisitTreatmentSnapshot"/>.
/// </summary>
public enum TreatmentSource
{
    /// <summary>Auto-generated from weak domains (percentage &lt; 65%).</summary>
    Generated = 1,

    /// <summary>Manually added by the evaluator as a custom goal.</summary>
    Manual = 2
}
