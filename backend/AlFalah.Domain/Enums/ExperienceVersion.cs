namespace AlFalah.Domain.Enums;

/// <summary>
/// Identifies the UI/scoring experience version under which a visit was created.
/// Allows coexistence of legacy and V2 visits in the same database.
/// </summary>
public enum ExperienceVersion
{
    /// <summary>Legacy V1 visits (existing system, 0–4 scale, 6-level thresholds).</summary>
    Legacy = 1,

    /// <summary>V2 visits created with the approved prototype rubric (1–4 scale, percentage-based thresholds).</summary>
    PrototypeV2 = 2
}
