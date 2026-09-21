namespace AlFalah.Application.Common;

/// <summary>
/// Strong-typed options for platform-wide feature flags.
/// Configured via the "FeatureFlags" section in appsettings.json.
/// </summary>
public sealed class FeatureFlagsOptions
{
    public const string SectionName = "FeatureFlags";

    /// <summary>
    /// When true, enables Classroom Visits V2 (prototype parity engine, V2 endpoints, and observation workspace).
    /// Default is false for safe, gradual rollout.
    /// </summary>
    public bool VisitsV2 { get; set; } = false;

    public int[] VisitsV2SchoolIds { get; set; } = Array.Empty<int>();
}
