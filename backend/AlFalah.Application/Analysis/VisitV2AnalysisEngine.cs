using System;
using System.Collections.Generic;
using System.Linq;

namespace AlFalah.Application.Analysis;

/// <summary>
/// Classroom Visits V2 Analysis Engine — 100% pure domain/application logic with zero DB/EF dependencies.
/// Verbatim implementation of the approved HTML prototype:
///  - Total Score: sum of all 25 standard ratings (range 25..100).
///  - Domain Percentage: Math.Round((domainSum / (standardsCount * 4)) * 100).
///  - Overall &amp; Domain Performance Level:
///      >= 85: متحقق بدرجة مرتفعة جداً
///      >= 65: متحقق بدرجة مرتفعة
///      >= 50: متحقق بدرجة متوسطة
///      &lt; 50: متحقق بدرجة منخفضة
///  - Strengths: domains with percentage >= 75%.
///  - Improvement Areas / Weaknesses: domains with percentage &lt; 65%.
///  - Neutral Zone: 65%..74% are neither strength nor weakness.
///  - Indicator suggestion logic:
///      * 2-indicator standard: 0 -> 1, 1 -> 3, 2 -> 4.
///      * 3-indicator standard: 0 -> 1, 1 -> 2, 2 -> 3, 3 -> 4.
///  - Quick Bulk Rating: sets all standards to score (1..4).
/// </summary>
public static class VisitV2AnalysisEngine
{
    // ─── Classification Thresholds ───
    public const int ThresholdVeryHigh = 85;
    public const int ThresholdHigh = 65;
    public const int ThresholdMedium = 50;

    public const int ThresholdStrength = 75;
    public const int ThresholdWeakness = 65;

    public const int MinScorePerStandard = 1;
    public const int MaxScorePerStandard = 4;
    public const int TotalStandardsCount = 25;
    public const int MaxTotalScore = 100;
    public const int MinTotalScore = 25;

    public const string LevelVeryHighText = "متحقق بدرجة مرتفعة جداً";
    public const string LevelHighText = "متحقق بدرجة مرتفعة";
    public const string LevelMediumText = "متحقق بدرجة متوسطة";
    public const string LevelLowText = "متحقق بدرجة منخفضة";

    /// <summary>
    /// Computes the complete V2 evaluation snapshot from the given per-standard scores.
    /// </summary>
    public static VisitV2AnalysisResult Calculate(IReadOnlyList<StandardScoreV2Input> standards)
    {
        if (standards == null) throw new ArgumentNullException(nameof(standards));
        if (standards.Count == 0)
            throw new ArgumentException("لا توجد معايير مُقيَّمة لحساب التحليل.", nameof(standards));

        foreach (var s in standards)
        {
            if (s.Score < MinScorePerStandard || s.Score > MaxScorePerStandard)
                throw new ArgumentOutOfRangeException(
                    nameof(standards),
                    $"درجة المعيار {s.StandardId} ({s.Score}) خارج النطاق المسموح به (1 إلى 4).");
        }

        var domainGroups = standards
            .GroupBy(s => new { s.DomainId, s.DomainName })
            .OrderBy(g => g.Key.DomainId)
            .ToList();

        var domainScores = new List<DomainScoreV2Result>(domainGroups.Count);
        var strengths = new List<string>();
        var weaknesses = new List<WeaknessV2Result>();

        foreach (var g in domainGroups)
        {
            var standardsCount = g.Count();
            var domainSum = g.Sum(s => s.Score);
            var domainMax = standardsCount * MaxScorePerStandard;
            var domainPercent = (int)Math.Round((double)domainSum / domainMax * 100, MidpointRounding.AwayFromZero);
            var domainLevel = GetPerformanceLevel(domainPercent);

            var isStrength = domainPercent >= ThresholdStrength;
            var isWeakness = domainPercent < ThresholdWeakness;

            domainScores.Add(new DomainScoreV2Result
            {
                DomainId = g.Key.DomainId,
                DomainName = g.Key.DomainName,
                StandardsCount = standardsCount,
                Sum = domainSum,
                MaxScore = domainMax,
                Percent = domainPercent,
                Level = domainLevel,
                IsStrength = isStrength,
                IsWeakness = isWeakness
            });

            if (isStrength)
            {
                strengths.Add($"{g.Key.DomainName} ({domainPercent}%)");
            }

            if (isWeakness)
            {
                weaknesses.Add(new WeaknessV2Result
                {
                    DomainId = g.Key.DomainId,
                    DomainName = g.Key.DomainName,
                    Percent = domainPercent,
                    Level = domainLevel
                });
            }
        }

        var totalScore = standards.Sum(s => s.Score);
        var maxTotal = standards.Count * MaxScorePerStandard;
        var overallPercent = (int)Math.Round((double)totalScore / maxTotal * 100, MidpointRounding.AwayFromZero);
        var overallLevel = GetPerformanceLevel(totalScore <= 100 && maxTotal == 100 ? totalScore : overallPercent);

        return new VisitV2AnalysisResult
        {
            TotalScore = totalScore,
            MaxTotalScore = maxTotal,
            OverallPercent = overallPercent,
            OverallLevel = overallLevel,
            DomainScores = domainScores,
            Strengths = strengths,
            Weaknesses = weaknesses
        };
    }

