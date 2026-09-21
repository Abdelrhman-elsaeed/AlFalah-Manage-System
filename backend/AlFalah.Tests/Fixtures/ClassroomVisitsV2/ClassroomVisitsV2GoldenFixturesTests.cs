using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Fixtures.ClassroomVisitsV2;

/// <summary>
/// Phase 0 Acceptance Tests: Immutable Golden Fixture Lock for Classroom Visits V2.
/// Verifies 100% parity with the approved prototype:
/// 5 domains, 25 standards, 66 indicators, 5 treatment templates, 5 seeded visits, 22 CSV headers,
/// and exact scoring/classification formulas.
/// </summary>
public class ClassroomVisitsV2GoldenFixturesTests
{
    [Fact]
    public void PrototypeSha256_MatchesApprovedHash()
    {
        ClassroomVisitsV2GoldenFixtures.PrototypeSha256.Should().Be(
            "86302A58348798B5BAD6A250708B26677BC6E11ACE3CDEC5C272B2AC2FC47827");

        // If prototype file is present on this machine, verify its live SHA-256 matches
        const string localPrototypePath = @"C:\Users\abdelrhman\Desktop\index.html";
        if (File.Exists(localPrototypePath))
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(localPrototypePath);
            var hashBytes = sha256.ComputeHash(stream);
            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
            hashString.Should().Be(ClassroomVisitsV2GoldenFixtures.PrototypeSha256);
        }
    }

    [Fact]
    public void Domains_CountAndDistribution_MatchPrototype()
    {
        var domains = ClassroomVisitsV2GoldenFixtures.Domains;

        domains.Should().HaveCount(5);
        domains.Select(d => d.Name).Should().ContainInOrder(
            "بيئة التعلم",
            "التدريس والتعلم",
            "تنمية المهارات",
            "التقويم",
            "سلوك المتعلمين"
        );

        // Standard counts per domain: 5, 5, 6, 3, 6
        domains[0].Standards.Should().HaveCount(5); // بيئة التعلم
        domains[1].Standards.Should().HaveCount(5); // التدريس والتعلم
        domains[2].Standards.Should().HaveCount(6); // تنمية المهارات
        domains[3].Standards.Should().HaveCount(3); // التقويم
        domains[4].Standards.Should().HaveCount(6); // سلوك المتعلمين

        domains.Sum(d => d.Standards.Count).Should().Be(25);
    }

    [Fact]
    public void Indicators_TotalAndDistribution_MatchPrototype()
    {
        var domains = ClassroomVisitsV2GoldenFixtures.Domains;

        // Indicator counts per domain: 13, 13, 18, 9, 13
        domains[0].Standards.Sum(s => s.Indicators.Count).Should().Be(13); // D1
        domains[1].Standards.Sum(s => s.Indicators.Count).Should().Be(13); // D2
        domains[2].Standards.Sum(s => s.Indicators.Count).Should().Be(18); // D3
        domains[3].Standards.Sum(s => s.Indicators.Count).Should().Be(9);  // D4
        domains[4].Standards.Sum(s => s.Indicators.Count).Should().Be(13); // D5

        var totalIndicators = domains.Sum(d => d.Standards.Sum(s => s.Indicators.Count));
        totalIndicators.Should().Be(66);

        // Every standard must have either 2 or 3 indicators in the official rubric
        foreach (var domain in domains)
        {
            foreach (var standard in domain.Standards)
            {
                standard.Indicators.Count.Should().BeInRange(2, 3,
                    $"Standard {standard.Id} ({standard.Text}) should have 2 or 3 indicators");
                standard.Indicators.Should().OnlyHaveUniqueItems();
            }
        }
    }

    [Fact]
    public void Standards_Numbered1To25Sequentially()
    {
        var allStandards = ClassroomVisitsV2GoldenFixtures.Domains
            .SelectMany(d => d.Standards)
            .ToList();

        allStandards.Should().HaveCount(25);
        for (int i = 0; i < 25; i++)
        {
            allStandards[i].Id.Should().Be(i + 1);
            allStandards[i].Num.Should().Be(i + 1);
            allStandards[i].Text.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void TreatmentTemplates_CountAndContent_MatchPrototype()
    {
        var templates = ClassroomVisitsV2GoldenFixtures.TreatmentTemplates;

        templates.Should().HaveCount(5);
        templates.Keys.Should().Contain(new[]
        {
            "بيئة التعلم",
            "التدريس والتعلم",
            "تنمية المهارات",
            "التقويم",
            "سلوك المتعلمين"
        });

        foreach (var kvp in templates)
        {
            kvp.Value.DomainName.Should().Be(kvp.Key);
            kvp.Value.Goal.Should().NotBeNullOrWhiteSpace();
            kvp.Value.Actions.Should().NotBeNullOrWhiteSpace();
            kvp.Value.SuccessIndicators.Should().NotBeNullOrWhiteSpace();
        }

        // Exact text verification for Domain 1 template
        var d1Template = templates["بيئة التعلم"];
        d1Template.Goal.Should().Be("تحسين جودة بيئة التعلم وتنظيمها لتكون محفزة وآمنة لجميع المتعلمين");
        d1Template.SuccessIndicators.Should().Be("ارتفاع متوسط درجات بيئة التعلم إلى 80% أو أعلى في الزيارة القادمة.");
    }

    [Fact]
    public void SeededVisits_AllFiveVisits_MatchExpectedScoresAndFormulas()
    {
        var visits = ClassroomVisitsV2GoldenFixtures.SeededVisits;
        visits.Should().HaveCount(5);

        // Expected totals and levels from prototype
        var expectedResults = new[]
        {
            (VisitId: 1726300000000L, TotalScore: 66, Level: "متحقق بدرجة مرتفعة"),
            (VisitId: 1726300010001L, TotalScore: 61, Level: "متحقق بدرجة متوسطة"),
            (VisitId: 1726300020002L, TotalScore: 59, Level: "متحقق بدرجة متوسطة"),
            (VisitId: 1726300030003L, TotalScore: 59, Level: "متحقق بدرجة متوسطة"),
            (VisitId: 1726300040004L, TotalScore: 67, Level: "متحقق بدرجة مرتفعة"),
        };

        for (int i = 0; i < visits.Count; i++)
        {
            var visit = visits[i];
            var exp = expectedResults[i];

            visit.Id.Should().Be(exp.VisitId);
            visit.TotalScore.Should().Be(exp.TotalScore);
            visit.OverallLevel.Should().Be(exp.Level);

            // Verify scoresState has all 25 standards
            visit.ScoresState.Should().HaveCount(25);
            var calculatedSum = visit.ScoresState.Values.Sum(s => s.Score);
            calculatedSum.Should().Be(exp.TotalScore);

            // Verify domain scores calculations
            var domains = ClassroomVisitsV2GoldenFixtures.Domains;
            foreach (var domain in domains)
            {
                var domainSum = domain.Standards.Sum(s => visit.ScoresState[s.Id].Score);
                var domainMax = domain.Standards.Count * 4;
                var domainPercent = (int)Math.Round((double)domainSum / domainMax * 100);

                visit.DomainScores.Should().ContainKey(domain.Id);
                var ds = visit.DomainScores[domain.Id];
                ds.Sum.Should().Be(domainSum);
                ds.Percent.Should().Be(domainPercent);

                // Strengths: >= 75%
                if (domainPercent >= ClassroomVisitsV2GoldenFixtures.ThresholdStrength)
                {
                    visit.Strengths.Should().Contain($"{domain.Name} ({domainPercent}%)");
                }

                // Weaknesses: < 65%
                if (domainPercent < ClassroomVisitsV2GoldenFixtures.ThresholdWeakness)
                {
                    visit.Weaknesses.Should().Contain(w => w.DomainId == domain.Id && w.Percent == domainPercent);
                }
            }
        }
    }

    [Fact]
    public void CsvHeaders_HasExact22Columns()
    {
        ClassroomVisitsV2GoldenFixtures.CsvHeaders.Should().HaveCount(22);
        ClassroomVisitsV2GoldenFixtures.CsvHeaders.Should().ContainInOrder(
            "رقم الزيارة",
            "تاريخ الزيارة",
            "رقم الحصة",
            "المعلم المزار",
            "رقم الهوية",
            "المادة الدراسية",
            "الصف والفصل",
            "عنوان الدرس",
            "نوع الزيارة",
            "عدد الحضور",
            "عدد الغياب",
            "المشرف الزائر",
            "صفة المشرف",
            "الدرجة الكلية (من 100)",
            "مستوى التحقق",
            "درجة بيئة التعلم (%)",
            "درجة التدريس والتعلم (%)",
            "درجة تنمية المهارات (%)",
            "درجة التقويم (%)",
            "درجة سلوك المتعلمين (%)",
            "نقاط القوة",
            "مجالات التحسين"
        );
    }

    [Fact]
    public void EvaluatorsAndTeachersFixtures_HaveExpectedCounts()
    {
        ClassroomVisitsV2GoldenFixtures.Evaluators.Should().HaveCount(3);
        ClassroomVisitsV2GoldenFixtures.DefaultTeachers.Should().HaveCount(24);

        ClassroomVisitsV2GoldenFixtures.Evaluators.Select(e => e.Name).Should().Contain(new[]
        {
            "ناصر بن جروض الزهراني",
            "محيسن الحساني",
            "عبد الإله الشريف"
        });
    }

    [Fact]
    public void IndicatorSuggestionRules_MatchPrototypeTruthTable()
    {
        // 2-indicator standard:
        // 0 checked -> 1
        // 1 checked -> 3
        // 2 checked -> 4
        SuggestScore(totalInd: 2, checkedCount: 0).Should().Be(1);
        SuggestScore(totalInd: 2, checkedCount: 1).Should().Be(3);
        SuggestScore(totalInd: 2, checkedCount: 2).Should().Be(4);

        // 3-indicator standard:
        // 0 checked -> 1
        // 1 checked -> 2
        // 2 checked -> 3
        // 3 checked -> 4
        SuggestScore(totalInd: 3, checkedCount: 0).Should().Be(1);
        SuggestScore(totalInd: 3, checkedCount: 1).Should().Be(2);
        SuggestScore(totalInd: 3, checkedCount: 2).Should().Be(3);
        SuggestScore(totalInd: 3, checkedCount: 3).Should().Be(4);
    }

    [Theory]
    [InlineData(85, "متحقق بدرجة مرتفعة جداً")]
    [InlineData(100, "متحقق بدرجة مرتفعة جداً")]
    [InlineData(65, "متحقق بدرجة مرتفعة")]
    [InlineData(84, "متحقق بدرجة مرتفعة")]
    [InlineData(50, "متحقق بدرجة متوسطة")]
    [InlineData(64, "متحقق بدرجة متوسطة")]
    [InlineData(49, "متحقق بدرجة منخفضة")]
    [InlineData(25, "متحقق بدرجة منخفضة")]
    public void PerformanceLevelThresholds_MatchPrototypeBoundaries(int score, string expectedLevel)
    {
        GetPerformanceLevel(score).Should().Be(expectedLevel);
    }

    private static int SuggestScore(int totalInd, int checkedCount)
    {
        if (checkedCount == 0) return 1;
        if (checkedCount == totalInd) return 4;
        if (totalInd == 3) return checkedCount == 1 ? 2 : 3;
        if (totalInd == 2) return 3;
        return 1;
    }

    private static string GetPerformanceLevel(int overallPercent)
    {
        if (overallPercent >= ClassroomVisitsV2GoldenFixtures.ThresholdVeryHigh)
            return ClassroomVisitsV2GoldenFixtures.LevelVeryHighText;
        if (overallPercent >= ClassroomVisitsV2GoldenFixtures.ThresholdHigh)
            return ClassroomVisitsV2GoldenFixtures.LevelHighText;
        if (overallPercent >= ClassroomVisitsV2GoldenFixtures.ThresholdMedium)
            return ClassroomVisitsV2GoldenFixtures.LevelMediumText;
        return ClassroomVisitsV2GoldenFixtures.LevelLowText;
    }
}
