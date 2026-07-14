namespace AlFalah.Application.Analysis;

/// <summary>
/// Visit analysis engine — verbatim implementation of docs/09 (DYNAMIC, no fixed count).
///  - Domain average = mean of standard scores in that domain (each domain divides
///    by ITS OWN standard count — uneven distribution respected; never a fixed divisor).
///  - Overall score = mean of all scored standards in the snapshot (3 decimals).
///  - Performance level: متميز >=3.5, جيد جداً >=3.0, جيد >=2.5,
///    متحقق جزئياً >=2.0, يحتاج تحسين >=1.0, غير مشاهد <1.0 (highest first).
///  - Strengths = domains with average >= 3.0.
///  - Improvement areas = domains with average < 2.5.
///  - Priority standards = individual standards with score &lt;= 1.5.
///
/// THE INITIAL RUBRIC SEED IS 5 DOMAINS / 25 STANDARDS (DISTRIBUTION 6/4/6/3/6); that
/// count is the SEED ONLY. Main Manager edits in the Rubric editor (Phase 3 copy-on-write)
/// add or remove standards freely — visits always snapshot whatever the active rubric
/// had at their creation time, so the engine must handle ANY positive N. See D-65.
/// </summary>
public static class VisitAnalysisEngine
{
    /// <summary>
    /// Computes the analysis snapshot for a visit with the given per-standard scores.
    /// ALL input sizes are accepted (any positive N) — the engine divides by each
    /// domain's OWN standard count and by the input total. Throws when input is
    /// null/empty or when any score is outside 0..4.
    /// </summary>
    public static VisitAnalysisResult Compute(IReadOnlyList<StandardScoreInput> standards)
    {
        if (standards == null) throw new ArgumentNullException(nameof(standards));
        if (standards.Count == 0)
            throw new InvalidOperationException(
                "لا توجد معايير مُقيَّمة لحساب التحليل.");

        foreach (var s in standards)
        {
            if (s.Score < 0 || s.Score > 4)
                throw new InvalidOperationException(
                    $"درجة المعيار {s.StandardCode} ({s.Score}) خارج النطاق 0..4.");
        }

        // Domain averages — respect the live distribution in the snapshot
        // (D-65: any N, uneven per-domain counts accepted; each domain averages
        // over its OWN standard count, never a fixed divisor).
        var domainGroups = standards
            .GroupBy(s => new { s.RubricDomainId, s.DomainCode, s.DomainNameAr })
            .OrderBy(g => g.Key.DomainCode)
            .ToList();

        var domainAverages = domainGroups
            .Select(g => new DomainAverageRow
            {
                RubricDomainId = g.Key.RubricDomainId,
                DomainCode = g.Key.DomainCode,
                DomainNameAr = g.Key.DomainNameAr,
                AverageScore = Math.Round(g.Average(x => (decimal)x.Score), 3)
            })
            .ToList();

        var overall = Math.Round(standards.Average(s => (decimal)s.Score), 3);
        var level = MapPerformanceLevel(overall);

        var strengths = domainAverages
            .Where(d => d.AverageScore >= 3.0m)
            .Select(d => new DomainAverageRow
            {
                RubricDomainId = d.RubricDomainId,
                DomainCode = d.DomainCode,
                DomainNameAr = d.DomainNameAr,
                AverageScore = d.AverageScore
            })
            .ToList();

        var improvements = domainAverages
            .Where(d => d.AverageScore < 2.5m)
            .Select(d => new DomainAverageRow
            {
                RubricDomainId = d.RubricDomainId,
                DomainCode = d.DomainCode,
                DomainNameAr = d.DomainNameAr,
                AverageScore = d.AverageScore
            })
            .ToList();

        // Priority standards = individual standards with score <= 1.5 (docs/09 verbatim).
        var priorities = standards
            .Where(s => (decimal)s.Score <= 1.5m)
            .Select(s => new PriorityStandardRow
            {
                RubricStandardId = s.RubricStandardId,
                DomainCode = s.DomainCode,
                StandardCode = s.StandardCode,
                StandardTextAr = s.StandardTextAr,
                Score = s.Score
            })
            .OrderBy(p => p.Score)
            .ThenBy(p => p.StandardCode)
            .ToList();

        return new VisitAnalysisResult
        {
            OverallScore = overall,
            PerformanceLevelAr = level,
            DomainAverages = domainAverages,
            Strengths = strengths,
            ImprovementAreas = improvements,
            PriorityStandards = priorities
        };
    }

    /// <summary>
    /// Maps an overall score to its Arabic performance level per docs/09.
    /// Ordered highest → lowest so the first matching threshold wins.
    /// </summary>
    public static string MapPerformanceLevel(decimal overall)
    {
        if (overall >= 3.5m) return "متميز";
        if (overall >= 3.0m) return "جيد جداً";
        if (overall >= 2.5m) return "جيد";
        if (overall >= 2.0m) return "متحقق جزئياً";
        if (overall >= 1.0m) return "يحتاج تحسين";
        return "غير مشاهد";
    }
}