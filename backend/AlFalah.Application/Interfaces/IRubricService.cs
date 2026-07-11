using AlFalah.Application.DTOs.Rubric;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// Rubric management service (Phase 3).
/// Rubric is GLOBAL — not school-scoped (D-21).
/// Only one RubricVersion may be active at a time (enforced at DB level by filtered unique index).
/// Editing creates a new version (copy-on-write); historical rows are never mutated (MOD-4).
/// </summary>
public interface IRubricService
{
    /// <summary>
    /// Returns the full tree (version + domains + standards, ordered by SortOrder)
    /// for the currently active RubricVersion.
    /// Throws <see cref="KeyNotFoundException"/> if no active version exists.
    /// </summary>
    Task<RubricVersionDto> GetActiveVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a lightweight list of all versions (no standards or domains inline),
    /// ordered by VersionNumber descending.
    /// </summary>
    Task<List<RubricVersionListDto>> GetVersionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full tree for the specified version.
    /// Throws <see cref="KeyNotFoundException"/> if the version does not exist.
    /// </summary>
    Task<RubricVersionDto> GetVersionByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new RubricVersion by cloning the provided tree (copy-on-write).
    /// New rows are created for every domain and standard; the previous active version
    /// is deactivated. Historical visit rows continue referencing the old version.
    /// Returns the newly created version.
    /// </summary>
    Task<RubricVersionDto> CreateNewVersionAsync(CreateRubricVersionDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a specific version and deactivates all others.
    /// Throws <see cref="KeyNotFoundException"/> if the version does not exist.
    /// </summary>
    Task<RubricVersionDto> ActivateVersionAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the global score scale (0–4 labels + performance level thresholds)
    /// from docs/09-RUBRIC-AND-EVALUATION.md. Values are compile-time constants; no DB hit.
    /// </summary>
    ScoreScaleDto GetScoreScale();
}