    /// <summary>
    /// Maps a score or percentage to its official Arabic performance level string.
    /// Ordered highest to lowest.
    /// </summary>
    public static string GetPerformanceLevel(int percentOrScore)
    {
        if (percentOrScore >= ThresholdVeryHigh) return LevelVeryHighText;
        if (percentOrScore >= ThresholdHigh) return LevelHighText;
        if (percentOrScore >= ThresholdMedium) return LevelMediumText;
        return LevelLowText;
    }

    /// <summary>
    /// Suggests a standard score (1..4) based on observed indicator check marks according to prototype truth table.
    /// </summary>
    public static int SuggestScore(int totalIndicators, int checkedCount)
    {
        if (totalIndicators <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalIndicators), "إجمالي المؤشرات يجب أن يكون أكبر من الصفر.");

        if (checkedCount < 0) checkedCount = 0;
        if (checkedCount > totalIndicators) checkedCount = totalIndicators;

        if (checkedCount == 0) return 1;
        if (checkedCount == totalIndicators) return 4;

        if (totalIndicators == 2)
        {
            // 2-indicator standard: 0 checked -> 1, 1 checked -> 3, 2 checked -> 4
            return 3;
        }

        if (totalIndicators == 3)
        {
            // 3-indicator standard: 0 checked -> 1, 1 checked -> 2, 2 checked -> 3, 3 checked -> 4
            return checkedCount == 1 ? 2 : 3;
        }

        // Generic fallback for any other indicator count
        return 1 + (int)Math.Round((double)checkedCount / totalIndicators * 3.0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Generates uniform standard scores for quick bulk rating (1, 2, 3, or 4).
    /// </summary>
    public static IReadOnlyList<StandardScoreV2Input> ApplyBulkRating(
        int bulkScore,
        IEnumerable<(int DomainId, string DomainName, int StandardId, string? StandardText)> standards)
    {
        if (bulkScore < MinScorePerStandard || bulkScore > MaxScorePerStandard)
            throw new ArgumentOutOfRangeException(nameof(bulkScore), $"درجة الرصد السريع ({bulkScore}) يجب أن تكون بين 1 و 4.");

        if (standards == null) throw new ArgumentNullException(nameof(standards));

        return standards.Select(s => new StandardScoreV2Input
        {
            DomainId = s.DomainId,
            DomainName = s.DomainName,
            StandardId = s.StandardId,
            StandardText = s.StandardText ?? string.Empty,
            Score = bulkScore
        }).ToList();
    }
}
