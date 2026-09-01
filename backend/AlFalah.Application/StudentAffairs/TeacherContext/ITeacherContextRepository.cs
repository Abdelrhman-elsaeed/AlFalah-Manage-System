using AlFalah.Domain.Enums;

namespace AlFalah.Application.StudentAffairs.TeacherContext;

public sealed record TeacherContextLookup(
    int SchoolId,
    string TeacherUserId,
    DateOnly SchoolLocalDate,
    TimetableDay? SchoolLocalDay,
    byte? CurrentPeriod,
    byte FallbackPeriod,
    bool AllowOffHoursFallback,
    DateTimeOffset UtcNow);

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
    byte Period,
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
}
