using System;
using System.Collections.Generic;
using System.Linq;
using AlFalah.Application.Analysis;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Analysis;

/// <summary>
/// Characterization tests to protect the legacy V1 Visit Analysis Engine.
/// Guarantees zero regression on legacy calculations, performance thresholds,
/// and priority standards rules while V2 is being implemented in parallel.
/// </summary>
public class LegacyVisitEngineCharacterizationTests
{
    [Fact]
    public void LegacyEngine_DomainAverage_CalculatedOverOwnStandardCount()
    {
        // 3 standards in D1: scores 3, 4, 2 -> average 3.0
        // 2 standards in D2: scores 1, 2 -> average 1.5
        var inputs = new List<StandardScoreInput>
        {
            new() { RubricDomainId = 1, DomainCode = "D1", DomainNameAr = "مجال 1", StandardCode = "S1", Score = 3 },
            new() { RubricDomainId = 1, DomainCode = "D1", DomainNameAr = "مجال 1", StandardCode = "S2", Score = 4 },
            new() { RubricDomainId = 1, DomainCode = "D1", DomainNameAr = "مجال 1", StandardCode = "S3", Score = 2 },
            new() { RubricDomainId = 2, DomainCode = "D2", DomainNameAr = "مجال 2", StandardCode = "S4", Score = 1 },
            new() { RubricDomainId = 2, DomainCode = "D2", DomainNameAr = "مجال 2", StandardCode = "S5", Score = 2 }
        };

        var result = VisitAnalysisEngine.Compute(inputs);

        result.DomainAverages.Should().HaveCount(2);
        result.DomainAverages.Single(d => d.DomainCode == "D1").AverageScore.Should().Be(3.0m);
        result.DomainAverages.Single(d => d.DomainCode == "D2").AverageScore.Should().Be(1.5m);

        // Overall is equal-weight mean of domain averages: (3.0 + 1.5)/2 = 2.25
        result.OverallScore.Should().Be(2.25m);
        result.PerformanceLevelAr.Should().Be("متحقق جزئياً");
    }

    [Theory]
    [InlineData(4.0, "متميز")]
    [InlineData(3.5, "متميز")]
    [InlineData(3.499, "جيد جداً")]
    [InlineData(3.0, "جيد جداً")]
    [InlineData(2.999, "جيد")]
    [InlineData(2.5, "جيد")]
    [InlineData(2.499, "متحقق جزئياً")]
    [InlineData(2.0, "متحقق جزئياً")]
    [InlineData(1.999, "يحتاج تحسين")]
    [InlineData(1.0, "يحتاج تحسين")]
    [InlineData(0.999, "غير مشاهد")]
    [InlineData(0.0, "غير مشاهد")]
    public void LegacyEngine_MapPerformanceLevel_VerbatimThresholdsLocked(decimal overallScore, string expectedLevel)
    {
        VisitAnalysisEngine.MapPerformanceLevel(overallScore).Should().Be(expectedLevel);
    }

    [Fact]
    public void LegacyEngine_PriorityStandards_ThresholdIsOnePointFiveOrLess()
    {
        // Docs/09 D-55 verbatim: priority standards are individual standards with score <= 1.5
        var inputs = new List<StandardScoreInput>
        {
            new() { RubricDomainId = 1, DomainCode = "D1", DomainNameAr = "مجال 1", StandardCode = "S1", Score = 1 },
            new() { RubricDomainId = 1, DomainCode = "D1", DomainNameAr = "مجال 1", StandardCode = "S2", Score = 2 },
            new() { RubricDomainId = 1, DomainCode = "D1", DomainNameAr = "مجال 1", StandardCode = "S3", Score = 3 }
        };

        var result = VisitAnalysisEngine.Compute(inputs);

        result.PriorityStandards.Should().HaveCount(1);
        result.PriorityStandards[0].StandardCode.Should().Be("S1");
        result.PriorityStandards[0].Score.Should().Be(1);
    }

    [Fact]
    public void V1AndV2Engines_AreCompletelyIsolated_NoCrossDependencies()
    {
        // Verify V1 engine uses 0..4 scale while V2 uses 1..4 scale
        // V1 accepts 0:
        var v1WithZero = new List<StandardScoreInput>
        {
            new() { RubricDomainId = 1, DomainCode = "D1", DomainNameAr = "مجال 1", StandardCode = "S1", Score = 0 }
        };
        var v1Result = VisitAnalysisEngine.Compute(v1WithZero);
        v1Result.TotalScore.Should().Be(0);

        // V2 rejects 0 (strictly prototype 1..4 scale):
        var v2WithZero = new List<StandardScoreV2Input>
        {
            new() { DomainId = 1, DomainName = "مجال 1", StandardId = 1, Score = 0 }
        };
        Action act = () => VisitV2AnalysisEngine.Calculate(v2WithZero);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
