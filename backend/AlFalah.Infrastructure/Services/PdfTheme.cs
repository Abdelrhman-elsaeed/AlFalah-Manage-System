using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// One shared visual language for every QuestPDF document the system prints
/// (visit reports, dashboards, attendance sheets).
///
/// The printed output is an official school document, so the rules here are
/// deliberately strict:
///   • Arabic always renders through the embedded Amiri font. Never rely on a
///     system font — the same PDF must look identical on Windows and Linux.
///   • Tables always have a coloured header band, ruled cells and consistent
///     padding. A bare <c>Cell().Text(...)</c> produces the unreadable
///     "words floating with no columns" look and is not used anywhere.
///   • A table with no rows prints an explicit "no data" line instead of a
///     lone header, so a section never looks truncated.
/// </summary>
internal static class PdfTheme
{
    /// <summary>Family name exposed by both Amiri TTFs.</summary>
    public const string Font = "Amiri";

    public const string Brand = "#0F7132";
    public const string BrandDark = "#0B5426";
    public const string BrandTint = "#EAF5EE";
    public const string Gold = "#D4AF37";
    public const string Text = "#1E293B";
    public const string Muted = "#64748B";
    public const string Border = "#D9E1DC";
    public const string BorderStrong = "#B9C7BF";
    public const string White = "#FFFFFF";
    public const string ZebraRow = "#F7FAF8";

    public const float BorderWidth = 0.6f;

    // ─── Fonts ───────────────────────────────────────────────────────────────

    private static int _fontsRegistered;

