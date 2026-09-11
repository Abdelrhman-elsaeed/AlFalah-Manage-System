using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;

namespace AlFalah.Application.IntelligentTimetable;

public static class TeachingAllocationPolicy
{
    public static void Validate(ClassSubjectRequirement requirement, TeachingCellRequest cell)
    {
        if (cell.Members is null || cell.Members.Any(m => m is null)) throw new ArgumentException("قائمة المعلمين مطلوبة.");
        if (cell.Mode is not ("SingleTeacher" or "CoTeaching" or "SplitQuota")) throw new ArgumentException("اختر نوع الإسناد.");
        if (cell.Members.Count == 0) return;
        if (cell.Members.Select(m => m.TeacherTimetableProfileId).Distinct().Count() != cell.Members.Count)
            throw new ArgumentException("لا يمكن تكرار المعلم في إسناد المادة.");
        if (cell.Mode == "SingleTeacher" && cell.Members.Count != 1 || cell.Mode != "SingleTeacher" && cell.Members.Count < 2)
            throw new ArgumentException("الإسناد الفردي يتطلب معلماً واحداً، والمشترك أو المقسم يتطلب معلمين على الأقل.");
        if (cell.Members.Any(m => m.AllocatedPeriodCount < 0 || m.AllocatedPeriodCount > requirement.TotalWeeklyPeriods ||
            m.AllocatedPairedBlockCount < 0 || m.AllocatedPairedBlockCount > requirement.PairedBlockCount ||
            2L * m.AllocatedPairedBlockCount > m.AllocatedPeriodCount))
            throw new ArgumentException("الحصة المزدوجة كاملة لمعلم واحد؛ لا يمكن تقسيم حصتي الزوج بين معلمين.");
        if (cell.Mode == "SplitQuota")
        {
            if (cell.Members.Sum(m => (long)m.AllocatedPeriodCount) != requirement.TotalWeeklyPeriods ||
                cell.Members.Sum(m => (long)m.AllocatedPairedBlockCount) != requirement.PairedBlockCount)
                throw new ArgumentException("أكمل توزيع النصاب والحصص المزدوجة كاملة؛ كل زوج مملوك لمعلم واحد.");
        }
        else if (cell.Members.Any(m => m.AllocatedPeriodCount != requirement.TotalWeeklyPeriods || m.AllocatedPairedBlockCount != requirement.PairedBlockCount))
            throw new ArgumentException("في الإسناد الفردي والمشترك يحصل كل معلم على النصاب الكامل، شاملاً الحصص المزدوجة.");
    }
}
