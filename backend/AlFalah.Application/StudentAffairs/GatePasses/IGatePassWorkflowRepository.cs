using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;

namespace AlFalah.Application.StudentAffairs.GatePasses;

public sealed record GuardianGatePassLinkSnapshot(
    int GuardianProfileId,
    bool GuardianIsActive,
    bool StudentIsActive,
    bool CanRequestGatePass,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

public sealed record GatePassEnrollmentSnapshot(
    int AcademicTermId,
    int AcademicYearId,
    TimetableSemester Semester,
    int ClassroomId,
    string ClassroomLabel);

public sealed record GatePassTimetableSnapshot(
    int SchoolTimetableId,
    int SchoolTimetableEntryId,
    int InstructorProfileId,
    byte Period);

public interface IGatePassWorkflowRepository
{
    Task<GuardianGatePassLinkSnapshot?> GetGuardianLinkAsync(
        int schoolId,
        string guardianUserId,
        int studentId,
        CancellationToken cancellationToken);

    Task<bool> IsGuardianLinkActiveAsync(
        int schoolId,
        int guardianProfileId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<GatePassEnrollmentSnapshot?> GetActiveEnrollmentAsync(
        int schoolId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<GatePassDto?> GetByIdempotencyKeyAsync(
        int schoolId,
        int guardianProfileId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<bool> HasOverlappingActivePassAsync(
        int schoolId,
        int studentId,
        DateTimeOffset windowStartsAt,
        DateTimeOffset windowEndsAt,
        CancellationToken cancellationToken);

    Task<GatePass?> GetForUpdateAsync(int schoolId, int gatePassId, CancellationToken cancellationToken);

    Task<GatePassTimetableSnapshot?> ResolvePublishedTimetableAsync(
        int schoolId,
        int academicYearId,
        TimetableSemester semester,
        int classroomId,
        string classroomLabel,
        TimetableDay day,
        CancellationToken cancellationToken);

    void Add(GatePass gatePass);
    void SetExpectedRowVersion(GatePass gatePass, byte[] rowVersion);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<GatePassDto?> GetDtoAsync(int schoolId, int gatePassId, CancellationToken cancellationToken);

    Task<PagedResult<GatePassDto>> GetGatePassesAsync(
        int schoolId,
        GatePassListQuery query,
        CancellationToken cancellationToken);

    Task<PagedResult<GatePassDto>> GetMyGatePassesAsync(
        int schoolId,
        string guardianUserId,
        GatePassListQuery query,
        CancellationToken cancellationToken);

    Task<PagedResult<SecurityGatePassQueueItemDto>> GetSecurityGatePassQueueAsync(
        int schoolId,
        GatePassListQuery query,
        CancellationToken cancellationToken);

    Task<GatePassHistoryDto?> GetHistoryAsync(
        int schoolId,
        int gatePassId,
        CancellationToken cancellationToken);
}
