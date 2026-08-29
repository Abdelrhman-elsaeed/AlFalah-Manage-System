using AlFalah.Application.DTOs.Timetables;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlFalah.Infrastructure.Services;

public sealed class SchoolTimetableDocumentService : ISchoolTimetableDocumentService
{
    private static readonly TimetableDay[] PhysicalDays =
    {
        TimetableDay.Thursday,
        TimetableDay.Wednesday,
        TimetableDay.Tuesday,
        TimetableDay.Monday,
        TimetableDay.Sunday,
        TimetableDay.Saturday
    };

    public TimetableFileDto BuildPdf(
        SchoolTimetableDto timetable,
        TimetableCatalogDto catalog,
        TimetablePdfColorMode colorMode)
    {
        PdfTheme.EnsureFonts();
        var palette = PdfPalette.For(colorMode);
        var entries = timetable.Entries
            .GroupBy(x => (x.InstructorProfileId, x.Day, x.Period))
            .ToDictionary(g => g.Key, g => g.Last());
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Logo.png");
        var logo = colorMode == TimetablePdfColorMode.Color && File.Exists(logoPath)
            ? File.ReadAllBytes(logoPath)
            : null;

        var document = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(6);
            page.DefaultTextStyle(style => style.FontFamily(PdfTheme.Font).FontSize(4.5f)
                .DirectionFromRightToLeft().FontColor(palette.BodyText));
            page.Header().Height(30).Element(header => ComposeHeader(header, timetable, catalog, logo, palette));
            page.Content().PaddingTop(2).Element(content => ComposeGrid(content, catalog.Teachers, entries, palette));
            page.Footer().Height(9).AlignCenter().Text(
                $"نسخة رقم {timetable.Revision} • آخر تحديث {timetable.UpdatedAt:yyyy-MM-dd HH:mm} • A4 أفقي • {palette.Label}")
                .FontSize(3.6f).FontColor(palette.Muted);
        }));

        var safeTitle = SanitizeFileName(timetable.Title);
        return new TimetableFileDto(
            document.GeneratePdf(),
            "application/pdf",
            $"{safeTitle}-A4-{palette.FileSuffix}.pdf");
    }

    public TimetableFileDto BuildImportTemplate(SchoolTimetableDto timetable, TimetableCatalogDto catalog)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("الجدول");
        sheet.RightToLeft = true;
        sheet.SheetView.FreezeRows(1);
        sheet.SheetView.FreezeColumns(2);
        sheet.Cell(1, 1).Value = "الرقم الوظيفي";
        sheet.Cell(1, 2).Value = "اسم المعلم";
        var column = 3;
        foreach (var day in Enum.GetValues<TimetableDay>())
        foreach (var period in Enumerable.Range(1, 8))
            sheet.Cell(1, column++).Value = $"{SchoolTimetableService.DayLabel(day)} - {period}";

        var entryLookup = timetable.Entries.ToDictionary(x => (x.InstructorProfileId, x.Day, x.Period));
        var row = 2;
        foreach (var teacher in catalog.Teachers)
        {
            sheet.Cell(row, 1).Value = teacher.EmployeeNumber ?? string.Empty;
            sheet.Cell(row, 2).Value = teacher.FullName;
            column = 3;
            foreach (var day in Enum.GetValues<TimetableDay>())
            foreach (var period in Enumerable.Range(1, 8))
            {
                if (entryLookup.TryGetValue((teacher.InstructorProfileId, day, (byte)period), out var entry))
                {
                    sheet.Cell(row, column).Value = entry.EntryType == TimetableEntryType.Standby
                        ? "منتظر"
                        : $"{entry.ClassLabel} | {entry.Subject}";
                }
                column++;
            }
            row++;
        }

        var used = sheet.Range(1, 1, Math.Max(2, row - 1), 50);
        used.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        used.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        used.Style.Alignment.WrapText = true;
        used.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        used.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        sheet.Range(1, 1, 1, 50).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, 50).Style.Fill.BackgroundColor = XLColor.FromHtml("#DFF1E9");
        sheet.Column(1).Width = 16;
        sheet.Column(2).Width = 28;
        for (var index = 3; index <= 50; index++) sheet.Column(index).Width = 16;
        sheet.Rows().Height = 28;

        var notes = workbook.Worksheets.Add("تعليمات");
        notes.RightToLeft = true;
        notes.Cell("A1").Value = "تعليمات الاستيراد";
        notes.Cell("A1").Style.Font.Bold = true;
        notes.Cell("A2").Value = "اكتب الحصة داخل الخانة بهذه الصيغة: الفصل | المادة";
        notes.Cell("A3").Value = "اكتب منتظر للحصة الاحتياطية، واترك الخانة فارغة إذا لم توجد حصة.";
        notes.Cell("A4").Value = "لا تعدّل أول عمودين حتى يستطيع النظام مطابقة المعلمين بدقة.";
        notes.Column(1).Width = 90;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new TimetableFileDto(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"نموذج-{SanitizeFileName(timetable.Title)}.xlsx");
    }

    public TimetableImportRows ParseImport(Stream stream, TimetableCatalogDto catalog)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        var employeeLookup = catalog.Teachers
            .Where(x => !string.IsNullOrWhiteSpace(x.EmployeeNumber))
            .GroupBy(x => x.EmployeeNumber!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);
        var nameLookup = catalog.Teachers
            .GroupBy(x => x.FullName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var importedRows = new List<TimetableImportedRow>();

        for (var row = 2; row <= lastRow; row++)
        {
            var employeeNumber = sheet.Cell(row, 1).GetString().Trim();
            var fullName = sheet.Cell(row, 2).GetString().Trim();
            if (employeeNumber.Length == 0 && fullName.Length == 0) continue;
            TimetableTeacherDto? teacher = null;
            if (employeeNumber.Length > 0) employeeLookup.TryGetValue(employeeNumber, out teacher);
            if (teacher is null && fullName.Length > 0) nameLookup.TryGetValue(fullName, out teacher);
            if (teacher is null)
            {
                warnings.Add($"تم تجاهل الصف {row}: تعذر مطابقة المعلم «{fullName}».");
                continue;
            }

            var entries = new List<SaveTimetableEntryRequest>();
            var column = 3;
            foreach (var day in Enum.GetValues<TimetableDay>())
            foreach (var period in Enumerable.Range(1, 8))
            {
                var value = sheet.Cell(row, column++).GetString().Trim();
                if (value.Length == 0) continue;
                if (string.Equals(value, "منتظر", StringComparison.OrdinalIgnoreCase))
                {
                    entries.Add(new SaveTimetableEntryRequest(teacher.InstructorProfileId, day, (byte)period, TimetableEntryType.Standby, null, null));
                    continue;
                }

                var separator = value.IndexOf('|');
                if (separator < 0) separator = value.IndexOf('\n');
                if (separator <= 0 || separator >= value.Length - 1)
                {
                    warnings.Add($"تم تجاهل خلية {sheet.Cell(row, column - 1).Address}: استخدم «الفصل | المادة» أو «منتظر».");
                    continue;
                }
                var classLabel = value[..separator].Trim();
                var subject = value[(separator + 1)..].Trim();
                entries.Add(new SaveTimetableEntryRequest(teacher.InstructorProfileId, day, (byte)period, TimetableEntryType.Lesson, classLabel, subject));
            }
            importedRows.Add(new TimetableImportedRow(teacher.InstructorProfileId, entries));
        }

        return new TimetableImportRows(importedRows, warnings);
    }

    private static void ComposeHeader(
        IContainer container,
        SchoolTimetableDto timetable,
        TimetableCatalogDto catalog,
        byte[]? logo,
        PdfPalette palette)
    {
        container.BorderBottom(1f).BorderColor(palette.Accent).PaddingBottom(2).Row(row =>
        {
            // Equal side columns keep the heading centered on the physical page,
            // regardless of whether the colored version includes a logo.
            row.ConstantItem(34);
            row.RelativeItem().AlignCenter().Column(column =>
            {
                column.Item().AlignCenter().Text(timetable.Title).FontSize(8f).Bold().FontColor(palette.HeadingText);
                column.Item().AlignCenter().Text($"{catalog.SchoolName} • {timetable.AcademicYearName} • {timetable.SemesterLabelAr}")
                    .FontSize(4.6f).SemiBold().FontColor(palette.Muted);
            });
            var logoItem = row.ConstantItem(34).Height(25);
            if (logo is not null) logoItem.Image(logo).FitArea();
        });
    }

    private static void ComposeGrid(
        IContainer container,
        IReadOnlyList<TimetableTeacherDto> teachers,
        IReadOnlyDictionary<(int InstructorProfileId, TimetableDay Day, byte Period), TimetableEntryDto> entries,
        PdfPalette palette)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                for (var index = 0; index < 48; index++) columns.RelativeColumn();
                columns.ConstantColumn(58);
            });

            table.Header(header =>
            {
                foreach (var day in PhysicalDays)
                    HeaderCell(header.Cell().ColumnSpan(8), SchoolTimetableService.DayLabel(day), palette, 5.2f);
                HeaderCell(header.Cell(), string.Empty, palette);

                foreach (var _ in PhysicalDays)
                for (var period = 8; period >= 1; period--)
                    HeaderCell(header.Cell(), period.ToString(), palette, 4.5f);
                HeaderCell(header.Cell(), "المعلم", palette, 5.2f);
            });

            var rowIndex = 0;
            foreach (var teacher in teachers)
            {
                foreach (var day in PhysicalDays)
                for (var period = 8; period >= 1; period--)
                {
                    entries.TryGetValue((teacher.InstructorProfileId, day, (byte)period), out var entry);
                    if (entry?.EntryType == TimetableEntryType.Standby)
                        BodyCell(table.Cell(), "منتظر", true, rowIndex, palette);
                    else if (entry is not null)
                        BodyCell(table.Cell(), $"{entry.ClassLabel}\n{entry.Subject}", false, rowIndex, palette);
                    else
                        BodyCell(table.Cell(), string.Empty, false, rowIndex, palette);
                }
                BodyCell(table.Cell(), teacher.FullName, false, rowIndex, palette, true, true);
                rowIndex++;
            }
        });
    }

    private static void HeaderCell(
        IContainer cell,
        string value,
        PdfPalette palette,
        float fontSize = 3.8f) =>
        cell.Background(palette.HeaderBackground).Border(0.55f).BorderColor(palette.BorderStrong)
            .MinHeight(10).PaddingVertical(1.1f).PaddingHorizontal(0.35f).AlignCenter().AlignMiddle()
            .Text(value).FontSize(fontSize).Bold().FontColor(palette.HeadingText);

    private static void BodyCell(
        IContainer cell,
        string value,
        bool standby,
        int rowIndex,
        PdfPalette palette,
        bool bold = false,
        bool alignRight = false)
    {
        var styled = cell
            .Background(standby ? palette.StandbyBackground : rowIndex % 2 == 0 ? palette.White : palette.ZebraRow)
            .Border(0.55f).BorderColor(palette.Border)
            .MinHeight(17).PaddingVertical(0.8f).PaddingHorizontal(0.25f).AlignMiddle();
        styled = alignRight
            ? styled.PaddingRight(1.6f).AlignRight()
            : styled.AlignCenter();
        var text = styled.Text(value).FontSize(standby ? 4.2f : 4.4f).Bold()
            .FontColor(standby ? palette.StandbyText : palette.BodyText);
        if (bold) text.Bold().FontSize(5f);
    }

    private sealed record PdfPalette(
        string Label,
        string FileSuffix,
        string Accent,
        string HeaderBackground,
        string HeadingText,
        string BodyText,
        string Muted,
        string Border,
        string BorderStrong,
        string ZebraRow,
        string StandbyBackground,
        string StandbyText,
        string White)
    {
        public static PdfPalette For(TimetablePdfColorMode colorMode) => colorMode switch
        {
            TimetablePdfColorMode.Color => new(
                "نسخة ملونة", "ملون", PdfTheme.Brand, PdfTheme.BrandTint, PdfTheme.BrandDark,
                PdfTheme.Text, PdfTheme.Muted, PdfTheme.Border, PdfTheme.BorderStrong, PdfTheme.ZebraRow,
                "#DDE6E1", "#34463D", PdfTheme.White),
            TimetablePdfColorMode.Monochrome => new(
                "نسخة أبيض وأسود", "أبيض-وأسود", "#333333", "#E7E7E7", "#111111",
                "#111111", "#555555", "#A0A0A0", "#6F6F6F", "#F5F5F5",
                "#D8D8D8", "#111111", "#FFFFFF"),
            _ => throw new ArgumentOutOfRangeException(nameof(colorMode), colorMode, null)
        };
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value.Where(character => !invalid.Contains(character)).ToArray()).Trim();
        return sanitized.Length == 0 ? "الجدول-المدرسي" : sanitized;
    }
}
