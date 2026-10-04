using System.Globalization;
using System.Text;
using System.Text.Json;
using AlFalah.Application.Storage;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlFalah.Infrastructure.Services;

public sealed class ReadinessExportRenderer : IReadinessExportRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    public static string Label(string value) => value switch {
        "NoRequirements" => "لا متطلبات", "Calculated" => "محسوبة", "Fulfilled" => "مستوفى", "NoFile" => "غياب ملف",
        "Unavailable" => "ملف غير متاح أو نسخة غير معتمدة أو منحة مسحوبة", "AwaitingReview" => "انتظار المراجعة", "Rejected" => "رفض الشاهد",
        "InsufficientApprovedLinks" => "عدد الروابط المعتمدة غير مكتمل", "Critical" => "حرجة", "Important" => "مهمة", "Normal" => "عادية",
        "NotStarted" => "لم تبدأ المتابعة", "InProgress" => "قيد التنفيذ", "ReadyForReview" => "جاهز للمراجعة", _ => value };
    private static readonly string[] Columns = ["رمز المتطلب", "المتطلب", "المجال", "المعيار", "المسؤول", "الدور", "الأهمية", "إلزامي", "الحالة", "أسباب النقص", "الروابط", "المعتمد", "المطلوب", "الإجراء", "تاريخ الاستحقاق", "المتابعة", "المرجع", "رابط المهمة", "بصمة المصدر"];
    private static string[] Values(ReadinessRequirementDto r) => [r.Code, r.Name, r.DomainCode ?? "", r.StandardCode ?? "", r.ResponsibleName, r.ResponsibleRole ?? "", Label(r.Importance), r.IsMandatory ? "نعم" : "لا",
        Label(r.Status), string.Join("؛ ", r.GapReasons.Select(Label)), r.Links.ToString(Invariant), r.ApprovedLinks.ToString(Invariant), r.RequiredLinks.ToString(Invariant), r.CompletionAction,
        r.DueDate?.ToString("yyyy-MM-dd", Invariant) ?? "", Label(r.FollowUpStatus), r.ReferencePath ?? "", r.ActionUrl, r.SourceSHA256 ?? ""];
    public ReadinessExportResult Render(string format, ReadinessExportData data)
    {
        var name = $"school-readiness-{data.Summary.AcademicYearId}-v{data.Summary.TemplateVersion}";
        return format switch {
            "csv" => new(Csv(data), "text/csv; charset=utf-8", name + ".csv"),
            "excel" => new(Excel(data), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + ".xlsx"),
            "pdf" => new(Pdf(data), "application/pdf", name + ".pdf"), _ => throw new ArgumentException("صيغة غير صالحة.") };
    }
    private static IEnumerable<string[]> Metadata(ReadinessExportData d)
    {
        yield return ["المدرسة", d.Summary.SchoolName]; yield return ["السنة", d.Summary.AcademicYearName];
        yield return ["إصدار القالب", d.Summary.TemplateVersion.ToString(Invariant)]; yield return ["بصمة القالب", d.Summary.TemplateSHA256];
        yield return ["وقت الحساب UTC", d.Summary.CalculatedAtUtc.ToString("O", Invariant)];
        yield return ["عقد التقريب", "منزلتان عشريتان، المنتصف بعيدًا عن الصفر"];
        yield return ["المرشحات", JsonSerializer.Serialize(d.Summary.Filters, new JsonSerializerOptions(JsonSerializerDefaults.Web))];
        if (d.HeaderText != null) yield return ["ترويسة المدرسة", d.HeaderText];
    }
    private static string[] Metric(ReadinessMetric m) => [m.Code, m.Name, m.Requirements.ToString(Invariant), m.Numerator.ToString(Invariant), m.Denominator.ToString(Invariant),
        m.Percentage?.ToString("0.00", Invariant) ?? "", Label(m.CalculationState), m.UniqueFiles.ToString(Invariant), m.Links.ToString(Invariant), m.ApprovedLinks.ToString(Invariant), m.Gaps.ToString(Invariant), m.CriticalGaps.ToString(Invariant)];
    private static readonly string[] MetricColumns = ["الرمز", "النطاق", "المتطلبات", "البسط", "المقام", "النسبة", "حالة الحساب", "الملفات الفريدة", "الروابط", "المعتمد", "النواقص", "النواقص الحرجة"];
    private static readonly string[] FileColumns = ["الفهرس الرقمي", "اسم الملف", "الصيغة", "الحجم بالبايت", "الروابط", "المعتمد", "المعاينة المصرح بها"];
    private static string[] FileValues(DigitalIndexFileDto f, int year) => [f.FileId.ToString(Invariant), f.Name, f.MimeType ?? "", f.Size.ToString(Invariant), f.Links.ToString(Invariant), f.ApprovedLinks.ToString(Invariant), $"/school-manager/storage?file={f.FileId}&academicYearId={year}"];
    private static IEnumerable<ReadinessMetric> Metrics(ReadinessDto s) => new[] { s.Overall }.Concat(s.Domains).Concat(s.Standards);
    private static byte[] Csv(ReadinessExportData data)
    {
        var text = new StringBuilder();
        void Line(IEnumerable<string> row) => text.AppendJoin(',', row.Select(x => '"' + SafeCell(x).Replace("\"", "\"\"") + '"')).Append("\r\n");
        foreach (var row in Metadata(data)) Line(row);
        Line(MetricColumns); foreach (var m in Metrics(data.Summary)) Line(Metric(m));
        Line(Columns); foreach (var r in data.Rows) Line(Values(r));
        Line(["التقويم اليدوي مستقل عن الجاهزية", "الحكم", "القيمة", "السبب", "المقيّم", "التاريخ", "المراجعة"]);
        foreach (var e in data.ManualEvaluations) Line([e.ScopeCode, e.Judgment, e.Value?.ToString(Invariant) ?? "", e.Reason, e.EvaluatorName, e.EvaluatedAtUtc.ToString("O"), e.Revision.ToString(Invariant)]);
        Line(FileColumns); foreach (var f in data.Files ?? []) Line(FileValues(f, data.Summary.AcademicYearId));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
    }
    // Spreadsheet formula injection is blocked even after leading whitespace/control characters.
    private static string SafeCell(string value) => value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@' ? "'" + value : value;
    private static byte[] Excel(ReadinessExportData data)
    {
        using var book = new XLWorkbook();
        void Sheet(string name, IEnumerable<string[]> rows)
        {
            var sheet = book.Worksheets.Add(name); sheet.RightToLeft = true; var i = 0;
            foreach (var row in rows) { i++; for (var j = 0; j < row.Length; j++) sheet.Cell(i, j + 1).Value = SafeCell(row[j]); }
            sheet.Row(1).Style.Font.Bold = true; sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml(PdfTheme.BrandTint);
            sheet.Style.Alignment.WrapText = true; sheet.Style.Font.FontName = "Amiri"; sheet.Columns().Width = 24;
            sheet.SheetView.FreezeRows(1); sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        }
        Sheet("سياق التقرير", Metadata(data)); Sheet("الجاهزية", new[] { MetricColumns }.Concat(Metrics(data.Summary).Select(Metric)));
        Sheet("المتطلبات والنواقص", new[] { Columns }.Concat(data.Rows.Select(Values)));
        Sheet("التقويم اليدوي", new[] { new[] { "النطاق", "الحكم", "القيمة", "السبب", "المقيّم", "التاريخ", "المراجعة" } }.Concat(data.ManualEvaluations.Select(e => new[] { e.ScopeCode, e.Judgment, e.Value?.ToString(Invariant) ?? "", e.Reason, e.EvaluatorName, e.EvaluatedAtUtc.ToString("O"), e.Revision.ToString(Invariant) })));
        Sheet("الفهرس الرقمي", new[] { FileColumns }.Concat((data.Files ?? []).Select(f => FileValues(f, data.Summary.AcademicYearId))));
        using var stream = new MemoryStream(); book.SaveAs(stream); return stream.ToArray();
    }
    private static byte[] Pdf(ReadinessExportData data)
    {
        PdfTheme.EnsureFonts();
        return Document.Create(document => document.Page(page => {
            page.Size(PageSizes.A4); page.Margin(24); page.ContentFromRightToLeft();
            page.DefaultTextStyle(x => x.FontFamily(PdfTheme.Font).FontSize(10).FontColor(PdfTheme.Text));
            page.Header().Column(c => {
                c.Item().Text("تقرير الجاهزية والتقويم الذاتي").Bold().FontSize(18).FontColor(PdfTheme.BrandDark);
                c.Item().Text(data.Summary.SchoolName + " — " + data.Summary.AcademicYearName);
                if (data.HeaderText != null) c.Item().Text(data.HeaderText);
            });
            page.Content().PaddingVertical(12).Column(c => {
                c.Spacing(10);
                c.Item().Text($"إصدار القالب {data.Summary.TemplateVersion} · {data.Summary.CalculatedAtUtc:yyyy-MM-dd HH:mm} UTC · تقريب منزلتين بعيدًا عن الصفر").FontSize(9);
                c.Item().Text($"الجاهزية: {data.Summary.Overall.Numerator}/{data.Summary.Overall.Denominator} · " + (data.Summary.Overall.Percentage?.ToString("0.00", Invariant) + "%" is var p && data.Summary.Overall.Percentage != null ? p : "لا متطلبات")).Bold();
                c.Item().Text($"الملفات الفريدة: {data.Summary.Overall.UniqueFiles} · الروابط: {data.Summary.Overall.Links} · المعتمد: {data.Summary.Overall.ApprovedLinks} · النواقص: {data.Summary.Overall.Gaps} · الحرجة: {data.Summary.Overall.CriticalGaps}");
                c.Item().Text("المرشحات: " + FilterText(data.Summary.Filters)).FontSize(9);
                c.Item().Table(t => {
                    t.ColumnsDefinition(cols => { cols.RelativeColumn(3); cols.RelativeColumn(); cols.RelativeColumn(); cols.RelativeColumn(); });
                    t.Header(h => { foreach (var v in new[] { "المجال / المعيار", "الاستيفاء", "الجاهزية", "النواقص" }) h.Cell().Background(PdfTheme.BrandTint).Padding(5).Text(v).Bold(); });
                    foreach (var m in data.Summary.Domains.Concat(data.Summary.Standards)) {
                        foreach (var v in new[] { m.Code + " " + m.Name, $"{m.Numerator}/{m.Denominator}", m.Percentage?.ToString("0.00", Invariant) + (m.Percentage == null ? "لا متطلبات" : "%"), m.Gaps.ToString(Invariant) })
                            t.Cell().BorderBottom(0.5f).BorderColor(PdfTheme.Border).Padding(5).Text(v);
                    }
                });
                c.Item().EnsureSpace(180).Text("المتطلبات وخطة الاستكمال").Bold().FontSize(14);
                if (data.Rows.Count == 0) c.Item().Text("لا توجد متطلبات ضمن مرشحات العرض.");
                foreach (var r in data.Rows) c.Item().EnsureSpace(150).Border(0.5f).BorderColor(PdfTheme.Border).Padding(8).Column(row => {
                    row.Item().Text($"{r.Code} · {r.Name}").Bold();
                    row.Item().Text($"المعيار {r.StandardCode ?? "غير مصنف"} · {r.ResponsibleName} {r.ResponsibleRole} · {Label(r.Importance)} · {Label(r.Status)}");
                    row.Item().Text($"المعتمد {r.ApprovedLinks}/{r.RequiredLinks} · الروابط {r.Links} · الاستحقاق {r.DueDate?.ToString("yyyy-MM-dd") ?? "غير محدد"}");
                    if (!r.Fulfilled) row.Item().Text(string.Join("؛ ", r.GapReasons.Select(Label))).FontColor("#9A3412");
                    row.Item().Text(r.CompletionAction);
                    if (r.ReferencePath != null) row.Item().Text("مرجع: " + r.ReferencePath).FontSize(8);
                    row.Item().Text("المهمة: " + r.ActionUrl).FontSize(8);
                });
                c.Item().Text("التقويم اليدوي — مستقل عن الجاهزية المحسوبة").Bold().FontSize(14);
                if (data.ManualEvaluations.Count == 0) c.Item().Text("لا يوجد تقويم يدوي محفوظ.");
                foreach (var e in data.ManualEvaluations) c.Item().EnsureSpace(100).Column(row => {
                    row.Item().Text($"{e.ScopeCode} · {e.Judgment} · {e.Value} · مراجعة {e.Revision}").Bold();
                    row.Item().Text(e.Reason); row.Item().Text($"{e.EvaluatorName} · {e.EvaluatedAtUtc:yyyy-MM-dd HH:mm} UTC");
                });
                c.Item().Text("الفهرس الرقمي — ملفات موجودة ضمن مرشحات العرض").Bold().FontSize(14);
                if (data.Files == null || data.Files.Count == 0) c.Item().Text("لا توجد ملفات ضمن مرشحات العرض.");
                foreach (var f in data.Files ?? []) c.Item().EnsureSpace(65).Column(row => {
                    row.Item().Text(f.Name).Bold();
                    row.Item().Text($"{f.MimeType} · {f.Size} bytes · الروابط {f.Links} · المعتمد {f.ApprovedLinks}");
                    row.Item().Text($"/school-manager/storage?file={f.FileId}&academicYearId={data.Summary.AcademicYearId}").FontSize(8);
                });
            });
            page.Footer().AlignCenter().Text(t => { t.Span(data.Summary.SchoolName + " · "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
        })).GeneratePdf();
    }
    private static string FilterText(ReadinessFilter f) => $"السنة {f.AcademicYearId}؛ القالب {f.TemplateVersion}؛ المجال {f.DomainCode ?? "الكل"}؛ المعيار {f.StandardCode ?? "الكل"}؛ المسؤول {f.ResponsibleUserId ?? "الكل"}؛ الأهمية {Label(f.Importance?.ToString() ?? "الكل")}؛ الحالة {Label(f.Status ?? "الكل")}؛ البحث {f.Search ?? "—"}؛ حرجة فقط {f.CriticalOnly}؛ إخفاء المكتمل {f.HideCompleted}؛ بنود المتابعة فقط {f.TrackerOnly}";
}
