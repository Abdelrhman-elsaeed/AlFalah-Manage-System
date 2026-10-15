using System.Globalization;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Classrooms;

public static class ClassroomIdentity
{
    public static int GradeCount(SchoolStage stage) => stage switch
    {
        SchoolStage.Primary => 6,
        SchoolStage.Intermediate or SchoolStage.Secondary => 3,
        _ => throw new ArgumentException("المرحلة الدراسية غير صالحة.")
    };
    public static string GradeLabel(SchoolStage stage, int grade)
    {
        if (grade < 1 || grade > GradeCount(stage)) throw new ArgumentException("الصف لا ينتمي إلى المرحلة الدراسية.");
        string[] names = ["الأول", "الثاني", "الثالث", "الرابع", "الخامس", "السادس"];
        return names[grade - 1] + " " + (stage switch { SchoolStage.Primary => "الابتدائي", SchoolStage.Intermediate => "المتوسط", _ => "الثانوي" });
    }
    public static int Section(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("حدّد رقم الشعبة.");
        var digits = value.Trim().Select(c => char.GetUnicodeCategory(c) == UnicodeCategory.DecimalDigitNumber
            ? (char)('0' + (int)char.GetNumericValue(c)) : c).ToArray();
        if (!int.TryParse(new string(digits), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
            throw new ArgumentException("رقم الشعبة يجب أن يكون عددًا صحيحًا موجبًا.");
        return number;
    }
    public static string Label(SchoolStage stage, int grade, int section) => GradeLabel(stage, grade) + " · شعبة " + section.ToString(CultureInfo.InvariantCulture);
}
