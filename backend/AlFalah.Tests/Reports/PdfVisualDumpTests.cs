using AlFalah.Application.DTOs.Reports;
using AlFalah.Infrastructure.Services;
using QuestPDF.Infrastructure;
using Xunit;

namespace AlFalah.Tests.Reports;

/// <summary>
/// Development aid: renders a fully-populated visit report to
/// PDF_DUMP_DIR so the printed layout can be eyeballed. Skipped unless the
/// environment variable is set, so CI never writes files.
/// </summary>
public class PdfVisualDumpTests
{
    [Fact]
    public async Task Dump_visit_report()
    {
        var dir = Environment.GetEnvironmentVariable("PDF_DUMP_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;

        QuestPDF.Settings.License = LicenseType.Community;

        var dto = new VisitReportDto
        {
            VisitId = 2019,
            SchoolId = 1,
            SchoolName = "مدرسة الفلاح النموذجية",
            SchoolInitials = "م.ف",
            HeaderText = "مدارس الفلاح — الإدارة التعليمية بجدة",
            FooterText = "تقرير رسمي صادر عن نظام تقييم مدارس الفلاح",
            InstructorFullName = "عبدالرحمن السعيد",
            CreatedByFullName = "محمود السعيد",
            ApprovedByFullName = "ماجد عبدالله",
            VisitCategoryLabelAr = "زيارة تبادلية",
            VisitSequenceLabelAr = "أولى",
            Subject = "اللغة الإنجليزية",
            GradeClass = "2/3",
            LessonTitle = "Present Perfect Tense",
            PresentCount = 22,
            AbsentCount = 3,
            Notes = "المعلم متمكن من مادته، ويحتاج إلى تنويع أساليب التقويم البنائي داخل الحصة.",
            VisitDate = DateTimeOffset.Parse("2026-07-27T08:00:00Z"),
            SubmittedAt = DateTimeOffset.Parse("2026-07-27T09:10:00Z"),
            ApprovedAt = DateTimeOffset.Parse("2026-07-27T17:55:00Z"),
            RubricVersionNumber = 3,
            TotalScore = 78m,
            MaximumScore = 100m,
            OverallScore = 3.12m,
            PerformanceLevelAr = "جيد جداً",
            ShowModeratorSignature = true,
            ShowManagerSignature = true,
            Recommendations = new()
            {
                "بخصوص تنمية المهارات: تصميم أنشطة تعلم تستهدف مهارات التفكير الناقد.",
                "بخصوص التقويم: إعداد خطة تقويم تشمل التشخيصي والبنائي والختامي.",
                "بخصوص سلوك المتعلمين: وضع قواعد صفية واضحة بمشاركة الطلاب."
            },
            Strengths = new()
            {
                new ReportStrengthDto { DomainCode = "D1", DomainNameAr = "بيئة التعلم", AverageScore = 3.667m },
                new ReportStrengthDto { DomainCode = "D2", DomainNameAr = "التخطيط والتنفيذ", AverageScore = 3.5m }
            },
            ImprovementAreas = new()
            {
                new ReportImprovementDto { DomainCode = "D3", DomainNameAr = "تنمية المهارات", AverageScore = 1.167m },
                new ReportImprovementDto { DomainCode = "D4", DomainNameAr = "التقويم", AverageScore = 1.333m },
                new ReportImprovementDto { DomainCode = "D5", DomainNameAr = "سلوك المتعلمين", AverageScore = 1.167m }
            },
            PriorityStandards = new()
            {
                new ReportPriorityStandardDto { StandardCode = "D3-S1", StandardTextAr = "ينفذ المعلم أنشطة واستراتيجيات تدريس تستوفي نواتج التعلم.", Score = 1, ScoreLabelAr = "يحتاج تحسين" },
                new ReportPriorityStandardDto { StandardCode = "D3-S2", StandardTextAr = "تتنوع استراتيجيات التدريس وفق قدرات المتعلمين وتراعي الفروق الفردية.", Score = 1, ScoreLabelAr = "يحتاج تحسين" },
                new ReportPriorityStandardDto { StandardCode = "D4-S1", StandardTextAr = "يطبق المعلم أساليب وأدوات تقويم متنوعة لقياس تحقق نواتج التعلم.", Score = 1, ScoreLabelAr = "يحتاج تحسين" },
                new ReportPriorityStandardDto { StandardCode = "D5-S1", StandardTextAr = "يلتزم المتعلمون بقواعد السلوك والانضباط.", Score = 1, ScoreLabelAr = "يحتاج تحسين" }
            },
            PlanFollowUps = new()
            {
                new ReportPlanFollowUpDto
                {
                    FollowDate = DateTimeOffset.Parse("2026-08-10T00:00:00Z"),
                    ProgressScore = 40,
                    ProgressNote = "تم تنفيذ ورشة داخلية عن التقويم البنائي وحضرها المعلم.",
                    EvidenceNote = "صور من الورشة + خطة الحصة المعدلة"
                }
            },
            Domains = BuildDomains()
        };

        var bytes = await new PdfReportService().RenderAsync(dto);
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(Path.Combine(dir, "visit-report.pdf"), bytes);
    }

    private static List<ReportDomainBlockDto> BuildDomains()
    {
        var specs = new (string Code, string Name, decimal Average, decimal Percent, (string Text, int Score)[] Standards)[]
        {
            ("D1", "بيئة التعلم", 3.667m, 91.7m, new[]
            {
                ("تنفذ المدرسة برامج وأنشطة لتعزيز القيم الإسلامية والهوية الوطنية لدى المتعلمين.", 4),
                ("تتوفر بيئة التعلم مصادر وأنشطة متنوعة تلبي احتياجات المتعلمين.", 3),
                ("تشجع بيئة التعلم على تنمية مهارات القراءة والكتابة.", 4)
            }),
            ("D2", "التخطيط والتنفيذ", 3.5m, 87.5m, new[]
            {
                ("يخطط المعلم للدرس وفق نواتج تعلم واضحة وقابلة للقياس.", 4),
                ("يدير المعلم زمن الحصة بفاعلية.", 3)
            }),
            ("D3", "تنمية المهارات", 1.167m, 29.2m, new[]
            {
                ("ينفذ المعلم أنشطة واستراتيجيات تدريس تستوفي نواتج التعلم.", 1),
                ("تتنوع استراتيجيات التدريس وفق قدرات المتعلمين وتراعي الفروق الفردية.", 1),
                ("تشجع الممارسات التدريسية على تنمية مهارات التفكير والبحث والابتكار.", 2)
            }),
            ("D4", "التقويم", 1.333m, 33.3m, new[]
            {
                ("يطبق المعلم أساليب وأدوات تقويم متنوعة لقياس تحقق نواتج التعلم.", 1),
                ("يقدم المعلم تغذية راجعة متنوعة تركز على تحسين أداء المتعلمين.", 2)
            }),
            ("D5", "سلوك المتعلمين", 1.167m, 29.2m, new[]
            {
                ("يلتزم المتعلمون بقواعد السلوك والانضباط.", 1),
                ("يظهر المتعلمون الاتجاهات الإيجابية نحو ذواتهم والآخرين.", 1),
                ("يظهر المتعلمون الاعتزاز بثقافتهم واحترام التنوع الثقافي في المجتمع.", 2)
            })
        };

        var labels = new Dictionary<int, string>
        {
            [1] = "يحتاج تحسين",
            [2] = "متحقق جزئياً",
            [3] = "متحقق بدرجة جيدة",
            [4] = "متميز"
        };

        return specs.Select(spec => new ReportDomainBlockDto
        {
            DomainCode = spec.Code,
            DomainNameAr = spec.Name,
            AverageScore = spec.Average,
            PercentageScore = spec.Percent,
            Standards = spec.Standards.Select((std, i) => new ReportStandardScoreDto
            {
                StandardCode = $"{spec.Code}-S{i + 1}",
                StandardTextAr = std.Text,
                Score = std.Score,
                ScoreLabelAr = labels[std.Score],
                EvidenceNote = i == 0 ? "شاهد صفي موثق" : null
            }).ToList()
        }).ToList();
    }
}
