using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
        var brand = NormalizeColor(assets.PrimaryColor);
        var generatedAt = DateTimeOffset.UtcNow;
        var logoBytes = logo.HasValue && !logo.Value.IsEmpty ? logo.Value.Bytes : null;
        var safeHeaderText = SafeBrandText(assets.HeaderText, visit.SchoolName);
        var safeFooterText = SafeBrandText(assets.FooterText, $"{visit.SchoolName} • زيارة #{visit.Id}");

        var bytes = Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(24);
            page.PageColor(PdfTheme.White);
            page.DefaultTextStyle(x => x.FontFamily(PdfTheme.Font).FontSize(8.8f).FontColor(PdfTheme.Text));
            page.ContentFromRightToLeft();
            page.Header().Column(header =>
            {
                header.Item().ShowOnce().Element(c => ComposePrimaryHeader(c, visit, safeHeaderText, logoBytes, brand));
                header.Item().SkipOnce().Element(c => ComposeRunningHeader(c, visit, brand));
            });
            page.Content().Element(c => ComposeContent(
                c, visit, brand, instructorSignature?.Bytes, evaluatorSignature?.Bytes, managerSignature?.Bytes,
                assets.ShowManagerSignature));
            page.Footer().Element(c => ComposeFooter(c, visit, safeFooterText, generatedAt));
        })).GeneratePdf();

        return new VisitV2PdfExportDto(bytes, $"تقرير-زيارة-{visit.Id}.pdf");
    }

    private static void ComposePrimaryHeader(
        IContainer container,
        VisitV2DetailDto visit,
        string headerText,
        byte[]? logo,
        string brand)
    {
        container.PaddingBottom(8).Column(outer =>
        {
            outer.Item().Element(c => PdfTheme.RoundedPanel(c, content => content
                .PaddingVertical(10).PaddingHorizontal(14).Row(row =>
            {
                // QuestPDF positions row items physically LTR. Text is emitted first so the logo lands on the visual right.
                row.RelativeItem().AlignMiddle().Column(text =>
                {
                    text.Item().AlignRight().Text(headerText)
                        .Bold().FontSize(11).FontColor("#DDF2E5");
                    text.Item().PaddingTop(2).AlignRight().Text("تقرير الزيارة الصفية")
                        .Bold().FontSize(20).FontColor(PdfTheme.White);
                    text.Item().PaddingTop(3).AlignRight().Text($"{visit.VisitCategoryLabelAr} • {visit.VisitSequenceLabelAr}")
                        .FontSize(9.5f).FontColor("#EAF5EE");
                });

                row.ConstantItem(14);
                row.ConstantItem(64).Height(56).Element(c => PdfTheme.RoundedPanel(c,
                    logoContainer => logoContainer.Padding(5).AlignMiddle().AlignCenter()
                        .Element(inner => RenderLogo(inner, logo, visit.SchoolName, brand)),
                    PdfTheme.White, PdfTheme.White, PdfTheme.CardRadius));
            }), brand, brand, PdfTheme.PillRadius));

            outer.Item().Height(3).Background(PdfTheme.Gold);
            outer.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().AlignLeft().Text($"#{visit.Id.ToString(CultureInfo.InvariantCulture)}")
                    .Bold().FontSize(9).FontColor(PdfTheme.Muted);
                row.RelativeItem().AlignCenter().Text(visit.VisitDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                    .FontSize(9).FontColor(PdfTheme.Muted);
                row.RelativeItem().AlignRight().Text(visit.StatusLabelAr)
                    .Bold().FontSize(9).FontColor(brand);
            });
        });
    }

    private static void ComposeRunningHeader(IContainer container, VisitV2DetailDto visit, string brand)
    {
        container.PaddingBottom(9).BorderBottom(1).BorderColor(brand).PaddingBottom(5).Row(row =>
        {
            row.ConstantItem(120).AlignLeft().Text($"زيارة #{visit.Id}")
                .FontSize(8.5f).FontColor(PdfTheme.Muted);
            row.ConstantItem(10);
            row.RelativeItem().AlignRight().Text($"تقرير الزيارة الصفية — {visit.InstructorName}")
                .Bold().FontSize(9).FontColor(brand);
        });
    }

    private static void ComposeContent(
        IContainer container,
        VisitV2DetailDto visit,
        string brand,
        byte[]? instructorSignature,
        byte[]? evaluatorSignature,
        byte[]? managerSignature,
        bool showManagerSignature)
    {
        container.PaddingVertical(5).Column(col =>
        {
            col.Spacing(7);
            col.Item().ShowEntire().Element(c => ComposeExecutiveSummary(c, visit, brand));
            col.Item().ShowEntire().Element(c => ComposeVisitDetails(c, visit));

            if (visit.Analysis != null)
            {
                col.Item().ShowEntire().Element(c => ComposeDomainPerformance(c, visit.Analysis, brand));
                if (visit.Analysis.Strengths.Count > 0 || visit.Analysis.ImprovementAreas.Count > 0)
                    col.Item().ShowEntire().Element(c => ComposeInsights(c, visit.Analysis));
            }

            if (visit.Domains.Count > 0)
            {
                col.Item().Element(c => PdfTheme.SectionHeading(c, "تفاصيل بطاقة الملاحظة", brand));
                foreach (var domain in visit.Domains)
                    col.Item().Element(c => ComposeDomainDetails(c, domain, visit.Analysis, brand));
            }

            var orderedTreatments = visit.Treatments.OrderBy(x => x.SortOrder).ToArray();
            var approvalTreatmentCount = GetApprovalTreatmentCount(orderedTreatments);
            var leadingTreatmentCount = orderedTreatments.Length - approvalTreatmentCount;
            if (leadingTreatmentCount > 0)
                col.Item().Element(c => ComposeTreatments(
                    c, orderedTreatments.Take(leadingTreatmentCount).ToArray(), brand));

            // Keep one or two reasonably-sized treatment cards with the
            // signatures. This balances the final sheet instead of leaving an
            // almost-empty signature page, without forcing unbounded text into
            // a ShowEntire block.
            col.Item().ShowEntire().Column(approval =>
            {
                approval.Spacing(7);
                if (approvalTreatmentCount > 0)
                    approval.Item().Element(c => ComposeTreatments(
                        c,
                        orderedTreatments.Skip(leadingTreatmentCount).ToArray(),
                        brand,
                        showHeading: leadingTreatmentCount == 0,
                        startIndex: leadingTreatmentCount));
                if (!string.IsNullOrWhiteSpace(visit.Notes))
                    approval.Item().Element(c => ComposeNotes(c, visit.Notes!));
                approval.Item().Element(c => ComposeSignatures(
                    c, visit, instructorSignature, evaluatorSignature, managerSignature, showManagerSignature, brand));
            });
        });
    }

    private static void ComposeExecutiveSummary(IContainer container, VisitV2DetailDto visit, string brand)
    {
        PdfTheme.RoundedPanel(container, content => content.Padding(9).Row(row =>
        {
            row.RelativeItem().AlignMiddle().Column(info =>
            {
                info.Item().Text("المعلم محل الزيارة").FontSize(8).FontColor(PdfTheme.Muted);
                info.Item().PaddingTop(1).Text(visit.InstructorName).Bold().FontSize(14).FontColor(PdfTheme.Text);
                info.Item().PaddingTop(4).Text(visit.LessonTitle).Bold().FontSize(10).FontColor(brand);
                info.Item().PaddingTop(2).Text($"{visit.Subject} • {visit.GradeClass} • الحصة {visit.ClassroomPeriod}")
                    .FontSize(8.5f).FontColor(PdfTheme.Muted);
            });

            row.ConstantItem(14);
            row.ConstantItem(126).MinHeight(82).Element(c => PdfTheme.RoundedPanel(c,
                scoreContent => scoreContent.Padding(7).AlignCenter().AlignMiddle().Column(score =>
            {
                score.Item().Text("النتيجة الإجمالية").FontSize(8.5f).FontColor("#DDF2E5");
                if (visit.Analysis == null)
                {
                    score.Item().PaddingTop(7).Text("—").Bold().FontSize(28).FontColor(PdfTheme.White);
                    score.Item().Text("لم تحسب بعد").FontSize(8).FontColor("#DDF2E5");
                    return;
                }

                score.Item().PaddingTop(2).Text($"{visit.Analysis.OverallPercentage}%")
                    .Bold().FontSize(24).FontColor(PdfTheme.White);
                score.Item().Text(visit.Analysis.PerformanceLevelAr).Bold().FontSize(9).FontColor(PdfTheme.Gold);
                score.Item().PaddingTop(2).Text($"{visit.Analysis.TotalScore} من {visit.Analysis.MaximumScore}")
                    .FontSize(7.5f).FontColor("#DDF2E5");
            }), brand, brand, PdfTheme.CardRadius));
        }), PdfTheme.White, PdfTheme.Border, PdfTheme.CardRadius);
    }

    private static void ComposeVisitDetails(IContainer container, VisitV2DetailDto visit)
    {
        PdfTheme.SectionCard(container, "بيانات الزيارة", body =>
        {
            body.Item().Element(grid => PdfTheme.DetailGrid(grid, new[]
            {
                ("المدرسة", visit.SchoolName, (string?)null),
                ("المقيم", $"{visit.EvaluatorName} — {visit.EvaluatorRole}", (string?)null),
                ("تاريخ الزيارة", visit.VisitDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), (string?)null),
                ("الحصة", visit.ClassroomPeriod.ToString(CultureInfo.InvariantCulture), (string?)null),
                ("المادة", visit.Subject, (string?)null),
                ("الصف والفصل", visit.GradeClass, (string?)null),
                ("عنوان الدرس", visit.LessonTitle, (string?)null),
                ("نوع الزيارة", $"{visit.VisitCategoryLabelAr} — {visit.VisitSequenceLabelAr}", (string?)null),
                ("الحضور", visit.PresentCount.ToString(CultureInfo.InvariantCulture), (string?)null),
                ("الغياب", visit.AbsentCount.ToString(CultureInfo.InvariantCulture), (string?)null)
            }));
        });
    }

    private static void ComposeDomainPerformance(IContainer container, VisitV2AnalysisDto analysis, string brand)
    {
        PdfTheme.SectionCard(container, "ملخص الأداء حسب المجال", body =>
        {
            foreach (var domain in analysis.Domains)
            {
                body.Item().PaddingVertical(2).Row(row =>
                {
                    row.ConstantItem(48).AlignLeft().Text($"{domain.Percentage}%")
                        .Bold().FontSize(10).FontColor(ScoreColor(domain.Percentage));
                    row.RelativeItem().PaddingHorizontal(7).AlignMiddle()
                        .Element(c => PdfTheme.ProgressBar(c, domain.Percentage / 100d, ScoreColor(domain.Percentage)));
                    row.ConstantItem(175).AlignRight().Text(domain.DomainNameAr)
                        .Bold().FontSize(9).FontColor(PdfTheme.Text);
                });
            }
        }, brand);
    }

    private static void ComposeInsights(IContainer container, VisitV2AnalysisDto analysis)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(c => ComposeInsightCard(
                c, "فرص التحسين", analysis.ImprovementAreas, "#FFF7ED", "#C2410C"));
            row.ConstantItem(8);
            row.RelativeItem().Element(c => ComposeInsightCard(
                c, "نقاط القوة", analysis.Strengths, "#ECFDF5", PdfTheme.Brand));
        });
    }

    private static void ComposeInsightCard(
        IContainer container,
        string title,
        IReadOnlyList<string> items,
        string background,
        string accent)
    {
        PdfTheme.RoundedPanel(container.MinHeight(58), content => content.Padding(7).Column(col =>
        {
            col.Item().Text(title).Bold().FontSize(10).FontColor(accent);
            if (items.Count == 0)
            {
                col.Item().PaddingTop(5).Text("لا توجد عناصر مسجلة").FontSize(8).FontColor(PdfTheme.Muted);
                return;
            }

            foreach (var item in items)
                col.Item().PaddingTop(4).Text($"• {item}").FontSize(8.5f).FontColor(PdfTheme.Text);
        }), background, accent, PdfTheme.CardRadius);
    }

    private static void ComposeDomainDetails(
        IContainer container,
        VisitV2DomainDto domain,
        VisitV2AnalysisDto? analysis,
        string brand)
    {
        var domainResult = analysis?.Domains.FirstOrDefault(x =>
            x.RubricDomainId == domain.Id || string.Equals(x.DomainCode, domain.Code, StringComparison.OrdinalIgnoreCase));

        container.Column(col =>
        {
            col.Item().Element(c => PdfTheme.RoundedPanel(c, content => content
                .PaddingVertical(4).PaddingHorizontal(8).Row(header =>
                {
                    header.ConstantItem(58).Element(score => PdfTheme.RoundedPanel(score,
                        inner => inner.PaddingVertical(2).AlignCenter()
                            .Text(domainResult == null ? "—" : $"{domainResult.Percentage}%")
                            .Bold().FontSize(10).FontColor(domainResult == null ? PdfTheme.Muted : ScoreColor(domainResult.Percentage)),
                        PdfTheme.White, PdfTheme.Border, PdfTheme.PillRadius));
                    header.ConstantItem(8);
                    header.RelativeItem().AlignRight().Text($"{domain.Code}  |  {domain.NameAr}")
                        .Bold().FontSize(11).FontColor(brand);
                }), "#F5F9F7", PdfTheme.Border, PdfTheme.CardRadius));

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(48);
                    columns.RelativeColumn();
                    columns.ConstantColumn(52);
                });
                table.Header(header =>
                {
                    PdfTheme.HeaderCell(header.Cell(), "الدرجة", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(header.Cell(), "المعيار والشواهد المرصودة");
                    PdfTheme.HeaderCell(header.Cell(), "الرمز", PdfTheme.CellAlign.Center);
                });

                var index = 0;
                foreach (var standard in domain.Standards)
                {
                    var observed = standard.Indicators.Where(i => i.IsObserved).Select(i => $"✓ {i.TextAr}").ToArray();
                    var details = standard.TextAr;
                    if (observed.Length > 0)
                        details += "\n" + string.Join("\n", observed);
                    if (!string.IsNullOrWhiteSpace(standard.EvidenceNote))
                        details += $"\nشاهد المقيم: {standard.EvidenceNote}";

                    ComposeScoreCell(table.Cell(), standard.Score, index % 2 == 1);
                    PdfTheme.BodyCell(table.Cell(), details, zebra: index % 2 == 1);
                    PdfTheme.BodyCell(table.Cell(), standard.Code, PdfTheme.CellAlign.Center, index % 2 == 1, true, brand);
                    index++;
                }

                if (domain.Standards.Count == 0)
                    PdfTheme.EmptyRow(table, 3, "لا توجد معايير مسجلة لهذا المجال.");
            });
        });
    }

    private static void ComposeScoreCell(IContainer container, int score, bool zebra)
    {
        container.Background(zebra ? PdfTheme.ZebraRow : PdfTheme.White)
            .BorderBottom(PdfTheme.BorderWidth).BorderRight(PdfTheme.BorderWidth).BorderColor(PdfTheme.Border)
            .PaddingVertical(3).PaddingHorizontal(4).AlignMiddle().AlignCenter().Text(text =>
            {
                text.Span(score.ToString(CultureInfo.InvariantCulture)).Bold().FontSize(11).FontColor(ScoreColor(score * 25));
                text.Span(" من 4").FontSize(7.5f).FontColor(PdfTheme.Muted);
            });
    }

    private static void ComposeTreatments(
        IContainer container,
        IReadOnlyList<VisitV2TreatmentDto> treatments,
        string brand,
        bool showHeading = true,
        int startIndex = 0)
    {
        container.Column(section =>
        {
            if (showHeading)
                section.Item().Element(c => PdfTheme.SectionHeading(c, "الخطة العلاجية ومتابعة التحسين", brand));
            var ordered = treatments.OrderBy(x => x.SortOrder).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                var item = ordered[index];
                section.Item().PaddingTop(5).Element(c => PdfTheme.RoundedPanel(c,
                    content => content.Padding(6).Column(card =>
                    {
                        card.Item().Element(headerContainer => PdfTheme.RoundedPanel(headerContainer,
                            headerContent => headerContent.PaddingVertical(5).PaddingHorizontal(8).Row(header =>
                            {
                                header.ConstantItem(22).Height(22).Layers(badge =>
                                {
                                    badge.Layer().Element(c => PdfTheme.Circle(c, PdfTheme.Gold));
                                    badge.PrimaryLayer().AlignCenter().AlignMiddle()
                                        .Text((startIndex + index + 1).ToString(CultureInfo.InvariantCulture))
                                        .Bold().FontSize(9).FontColor(PdfTheme.Text);
                                });
                                header.ConstantItem(8);
                                header.RelativeItem().AlignRight().AlignMiddle().Text(item.DomainNameAr)
                                    .Bold().FontSize(11).FontColor(brand);
                            }), "#F2F8F5", PdfTheme.Border, PdfTheme.PillRadius));

                        card.Item().PaddingTop(6).Column(fields =>
                        {
                            fields.Spacing(6);
                            fields.Item().Element(c => ComposeTreatmentField(c, "الهدف", item.Goal, "#EFF8F4", brand));
                            fields.Item().Row(row => PdfTheme.RtlRow(row, new Action<IContainer>[]
                            {
                                c => ComposeTreatmentField(c, "الإجراءات التنفيذية", item.Actions, "#F8FAF9", PdfTheme.Text),
                                c => ComposeTreatmentField(c, "مؤشرات النجاح", item.SuccessIndicators, "#FFFBEB", "#8A6415")
                            }, spacing: 6));
                        });
                    }), PdfTheme.White, PdfTheme.Border, PdfTheme.CardRadius));
            }
        });
    }

    private static int GetApprovalTreatmentCount(IReadOnlyList<VisitV2TreatmentDto> treatments)
    {
        static int TextLength(VisitV2TreatmentDto item) =>
            item.DomainNameAr.Length + item.Goal.Length + item.Actions.Length + item.SuccessIndicators.Length;

        if (treatments.Count >= 2 && treatments.TakeLast(2).Sum(TextLength) <= 1800)
            return 2;
        if (treatments.Count >= 1 && TextLength(treatments[^1]) <= 1000)
            return 1;
        return 0;
    }

    private static void ComposeTreatmentField(
        IContainer container,
        string label,
        string value,
        string background,
        string accent)
    {
        PdfTheme.RoundedPanel(container, content => content.PaddingVertical(5).PaddingHorizontal(7).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(5).Height(12).Element(c => PdfTheme.Circle(c, accent));
                row.RelativeItem().PaddingHorizontal(5).AlignRight().Text(label).Bold().FontSize(8).FontColor(accent);
            });
            col.Item().PaddingTop(2).AlignRight().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                .FontSize(8.5f).FontColor(PdfTheme.Text).LineHeight(1.15f);
        }), background, accent, PdfTheme.CardRadius, 0.5f);
    }

    private static void ComposeNotes(IContainer container, string notes)
    {
        PdfTheme.RoundedPanel(container, content => content.Padding(7).Column(col =>
        {
            col.Item().Text("ملاحظات المقيم").Bold().FontSize(10).FontColor("#8A6415");
            col.Item().PaddingTop(3).Text(notes).FontSize(8.5f).FontColor(PdfTheme.Text);
        }), "#FFFBEB", "#E5C765", PdfTheme.CardRadius);
    }

    private static void ComposeSignatures(
        IContainer container,
        VisitV2DetailDto visit,
        byte[]? instructorSignature,
        byte[]? evaluatorSignature,
        byte[]? managerSignature,
        bool showManagerSignature,
        string brand)
    {
        PdfTheme.RoundedPanel(container, content => content.Column(card =>
        {
            card.Item().Element(c => PdfTheme.SectionHeading(c, "الاعتماد والتوقيعات", brand));
            card.Item().PaddingVertical(8).PaddingHorizontal(9).Row(row =>
            {
                // PdfTheme.RtlRow maps this list from visual left to right in the generated page.
                var signatures = new List<Action<IContainer>>();
                if (visit.ApprovedAt.HasValue && showManagerSignature)
                    signatures.Add(c => ComposeSignature(c, "توقيع المعتمد", "مدير المدرسة / المعتمد", managerSignature));
                signatures.Add(c => ComposeSignature(c, "توقيع المقيم", visit.EvaluatorName, evaluatorSignature));
                signatures.Add(c => ComposeSignature(c, "توقيع المعلم", visit.InstructorName, instructorSignature));
                PdfTheme.RtlRow(row, signatures, signatures.Count, 14);
            });
        }), PdfTheme.White, PdfTheme.Border, PdfTheme.CardRadius);
    }

    private static void ComposeSignature(IContainer container, string label, string name, byte[]? signature)
    {
        container.AlignCenter().Column(col =>
        {
            col.Item().AlignCenter().Text(label).Bold().FontSize(9).FontColor(PdfTheme.Text);
            RenderSignature(col.Item().PaddingTop(2), signature);
            col.Item().PaddingTop(3).AlignCenter().Text(name).FontSize(8).FontColor(PdfTheme.Muted);
        });
    }

    private static void ComposeFooter(
        IContainer container,
        VisitV2DetailDto visit,
        string footerText,
        DateTimeOffset generatedAt)
    {
        container.PaddingTop(8).BorderTop(PdfTheme.BorderWidth).BorderColor(PdfTheme.Border).PaddingTop(5).Row(row =>
        {
            row.ConstantItem(92).AlignLeft().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontFamily(PdfTheme.Font).FontSize(8).FontColor(PdfTheme.Muted));
                text.Span("صفحة ");
                text.CurrentPageNumber();
                text.Span(" من ");
                text.TotalPages();
            });
            row.RelativeItem().AlignCenter().Text($"أُصدر في {generatedAt:yyyy-MM-dd HH:mm} UTC")
                .FontSize(7.5f).FontColor(PdfTheme.Muted);
            row.RelativeItem().AlignRight().Text(footerText)
                .FontSize(8).FontColor(PdfTheme.Muted);
        });
    }

    private static void RenderLogo(IContainer container, byte[]? logo, string schoolName, string brand)
    {
        if (logo is { Length: > 0 })
        {
            try
            {
                container.Image(logo).FitArea();
                return;
            }
            catch
            {
                // A broken optional branding asset must not prevent an official report from being generated.
            }
        }

        var initials = string.Concat(schoolName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => x[0]));
        container.Text(string.IsNullOrWhiteSpace(initials) ? "ف" : initials).Bold().FontSize(20).FontColor(brand);
    }

    private static string ScoreColor(int percentage) => percentage switch
    {
        >= 85 => PdfTheme.Brand,
        >= 70 => "#1D6F91",
        >= 50 => "#A96A16",
        _ => "#B42318"
    };

    private static void RenderSignature(IContainer container, byte[]? bytes)
    {
        if (bytes is { Length: > 0 })
            container.Height(64).MaxWidth(190).AlignCenter().Image(bytes).FitArea();
        else
            container.Height(54).MaxWidth(150).AlignCenter().PaddingTop(35).BorderBottom(0.5f).BorderColor(PdfTheme.Muted);
    }

    internal static string SafeBrandText(string? value, string fallback)
    {
        var candidate = value?.Trim();
        return string.IsNullOrWhiteSpace(candidate) || Regex.IsMatch(candidate, "[?\\uFFFD]{3,}")
            ? fallback
            : candidate;
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
