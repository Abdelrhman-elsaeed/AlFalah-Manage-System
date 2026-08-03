using AlFalah.Application.Analysis;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Analysis;

public sealed class StudentAnalysisTextSanitizerTests
{
    [Fact]
    public void Removes_openrouter_chat_template_suffix_and_malformed_markdown_tail()
    {
        const string text = """
            * **السلوكي:** اقتصاد العلامات، التعزيز التفاضلي للسلوكيات المنخفضة ` ** **
            ***
            "
            آ
             point:
             user |
            """;

        var result = StudentAnalysisTextSanitizer.Sanitize(text);

        result.Should().Be("* **السلوكي:** اقتصاد العلامات، التعزيز التفاضلي للسلوكيات المنخفضة");
        result.Should().NotContain("point:");
        result.Should().NotContain("user |");
    }

    [Fact]
    public void Preserves_valid_markdown_when_no_chat_template_marker_exists()
    {
        const string text = "## الخلاصة\nاستخدم `Token Economy` مع **تعزيز إيجابي**.\n***";

        StudentAnalysisTextSanitizer.Sanitize(text).Should().Be(text);
    }
}
