namespace AlFalah.Domain.Enums;

public enum SchoolStage
{
    Primary = 1,       // ابتدائي
    Intermediate = 2,  // متوسط
    Secondary = 3      // ثانوي
}

/// <summary>
/// Canonical Arabic labels for <see cref="SchoolStage"/> — verbatim from
/// <c>docs/11-CONSTANTS-AND-ENUMS.md</c>. Mirrors the
/// <c>VisitCategoryExtensions.ToArabicString()</c> pattern.
/// </summary>
public static class SchoolStageExtensions
{
    public static string ToArabicString(this SchoolStage s) => s switch
    {
        SchoolStage.Primary => "ابتدائي",
        SchoolStage.Intermediate => "متوسط",
        SchoolStage.Secondary => "ثانوي",
        _ => string.Empty
    };
}
