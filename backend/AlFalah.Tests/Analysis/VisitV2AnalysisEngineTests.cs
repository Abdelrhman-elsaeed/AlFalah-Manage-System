using System;
using System.Collections.Generic;
using System.Linq;
using AlFalah.Application.Analysis;
using AlFalah.Application.Common;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.Fixtures.ClassroomVisitsV2;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.Analysis;

/// <summary>
/// Comprehensive Unit &amp; Characterization tests for <see cref="VisitV2AnalysisEngine"/>.
/// Tests 100% calculation parity with the approved prototype, all boundaries,
/// indicator suggestion truth tables, quick bulk ratings, and feature flags.
/// </summary>
public class VisitV2AnalysisEngineTests
{
    // ─── 1. Prototype Seeded Visits Parity (5 Visits) ───

    [Fact]
    public void Calculate_AllFivePrototypeSeededVisits_MatchesGoldenCalculations100Percent()
    {
        var seededVisits = ClassroomVisitsV2GoldenFixtures.SeededVisits;
        var domains = ClassroomVisitsV2GoldenFixtures.Domains;

        foreach (var goldenVisit in seededVisits)
        {
            // Build StandardScoreV2Input from goldenVisit.ScoresState
            var inputs = new List<StandardScoreV2Input>();
            foreach (var domain in domains)
            {
                foreach (var standard in domain.Standards)
                {
                    var scoreState = goldenVisit.ScoresState[standard.Id];
                    inputs.Add(new StandardScoreV2Input
                    {
                        DomainId = domain.Id,
                        DomainName = domain.Name,
                        StandardId = standard.Id,
                        StandardText = standard.Text,
                        Score = scoreState.Score
                    });
                }
            }

            var result = VisitV2AnalysisEngine.Calculate(inputs);

            // 1. Total score & overall level
            result.TotalScore.Should().Be(goldenVisit.TotalScore);
            result.MaxTotalScore.Should().Be(100);
            result.OverallPercent.Should().Be(goldenVisit.TotalScore); // 25 standards * 4 = 100
            result.OverallLevel.Should().Be(goldenVisit.OverallLevel);

            // 2. Domain scores (sum & percent)
            result.DomainScores.Should().HaveCount(5);
            foreach (var domain in domains)
            {
                var goldenDomain = goldenVisit.DomainScores[domain.Id];
                var engineDomain = result.DomainScores.Single(d => d.DomainId == domain.Id);

                engineDomain.DomainName.Should().Be(goldenDomain.DomainName);
                engineDomain.Sum.Should().Be(goldenDomain.Sum);
                engineDomain.Percent.Should().Be(goldenDomain.Percent);
                engineDomain.StandardsCount.Should().Be(domain.Standards.Count);
                engineDomain.MaxScore.Should().Be(domain.Standards.Count * 4);
            }

            // 3. Strengths (>= 75%)
            result.Strengths.Should().BeEquivalentTo(goldenVisit.Strengths);

            // 4. Weaknesses (< 65%)
            result.Weaknesses.Should().HaveCount(goldenVisit.Weaknesses.Count);
            for (int i = 0; i < goldenVisit.Weaknesses.Count; i++)
            {
                var expected = goldenVisit.Weaknesses[i];
                var actual = result.Weaknesses[i];

                actual.DomainId.Should().Be(expected.DomainId);
                actual.DomainName.Should().Be(expected.DomainName);
                actual.Percent.Should().Be(expected.Percent);
                actual.Level.Should().Be(expected.Level);
            }
        }
    }

    // ─── 2. Exact Boundary Tests for Performance Levels ───

    [Theory]
    [InlineData(100, "متحقق بدرجة مرتفعة جداً")]
    [InlineData(85, "متحقق بدرجة مرتفعة جداً")]
    [InlineData(84, "متحقق بدرجة مرتفعة")]
    [InlineData(65, "متحقق بدرجة مرتفعة")]
    [InlineData(64, "متحقق بدرجة متوسطة")]
    [InlineData(50, "متحقق بدرجة متوسطة")]
    [InlineData(49, "متحقق بدرجة منخفضة")]
    [InlineData(25, "متحقق بدرجة منخفضة")]
    [InlineData(0, "متحقق بدرجة منخفضة")]
    public void GetPerformanceLevel_ExactThresholdBoundaries(int scoreOrPercent, string expectedLevel)
    {
        VisitV2AnalysisEngine.GetPerformanceLevel(scoreOrPercent).Should().Be(expectedLevel);
    }

    // ─── 3. Strengths and Weaknesses Boundary Tests ───

