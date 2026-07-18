namespace AlFalah.Application.Analysis;

/// <summary>
/// A single standard row fed into the analysis engine. Pure-data POCO so the
/// engine can be unit-tested without an EF Core DbContext.
/// </summary>
public sealed class StandardScoreInput
{
    public int RubricDomainId { get; init; }
    public int RubricStandardId { get; init; }
    public string DomainCode { get; init; } = string.Empty;
    public string DomainNameAr { get; init; } = string.Empty;
    public string StandardCode { get; init; } = string.Empty;
    public string StandardTextAr { get; init; } = string.Empty;
    public int Score { get; init; }
}

/// <summary>
/// Result of running the analysis engine. Pure-data POCO so unit tests can
/// assert on it without EF Core.
/// </summary>
public sealed class VisitAnalysisResult
{
    public decimal OverallScore { get; init; }
    public decimal TotalScore { get; init; }
    public decimal MaximumScore { get; init; }
    public string PerformanceLevelAr { get; init; } = string.Empty;
    public List<DomainAverageRow> DomainAverages { get; init; } = new();
    public List<DomainAverageRow> Strengths { get; init; } = new();
    public List<DomainAverageRow> ImprovementAreas { get; init; } = new();
    public List<PriorityStandardRow> PriorityStandards { get; init; } = new();
}

public sealed class DomainAverageRow
{
    public int RubricDomainId { get; init; }
    public string DomainCode { get; init; } = string.Empty;
    public string DomainNameAr { get; init; } = string.Empty;
    public decimal AverageScore { get; init; }
}

public sealed class PriorityStandardRow
{
    public int RubricStandardId { get; init; }
    public string DomainCode { get; init; } = string.Empty;
    public string StandardCode { get; init; } = string.Empty;
    public string StandardTextAr { get; init; } = string.Empty;
    public int Score { get; init; }
}
