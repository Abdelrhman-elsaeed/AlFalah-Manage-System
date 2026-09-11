using AlFalah.Domain.Enums;

namespace AlFalah.Application.StudentAffairs.TeacherContext;

public sealed record TeacherContextLookup(
    int SchoolId,
    string TeacherUserId,
    DateOnly SchoolLocalDate,
    TimetableDay? SchoolLocalDay,
    int? CurrentPeriod,
    int FallbackPeriod,
    bool AllowOffHoursFallback,
    DateTimeOffset UtcNow,
    int? BellScheduleRevisionId = null);

public sealed record TeacherIdentitySnapshot(
    int InstructorProfileId,
    string UserId,
    string DisplayName);

public sealed record TeacherClassroomSnapshot(
    int Id,
    string Label,
    SchoolStage Stage,
    byte GradeLevel,
    string Section);

public sealed record TeacherTimetablePeriodSnapshot(
    int TimetableEntryId,
    int Period,
    string Subject,
    TeacherClassroomSnapshot Classroom);

public sealed record TeacherContextSnapshot(
    TeacherIdentitySnapshot Teacher,
    int TimetableRevision,
    TeacherTimetablePeriodSnapshot? CurrentPeriod,
    IReadOnlyList<TeacherRosterStudentSnapshot> Roster,
    int PendingGatePassAcknowledgements,
    int PendingEntryPermitAcknowledgements);

public sealed record TeacherRosterStudentSnapshot(
    int Id,
    string StudentNumber,
    string DisplayName,
    int ClassroomId,
    string ClassLabel,
    bool IsActive,
    string? PhotoUrl);

public interface ITeacherContextRepository
{
    Task<TeacherContextSnapshot?> GetTopPriorityAsync(
        TeacherContextLookup lookup,
        CancellationToken cancellationToken);

    Task<TeacherContextSnapshot?> GetPeriodRosterAsync(
        int schoolId,
        string teacherUserId,
        int timetableEntryId,
        DateOnly localDate,
        int? timingRevisionId,
        CancellationToken cancellationToken);
}
