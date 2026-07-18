using AlFalah.Application.DTOs.Attendance;
using AlFalah.Application.Interfaces;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlFalah.Infrastructure.Services;

public sealed class AttendancePdfService : IAttendancePdfService
{
    private static int _fontsRegistered;
    private static readonly object FontLock = new();

    public Task<byte[]> RenderAsync(IReadOnlyList<AttendanceRecordItemDto> records, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Logo.png");
        var logo = File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null;
        var document = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontFamily("Arial").FontSize(10).DirectionFromRightToLeft().FontColor("#1E293B"));
            page.Header().Element(header => ComposeHeader(header, logo, records.Count));
            page.Content().PaddingTop(16).Element(content => ComposeTable(content, records));
            page.Footer().AlignCenter().Text(text => text.Span("سجل حضور الموظفين • مدارس الفلاح النموذجية").FontFamily("Amiri").FontSize(8).FontColor("#64748B"));
        }));
        return Task.FromResult(document.GeneratePdf());
    }

    private static void ComposeHeader(IContainer container, byte[]? logo, int count)
    {
        container.BorderBottom(2).BorderColor("#0F7132").PaddingBottom(10).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("سجل حضور الموظفين").FontFamily("Arial").FontSize(20).Bold().FontColor("#075E54");
                column.Item().Text($"عدد السجلات: {count} • تاريخ التصدير: {DateTime.Now:yyyy-MM-dd}").FontFamily("Arial").FontSize(9).FontColor("#64748B");
            });
            if (logo is not null) row.ConstantItem(58).Height(48).Image(logo).FitArea();
        });
    }

    private static void ComposeTable(IContainer container, IReadOnlyList<AttendanceRecordItemDto> records)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.2f); columns.RelativeColumn(1.1f); columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.3f); columns.RelativeColumn(2.4f);
            });
            // QuestPDF lays out table columns physically from left to right.
            // Put the desired RTL order in reverse so the visual right side is:
            // الموظف، الحالة، الدور، التاريخ، الملاحظة.
            HeaderCell(table.Cell(), "ملاحظة"); HeaderCell(table.Cell(), "التاريخ"); HeaderCell(table.Cell(), "الدور");
            HeaderCell(table.Cell(), "الحالة"); HeaderCell(table.Cell(), "الموظف");
            foreach (var item in records)
            {
                BodyCell(table.Cell(), string.IsNullOrWhiteSpace(item.Notes) ? "—" : item.Notes!);
                BodyCell(table.Cell(), item.Date.ToString("yyyy-MM-dd"));
                BodyCell(table.Cell(), item.Role);
                BodyCell(table.Cell(), StatusLabel(item.Status));
                BodyCell(table.Cell(), item.FullName);
            }
        });
    }

    private static void HeaderCell(IContainer cell, string value) => cell.Background("#DFF1E9").Border(1).BorderColor("#B8D6C9").Padding(8).AlignCenter().Text(value).FontFamily("Arial").Bold().FontColor("#075E54");
    private static void BodyCell(IContainer cell, string value) => cell.Border(1).BorderColor("#D5E1DC").Padding(7).AlignCenter().Text(value).FontFamily("Arial");
    private static string StatusLabel(Domain.Enums.AttendanceStatus status) => status switch { Domain.Enums.AttendanceStatus.Present => "حاضر", Domain.Enums.AttendanceStatus.Absent => "غائب", Domain.Enums.AttendanceStatus.Excused => "غائب بعذر", _ => "—" };

    private static void EnsureFonts()
    {
        if (Interlocked.CompareExchange(ref _fontsRegistered, 1, 0) != 0) return;
        lock (FontLock)
        {
            var regular = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Amiri-Regular.ttf");
            var bold = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Amiri-Bold.ttf");
            if (File.Exists(regular)) using (var stream = File.OpenRead(regular)) FontManager.RegisterFont(stream);
            if (File.Exists(bold)) using (var stream = File.OpenRead(bold)) FontManager.RegisterFont(stream);
        }
    }
}
