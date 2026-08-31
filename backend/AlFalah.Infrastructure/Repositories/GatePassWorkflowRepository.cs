using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.GatePasses;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class GatePassWorkflowRepository : IGatePassWorkflowRepository
{
    private readonly AlFalahDbContext _context;

    public GatePassWorkflowRepository(AlFalahDbContext context) => _context = context;

    public Task<GuardianGatePassLinkSnapshot?> GetGuardianLinkAsync(
        int schoolId,
        string guardianUserId,
        int studentId,
        CancellationToken cancellationToken) =>
        _context.StudentGuardians
            .AsNoTracking()
            .Where(link => link.SchoolId == schoolId
                && link.StudentId == studentId
                && link.GuardianProfile.SchoolId == schoolId
                && link.GuardianProfile.ApplicationUserId == guardianUserId)
            .Select(link => new GuardianGatePassLinkSnapshot(
                link.GuardianProfileId,
                link.GuardianProfile.IsActive,
                link.Student.IsActive,
                link.CanRequestGatePass,
                link.ValidFrom,
                link.ValidTo))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> IsGuardianLinkActiveAsync(
        int schoolId,
        int guardianProfileId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken) =>
        _context.StudentGuardians.AsNoTracking().AnyAsync(link =>
            link.SchoolId == schoolId
            && link.GuardianProfileId == guardianProfileId
            && link.StudentId == studentId
            && link.GuardianProfile.SchoolId == schoolId
            && link.GuardianProfile.IsActive
            && link.Student.IsActive
            && link.CanRequestGatePass
            && link.ValidFrom <= onDate
            && (link.ValidTo == null || link.ValidTo >= onDate), cancellationToken);

    public Task<GatePassEnrollmentSnapshot?> GetActiveEnrollmentAsync(
        int schoolId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken) =>
        _context.StudentEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.SchoolId == schoolId
                && enrollment.StudentId == studentId
                && enrollment.Student.IsActive
                && enrollment.Status == StudentEnrollmentStatus.Active
                && enrollment.EnrolledOn <= onDate
                && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= onDate)
                && enrollment.AcademicTerm.SchoolId == schoolId
                && enrollment.AcademicTerm.IsActive
                && enrollment.AcademicTerm.StartsOn <= onDate
                && enrollment.AcademicTerm.EndsOn >= onDate
                && enrollment.Classroom.SchoolId == schoolId
                && enrollment.Classroom.IsActive)
            .Select(enrollment => new GatePassEnrollmentSnapshot(
                enrollment.AcademicTermId,
                enrollment.AcademicTerm.AcademicYearId,
                enrollment.AcademicTerm.Semester,
                enrollment.ClassroomId,
                enrollment.Classroom.ClassLabel))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<GatePassDto?> GetByIdempotencyKeyAsync(
        int schoolId,
        int guardianProfileId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var gatePassId = await _context.GatePasses
            .AsNoTracking()
            .Where(gatePass => gatePass.SchoolId == schoolId
                && gatePass.RequestedByGuardianProfileId == guardianProfileId
                && gatePass.IdempotencyKey == idempotencyKey)
            .Select(gatePass => (int?)gatePass.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return gatePassId is null
            ? null
            : await GetDtoAsync(schoolId, gatePassId.Value, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> HasOverlappingActivePassAsync(
        int schoolId,
        int studentId,
        DateTimeOffset windowStartsAt,
        DateTimeOffset windowEndsAt,
        CancellationToken cancellationToken) =>
        _context.GatePasses.AsNoTracking().AnyAsync(gatePass =>
            gatePass.SchoolId == schoolId
            && gatePass.StudentId == studentId
            && (gatePass.Status == GatePassStatus.Requested
                || gatePass.Status == GatePassStatus.Approved
                || gatePass.Status == GatePassStatus.SecurityAcknowledged)
            && (gatePass.ApprovedWindowStartsAt ?? gatePass.RequestedExitAt) <= windowEndsAt
            && (gatePass.ApprovedWindowEndsAt ?? gatePass.RequestedExitAt) >= windowStartsAt,
            cancellationToken);

    public Task<GatePass?> GetForUpdateAsync(
        int schoolId,
        int gatePassId,
        CancellationToken cancellationToken) =>
        _context.GatePasses
            .AsTracking()
            .Where(gatePass => gatePass.Id == gatePassId && gatePass.SchoolId == schoolId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<GatePassTimetableSnapshot?> ResolvePublishedTimetableAsync(
        int schoolId,
        int academicYearId,
        TimetableSemester semester,
        int classroomId,
        string classroomLabel,
        TimetableDay day,
        CancellationToken cancellationToken)
    {
        var exactMatches = await PublishedEntries(schoolId, academicYearId, semester, day)
            .Where(entry => entry.ClassroomId == classroomId)
            .Select(entry => new GatePassTimetableSnapshot(
                entry.SchoolTimetableId,
                entry.Id,
                entry.InstructorProfileId,
                entry.Period))
            .Take(2)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (exactMatches.Count == 1) return exactMatches[0];
        if (exactMatches.Count > 1) return null;

        var fallbackMatches = await PublishedEntries(schoolId, academicYearId, semester, day)
            .Where(entry => entry.ClassroomId == null && entry.ClassLabel == classroomLabel)
            .Select(entry => new GatePassTimetableSnapshot(
                entry.SchoolTimetableId,
                entry.Id,
                entry.InstructorProfileId,
                entry.Period))
            .Take(2)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return fallbackMatches.Count == 1 ? fallbackMatches[0] : null;
    }

    public void Add(GatePass gatePass) => _context.GatePasses.Add(gatePass);

    public void SetExpectedRowVersion(GatePass gatePass, byte[] rowVersion) =>
        _context.Entry(gatePass).Property(entity => entity.RowVersion).OriginalValue = rowVersion;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new GatePassConcurrencyException(exception);
        }
        catch (DbUpdateException exception)
        {
            throw new GatePassPersistenceConflictException(exception);
        }
    }

    public async Task<GatePassDto?> GetDtoAsync(
        int schoolId,
        int gatePassId,
        CancellationToken cancellationToken)
    {
        var projection = await _context.GatePasses
            .AsNoTracking()
            .Where(gatePass => gatePass.Id == gatePassId && gatePass.SchoolId == schoolId)
            .Select(gatePass => new
            {
                gatePass.Id,
                gatePass.StudentId,
                gatePass.RequestedAt,
                gatePass.RequestedExitAt,
                gatePass.Reason,
                gatePass.PickupPersonName,
                gatePass.PickupRelationship,
                gatePass.PickupIdentityHint,
                gatePass.Status,
                gatePass.ApprovedWindowStartsAt,
                gatePass.ApprovedWindowEndsAt,
                gatePass.ReviewedAt,
                gatePass.ExitedAt,
                gatePass.CurrentClassroomId,
                gatePass.RowVersion,
                gatePass.Student.StudentNumber,
                gatePass.Student.IsActive,
                StudentDisplayName = (gatePass.Student.FirstName + " "
                    + (gatePass.Student.MiddleName ?? string.Empty) + " "
                    + gatePass.Student.LastName).Trim(),
                ClassroomLabel = gatePass.CurrentClassroom == null ? null : gatePass.CurrentClassroom.ClassLabel,
                ClassroomStage = gatePass.CurrentClassroom == null ? null : gatePass.CurrentClassroom.Stage.ToString(),
                ClassroomGrade = gatePass.CurrentClassroom == null ? (byte?)null : gatePass.CurrentClassroom.GradeLevel,
                ClassroomSection = gatePass.CurrentClassroom == null ? null : gatePass.CurrentClassroom.Section,
                TeacherUserId = gatePass.CurrentInstructorProfile == null ? null : gatePass.CurrentInstructorProfile.UserId,
                TeacherFirstName = gatePass.CurrentInstructorProfile == null ? null : gatePass.CurrentInstructorProfile.User.FirstName,
                TeacherLastName = gatePass.CurrentInstructorProfile == null ? null : gatePass.CurrentInstructorProfile.User.LastName
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (projection is null) return null;
        var classroom = projection.CurrentClassroomId is null
            ? null
            : new ClassroomSummaryDto(
                projection.CurrentClassroomId.Value,
                projection.ClassroomLabel ?? string.Empty,
                projection.ClassroomStage ?? string.Empty,
                projection.ClassroomGrade ?? 0,
                projection.ClassroomSection ?? string.Empty);
        var teacher = projection.TeacherUserId is null
            ? null
            : new ActorSummaryDto(
                projection.TeacherUserId,
                $"{projection.TeacherFirstName} {projection.TeacherLastName}".Trim(),
                RoleNames.Instructor);

        return new GatePassDto(
            projection.Id,
            new StudentSummaryDto(
                projection.StudentId,
                projection.StudentNumber,
                projection.StudentDisplayName,
                projection.CurrentClassroomId,
                projection.ClassroomLabel,
                projection.IsActive,
                null),
            projection.RequestedAt,
            projection.RequestedExitAt,
            projection.Reason,
            new PickupPersonDto(
                projection.PickupPersonName,
                projection.PickupRelationship,
                projection.PickupIdentityHint),
            projection.Status,
            projection.ApprovedWindowStartsAt,
            projection.ApprovedWindowEndsAt,
            projection.ReviewedAt,
            projection.ExitedAt,
            classroom,
            teacher,
            Array.Empty<NotificationDeliveryDto>(),
            Convert.ToBase64String(projection.RowVersion));
    }

    private IQueryable<AlFalah.Domain.Entities.SchoolTimetableEntry> PublishedEntries(
        int schoolId,
        int academicYearId,
        TimetableSemester semester,
        TimetableDay day) =>
        _context.SchoolTimetableEntries
            .AsNoTracking()
            .Where(entry => entry.SchoolId == schoolId
                && entry.Day == day
                && entry.EntryType == TimetableEntryType.Lesson
                && entry.InstructorProfile.SchoolId == schoolId
                && entry.InstructorProfile.IsActive
                && entry.SchoolTimetable.SchoolId == schoolId
                && entry.SchoolTimetable.AcademicYearId == academicYearId
                && entry.SchoolTimetable.Semester == semester
                && entry.SchoolTimetable.IsPublished);
}
