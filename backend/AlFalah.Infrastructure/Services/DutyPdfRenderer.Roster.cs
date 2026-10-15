using System.Globalization;
using AlFalah.Application.Duty;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlFalah.Infrastructure.Services;

public sealed partial class DutyPdfRenderer
{
    public string RosterVersion => "school-duty/roster-source-2026-v5";
    private const string RosterInk = "#161616", RosterRule = "#606060";

    public DutyPdf Roster(DutyRosterSnapshot snapshot)
    {
        PdfTheme.EnsureFonts();
        var weeks = snapshot.Rows.OrderBy(x => x.Date)
            .GroupBy(x => (x.Date.DayNumber - snapshot.From.DayNumber + (int)snapshot.From.DayOfWeek) / 7)
            .Select(x => (Number: x.Key + 1, Rows: (IReadOnlyList<DutyRosterRow>)x.ToArray())).ToArray();
        var logo = snapshot.Logo is { State: "Frozen", Data: not null }
            ? Convert.FromBase64String(snapshot.Logo.Data.Split(',').Last()) : null;
        if (logo is not null && DutyDocumentIntegrity.Hash(logo) != snapshot.Logo!.Hash) throw DutyDocumentIntegrity.Error();
        // Keep a physical sheet indivisible. Letting the paired columns paginate
        // independently can clip repeated table content in QuestPDF continuations.
        // Retry with fewer whole weeks, preserving font sizes and every row.
        for (var capacity = 9; capacity >= 1; capacity--)
        {
            try
            {
                var pdf = RosterPages(snapshot, weeks, logo, capacity);
                return new(pdf, $"جدول المناوبة - {DutyDocumentIntegrity.SafeName(snapshot.Header.Year)} - {Term(snapshot.Header.Semester)}.pdf", DutyDocumentIntegrity.Hash(pdf), RosterVersion);
            }
            catch (DocumentLayoutException) when (capacity > 1) { }
        }
        throw DutyDocumentIntegrity.Error();
    }

    private static byte[] RosterPages(DutyRosterSnapshot snapshot, (int Number, IReadOnlyList<DutyRosterRow> Rows)[] weeks, byte[]? logo, int capacity)
    {
        // Each physical sheet has its own layout pass. Merging the independently
        // composed sheets avoids cross-sheet cache reuse in the pinned PDF engine.
        var sheets = weeks.Length == 0 ? new[] { weeks } : weeks.Chunk(capacity * 2);
        var documents = sheets.Select(sheet => Document.Create(document =>
            {
                var split = (sheet.Length + 1) / 2;
                document.Page(page =>
                {
                    page.Size(PageSizes.A4); page.MarginHorizontal(26); page.MarginVertical(24); page.ContentFromRightToLeft();
                    page.DefaultTextStyle(x => x.FontFamily(PdfTheme.Font).FontSize(8.4f).FontColor(RosterInk).DirectionFromRightToLeft().LineHeight(1));
                    page.Header().Element(x => RosterHeading(x, snapshot.Header, logo));
                    page.Content().ShowEntire().Table(columns =>
                    {
                        columns.ColumnsDefinition(x => { x.RelativeColumn(); x.RelativeColumn(); });
                        RosterColumn(columns.Cell(), sheet.Take(split));
                        RosterColumn(columns.Cell(), sheet.Skip(split));
                    });
                    page.Footer().PaddingTop(9).Column(footer =>
                    {
                        footer.Item().Row(row =>
                        {
                            row.RelativeItem().Text("وكيل الشؤون التعليمية: __________   التوقيع: __________").FontSize(9);
                            row.RelativeItem().AlignLeft().Text($"مدير المدرسة: {snapshot.Header.Manager}   التوقيع: __________").FontSize(9);
                        });
                        if (!string.IsNullOrWhiteSpace(snapshot.Header.Footer)) footer.Item().PaddingTop(3).Text(snapshot.Header.Footer).FontSize(7);
                        footer.Item().PaddingTop(3).AlignCenter().ContentFromLeftToRight().DefaultTextStyle(x => x.DirectionFromLeftToRight().FontSize(7))
                            .Text(t => { t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
                    });
                });
            })).ToArray();
        return Document.Merge(documents).UseContinuousPageNumbers().GeneratePdf();
    }

    private static void RosterColumn(IContainer container, IEnumerable<(int Number, IReadOnlyList<DutyRosterRow> Rows)> weeks) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(x => x.RelativeColumn());
            foreach (var week in weeks) table.Cell().Element(x => RosterWeek(x, week.Number, week.Rows));
        });

