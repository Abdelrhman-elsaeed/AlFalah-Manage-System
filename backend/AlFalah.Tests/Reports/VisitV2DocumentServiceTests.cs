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
