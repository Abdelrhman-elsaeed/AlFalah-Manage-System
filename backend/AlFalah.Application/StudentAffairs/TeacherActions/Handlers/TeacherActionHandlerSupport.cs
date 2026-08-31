using AlFalah.Domain.Enums;

namespace AlFalah.Application.StudentAffairs.TeacherActions.Handlers;

internal static class TeacherActionHandlerSupport
{
    public const string AuthenticationRequired = "An authenticated teacher and active school are required";
    public const string PermissionDenied = "You do not have permission to perform this action";
    public const string ScopeDenied = "Student is not in the current teacher timetable scope";

    public static TimetableDay? ToTimetableDay(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Saturday => TimetableDay.Saturday,
        DayOfWeek.Sunday => TimetableDay.Sunday,
        DayOfWeek.Monday => TimetableDay.Monday,
        DayOfWeek.Tuesday => TimetableDay.Tuesday,
        DayOfWeek.Wednesday => TimetableDay.Wednesday,
        DayOfWeek.Thursday => TimetableDay.Thursday,
        _ => null
    };
}
