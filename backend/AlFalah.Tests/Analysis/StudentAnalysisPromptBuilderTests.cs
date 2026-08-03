using AlFalah.Application.Analysis;
using AlFalah.Application.DTOs.StudentAnalyzer;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Analysis;

public sealed class StudentAnalysisPromptBuilderTests
{
    [Fact]
    public void Build_PreservesPrototypeContractAndDataOrder()
    {
        var grants = new[]
        {
            new StudentAnalyzerDataPointDto("التعاون", "3", 3m),
            new StudentAnalyzerDataPointDto("الإبداع", "1.5", 1.5m)
        };
        var deductions = new[]
        {
            new StudentAnalyzerDataPointDto("التأخر", "2", 2m)
        };

        var prompt = StudentAnalysisPromptBuilder.Build("أحمد محمد", grants, deductions);

        StudentAnalysisPromptBuilder.Version.Should().Be("student-analyzer-v3.0-verbatim");
        StudentAnalysisPromptBuilder.SystemPrompt.Should().Be(
            "أنت مرشد طلابي متخصص وخبير في علم النفس التربوي. تقدم تقارير نفسية وتربوية شاملة باللغة العربية الفصحى.");
        prompt.Should().Contain("**اسم الطالب:** أحمد محمد");
        prompt.Should().Contain("**إجمالي المنح:** 4.5 نقطة");
        prompt.Should().Contain("**إجمالي الخصم:** 2 نقطة");
        prompt.IndexOf("- التعاون: 3", StringComparison.Ordinal)
            .Should().BeLessThan(prompt.IndexOf("- الإبداع: 1.5", StringComparison.Ordinal));
        RequiredHeadings.Should().OnlyContain(heading => prompt.Contains($"## {heading}", StringComparison.Ordinal));
        prompt.Split("\n## ", StringSplitOptions.None).Length.Should().Be(9);
    }

    [Fact]
    public void Build_UsesPrototypeEmptyStateWording()
    {
        var prompt = StudentAnalysisPromptBuilder.Build(
            "سارة علي",
            Array.Empty<StudentAnalyzerDataPointDto>(),
            Array.Empty<StudentAnalyzerDataPointDto>());

        prompt.Should().Contain("- لا توجد بيانات منح");
        prompt.Should().Contain("- لا توجد مخالفات مسجلة");
    }

    private static readonly string[] RequiredHeadings =
    {
        "ملخص تنفيذي",
        "التشخيص النفسي والسلوكي",
        "المشكلات المحددة وأسبابها",
        "خطة التدخل والحلول العملية",
        "أساليب التعلم المناسبة",
        "توصيات للأسرة",
        "توصيات للمعلمين",
        "الخلاصة والتوقعات"
    };
}

