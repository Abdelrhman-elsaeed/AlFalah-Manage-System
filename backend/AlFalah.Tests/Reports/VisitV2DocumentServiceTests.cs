using System.Text;
using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using QuestPDF.Infrastructure;
using Xunit;

namespace AlFalah.Tests.Reports;

public sealed class VisitV2DocumentServiceTests
{
    public VisitV2DocumentServiceTests() => QuestPDF.Settings.License = LicenseType.Community;

    [Fact]
    public void Csv_has_utf8_bom_exactly_22_columns_and_escapes_values()
    {
        var service = new VisitV2DocumentService(new ImageAssetLoader());
        var row = new VisitV2CsvRow(
            7, new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero), 3, "معلم، تجريبي", "42",
            "لغة عربية", "1/أ", "درس \"مميز\"", "استطلاعية - الأولى", 20, 1, "المقيم", "مشرف",
            88, "متحقق بدرجة مرتفعة جداً", new Dictionary<string, int>(), "قوة", "تحسين");

        var result = service.BuildCsv(new[] { row });
        result.Content.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var lines = Encoding.UTF8.GetString(result.Content).TrimStart('\uFEFF').Split(Environment.NewLine);
        ParseCsv(lines[0]).Should().HaveCount(22);
        ParseCsv(lines[1]).Should().HaveCount(22);
        lines[1].Should().Contain("\"درس \"\"مميز\"\"\"");
    }

    [Fact]
    public async Task Pdf_uses_safe_branding_and_signature_fallbacks()
    {
        var service = new VisitV2DocumentService(new ImageAssetLoader());
        var now = DateTimeOffset.UtcNow;
        var detail = new VisitV2DetailDto(
            9, 1, "مدرسة الفلاح", "teacher", "المعلم", "المقيم", "مشرف",
            1, "استطلاعية", 1, "الأولى", 2, "بانتظار الاعتماد", now, 2,
            "الرياضيات", "الأول أ", "الأعداد", 20, 1, null, 2,
            Array.Empty<VisitV2DomainDto>(), null, Array.Empty<VisitV2TreatmentDto>(),
            now, now, now, null, null, null, true);
        var assets = new VisitV2PdfAssetSources(
            "مدرسة الفلاح", "", "not-a-color", null, null, null, null, true, true);

        var result = await service.BuildPdfAsync(detail, assets);
        result.Content.Should().NotBeEmpty();
        Encoding.ASCII.GetString(result.Content, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Dump_professional_v2_visit_report_for_visual_review()
    {
        var dir = Environment.GetEnvironmentVariable("PDF_DUMP_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;

        var now = new DateTimeOffset(2026, 9, 22, 8, 30, 0, TimeSpan.Zero);
        var domains = new[]
        {
            Domain(11, "D1", "بيئة التعلم", 4,
                ("D1-S1", "يوفر المعلم بيئة تعلم آمنة ومحفزة تدعم مشاركة جميع المتعلمين.", 4),
                ("D1-S2", "يوظف مصادر التعلم بما يلائم احتياجات المتعلمين والفروق الفردية.", 3)),
            Domain(12, "D2", "التدريس والتعلم", 3,
                ("D2-S1", "يخطط المعلم لنواتج تعلم واضحة وقابلة للقياس.", 3),
                ("D2-S2", "ينوع استراتيجيات التدريس ويحفز التفكير الناقد وحل المشكلات.", 2)),
            Domain(13, "D3", "التقويم", 2,
                ("D3-S1", "يستخدم أدوات تقويم متنوعة ويتابع تقدم المتعلمين أثناء الحصة.", 2),
                ("D3-S2", "يقدم تغذية راجعة محددة تساعد المتعلم على تحسين أدائه.", 3))
        };
        var analysis = new VisitV2AnalysisDto(
            17, 24, 71, "متحقق بدرجة جيدة", new[]
            {
                new VisitV2DomainAnalysisDto(11, "D1", "بيئة التعلم", 7, 8, 88, "مرتفع", true, false),
                new VisitV2DomainAnalysisDto(12, "D2", "التدريس والتعلم", 5, 8, 63, "متوسط", false, true),
                new VisitV2DomainAnalysisDto(13, "D3", "التقويم", 5, 8, 63, "متوسط", false, true)
            },
            new[] { "بيئة تعلم آمنة ومحفزة", "إدارة صفية فعالة" },
            new[] { "تنويع أدوات التقويم البنائي", "رفع مستوى الأسئلة العليا" }, now);
        var detail = new VisitV2DetailDto(
            2042, 1, "مدارس الفلاح الأهلية", "teacher-1", "أحمد عبدالرحمن السعيد",
            "محمد عبدالله القحطاني", "مشرف تربوي", 1, "استطلاعية", 1, "الأولى", 4, "معتمدة",
            now, 3, "الرياضيات", "الثاني المتوسط / أ", "حل المعادلات الخطية", 24, 2,
            "ظهر وضوح أهداف الدرس وحسن إدارة زمن الحصة، ويوصى بتوسيع فرص التقويم الذاتي بين الطلاب.",
            2, domains, analysis,
            new[]
            {
                new VisitV2TreatmentDto(1, 12, "التدريس والتعلم", "رفع مستوى الأسئلة الصفية العليا",
                    "تصميم ثلاثة أسئلة تحليلية في كل تحضير ومناقشتها مع المشرف أسبوعياً.",
                    "وصول نسبة مشاركة الطلاب في الإجابات التحليلية إلى 70٪.", 1, 1),
                new VisitV2TreatmentDto(2, 13, "التقويم", "تنويع أدوات التقويم البنائي",
                    "تطبيق بطاقة خروج وتقويم الأقران في حصتين أسبوعياً.",
                    "توثيق أربع عينات طلابية وتحسن نتائج بطاقة الخروج خلال شهر.", 1, 2)
            },
            now.AddDays(-2), now, now.AddHours(1), now.AddHours(8), null, null, true);

        var result = await new VisitV2DocumentService(new ImageAssetLoader()).BuildPdfAsync(
            detail,
            new VisitV2PdfAssetSources("مدارس الفلاح الأهلية", "تقرير رسمي صادر عن نظام مدارس الفلاح",
                "#176B58", null, null, null, null, true, true));

        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(Path.Combine(dir, "visit-v2-report.pdf"), result.Content);
    }

    private static VisitV2DomainDto Domain(
        int id,
        string code,
        string name,
        int seed,
        params (string Code, string Text, int Score)[] standards) =>
        new(id, code, name, id, standards.Select((standard, index) =>
            new VisitV2StandardDto(
                id * 10 + index, standard.Code, standard.Text, index, standard.Score,
                index == 1 ? "تمت ملاحظة تطبيق جزئي أثناء النشاط التعاوني." : null,
                new[]
                {
                    new VisitV2IndicatorDto(id * 100 + index * 2, $"I-{seed}-{index}-1",
                        "مؤشر ملاحظ وموثق في أثناء الحصة.", 1, true),
                    new VisitV2IndicatorDto(id * 100 + index * 2 + 1, $"I-{seed}-{index}-2",
                        "مؤشر إضافي لم يظهر بصورة مكتملة.", 2, false)
                })).ToArray());

    private static IReadOnlyList<string> ParseCsv(string line)
    {
        var fields = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
            else if (ch == '"') quoted = !quoted;
            else if (ch == ',' && !quoted) { fields.Add(value.ToString()); value.Clear(); }
            else value.Append(ch);
        }
        fields.Add(value.ToString());
        return fields;
    }
}
