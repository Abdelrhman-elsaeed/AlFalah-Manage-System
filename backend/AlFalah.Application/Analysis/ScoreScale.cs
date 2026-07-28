using System.Globalization;

namespace AlFalah.Application.Analysis;

/// <summary>
/// D-UI-1 — the single published score scale.
///
/// The rubric scores each standard 0..4 and every stored figure
/// (<c>VisitAnalysis.OverallScore</c>, <c>VisitDomainAverage.AverageScore</c>)
/// keeps that scale, because the performance-level thresholds
/// (متميز ≥ 3.5 …) and the historical snapshots are defined on it and are
/// never recomputed (docs/09, D-26).
///
/// Everything shown to a human — PDF, Excel, dashboard, web UI — is published
/// on ONE scale: **0..100**. Before this class the product printed three
/// scales at once (`78 / 100`, `3.12 / 4`, `91.7%`), sometimes inside the same
/// card, which made two adjacent numbers look like they disagreed.
///
/// The ONLY figure that may still be published on 0..4 is an individual
/// standard's score, because there it is not a score out of anything — it is
/// the rubric LEVEL, and it is always rendered next to its Arabic word
/// ("4 متميز"). See <see cref="IsRubricLevel"/> for the documented exception.
/// </summary>
public static class ScoreScale
{
    /// <summary>Highest raw score a single standard can be given.</summary>
    public const decimal RawMaximum = 4m;

    /// <summary>The published maximum. Every human-facing total is out of this.</summary>
    public const decimal PublishedMaximum = 100m;

    private const decimal Factor = PublishedMaximum / RawMaximum; // 25

    /// <summary>
    /// Converts a raw 0..4 average to the published 0..100 scale, rounded to one
    /// decimal. Out-of-range input is clamped rather than thrown: a report must
    /// never fail to render because a legacy snapshot holds an odd value.
    /// </summary>
    public static decimal ToPublished(decimal rawOutOfFour) =>
        Math.Round(Math.Clamp(rawOutOfFour, 0m, RawMaximum) * Factor, 1, MidpointRounding.AwayFromZero);

    /// <inheritdoc cref="ToPublished(decimal)"/>
    public static decimal? ToPublished(decimal? rawOutOfFour) =>
        rawOutOfFour is null ? null : ToPublished(rawOutOfFour.Value);

    /// <summary>
    /// Published figure formatted for display: <c>"91.7"</c> — one decimal, and
    /// no trailing ".0" on a whole number so a column of scores stays readable.
    /// </summary>
    public static string Format(decimal rawOutOfFour) =>
        ToPublished(rawOutOfFour).ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>Published figure, or an em dash when there is nothing to show.</summary>
    public static string Format(decimal? rawOutOfFour) =>
        rawOutOfFour is null ? "—" : Format(rawOutOfFour.Value);

    /// <summary>
    /// Published figure with its scale stated: <c>"91.7 / 100"</c>. Used for the
    /// headline figures where the reader has no column header to lean on.
    /// </summary>
    public static string FormatWithMaximum(decimal rawOutOfFour) =>
        $"{Format(rawOutOfFour)} / {PublishedMaximum:0}";

    /// <inheritdoc cref="FormatWithMaximum(decimal)"/>
    public static string FormatWithMaximum(decimal? rawOutOfFour) =>
        rawOutOfFour is null ? "—" : FormatWithMaximum(rawOutOfFour.Value);

    /// <summary>
    /// Publishes an already-0..100 pair (e.g. <c>TotalScore</c> /
    /// <c>MaximumScore</c> from the snapshot, or a raw points total) as
    /// <c>"78 / 100"</c>, normalising the maximum so the denominator is always
    /// the published one even for older snapshots that stored points.
    /// </summary>
    public static string FormatTotal(decimal total, decimal maximum)
    {
        if (maximum <= 0m)
            return $"— / {PublishedMaximum:0}";

        var normalised = Math.Round(Math.Clamp(total / maximum, 0m, 1m) * PublishedMaximum, 1,
            MidpointRounding.AwayFromZero);
        return $"{normalised.ToString("0.#", CultureInfo.InvariantCulture)} / {PublishedMaximum:0}";
    }

    /// <summary>
    /// Documented exception to D-UI-1: an individual standard's rubric level.
    /// Kept as a method rather than a comment so the exception is greppable and
    /// a future reader can see it was deliberate.
    /// </summary>
    public static string FormatRubricLevel(int? rawScore) =>
        rawScore?.ToString(CultureInfo.InvariantCulture) ?? "—";

    /// <summary>True when a figure is a per-standard rubric level, not a score.</summary>
    public static bool IsRubricLevel(decimal value) => value >= 0m && value <= RawMaximum;
}