    private static void RosterHeading(IContainer container, DutyExecutionHeader header, byte[]? logo)
    {
        container.PaddingBottom(2).Column(column =>
        {
            column.Item().MinHeight(53).Row(row =>
            {
                row.RelativeItem().AlignRight().Text(header.Header ?? "").FontSize(10).LineHeight(1.1f);
                row.ConstantItem(104).Height(48).PaddingHorizontal(12).Element(x => { if (logo is not null) x.Image(logo).FitArea(); });
                row.RelativeItem().AlignRight().Text($"المدرسة: {header.School}\n{header.City}").FontSize(11).LineHeight(1.2f);
            });
            column.Item().Table(title =>
            {
                title.ColumnsDefinition(columns => { columns.RelativeColumn(3); columns.RelativeColumn(1); });
                RosterCell(title.Cell()).PaddingVertical(1).Text("جدول المناوبة").FontSize(15).Bold().AlignCenter();
                RosterCell(title.Cell()).Text("QF71-B-2-2").FontSize(12).Bold().DirectionFromLeftToRight().AlignCenter();
            });
            column.Item().PaddingVertical(2).AlignCenter().Text(t =>
            {
                t.Span("العام الدراسي: ");
                var year = header.Year.StartsWith("العام الدراسي", StringComparison.Ordinal) ? header.Year["العام الدراسي".Length..].Trim(' ', ':', '：') : header.Year;
                t.Span("\u2066" + year + "\u2069").DirectionFromLeftToRight();
                t.Span($"     الفصل الدراسي: ({Term(header.Semester)})");
                t.DefaultTextStyle(x => x.FontSize(9));
            });
        });
    }

    private static IContainer RosterCell(IContainer container) => container.Border(.4f).BorderColor(RosterRule)
        .PaddingHorizontal(2).PaddingVertical(.3f).AlignMiddle();

    private static void RosterWeek(IContainer container, int number, IReadOnlyList<DutyRosterRow> rows)
    {
        container.ShowEntire().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(22); columns.ConstantColumn(31); columns.ConstantColumn(47);
                columns.RelativeColumn(3.1f); columns.RelativeColumn(1.4f); columns.RelativeColumn(1.5f);
            });
            foreach (var label in new[] { "أسبوع", "اليوم", "التاريخ", "الاسم", "التوقيع", "ملاحظات" })
                RosterCell(table.Cell()).MinHeight(10).Text(label).FontSize(8).AlignCenter();
            RosterCell(table.Cell().RowSpan((uint)rows.Count)).Text(number.ToString(CultureInfo.InvariantCulture)).FontSize(15).AlignCenter();
            foreach (var row in rows)
            {
                RosterCell(table.Cell()).MinHeight(11).Text(Days[(int)row.Date.DayOfWeek]).FontSize(8).AlignCenter();
                RosterCell(table.Cell()).Text(row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).FontSize(7.4f).DirectionFromLeftToRight().AlignCenter();
                RosterCell(table.Cell()).Text(row.Teacher).FontSize(8.5f).AlignCenter();
                RosterCell(table.Cell()).Text("");
                RosterCell(table.Cell()).Text(row.Notes).FontSize(8).AlignCenter();
            }
        });
    }
}
