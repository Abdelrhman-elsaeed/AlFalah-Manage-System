namespace AlFalah.Domain.Enums;

/// <summary>
/// Visit categories — Arabic labels verbatim from <c>docs/11-CONSTANTS-AND-ENUMS.md</c>.
/// Phase 4 uses only the category text; the enum value is used by the DB.
/// The frontend resolves the Arabic label via i18n; the canonical mapping
/// lives in <see cref="VisitCategoryExtensions.ToArabicString"/>.
/// </summary>
public enum VisitCategory
{
    ExploratoryGuidance = 1,            // استطلاعية / توجيهية
    ClassroomOrPeriodic = 2,            // زيارة صفية أو دورية
    PeerExchange = 3,                   // زيارة تبادلية
    NewTeacherProbation = 4,            // زيارة التثبيت / الترسيم للمعلمين الجدد
    FollowUpSupport = 5,                // زيارة المتابعة والدعم
    SurpriseInspection = 6,             // زيارة مفاجئة / تفتيشية
    Emergency = 7,                      // زيارة طارئة
    LegalVerificationFollowUp = 8,      // زيارة التحقق / متابعة قانونية
    CentralCommittees = 9               // زيارة اللجان المركزية
}

/// <summary>
/// Visit sequence — Arabic labels verbatim from <c>docs/11-CONSTANTS-AND-ENUMS.md</c>.
/// </summary>
public enum VisitSequence
{
    First = 1,                          // أولى
    Second = 2,                         // ثانية
    Third = 3,                          // ثالثة
    FollowUp = 4                        // متابعة
}

public static class VisitCategoryExtensions
{
    /// <summary>
    /// Returns the canonical Arabic label verbatim from docs/11.
    /// Used by the seeder for any text that must NOT be i18n-bound
    /// (e.g. the initial analysis snapshot's domain labels).
    /// </summary>
    public static string ToArabicString(this VisitCategory c) => c switch
    {
        VisitCategory.ExploratoryGuidance => "استطلاعية / توجيهية",
        VisitCategory.ClassroomOrPeriodic => "زيارة صفية أو دورية",
        VisitCategory.PeerExchange => "زيارة تبادلية",
        VisitCategory.NewTeacherProbation => "زيارة التثبيت / الترسيم للمعلمين الجدد",
        VisitCategory.FollowUpSupport => "زيارة المتابعة والدعم",
        VisitCategory.SurpriseInspection => "زيارة مفاجئة / تفتيشية",
        VisitCategory.Emergency => "زيارة طارئة",
        VisitCategory.LegalVerificationFollowUp => "زيارة التحقق / متابعة قانونية",
        VisitCategory.CentralCommittees => "زيارة اللجان المركزية",
        _ => string.Empty
    };
}

public static class VisitSequenceExtensions
{
    public static string ToArabicString(this VisitSequence s) => s switch
    {
        VisitSequence.First => "أولى",
        VisitSequence.Second => "ثانية",
        VisitSequence.Third => "ثالثة",
        VisitSequence.FollowUp => "متابعة",
        _ => string.Empty
    };
}