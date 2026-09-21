using System;
using System.Collections.Generic;

namespace AlFalah.Application.Analysis;

/// <summary>
/// Input for a single standard rating in Classroom Visits V2.
/// Pure-data POCO completely decoupled from EF Core and database contexts.
/// </summary>
public sealed class StandardScoreV2Input
{
    public int DomainId { get; init; }
    public string DomainName { get; init; } = string.Empty;
    public int StandardId { get; init; }
    public string StandardText { get; init; } = string.Empty;
    public int Score { get; init; } // 1..4
}

/// <summary>
/// Domain-level score and percentage calculation result in V2.
/// </summary>
public sealed class DomainScoreV2Result
{
    public int DomainId { get; init; }
    public string DomainName { get; init; } = string.Empty;
    public int StandardsCount { get; init; }
    public int Sum { get; init; }
    public int MaxScore { get; init; }
    public int Percent { get; init; }
    public string Level { get; init; } = string.Empty;
    public bool IsStrength { get; init; }
    public bool IsWeakness { get; init; }
}

/// <summary>
/// Identified weakness/improvement area in V2 (Domain with Percent &lt; 65%).
/// </summary>
public sealed class WeaknessV2Result
{
    public int DomainId { get; init; }
    public string DomainName { get; init; } = string.Empty;
    public int Percent { get; init; }
    public string Level { get; init; } = string.Empty;
}

/// <summary>
/// Full calculation result for a Classroom Visit V2.
/// </summary>
public sealed class VisitV2AnalysisResult
{
    public int TotalScore { get; init; }
    public int MaxTotalScore { get; init; }
    public int OverallPercent { get; init; }
    public string OverallLevel { get; init; } = string.Empty;
    public IReadOnlyList<DomainScoreV2Result> DomainScores { get; init; } = Array.Empty<DomainScoreV2Result>();
    public IReadOnlyList<string> Strengths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<WeaknessV2Result> Weaknesses { get; init; } = Array.Empty<WeaknessV2Result>();
}
