using System.Collections.Generic;
using AlFalah.Application.Analysis;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Analysis;

/// <summary>
/// Unit tests for the visit analysis engine — verifies verbatim compliance
/// with docs/09 (Domain averages, Overall, Performance level thresholds,
/// Strengths / Improvement / Priority thresholds) including the D-55 fix
/// (priority threshold changed from &lt;=1 to &lt;=1.5).
/// </summary>
public class VisitAnalysisEngineTests
{
    // Helper: every StandardScoreInput carries a RubricDomainId so the engine
    // groups standards correctly. We map D1..D5 → domainIds 1..5.
    private static List<StandardScoreInput> Build(
        int d1, int d2, int d3, int d4, int d5Base, params int[] d5Extras)
    {
        var list = new List<StandardScoreInput>(25);
        int stdIdx = 0;
        for (int i = 0; i < 6; i++) list.Add(Std("D1", 1, stdIdx++, d1));
        for (int i = 0; i < 4; i++) list.Add(Std("D2", 2, stdIdx++, d2));
        for (int i = 0; i < 6; i++) list.Add(Std("D3", 3, stdIdx++, d3));
        for (int i = 0; i < 3; i++) list.Add(Std("D4", 4, stdIdx++, d4));

        var d5Values = new List<int> { d5Base };
        d5Values.AddRange(d5Extras);
        if (d5Values.Count != 6)
            throw new ArgumentException($"D5 must have exactly 6 standards; got {d5Values.Count}.");
        foreach (var v in d5Values)
            list.Add(Std("D5", 5, stdIdx++, v));
        return list;
    }

    private static StandardScoreInput Std(string d, int domainId, int stdIdx, int score) => new()
    {
        RubricDomainId = domainId,
        RubricStandardId = stdIdx,
        DomainCode = d,
        DomainNameAr = d,
        StandardCode = $"{d}-S{stdIdx}",
        StandardTextAr = $"معيار {stdIdx}",
        Score = score
    };

    // ─── D-26 worked example: D1=6×4, D2=4×4, D3=6×4, D4=3×4, D5=[3,2,1,0,4,4] → overall 3.6 ───

    [Fact]
    public void D26_WorkedExample_Overall_3_6_Performance_Mumtaz()
    {
        // 6*4 + 4*4 + 6*4 + 3*4 + 14 = 90 / 25 = 3.6
        var input = Build(
            d1: 4, d2: 4, d3: 4, d4: 4,
            d5Base: 3, d5Extras: new[] { 2, 1, 0, 4, 4 });

        var result = VisitAnalysisEngine.Compute(input);

        result.OverallScore.Should().Be(3.6m);
        result.PerformanceLevelAr.Should().Be("متميز");
    }

    [Fact]
    public void D26_WorkedExample_DomainAverages_MatchVerbatim()
    {
        var input = Build(
            d1: 4, d2: 4, d3: 4, d4: 4,
            d5Base: 3, d5Extras: new[] { 2, 1, 0, 4, 4 });

        var result = VisitAnalysisEngine.Compute(input);

        result.DomainAverages.Should().HaveCount(5);
        result.DomainAverages.Single(d => d.DomainCode == "D1").AverageScore.Should().Be(4.0m);
        result.DomainAverages.Single(d => d.DomainCode == "D2").AverageScore.Should().Be(4.0m);
        result.DomainAverages.Single(d => d.DomainCode == "D3").AverageScore.Should().Be(4.0m);
        result.DomainAverages.Single(d => d.DomainCode == "D4").AverageScore.Should().Be(4.0m);
        // (3+2+1+0+4+4)/6 = 14/6 = 2.333
        result.DomainAverages.Single(d => d.DomainCode == "D5").AverageScore.Should().Be(2.333m);
    }

