namespace AlFalah.Application.DTOs.Rubric;

// ─── Read DTOs ────────────────────────────────────────────────────────────────

/// <summary>A single standard row returned in the rubric tree.</summary>
public class RubricStandardDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string TextAr { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>A domain with its nested standards.</summary>
public class RubricDomainDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<RubricStandardDto> Standards { get; set; } = new();
}

/// <summary>Full rubric version with nested domains + standards.</summary>
public class RubricVersionDto
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? Notes { get; set; }
    public List<RubricDomainDto> Domains { get; set; } = new();
}

/// <summary>Lightweight version summary for the versions list.</summary>
public class RubricVersionListDto
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? Notes { get; set; }
    public int DomainCount { get; set; }
    public int StandardCount { get; set; }
}

// ─── Write DTOs ───────────────────────────────────────────────────────────────

/// <summary>A standard entry in the create-version request.</summary>
public class RubricStandardWriteDto
{
    public string Code { get; set; } = string.Empty;
    public string TextAr { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>A domain entry in the create-version request.</summary>
public class RubricDomainWriteDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<RubricStandardWriteDto> Standards { get; set; } = new();
}

/// <summary>
/// Request body for POST /api/v1/rubric/versions.
/// The payload represents the FULL desired rubric tree for the new version.
/// The service clones it as new rows (copy-on-write) and deactivates the previous version.
/// </summary>
public class CreateRubricVersionDto
{
    public string? Notes { get; set; }
    public List<RubricDomainWriteDto> Domains { get; set; } = new();
}

// ─── Score scale DTOs (read-only constants, no DB entity) ─────────────────────

/// <summary>One entry in the 0–4 score scale. Labels are verbatim from docs/09.</summary>
public class ScoreScaleEntryDto
{
    public int Score { get; set; }

    /// <summary>Arabic label, verbatim from docs/09-RUBRIC-AND-EVALUATION.md.</summary>
    public string LabelAr { get; set; } = string.Empty;
}

/// <summary>One performance level threshold from docs/09.</summary>
public class PerformanceLevelDto
{
    /// <summary>Arabic label, e.g. "متميز".</summary>
    public string LabelAr { get; set; } = string.Empty;

    /// <summary>Minimum average score for this level (inclusive unless IsLessThan=true).</summary>
    public decimal MinScore { get; set; }

    /// <summary>When true the condition is "&lt; MinScore" (for the lowest level غير مشاهد).</summary>
    public bool IsLessThan { get; set; } = false;
}

/// <summary>
/// Full score-scale reference returned by GET /api/v1/rubric/score-scale.
/// Values are compile-time constants matching docs/09-RUBRIC-AND-EVALUATION.md exactly.
/// Phase 4 analysis logic MUST use these thresholds.
/// </summary>
public class ScoreScaleDto
{
    public List<ScoreScaleEntryDto> Scores { get; set; } = new();
    public List<PerformanceLevelDto> PerformanceLevels { get; set; } = new();
}
