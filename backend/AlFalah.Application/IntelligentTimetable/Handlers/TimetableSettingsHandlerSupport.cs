using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable.Handlers;

public static class TimetableSettingsHandlerSupport
{
    public const string AuthenticationRequired = "يلزم تسجيل الدخول واختيار مدرسة نشطة.";
    public const string PermissionDenied = "ليس لديك صلاحية للوصول إلى إعدادات الجدول الذكي.";
    public const string NotFound = "ملف إعداد الجدول غير موجود في المدرسة النشطة.";
    public const string DuplicateName = "يوجد ملف إعداد بالاسم نفسه لهذا العام والفصل الدراسي.";
    public const string InvalidAcademicScope = "العام أو الفصل الدراسي المحدد غير متاح للمدرسة النشطة.";
    public const string ConcurrencyConflict = "تم تعديل ملف الإعداد بواسطة مستخدم آخر. أعد تحميل أحدث نسخة وراجع تغييراتك.";

    public static bool IsExcludedRole(ICurrentUserService currentUser) =>
        currentUser.IsInRole(RoleNames.Instructor) || currentUser.IsInRole(RoleNames.Guardian);

    public static bool CanView(ICurrentUserService currentUser) =>
        currentUser.IsAuthenticated
        && !IsExcludedRole(currentUser)
        && (currentUser.HasPermission(PermissionNames.TimetableView)
            || currentUser.HasPermission(PermissionNames.TimetableManage));

    public static async Task<bool> CanManageAsync(
        ICurrentUserService currentUser,
        ITimetableSettingsRepository repository,
        int schoolId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || IsExcludedRole(currentUser)) return false;
        if (currentUser.HasPermission(PermissionNames.TimetableManage)) return true;
        return !string.IsNullOrWhiteSpace(currentUser.UserId)
            && await repository.HasEditorGrantAsync(schoolId, currentUser.UserId, cancellationToken).ConfigureAwait(false);
    }

    public static string SemesterLabel(TimetableSemester semester) => semester switch
    {
        TimetableSemester.First => "الفصل الدراسي الأول",
        TimetableSemester.Second => "الفصل الدراسي الثاني",
        _ => "فصل غير معروف"
    };

    public static string StatusLabel(TimetableSetupStatus status) => status switch
    {
        TimetableSetupStatus.Draft => "مسودة",
        TimetableSetupStatus.ReadyForGeneration => "جاهز للتوليد",
        TimetableSetupStatus.Generated => "تم توليد الجدول",
        TimetableSetupStatus.Archived => "مؤرشف",
        _ => "غير معروف"
    };

    public static (IReadOnlyList<TimetableSetupStepDto> Steps, IReadOnlyList<string> Warnings) BuildReadiness(
        TimetableSetupProfileDto? profile,
        TimetableReadinessData data)
    {
        var hasProfile = profile is not null;
        var classroomsComplete = data.ActiveClassrooms > 0 && data.ClassroomsMissingLocation == 0;
        var teachersStarted = data.ActiveTeachers > 0;
        var subjectsStarted = false;

        var steps = new List<TimetableSetupStepDto>
        {
            new("study-days", "الأيام الدراسية", profile?.BellScheduleTemplateId != null ? "complete" : hasProfile ? "not-started" : "blocked",
                profile?.BellScheduleTemplateId != null ? "تم اختيار قالب أيام الدراسة والحصص." : hasProfile ? "لم يتم إعداد أيام الدراسة والحصص بعد." : "أنشئ ملف إعداد أولاً.",
                "/intelligent-timetable/timings"),
            new("teachers", "المعلمين", teachersStarted ? "incomplete" : "not-started",
                teachersStarted ? "المعلمون مسجلون، وتبقى إتاحة المعلمين." : "لا يوجد معلمون نشطون في المدرسة.",
                "/intelligent-timetable/teachers"),
            new("classrooms", "الفصول", classroomsComplete ? "complete" : data.ActiveClassrooms > 0 ? "incomplete" : "not-started",
                classroomsComplete
                    ? "الفصول النشطة ومواقعها جاهزة."
                    : data.ActiveClassrooms == 0
                        ? "لا توجد فصول نشطة لهذا العام الدراسي."
                        : $"يوجد {data.ClassroomsMissingLocation} فصل بلا موقع مسجل.",
                "/student-affairs/classrooms"),
            new("subjects", "المواد", subjectsStarted ? "incomplete" : "not-started",
                "لم يتم إنشاء دليل المواد ومتطلبات الفصول بعد.",
                "/intelligent-timetable/subjects"),
            new("assignments", "إسنادات", "blocked", "يتطلب فصولاً ومواد ومعلمين جاهزين أولاً.",
                "/intelligent-timetable/assignments"),
            new("subject-rules", "تخصيص المواد", "blocked", "يتطلب اكتمال الإسنادات ومتطلبات المواد.",
                "/intelligent-timetable/subject-rules"),
            new("timetable", "الجدول", "blocked", "التوليد متوقف حتى تكتمل جميع المتطلبات الأساسية.",
                "/timetable")
        };

        var warnings = new List<string>();
        if (!hasProfile) warnings.Add("أنشئ ملف إعداد للعام والفصل الدراسي المحددين.");
        if (data.ActiveClassrooms == 0) warnings.Add("لا توجد فصول نشطة لهذا العام الدراسي.");
        if (data.ClassroomsMissingLocation > 0) warnings.Add($"سجّل الموقع الفعلي لـ {data.ClassroomsMissingLocation} فصل.");
        if (data.ActiveTeachers == 0) warnings.Add("لا يوجد معلمون نشطون في المدرسة.");
        warnings.Add("دليل المواد والإسنادات وقواعد الجدولة لم تكتمل بعد.");
        return (steps, warnings);
    }
}