    [Fact]
    public void D26_WorkedExample_Strengths_And_Improvements_And_Priorities()
    {
        var input = Build(
            d1: 4, d2: 4, d3: 4, d4: 4,
            d5Base: 3, d5Extras: new[] { 2, 1, 0, 4, 4 });

        var result = VisitAnalysisEngine.Compute(input);

        // Strengths = domains avg >= 3.0 → D1, D2, D3, D4
        result.Strengths.Select(s => s.DomainCode).Should()
            .BeEquivalentTo(new[] { "D1", "D2", "D3", "D4" });

        // Improvements = domains avg < 2.5 → only D5 (2.333)
        result.ImprovementAreas.Select(i => i.DomainCode).Should()
            .BeEquivalentTo(new[] { "D5" });

        // Priorities = standards with score <= 1.5 (D-55 fix; verbatim from docs/09)
        // → D5-S2 (score=1), D5-S3 (score=0) — both <= 1.5
        result.PriorityStandards.Should().HaveCount(2);
        result.PriorityStandards.Should().OnlyContain(p => p.Score <= 1.5m);
        result.PriorityStandards.Select(p => p.Score).Should()
            .BeEquivalentTo(new[] { 1, 0 });
    }

    // ─── Performance level boundaries (3.5 / 3.0 / 2.5 / 2.0 / 1.0) ───

    [Theory]
    [InlineData(4.0, "متميز")]      // >= 3.5
    [InlineData(3.5, "متميز")]
    [InlineData(3.499, "جيد جداً")]  // < 3.5 but >= 3.0
    [InlineData(3.0, "جيد جداً")]
    [InlineData(2.999, "جيد")]      // < 3.0 but >= 2.5
    [InlineData(2.5, "جيد")]
    [InlineData(2.499, "متحقق جزئياً")] // < 2.5 but >= 2.0
    [InlineData(2.0, "متحقق جزئياً")]
    [InlineData(1.999, "يحتاج تحسين")] // < 2.0 but >= 1.0
    [InlineData(1.0, "يحتاج تحسين")]
    [InlineData(0.999, "غير مشاهد")] // < 1.0
    [InlineData(0.0, "غير مشاهد")]
    public void PerformanceLevel_Verbatim_Boundaries(double overallDouble, string expectedLevel)
    {
        var level = VisitAnalysisEngine.MapPerformanceLevel((decimal)overallDouble);
        level.Should().Be(expectedLevel);
    }

    // ─── Uneven distribution: D4 averages over its 3 standards ───

    [Fact]
    public void UnevenDistribution_D4_Averages_Over_Three_Standards()
    {
        // D1=6×0, D2=4×0, D3=6×0, D4=3×3, D5=6×0 → D4-only strength, overall = 9/25 = 0.36
        var input = Build(d1: 0, d2: 0, d3: 0, d4: 3, d5Base: 0,
            d5Extras: new[] { 0, 0, 0, 0, 0 });

        var result = VisitAnalysisEngine.Compute(input);

        result.DomainAverages.Single(d => d.DomainCode == "D4").AverageScore.Should().Be(3.0m);
        result.Strengths.Should().ContainSingle(s => s.DomainCode == "D4");
        result.ImprovementAreas.Select(i => i.DomainCode).Should()
            .BeEquivalentTo(new[] { "D1", "D2", "D3", "D5" });
        result.PerformanceLevelAr.Should().Be("غير مشاهد"); // overall = 0.36
    }

    // ─── Strengths / Improvement thresholds ───

    [Fact]
    public void StrengthsThreshold_Domain_With_Avg_2_833_Is_NOT_Strength_And_NOT_Improvement()
    {
        // D1 standards at [3,3,3,3,3,2] → 17/6 = 2.833 — between 2.5 and 3.0.
        // Therefore it is NOT a strength (>= 3.0) and NOT an improvement (< 2.5).
        // All other domains score 4 → they ARE strengths, but D1 specifically
        // is in the neutral band.
        var list = new List<StandardScoreInput>();
        for (int i = 0; i < 6; i++) list.Add(Std("D1", 1, i, 3));
        list[5] = Std("D1", 1, 5, 2);
        for (int i = 0; i < 4; i++) list.Add(Std("D2", 2, 6 + i, 4));
        for (int i = 0; i < 6; i++) list.Add(Std("D3", 3, 10 + i, 4));
        for (int i = 0; i < 3; i++) list.Add(Std("D4", 4, 16 + i, 4));
        for (int i = 0; i < 6; i++) list.Add(Std("D5", 5, 19 + i, 4));

        var result = VisitAnalysisEngine.Compute(list);

        result.DomainAverages.Single(d => d.DomainCode == "D1").AverageScore.Should().Be(2.833m);
        result.Strengths.Should().NotContain(s => s.DomainCode == "D1");
        result.ImprovementAreas.Should().NotContain(i => i.DomainCode == "D1");
    }

