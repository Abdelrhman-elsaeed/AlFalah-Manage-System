using AlFalah.Application.StudentAffairs.DTOs.Permits;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;

namespace AlFalah.Application.StudentAffairs.Permits;

public enum ClassroomEntryPermitViewerScope
{
    Officer = 1,
    Instructor = 2,
    Guardian = 3
}

public sealed record ClassroomEntryPermitEnrollmentSnapshot(
    int AcademicTermId,
    int AcademicYearId,
    TimetableSemester Semester,
    int ClassroomId);

public sealed record ClassroomEntryPermitSaveResult(int PermitId, bool Created);

public interface IClassroomEntryPermitWorkflowRepository
{
    Task<ClassroomEntryPermitEnrollmentSnapshot?> GetActiveEnrollmentAsync(
        int schoolId,
        int studentId,
        int? classroomId,
        int academicYearId,
        TimetableSemester semester,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<int?> FindEquivalentActivePermitIdAsync(
        int schoolId,
        int studentId,
        int timetableEntryId,
        DateTimeOffset validFrom,
        DateTimeOffset validUntil,
        CancellationToken cancellationToken);

    Task<ClassroomEntryPermit?> GetForUpdateAsync(
        int schoolId,
        int permitId,
        CancellationToken cancellationToken);

    Task<int?> GetInstructorProfileIdAsync(
        int schoolId,
        string teacherUserId,
        CancellationToken cancellationToken);

    Task<ClassroomEntryPermitSaveResult> SaveNewPermitAsync(
        ClassroomEntryPermit permit,
        CancellationToken cancellationToken);
    void Add(ClassroomEntryPermit permit);
    void SetExpectedRowVersion(ClassroomEntryPermit permit, byte[] rowVersion);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task<ClassroomEntryPermitDto?> GetDtoAsync(
        int schoolId,
        int permitId,
        ClassroomEntryPermitViewerScope scope,
        string userId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken);

    Task<PagedResult<ClassroomEntryPermitDto>> GetPermitsAsync(
        int schoolId,
        ClassroomEntryPermitListQuery query,
        ClassroomEntryPermitViewerScope scope,
        string userId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken);
}

public sealed class ClassroomEntryPermitConcurrencyException(Exception innerException)
    : Exception("Classroom entry permit was modified by another user", innerException);

public sealed class ClassroomEntryPermitPersistenceConflictException(Exception innerException)
    : Exception("An equivalent classroom entry permit was created concurrently", innerException);