    /// <summary>
    /// Registers the embedded Amiri faces exactly once per process. Safe to
    /// call from every document build and from several threads at once.
    /// </summary>
    public static void EnsureFonts()
    {
        if (Interlocked.CompareExchange(ref _fontsRegistered, 1, 0) != 0)
            return;

        foreach (var file in new[] { "Amiri-Regular.ttf", "Amiri-Bold.ttf" })
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", file);
                if (!File.Exists(path)) continue;
                using var stream = File.OpenRead(path);
                FontManager.RegisterFont(stream);
            }
            catch
            {
                // Branding is best-effort: a missing font must never stop a
                // report from being produced.
            }
        }
    }

    // ─── Sections ────────────────────────────────────────────────────────────

    /// <summary>
    /// A titled white card. Every report section is wrapped in one of these so
    /// the page reads as a stack of discrete blocks rather than a run of loose
    /// paragraphs.
    /// </summary>
    public static void SectionCard(
        IContainer container,
        string title,
        Action<ColumnDescriptor> body,
        string? accent = null)
    {
        container
            .Background(White)
            .Border(BorderWidth).BorderColor(Border)
            .Column(card =>
            {
                card.Item()
                    .Background(BrandTint)
                    .BorderBottom(BorderWidth).BorderColor(Border)
                    .PaddingVertical(5).PaddingHorizontal(9)
                    .Row(row =>
                    {
                        row.ConstantItem(3).Background(accent ?? Gold);
                        row.RelativeItem().PaddingHorizontal(6).AlignRight()
                            .Text(title)
                            .FontFamily(Font).FontSize(11).Bold().FontColor(BrandDark);
                    });

                card.Item().Padding(8).Column(body);
            });
    }

    /// <summary>
    /// Just the title band, with no body box. Used when a section's content is
    /// a list of independently-paginated cards: wrapping those in a single
    /// bordered card makes the border split mid-page and leaves a stray rule
    /// hanging at the sheet edge.
    /// </summary>
    public static void SectionHeading(IContainer container, string title, string? accent = null)
    {
        container
            .Background(BrandTint)
            .Border(BorderWidth).BorderColor(Border)
            .PaddingVertical(5).PaddingHorizontal(9)
            .Row(row =>
            {
                row.ConstantItem(3).Background(accent ?? Gold);
                row.RelativeItem().PaddingHorizontal(6).AlignRight()
                    .Text(title)
                    .FontFamily(Font).FontSize(11).Bold().FontColor(BrandDark);
            });
    }

    /// <summary>
    /// Rows a section may hold and still be forced onto a single sheet.
    /// Above this it is allowed to split, because <c>ShowEntire</c> on content
    /// that cannot fit a whole page raises a layout exception.
    /// </summary>
    private const int MaxRowsToKeepTogether = 14;

    /// <summary>
    /// A titled table section. Short tables are kept whole so the heading is
    /// never stranded at the foot of a page with its table starting on the
    /// next one; long tables are allowed to flow, and their repeating
    /// <c>table.Header()</c> keeps the column labels visible on every sheet.
    /// </summary>
    public static void TableSection(
        IContainer container,
        string title,
        int rowCount,
        Action<TableDescriptor> buildTable,
        string? accent = null,
        string? caption = null)
    {
        var keepTogether = rowCount <= MaxRowsToKeepTogether;
        var host = keepTogether ? container.ShowEntire() : container;

        host.Column(col =>
        {
            col.Item().Element(c => SectionHeading(c, title, accent));
            col.Item().Table(buildTable);
            if (!string.IsNullOrWhiteSpace(caption))
                col.Item().PaddingTop(3).Element(c => Caption(c, caption!));
        });
    }

    /// <summary>Small caption under a section title (units, scope, filters…).</summary>
    public static void Caption(IContainer container, string text) =>
        container.AlignRight().Text(text).FontFamily(Font).FontSize(8).FontColor(Muted);

    // ─── RTL row helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Lays a sequence of equal-width cells out in READING order on an RTL
    /// sheet: <paramref name="cells"/>[0] lands on the visual RIGHT.
    ///
    /// QuestPDF places Row children left-to-right physically no matter what the
    /// page's text direction is, so a plain <c>foreach … row.RelativeItem()</c>
    /// puts the FIRST item on the far left and an Arabic reader — who starts at
    /// the right — reads the strip backwards. Every KPI/tile strip in the
    /// dashboard export had that defect.
    ///
    /// <paramref name="slots"/> pads the row out to a fixed width so a partial
    /// final row stays column-aligned with the rows above it. The padding is
    /// emitted FIRST (visual left / the RTL end), because a gap at the visual
    /// right is a gap where the reader starts and reads as a missing tile.
    /// </summary>
    public static void RtlRow(
        RowDescriptor row,
        IReadOnlyList<Action<IContainer>> cells,
        int? slots = null,
        float spacing = 6f)
    {
        row.Spacing(spacing);

        var width = Math.Max(slots ?? cells.Count, cells.Count);
        for (var i = cells.Count; i < width; i++)
            row.RelativeItem();

        for (var i = cells.Count - 1; i >= 0; i--)
        {
            var render = cells[i];
            row.RelativeItem().Element(render);
        }
    }

    /// <summary>
    /// A horizontal progress bar that fills from the RTL start edge (the visual
    /// right). <paramref name="fraction"/> is clamped to 0..1 and is the SAME
    /// quantity the caller prints beside the bar — the export previously filled
    /// <c>count / max</c> while printing <c>count / total</c>, so a bar at a
    /// third sat next to the text "16.7%".
    /// </summary>
    public static void ProgressBar(IContainer container, double fraction, string? fill = null)
    {
        var filled = (float)Math.Clamp(fraction, 0d, 1d);
        var empty = 1f - filled;

        container.Height(9).Border(BorderWidth).BorderColor(Border).Row(bar =>
        {
            // Physical L→R: [remainder][filled] → the fill sits on the right.
            if (empty > 0f) bar.RelativeItem(empty).Background("#EDF3EF");
            if (filled > 0f) bar.RelativeItem(filled).Background(fill ?? Brand);
        });
    }

    /// <summary>
    /// A label/value detail grid that fills its container.
    ///
    /// The previous shape — a two-column table with a relative value column —
    /// gave the value all the leftover width of a landscape sheet, so every
    /// value printed floating near the middle of the page with a void between
    /// it and its own label. Pairs are laid out two-per-row instead, exactly
    /// like the visit report's identification card: the label always holds the
    /// RTL start edge of its half and the value sits immediately beside it.
    /// </summary>
    public static void DetailGrid(
        IContainer container,
        IReadOnlyList<(string Label, string Value, string? Color)> rows,
        int pairsPerRow = 2)
    {
        if (rows.Count == 0) return;

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                // Physical L→R per pair: [value][label]; reading RTL the label
                // of the RIGHT-most pair is what the eye meets first.
                for (var i = 0; i < pairsPerRow; i++)
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(104);
                }
            });

            // QuestPDF fills cells left-to-right, so a row's pairs are emitted
            // in reverse to put rows[i] on the visual right.
            for (var i = 0; i < rows.Count; i += pairsPerRow)
            {
                var zebra = (i / pairsPerRow) % 2 == 1;
                var take = Math.Min(pairsPerRow, rows.Count - i);

                for (var slot = pairsPerRow - 1; slot >= 0; slot--)
                {
                    if (slot >= take)
                    {
                        // Pad the short final row so the grid stays rectangular.
                        BodyCell(table.Cell(), string.Empty, zebra: zebra);
                        BodyCell(table.Cell(), string.Empty, zebra: zebra);
                        continue;
                    }

                    var (label, value, color) = rows[i + slot];
                    BodyCell(table.Cell(), value, zebra: zebra, strong: true, color: color);
                    BodyCell(table.Cell(), label, zebra: zebra, color: Muted);
                }
            }
        });
    }

    // ─── Table cells ─────────────────────────────────────────────────────────

    /// <summary>
    /// Column alignment intent. Arabic labels read right-to-left and are
    /// aligned to the start of the column; figures are centred so that a whole
    /// numeric column lines up under its own header.
    /// </summary>
    public enum CellAlign { Start, Center }

    public static void HeaderCell(IContainer container, string text, CellAlign align = CellAlign.Start)
    {
        var cell = container
            .Background(Brand)
            .BorderRight(BorderWidth).BorderColor(BrandDark)
            .PaddingVertical(5).PaddingHorizontal(6);

        (align == CellAlign.Center ? cell.AlignCenter() : cell.AlignRight())
            .Text(text)
            .FontFamily(Font).FontSize(8.5f).Bold().FontColor(White);
    }

    public static void BodyCell(
        IContainer container,
        string text,
        CellAlign align = CellAlign.Start,
        bool zebra = false,
        bool strong = false,
        string? color = null)
    {
        var cell = container
            .Background(zebra ? ZebraRow : White)
            .BorderBottom(BorderWidth).BorderRight(BorderWidth).BorderColor(Border)
            .PaddingVertical(4).PaddingHorizontal(6)
            .AlignMiddle();

        var styled = align == CellAlign.Center ? cell.AlignCenter() : cell.AlignRight();

        var span = styled.Text(text)
            .FontFamily(Font)
            .FontSize(9)
            .FontColor(color ?? Text);

        if (strong) span.Bold();
    }

    /// <summary>
    /// Fills a whole table row with a single "no data" message so an empty
    /// section never prints as a naked header band.
    /// </summary>
    public static void EmptyRow(TableDescriptor table, int columnCount, string message = "لا توجد بيانات لهذه الفترة.")
    {
        table.Cell().ColumnSpan((uint)columnCount)
            .Background(White)
            .BorderBottom(BorderWidth).BorderColor(Border)
            .PaddingVertical(10)
            .AlignCenter()
            .Text(message)
            .FontFamily(Font).FontSize(9).FontColor(Muted);
    }

    /// <summary>Standalone "no data" note for non-tabular sections.</summary>
    public static void EmptyNote(IContainer container, string message = "لا توجد بيانات لهذه الفترة.") =>
        container.PaddingVertical(8).AlignCenter()
            .Text(message).FontFamily(Font).FontSize(9).FontColor(Muted);

    // ─── Score colours (shared with the frontend rubric legend) ──────────────

    public static string ScoreColor(decimal? averageOutOfFour) => averageOutOfFour switch
    {
        null => Muted,
        >= 3.5m => "#15803D",
        >= 3.0m => "#22C55E",
        >= 2.0m => Gold,
        >= 1.0m => "#EA8A0B",
        _ => "#DC2626"
    };
}
