using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Shared.Models;

namespace AlFalah.Application.StudentAffairs.Attendance;

public sealed record AttendanceRosterStudentSnapshot(
    int StudentId,
    int AcademicTermId);

public sealed record GuardianExcuseLinkSnapshot(
    int GuardianProfileId,
    bool GuardianIsActive,
    bool StudentIsActive,
    bool CanSubmitExcuses,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

public interface IAttendanceWorkflowRepository
{
    Task<IReadOnlyList<AttendanceRosterStudentSnapshot>> GetActiveRosterAsync(
        int schoolId,
        int classroomId,
        DateOnly attendanceDate,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyStudentAttendance>> GetAttendanceSheetForUpdateAsync(
        int schoolId,
        int classroomId,
        DateOnly attendanceDate,
        CancellationToken cancellationToken);

    Task<DailyStudentAttendance?> GetAttendanceForUpdateAsync(
        int schoolId,
        int attendanceId,
        CancellationToken cancellationToken);

    Task<GuardianExcuseLinkSnapshot?> GetGuardianExcuseLinkAsync(
        int schoolId,
        string guardianUserId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<AbsenceExcuseDto?> GetExcuseByIdempotencyKeyAsync(
        int schoolId,
        int guardianProfileId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<AbsenceExcuse?> GetExcuseForUpdateAsync(
        int schoolId,
        int excuseId,
        CancellationToken cancellationToken);

    void AddAttendance(DailyStudentAttendance attendance);
    void AddExcuse(AbsenceExcuse excuse);
    void SetExpectedRowVersion(AbsenceExcuse excuse, byte[] rowVersion);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task<StudentAttendanceSheetDto?> GetAttendanceSheetDtoAsync(
        int schoolId,
        int classroomId,
        DateOnly attendanceDate,
        string rosterRevision,
        CancellationToken cancellationToken);

    Task<AbsenceExcuseDto?> GetExcuseDtoAsync(
        int schoolId,
        int excuseId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AbsenceExcuseDto>> GetExcusesByAttendanceIdAsync(
        int schoolId,
        int attendanceId,
        CancellationToken cancellationToken);

    Task<PagedResult<StudentAttendanceRecordDto>> GetAttendanceRecordsAsync(
        int schoolId,
        StudentAttendanceRecordsQuery query,
        CancellationToken cancellationToken);

    Task<StudentAttendanceHistoryDto?> GetStudentAttendanceHistoryAsync(
        int schoolId,
        int studentId,
        int? academicTermId,
        CancellationToken cancellationToken);

    Task<StudentAttendanceRecordDto?> GetAttendanceRecordDtoAsync(
        int schoolId,
        int attendanceId,
        CancellationToken cancellationToken);

    Task<(AbsenceExcuseAttachment Attachment, AbsenceExcuse Excuse)?> GetExcuseAttachmentAsync(
        int schoolId,
        int excuseId,
        int attachmentId,
        CancellationToken cancellationToken);
}