    [Fact]
    public void ImprovementThreshold_Domain_With_Avg_2_167_Is_Improvement()
    {
        // D1: [2,2,2,2,2,3] → 13/6 = 2.167 → < 2.5 → improvement.
        var list = new List<StandardScoreInput>();
        for (int i = 0; i < 6; i++) list.Add(Std("D1", 1, i, 2));
        list[5] = Std("D1", 1, 5, 3);
        for (int i = 0; i < 4; i++) list.Add(Std("D2", 2, 6 + i, 4));
        for (int i = 0; i < 6; i++) list.Add(Std("D3", 3, 10 + i, 4));
        for (int i = 0; i < 3; i++) list.Add(Std("D4", 4, 16 + i, 4));
        for (int i = 0; i < 6; i++) list.Add(Std("D5", 5, 19 + i, 4));

        var result = VisitAnalysisEngine.Compute(list);

        result.DomainAverages.Single(d => d.DomainCode == "D1").AverageScore.Should().Be(2.167m);
        result.ImprovementAreas.Should().ContainSingle(i => i.DomainCode == "D1");
    }

    // ─── Priority threshold (D-55 fix: verbatim <= 1.5) ───

    [Fact]
    public void PriorityThreshold_D55_ScoreOf2_Is_NotPriority_ScoreOf1_IsPriority()
    {
        // D1 standards at scores [0,1,2,3,4,4] — 0 and 1 are priorities.
        var list = new List<StandardScoreInput>();
        var d1Scores = new[] { 0, 1, 2, 3, 4, 4 };
        for (int i = 0; i < 6; i++) list.Add(Std("D1", 1, i, d1Scores[i]));
        for (int i = 0; i < 4; i++) list.Add(Std("D2", 2, 6 + i, 4));
        for (int i = 0; i < 6; i++) list.Add(Std("D3", 3, 10 + i, 4));
        for (int i = 0; i < 3; i++) list.Add(Std("D4", 4, 16 + i, 4));
        for (int i = 0; i < 6; i++) list.Add(Std("D5", 5, 19 + i, 4));

        var result = VisitAnalysisEngine.Compute(list);

        // After D-55 fix: priorities include scores 1 and 0 (both <= 1.5).
        result.PriorityStandards.Should().HaveCount(2);
        result.PriorityStandards.Should().OnlyContain(p => p.Score <= 1);
    }

    [Fact]
    public void PriorityThreshold_D55_ScoreOf2_Is_NotPriority()
    {
        // All standards score 4 except a single one with score 2.
        // That score-2 standard must NOT be a priority (verbatim <= 1.5).
        var list = new List<StandardScoreInput>();
        int idx = 0;
        for (int i = 0; i < 5; i++) list.Add(Std("D1", 1, idx++, 4));
        list.Add(Std("D1", 1, idx++, 2));
        for (int i = 0; i < 4; i++) list.Add(Std("D2", 2, idx++, 4));
        for (int i = 0; i < 6; i++) list.Add(Std("D3", 3, idx++, 4));
        for (int i = 0; i < 3; i++) list.Add(Std("D4", 4, idx++, 4));
        for (int i = 0; i < 6; i++) list.Add(Std("D5", 5, idx++, 4));

        var result = VisitAnalysisEngine.Compute(list);

        result.PriorityStandards.Should().BeEmpty(
            because: "the lowest score in this snapshot is 2, which is > 1.5 and therefore not a priority");
    }

    // ─── Validation guards ───

    [Fact]
    public void EmptyInput_Throws_AllOtherCounts_Accepted()
    {
        // D-65: rubric is DYNAMIC. The engine accepts any positive N (any snapshot
        // size — could be 25, 27, 30, ...). Only an empty snapshot is invalid.
        Action empty = () => VisitAnalysisEngine.Compute(new List<StandardScoreInput>());
        empty.Should().Throw<InvalidOperationException>()
            .WithMessage("*لا توجد معايير*");

        // A snapshot with 1 standard should still be accepted (per-domain division
        // still works; that single standard's domain average equals its score).
        var single = new List<StandardScoreInput>
        {
            Std("D1", 1, 100, 3)
        };
        var r = VisitAnalysisEngine.Compute(single);
        r.DomainAverages.Should().ContainSingle(d => d.DomainCode == "D1" && d.AverageScore == 3.0m);
        r.OverallScore.Should().Be(3.0m);
    }

