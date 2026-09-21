using System.Text;
using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlFalah.Infrastructure.Services;

public sealed class VisitV2DocumentService(ImageAssetLoader imageLoader) : IVisitV2DocumentService
{
    private static readonly string[] CsvHeaders =
    {
        "رقم الزيارة", "تاريخ الزيارة", "رقم الحصة", "المعلم المزار", "رقم الهوية",
        "المادة الدراسية", "الصف والفصل", "عنوان الدرس", "نوع الزيارة", "عدد الحضور",
        "عدد الغياب", "المشرف الزائر", "صفة المشرف", "الدرجة الكلية (من 100)", "مستوى التحقق",
        "درجة بيئة التعلم (%)", "درجة التدريس والتعلم (%)", "درجة تنمية المهارات (%)",
        "درجة التقويم (%)", "درجة سلوك المتعلمين (%)", "نقاط القوة", "مجالات التحسين"
    };

    public VisitV2CsvExportDto BuildCsv(IReadOnlyList<VisitV2CsvRow> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',', CsvHeaders.Select(Escape)));
        foreach (var row in rows)
        {
            var values = new object?[]
            {
                row.VisitId, row.VisitDate.ToString("yyyy-MM-dd"), row.ClassroomPeriod, row.InstructorName,
                row.EmployeeNumber, row.Subject, row.GradeClass, row.LessonTitle, row.VisitType,
                row.PresentCount, row.AbsentCount, row.EvaluatorName, row.EvaluatorRole, row.TotalScore,
                row.PerformanceLevelAr,
                row.DomainPercentages.GetValueOrDefault("بيئة التعلم"),
                row.DomainPercentages.GetValueOrDefault("التدريس والتعلم"),
                row.DomainPercentages.GetValueOrDefault("تنمية المهارات"),
                row.DomainPercentages.GetValueOrDefault("التقويم"),
                row.DomainPercentages.GetValueOrDefault("سلوك المتعلمين"),
                row.Strengths, row.ImprovementAreas
            };
            csv.AppendLine(string.Join(',', values.Select(v => Escape(v?.ToString() ?? string.Empty))));
        }

        var utf8 = new UTF8Encoding(true);
        var content = utf8.GetPreamble().Concat(utf8.GetBytes(csv.ToString())).ToArray();
        return new VisitV2CsvExportDto(content, $"زيارات-v2-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    public async Task<VisitV2PdfExportDto> BuildPdfAsync(
        VisitV2DetailDto visit,
        VisitV2PdfAssetSources assets,
        CancellationToken cancellationToken = default)
    {
        var logo = await imageLoader.TryLoadAsync(assets.LogoSource, cancellationToken: cancellationToken);
        if (!logo.HasValue || logo.Value.IsEmpty)
        {
            var defaultLogoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Logo.png");
            logo = await imageLoader.TryLoadAsync(defaultLogoPath, cancellationToken: cancellationToken);
        }
        var instructorSignature = await imageLoader.TryLoadAsync(assets.InstructorSignatureSource, cancellationToken: cancellationToken);
        var evaluatorSignature = assets.ShowEvaluatorSignature
            ? await imageLoader.TryLoadAsync(assets.EvaluatorSignatureSource, cancellationToken: cancellationToken) : null;
        var managerSignature = assets.ShowManagerSignature
            ? await imageLoader.TryLoadAsync(assets.ManagerSignatureSource, cancellationToken: cancellationToken) : null;
        PdfTheme.EnsureFonts();
        var bytes = Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(24);
            page.DefaultTextStyle(x => x.FontFamily(PdfTheme.Font).FontSize(9).FontColor(PdfTheme.Text));
            page.ContentFromRightToLeft();
            page.Header().Background(NormalizeColor(assets.PrimaryColor)).Padding(12).Row(header =>
            {
                header.RelativeItem().AlignMiddle().Column(text =>
                {
                    text.Item().AlignCenter().Text("تقرير الزيارة الصفية V2").Bold().FontSize(18).FontColor(Colors.White);
                    text.Item().AlignCenter().Text(assets.HeaderText).FontSize(11).FontColor("#EAF5EE");
                });
                if (logo.HasValue && !logo.Value.IsEmpty)
                    header.ConstantItem(58).Height(48).AlignMiddle().Image(logo.Value.Bytes).FitArea();
            });
            page.Content().PaddingVertical(12).Column(col =>
            {
                col.Spacing(10);
                col.Item().Element(c => PdfTheme.SectionCard(c, "بيانات الزيارة", body =>
                {
                    body.Item().Element(grid => PdfTheme.DetailGrid(grid, new[]
                    {
                        ("المعلم", visit.InstructorName, (string?)null),
                        ("المقيم", $"{visit.EvaluatorName} — {visit.EvaluatorRole}", (string?)null),
                        ("التاريخ", visit.VisitDate.ToString("yyyy-MM-dd"), (string?)null),
                        ("الحصة", visit.ClassroomPeriod.ToString(), (string?)null),
                        ("المادة", visit.Subject, (string?)null),
                        ("الصف والفصل", visit.GradeClass, (string?)null),
                        ("عنوان الدرس", visit.LessonTitle, (string?)null),
                        ("الفئة والتسلسل", $"{visit.VisitCategoryLabelAr} — {visit.VisitSequenceLabelAr}", (string?)null),
                        ("الحضور", visit.PresentCount.ToString(), (string?)null),
                        ("الغياب", visit.AbsentCount.ToString(), (string?)null),
                    }));
                }));

                if (visit.Analysis != null)
                {
                    col.Item().Element(c => PdfTheme.SectionCard(c, "نتيجة التقييم", body =>
                    {
                        body.Item().AlignCenter().Text($"{visit.Analysis.TotalScore} / {visit.Analysis.MaximumScore}")
                            .Bold().FontSize(24).FontColor(PdfTheme.Brand);
                        body.Item().AlignCenter().Text($"{visit.Analysis.OverallPercentage}% — {visit.Analysis.PerformanceLevelAr}")
                            .Bold().FontSize(12);
                        body.Item().PaddingTop(6).Row(row =>
                        {
                            var cells = visit.Analysis.Domains.Select(domain => (Action<IContainer>)(cell =>
                                cell.Border(0.5f).BorderColor(PdfTheme.Border).Padding(5).AlignCenter().Column(domainCol =>
                                {
                                    domainCol.Item().Text(domain.DomainNameAr).Bold().FontSize(8);
                                    domainCol.Item().Text($"{domain.Percentage}%").Bold().FontSize(12).FontColor(PdfTheme.Brand);
                                }))).ToList();
                            PdfTheme.RtlRow(row, cells);
                        });
                    }));
                }

                foreach (var domain in visit.Domains)
                {
                    col.Item().Element(c => PdfTheme.TableSection(c, domain.NameAr, domain.Standards.Count, table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(38);
                            columns.RelativeColumn();
                            columns.ConstantColumn(42);
                        });
                        table.Header(header =>
                        {
                            PdfTheme.HeaderCell(header.Cell(), "الدرجة", PdfTheme.CellAlign.Center);
                            PdfTheme.HeaderCell(header.Cell(), "المعيار");
                            PdfTheme.HeaderCell(header.Cell(), "الرمز", PdfTheme.CellAlign.Center);
                        });
                        var index = 0;
                        foreach (var standard in domain.Standards)
                        {
                            var observed = standard.Indicators.Where(i => i.IsObserved).Select(i => "✓ " + i.TextAr);
                            var text = standard.TextAr +
                                       (observed.Any() ? "\n" + string.Join("\n", observed) : string.Empty) +
                                       (!string.IsNullOrWhiteSpace(standard.EvidenceNote) ? "\nشواهد: " + standard.EvidenceNote : string.Empty);
                            PdfTheme.BodyCell(table.Cell(), standard.Score.ToString(), PdfTheme.CellAlign.Center, index % 2 == 1, true);
                            PdfTheme.BodyCell(table.Cell(), text, zebra: index % 2 == 1);
                            PdfTheme.BodyCell(table.Cell(), standard.Code, PdfTheme.CellAlign.Center, index % 2 == 1);
                            index++;
                        }
                    }));
                }

                if (visit.Treatments.Count > 0)
                    col.Item().Element(c => PdfTheme.SectionCard(c, "الخطة العلاجية", body =>
                    {
                        foreach (var item in visit.Treatments)
                        {
                            body.Item().PaddingBottom(6).BorderBottom(0.5f).BorderColor(PdfTheme.Border).Column(t =>
                            {
                                t.Item().Text(item.DomainNameAr).Bold().FontColor(PdfTheme.Brand);
                                t.Item().Text("الهدف: " + item.Goal);
                                t.Item().Text("الإجراءات: " + item.Actions);
                                t.Item().Text("مؤشرات النجاح: " + item.SuccessIndicators);
                            });
                        }
                    }));

                if (!string.IsNullOrWhiteSpace(visit.Notes))
                    col.Item().Element(c => PdfTheme.SectionCard(c, "ملاحظات عامة", body => body.Item().Text(visit.Notes!)));

                col.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().AlignCenter().Column(x =>
                    {
                        x.Item().Text("توقيع المعلم").Bold();
                        RenderSignature(x.Item(), instructorSignature?.Bytes);
                        x.Item().Text(visit.InstructorName).FontSize(8);
                    });
                    row.ConstantItem(24);
                    row.RelativeItem().AlignCenter().Column(x =>
                    {
                        x.Item().Text("توقيع المقيم").Bold();
                        RenderSignature(x.Item(), evaluatorSignature?.Bytes);
                        x.Item().Text(visit.EvaluatorName).FontSize(8);
                    });
                    if (visit.ApprovedAt.HasValue && assets.ShowManagerSignature)
                    {
                        row.ConstantItem(24);
                        row.RelativeItem().AlignCenter().Column(x =>
                        {
                            x.Item().Text("توقيع المعتمد").Bold();
                            RenderSignature(x.Item(), managerSignature?.Bytes);
                        });
                    }
                });
            });
            page.Footer().AlignCenter().Text(text =>
                text.Span(string.IsNullOrWhiteSpace(assets.FooterText)
                    ? $"زيارة رقم {visit.Id} • {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC"
                    : assets.FooterText)
                    .FontFamily(PdfTheme.Font).FontSize(8).FontColor(PdfTheme.Muted));
        })).GeneratePdf();

        return new VisitV2PdfExportDto(bytes, $"تقرير-زيارة-{visit.Id}.pdf");
    }

    private static void RenderSignature(IContainer container, byte[]? bytes)
    {
        if (bytes is { Length: > 0 })
            container.Height(38).PaddingTop(3).Image(bytes).FitArea();
        else
            container.Height(38).PaddingTop(18).BorderBottom(0.5f).BorderColor(PdfTheme.Muted);
    }

    private static string NormalizeColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) && System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$")
            ? value
            : "#0F7132";

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r')) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
