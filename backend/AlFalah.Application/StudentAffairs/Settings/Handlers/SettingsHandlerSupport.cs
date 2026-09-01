using AlFalah.Application.StudentAffairs.DTOs.Settings;

namespace AlFalah.Application.StudentAffairs.Settings.Handlers;

public static class SettingsHandlerSupport
{
    public const string AuthenticationRequired = "An authenticated user and active school are required";
    public const string PermissionDenied = "You do not have permission to manage or view student affairs settings";
    public const string ConcurrencyConflict = "Settings have been modified by another user. Please reload and try again.";
    public const string NotFound = "Student affairs settings were not found for the active school";

    public static SchoolStudentAffairsSettingsDto CreateDefaultBaseline(DateTimeOffset effectiveFrom) =>
        new(
            Id: null,
            MorningDelayThresholdPerTerm: 10,
            BehaviorIncidentMultiplePerTerm: 10,
            AcademicConcernThresholdPerTerm: 3,
            ClassroomEntryPermitThresholdPerTerm: 5,
            AbsenceVisualAlertThresholdPerTerm: 3,
            AbsenceReferralThresholdPerTerm: 5,
            AbsenceChildRightsThresholdPerTerm: 10,
            BehaviorCountabilityPolicy: "all-upheld",
            ArrivalCutoffLocalTime: new TimeOnly(7, 0),
            ArrivalGraceMinutes: 0,
            EffectiveVersion: 1,
            EffectiveFrom: effectiveFrom,
            UsesLockedDefaults: true,
            RowVersion: string.Empty
        );

    public static string? ValidateThresholds(
        int morningDelay,
        int behaviorMultiple,
        int academicConcern,
        int classroomPermit,
        int absenceVisual,
        int absenceReferral,
        int absenceChildRights,
        int arrivalGraceMinutes,
        string? policy)
    {
        if (morningDelay <= 0) return "Morning delay threshold must be greater than 0";
        if (behaviorMultiple <= 0) return "Behavior incident multiple must be greater than 0";
        if (academicConcern <= 0) return "Academic concern threshold must be greater than 0";
        if (classroomPermit <= 0) return "Classroom entry permit threshold must be greater than 0";
        if (absenceVisual <= 0 || absenceReferral <= 0 || absenceChildRights <= 0)
            return "Absence thresholds must be greater than 0";
        if (absenceVisual >= absenceReferral || absenceReferral >= absenceChildRights)
            return "Absence escalation thresholds must strictly follow Visual < Referral < ChildRights order";
        if (arrivalGraceMinutes < 0) return "Arrival grace minutes must be greater than or equal to 0";
        if (string.IsNullOrWhiteSpace(policy)) return "Behavior countability policy is required";
        return null;
    }

    public static bool TryDecodeExpectedRowVersion(
        string encodedRowVersion,
        byte[] currentRowVersion,
        out byte[] expectedRowVersion)
    {
        expectedRowVersion = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(encodedRowVersion)) return false;
        try
        {
            expectedRowVersion = Convert.FromBase64String(encodedRowVersion);
            return expectedRowVersion.Length > 0
                && currentRowVersion.AsSpan().SequenceEqual(expectedRowVersion);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
