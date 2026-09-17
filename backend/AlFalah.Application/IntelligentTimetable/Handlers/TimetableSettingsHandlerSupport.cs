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
    public const string DuplicateAcademicYearScope = "العام الدراسي مرتبط بالفعل بهذه المدرسة.";
    public const string AcademicYearCodeConflict = "كود العام الدراسي مستخدم بتواريخ مختلفة. راجع الكود أو التواريخ.";
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
        var studyDaysComplete = profile?.BellScheduleTemplateId is not null;
        var classroomsComplete = data.ActiveClassrooms > 0 && data.ClassroomsMissingLocation == 0;
        var teachersComplete = studyDaysComplete
            && data.ActiveTeachers > 0
            && data.ConfiguredTeachers == data.ActiveTeachers;
        var subjectsComplete = data.AvailableSubjects > 0;
        var subjectRulesComplete = hasProfile
            && subjectsComplete
            && data.ActiveClassrooms > 0
            && data.RequirementCount > 0
            && data.CoveredClassrooms == data.ActiveClassrooms;
        var assignmentPrerequisitesComplete = studyDaysComplete
            && classroomsComplete
            && teachersComplete
            && subjectRulesComplete;
        var assignmentsComplete = assignmentPrerequisitesComplete
            && data.RequirementCount > 0
            && data.Assignments.Count == data.RequirementCount
            && data.Assignments.Select(x => x.RequirementId).Distinct().Count() == data.RequirementCount
            && data.Assignments.All(IsCompleteAssignment);
        var timetablePrerequisitesComplete = assignmentPrerequisitesComplete && assignmentsComplete;

        var steps = new List<TimetableSetupStepDto>
        {
            new("study-days", "الأيام الدراسية", studyDaysComplete ? "complete" : hasProfile ? "not-started" : "blocked",
                studyDaysComplete ? "تم اختيار قالب أيام الدراسة والحصص." : hasProfile ? "لم يتم إعداد أيام الدراسة والحصص بعد." : "أنشئ ملف إعداد أولاً.",
                "/intelligent-timetable/timings"),
            new("teachers", "المعلمين",
                !hasProfile || !studyDaysComplete ? "blocked" : teachersComplete ? "complete" : data.ActiveTeachers > 0 ? "incomplete" : "not-started",
                teachersComplete
                    ? $"تم إعداد إتاحة جميع المعلمين النشطين ({data.ActiveTeachers})."
                    : !hasProfile || !studyDaysComplete
                        ? "اختر ملف إعداد وقالب توقيت قبل ضبط إتاحة المعلمين."
                        : data.ActiveTeachers == 0
                            ? "لا يوجد معلمون نشطون في المدرسة."
                            : $"تم إعداد إتاحة {data.ConfiguredTeachers} من {data.ActiveTeachers} معلم نشط.",
                "/intelligent-timetable/teachers"),
            new("classrooms", "الفصول", classroomsComplete ? "complete" : data.ActiveClassrooms > 0 ? "incomplete" : "not-started",
                classroomsComplete
                    ? "الفصول النشطة ومواقعها جاهزة."
                    : data.ActiveClassrooms == 0
                        ? "لا توجد فصول نشطة لهذا العام الدراسي."
                        : $"يوجد {data.ClassroomsMissingLocation} فصل بلا موقع مسجل.",
                "/student-affairs/classrooms"),
            new("subjects", "المواد", subjectsComplete ? "complete" : "not-started",
                subjectsComplete ? $"دليل المدرسة يحتوي على {data.AvailableSubjects} مادة نشطة." : "لم يتم إنشاء دليل المواد بعد.",
                "/intelligent-timetable/subjects"),
            new("assignments", "إسنادات",
                !assignmentPrerequisitesComplete ? "blocked" : assignmentsComplete ? "complete" : data.Assignments.Count > 0 ? "incomplete" : "not-started",
                assignmentsComplete
                    ? $"تم إسناد جميع متطلبات المواد ({data.RequirementCount})."
                    : !assignmentPrerequisitesComplete
                        ? "يتطلب فصولاً ومواد ومعلمين جاهزين أولاً."
                        : data.Assignments.Count == 0
                            ? "لم يتم إسناد متطلبات المواد إلى المعلمين بعد."
                            : $"الإسنادات الصحيحة تغطي جزءاً من {data.RequirementCount} متطلب.",
                "/intelligent-timetable/assignments"),
            new("subject-rules", "تخصيص المواد",
                !hasProfile || !subjectsComplete || data.ActiveClassrooms == 0 ? "blocked" : subjectRulesComplete ? "complete" : data.RequirementCount > 0 ? "incomplete" : "not-started",
                subjectRulesComplete
                    ? $"متطلبات المواد مسجلة لجميع الفصول النشطة ({data.CoveredClassrooms})."
                    : !hasProfile || !subjectsComplete || data.ActiveClassrooms == 0
                        ? "يتطلب ملف إعداد وفصولاً ومواد نشطة أولاً."
                        : data.RequirementCount == 0
                            ? "لم يتم تسجيل حصص وقواعد المواد للفصول بعد."
                            : $"متطلبات المواد تغطي {data.CoveredClassrooms} من {data.ActiveClassrooms} فصل نشط.",
                "/intelligent-timetable/subjects"),
            new("timetable", "الجدول",
                !timetablePrerequisitesComplete ? "blocked" : data.HasCurrentTimetable ? "complete" : data.TimetableExists ? "incomplete" : "not-started",
                data.HasCurrentTimetable && timetablePrerequisitesComplete
                    ? "تم إنشاء جدول حالي ومتوافق مع ملف الإعداد."
                    : !timetablePrerequisitesComplete
                        ? "التوليد متوقف حتى تكتمل جميع المتطلبات الأساسية."
                        : data.TimetableExists
                            ? "الجدول الموجود يحتاج إلى إعادة توليد أو مراجعة بعد تغيّر الإعدادات."
                            : "المتطلبات مكتملة ويمكن الآن إنشاء الجدول.",
                "/timetable")
        };

        var warnings = new List<string>();
        if (!hasProfile) warnings.Add("أنشئ ملف إعداد للعام والفصل الدراسي المحددين.");
        if (data.ActiveClassrooms == 0) warnings.Add("لا توجد فصول نشطة لهذا العام الدراسي.");
        if (data.ClassroomsMissingLocation > 0) warnings.Add($"سجّل الموقع الفعلي لـ {data.ClassroomsMissingLocation} فصل.");
        if (data.ActiveTeachers == 0) warnings.Add("لا يوجد معلمون نشطون في المدرسة.");
        else if (studyDaysComplete && !teachersComplete)
            warnings.Add($"أكمل إعداد إتاحة {data.ActiveTeachers - data.ConfiguredTeachers} معلم نشط.");
        if (!subjectsComplete) warnings.Add("أضف مواد نشطة إلى دليل المدرسة.");
        if (subjectsComplete && data.ActiveClassrooms > 0 && !subjectRulesComplete)
            warnings.Add($"أكمل متطلبات المواد لجميع الفصول؛ المغطى حالياً {data.CoveredClassrooms} من {data.ActiveClassrooms}.");
        if (assignmentPrerequisitesComplete && !assignmentsComplete)
            warnings.Add("أكمل الإسنادات الصحيحة لجميع متطلبات المواد.");
        if (data.TimetableExists && !data.HasCurrentTimetable)
            warnings.Add("الجدول الموجود لا يطابق آخر إعدادات محفوظة ويحتاج إلى مراجعة أو إعادة توليد.");
        return (steps, warnings);
    }

    private static bool IsCompleteAssignment(TimetableAssignmentReadinessData assignment)
    {
        if (assignment.Members.Count == 0 || assignment.Members.Any(member => !member.IsTeacherReady))
            return false;

        var requirement = new Domain.Entities.ClassSubjectRequirement
        {
            IndividualPeriodCount = assignment.IndividualPeriodCount,
            PairedBlockCount = assignment.PairedBlockCount
        };
        var request = new TeachingCellRequest(
            assignment.RequirementId,
            assignment.Mode,
            assignment.Members.Select(member => new TeachingMemberRequest(
                member.TeacherTimetableProfileId,
                member.AllocatedPeriodCount,
                member.AllocatedPairedBlockCount)).ToArray());

        try
        {
            TeachingAllocationPolicy.Validate(requirement, request);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