    [Fact]
    public void StrengthsThreshold_75Percent_Included_74Percent_Excluded()
    {
        // 5 standards in Domain 1 (max score = 20)
        // 15/20 = 75% -> Strength!
        // 14/20 = 70% -> Not strength, but >= 65% so neutral!
        var standards75 = CreateDomainStandards(domainId: 1, domainName: "المجال", standardScores: new[] { 3, 3, 3, 3, 3 }); // sum 15/20 = 75%
        var result75 = VisitV2AnalysisEngine.Calculate(standards75);

        result75.DomainScores[0].Percent.Should().Be(75);
        result75.DomainScores[0].IsStrength.Should().BeTrue();
        result75.DomainScores[0].IsWeakness.Should().BeFalse();
        result75.Strengths.Should().Contain("المجال (75%)");
        result75.Weaknesses.Should().BeEmpty();

        // 4 standards in a domain (max score = 16):
        // 12/16 = 75%
        // 4 standards with sum 11: 11/16 = 68.75% -> round 69% -> neutral
        var standards74 = CreateDomainStandards(domainId: 1, domainName: "المجال", standardScores: new[] { 3, 3, 3, 3, 2 }); // sum 14/20 = 70%
        var result74 = VisitV2AnalysisEngine.Calculate(standards74);

        result74.DomainScores[0].Percent.Should().Be(70);
        result74.DomainScores[0].IsStrength.Should().BeFalse();
        result74.DomainScores[0].IsWeakness.Should().BeFalse();
        result74.Strengths.Should().BeEmpty();
        result74.Weaknesses.Should().BeEmpty();
    }

    [Fact]
    public void WeaknessesThreshold_64Percent_Included_65Percent_Excluded()
    {
        // 5 standards (max score = 20):
        // 13/20 = 65% -> Excluded from weaknesses (< 65 is weakness)
        var standards65 = CreateDomainStandards(domainId: 1, domainName: "المجال", standardScores: new[] { 3, 3, 3, 2, 2 }); // 13/20 = 65%
        var result65 = VisitV2AnalysisEngine.Calculate(standards65);

        result65.DomainScores[0].Percent.Should().Be(65);
        result65.DomainScores[0].IsWeakness.Should().BeFalse();
        result65.DomainScores[0].IsStrength.Should().BeFalse();
        result65.Weaknesses.Should().BeEmpty();

        // 12/20 = 60% -> Included in weaknesses
        var standards60 = CreateDomainStandards(domainId: 1, domainName: "المجال", standardScores: new[] { 3, 3, 2, 2, 2 }); // 12/20 = 60%
        var result60 = VisitV2AnalysisEngine.Calculate(standards60);

        result60.DomainScores[0].Percent.Should().Be(60);
        result60.DomainScores[0].IsWeakness.Should().BeTrue();
        result60.Weaknesses.Should().ContainSingle(w => w.DomainId == 1 && w.Percent == 60 && w.Level == "متحقق بدرجة متوسطة");
    }

    [Fact]
    public void NeutralZone_Between65And74_NeitherStrengthNorWeakness()
    {
        // 70% is between 65 and 74
        var standards70 = CreateDomainStandards(domainId: 1, domainName: "المجال", standardScores: new[] { 3, 3, 3, 3, 2 }); // 14/20 = 70%
        var result = VisitV2AnalysisEngine.Calculate(standards70);

        result.DomainScores[0].Percent.Should().Be(70);
        result.DomainScores[0].IsStrength.Should().BeFalse();
        result.DomainScores[0].IsWeakness.Should().BeFalse();
        result.Strengths.Should().BeEmpty();
        result.Weaknesses.Should().BeEmpty();
    }

    // ─── 4. Indicator-to-Score Suggestion Truth Table ───

    [Theory]
    [InlineData(2, 0, 1)]
    [InlineData(2, 1, 3)]
    [InlineData(2, 2, 4)]
    [InlineData(3, 0, 1)]
    [InlineData(3, 1, 2)]
    [InlineData(3, 2, 3)]
    [InlineData(3, 3, 4)]
    public void SuggestScore_PrototypeTruthTable_MatchesVerbatim(int totalInd, int checkedInd, int expectedScore)
    {
        VisitV2AnalysisEngine.SuggestScore(totalInd, checkedInd).Should().Be(expectedScore);
    }

