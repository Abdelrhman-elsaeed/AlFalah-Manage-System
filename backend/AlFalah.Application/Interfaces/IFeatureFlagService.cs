namespace AlFalah.Application.Interfaces;

/// <summary>
/// Service contract for querying runtime feature flags across application workflows.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Returns true if Classroom Visits V2 is active; otherwise false (legacy V1 behavior active).
    /// </summary>
    bool IsVisitsV2Enabled(int? schoolId = null);
}