    [Fact]
    public void OutOfRangeScore_Throws()
    {
        // Build a valid 25-input then mutate one score above 4.
        var input = Build(d1: 0, d2: 0, d3: 0, d4: 0, d5Base: 0,
            d5Extras: new[] { 0, 0, 0, 0, 0 });
        input[0] = Std("D1", 1, 0, 5); // out of range

        Action act = () => VisitAnalysisEngine.Compute(input);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*خارج النطاق*");
    }

    // ─── Submit gate (N/N — dynamic) ───

    [Fact]
    public void AnalysisEngine_AllScored_DoesNotThrow_AndProduces5DomainRows()
    {
        var input = Build(d1: 4, d2: 4, d3: 4, d4: 4, d5Base: 4,
            d5Extras: new[] { 4, 4, 4, 4, 4 });

        var result = VisitAnalysisEngine.Compute(input);

        result.DomainAverages.Should().HaveCount(5);
        result.OverallScore.Should().Be(4.0m);
        result.PerformanceLevelAr.Should().Be("متميز");
    }

    // ─── D-65: dynamic rubric — engine accepts ANY snapshot size ───

    [Fact]
    public void DynamicRubric_Engine_Accepts_27_Standard_Snapshot()
    {
        // Suppose the active rubric now has D1=7, D2=4, D3=7, D4=3, D5=6 → 27 total.
        var list = new List<StandardScoreInput>();
        int idx = 0;
        // D1=7
        for (int i = 0; i < 7; i++) list.Add(Std("D1", 1, idx++, 4));
        // D2=4
        for (int i = 0; i < 4; i++) list.Add(Std("D2", 2, idx++, 4));
        // D3=7
        for (int i = 0; i < 7; i++) list.Add(Std("D3", 3, idx++, 4));
        // D4=3
        for (int i = 0; i < 3; i++) list.Add(Std("D4", 4, idx++, 4));
        // D5=6
        for (int i = 0; i < 6; i++) list.Add(Std("D5", 5, idx++, 3));

        var result = VisitAnalysisEngine.Compute(list);

        // 5 domain rows, each divided by its OWN standard count (not 25, not 5).
        result.DomainAverages.Should().HaveCount(5);
        result.DomainAverages.Single(d => d.DomainCode == "D1").AverageScore.Should().Be(4.0m);
        result.DomainAverages.Single(d => d.DomainCode == "D5").AverageScore.Should().Be(3.0m);
        // Overall: (28 + 16 + 28 + 12 + 18) / 27 = 102/27 = 3.778
        result.OverallScore.Should().Be(3.778m);
        result.PerformanceLevelAr.Should().Be("متميز");
    }

    [Fact]
    public void DynamicRubric_Engine_Accepts_5_Standard_Snapshot()
    {
        // Single domain, 5 standards — total N=5, domain-average divides by 5.
        var list = new List<StandardScoreInput>();
        for (int i = 0; i < 5; i++)
            list.Add(Std("D1", 1, i, new[] { 4, 3, 2, 1, 0 }[i]));

        var result = VisitAnalysisEngine.Compute(list);

        result.DomainAverages.Should().ContainSingle();
        // avg = (4+3+2+1+0)/5 = 10/5 = 2.0 → 2.0 is < 2.5 → IS an improvement.
        result.DomainAverages.Single().AverageScore.Should().Be(2.0m);
        result.DomainAverages.Single().AverageScore.Should().BeInRange(1.0m, 3.0m);
        result.ImprovementAreas.Should().ContainSingle(i => i.DomainCode == "D1");
        result.Strengths.Should().BeEmpty();
        result.OverallScore.Should().Be(2.0m);
        result.PerformanceLevelAr.Should().Be("متحقق جزئياً");
    }
}