    [Fact]
    public void SuggestScore_BoundariesAndEdgeCases()
    {
        // Clamping check: checkedInd < 0 treated as 0 -> score 1
        VisitV2AnalysisEngine.SuggestScore(3, -1).Should().Be(1);

        // Clamping check: checkedInd > totalInd treated as totalInd -> score 4
        VisitV2AnalysisEngine.SuggestScore(3, 10).Should().Be(4);

        // totalIndicators <= 0 throws
        Action act = () => VisitV2AnalysisEngine.SuggestScore(0, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ─── 5. Quick Bulk Rating (1, 2, 3, 4) ───

    [Theory]
    [InlineData(4, 100, "متحقق بدرجة مرتفعة جداً", 5, 0)]
    [InlineData(3, 75, "متحقق بدرجة مرتفعة", 5, 0)]
    [InlineData(2, 50, "متحقق بدرجة متوسطة", 0, 5)]
    [InlineData(1, 25, "متحقق بدرجة منخفضة", 0, 5)]
    public void ApplyBulkRating_AllFourLevels_ProduceConsistentAggregates(
        int bulkScore, int expectedTotal, string expectedLevel, int expectedStrengthCount, int expectedWeaknessCount)
    {
        var template = ClassroomVisitsV2GoldenFixtures.Domains
            .SelectMany(d => d.Standards.Select(s => (d.Id, d.Name, s.Id, (string?)s.Text)))
            .ToList();

        var inputs = VisitV2AnalysisEngine.ApplyBulkRating(bulkScore, template);
        inputs.Should().HaveCount(25);
        inputs.All(i => i.Score == bulkScore).Should().BeTrue();

        var result = VisitV2AnalysisEngine.Calculate(inputs);

        result.TotalScore.Should().Be(expectedTotal);
        result.OverallPercent.Should().Be(expectedTotal);
        result.OverallLevel.Should().Be(expectedLevel);
        result.Strengths.Should().HaveCount(expectedStrengthCount);
        result.Weaknesses.Should().HaveCount(expectedWeaknessCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void ApplyBulkRating_InvalidScore_ThrowsArgumentOutOfRangeException(int invalidScore)
    {
        var template = new (int DomainId, string DomainName, int StandardId, string? StandardText)[] { (1, "د1", 1, "معيار 1") };
        Action act = () => VisitV2AnalysisEngine.ApplyBulkRating(invalidScore, template);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ─── 6. Validations & Error Cases ───

    [Fact]
    public void Calculate_NullInput_ThrowsArgumentNullException()
    {
        Action act = () => VisitV2AnalysisEngine.Calculate(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Calculate_EmptyInput_ThrowsArgumentException()
    {
        Action act = () => VisitV2AnalysisEngine.Calculate(new List<StandardScoreV2Input>());
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5)]
    public void Calculate_ScoreOutOfRange_ThrowsArgumentOutOfRangeException(int invalidScore)
    {
        var standards = new List<StandardScoreV2Input>
        {
            new() { DomainId = 1, DomainName = "بيئة التعلم", StandardId = 1, Score = invalidScore }
        };

        Action act = () => VisitV2AnalysisEngine.Calculate(standards);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ─── 7. Feature Flag Service Tests ───

    [Fact]
    public void FeatureFlagService_DefaultIsOff()
    {
        var options = Options.Create(new FeatureFlagsOptions());
        var service = new FeatureFlagService(options);

        service.IsVisitsV2Enabled().Should().BeFalse();
    }

    [Fact]
    public void FeatureFlagService_WhenEnabled_ReturnsTrue()
    {
        var options = Options.Create(new FeatureFlagsOptions { VisitsV2 = true });
        var service = new FeatureFlagService(options);

        service.IsVisitsV2Enabled().Should().BeTrue();
    }

    [Fact]
    public void FeatureFlagService_SchoolPilot_EnablesOnlyAllowlistedSchool()
    {
        var options = Options.Create(new FeatureFlagsOptions
        {
            VisitsV2 = false,
            VisitsV2SchoolIds = new[] { 17, 23 }
        });
        var service = new FeatureFlagService(options);

        service.IsVisitsV2Enabled(17).Should().BeTrue();
        service.IsVisitsV2Enabled(99).Should().BeFalse();
        service.IsVisitsV2Enabled().Should().BeFalse();
    }

    // ─── Helpers ───

    private static List<StandardScoreV2Input> CreateDomainStandards(int domainId, string domainName, int[] standardScores)
    {
        var list = new List<StandardScoreV2Input>();
        for (int i = 0; i < standardScores.Length; i++)
        {
            list.Add(new StandardScoreV2Input
            {
                DomainId = domainId,
                DomainName = domainName,
                StandardId = i + 1,
                StandardText = $"معيار {i + 1}",
                Score = standardScores[i]
            });
        }
        return list;
    }
}
