using System.Text;
using AlFalah.Application.DTOs.Reports;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using QuestPDF.Infrastructure;
using Xunit;

namespace AlFalah.Tests.Reports;

public class PdfReportServiceTests
{
    [Fact]
    public async Task RenderAsync_With_Three_Signature_Areas_And_Recommendations_Produces_Pdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var dto = new VisitReportDto
        {
            VisitId = 42,
            SchoolId = 1,
            SchoolName = "مدرسة الفلاح النموذجية",
            SchoolInitials = "م.ف",
            HeaderText = "مدرسة الفلاح النموذجية",
            InstructorFullName = "المعلم التجريبي",
            CreatedByFullName = "المشرف التجريبي",
            ApprovedByFullName = "مدير المدرسة",
            VisitCategoryLabelAr = "زيارة صفية أو دورية",
            VisitSequenceLabelAr = "أولى",
            VisitDate = DateTimeOffset.Parse("2026-07-15T08:00:00Z"),
            ApprovedAt = DateTimeOffset.Parse("2026-07-15T10:00:00Z"),
            RubricVersionNumber = 2,
            OverallScore = 3.25m,
            PerformanceLevelAr = "جيد جداً",
            Recommendations = new() { "بخصوص التقويم: إعداد خطة تقويم تشمل التشخيصي والبنائي والختامي" },
            ShowModeratorSignature = true,
            ShowManagerSignature = true,
            Domains = new()
            {
                new ReportDomainBlockDto
                {
                    DomainCode = "D4",
                    DomainNameAr = "التقويم",
                    AverageScore = 3.25m,
                    Standards = new()
                    {
                        new ReportStandardScoreDto
                        {
                            StandardCode = "D4-S1",
                            StandardTextAr = "يستخدم أدوات تقويم متنوعة",
                            Score = 3,
                            ScoreLabelAr = "متحقق بدرجة جيدة",
                            EvidenceNote = "شاهد صفي"
                        }
                    }
                }
            }
        };

        var bytes = await new PdfReportService().RenderAsync(dto);

        bytes.Length.Should().BeGreaterThan(10_000);
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }
}
