using AlFalah.Application.StudentAffairs.Attendance;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class AttendanceWorkflowRepository : IAttendanceWorkflowRepository
{
    private readonly AlFalahDbContext _context;

    public AttendanceWorkflowRepository(AlFalahDbContext context) => _context = context;

    public async Task<IReadOnlyList<AttendanceRosterStudentSnapshot>> GetActiveRosterAsync(
        int schoolId,
        int classroomId,
        DateOnly attendanceDate,
        CancellationToken cancellationToken) =>
        await _context.StudentEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.SchoolId == schoolId
                && enrollment.ClassroomId == classroomId
                && enrollment.Status == StudentEnrollmentStatus.Active
                && enrollment.EnrolledOn <= attendanceDate
                && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= attendanceDate)
                && enrollment.Student.SchoolId == schoolId
                && enrollment.Student.IsActive
                && enrollment.AcademicTerm.SchoolId == schoolId
                && enrollment.AcademicTerm.IsActive
                && enrollment.AcademicTerm.StartsOn <= attendanceDate
                && enrollment.AcademicTerm.EndsOn >= attendanceDate
                && enrollment.Classroom.SchoolId == schoolId
                && enrollment.Classroom.IsActive)
            .OrderBy(enrollment => enrollment.RollNumber)
            .ThenBy(enrollment => enrollment.StudentId)
            .Select(enrollment => new AttendanceRosterStudentSnapshot(
                enrollment.StudentId,
                enrollment.AcademicTermId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<DailyStudentAttendance>> GetAttendanceSheetForUpdateAsync(
        int schoolId,
        int classroomId,
        DateOnly attendanceDate,
        CancellationToken cancellationToken) =>
        await _context.DailyStudentAttendances
            .AsTracking()
            .Where(attendance => attendance.SchoolId == schoolId
                && attendance.ClassroomId == classroomId
                && attendance.AttendanceDate == attendanceDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<DailyStudentAttendance?> GetAttendanceForUpdateAsync(
        int schoolId,
        int attendanceId,
        CancellationToken cancellationToken) =>
        _context.DailyStudentAttendances
            .AsTracking()
            .Where(attendance => attendance.Id == attendanceId && attendance.SchoolId == schoolId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<GuardianExcuseLinkSnapshot?> GetGuardianExcuseLinkAsync(
        int schoolId,
        string guardianUserId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken) =>
        _context.StudentGuardians
            .AsNoTracking()
            .Where(link => link.SchoolId == schoolId
                && link.StudentId == studentId
                && link.GuardianProfile.SchoolId == schoolId
                && link.GuardianProfile.ApplicationUserId == guardianUserId
                && link.ValidFrom <= onDate
                && (link.ValidTo == null || link.ValidTo >= onDate))
            .Select(link => new GuardianExcuseLinkSnapshot(
                link.GuardianProfileId,
                link.GuardianProfile.IsActive,
                link.Student.IsActive,
                link.CanSubmitExcuses,
                link.ValidFrom,
                link.ValidTo))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<AbsenceExcuseDto?> GetExcuseByIdempotencyKeyAsync(
        int schoolId,
        int guardianProfileId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var excuseId = await _context.AbsenceExcuses
            .AsNoTracking()
            .Where(excuse => excuse.SchoolId == schoolId
                && excuse.GuardianProfileId == guardianProfileId
                && excuse.IdempotencyKey == idempotencyKey)
            .Select(excuse => (int?)excuse.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return excuseId is null
            ? null
            : await GetExcuseDtoAsync(schoolId, excuseId.Value, cancellationToken).ConfigureAwait(false);
    }

    public Task<AbsenceExcuse?> GetExcuseForUpdateAsync(
        int schoolId,
        int excuseId,
        CancellationToken cancellationToken) =>
        _context.AbsenceExcuses
            .AsTracking()
            .Include(excuse => excuse.DailyStudentAttendance)
            .Where(excuse => excuse.Id == excuseId
                && excuse.SchoolId == schoolId
                && excuse.DailyStudentAttendance.SchoolId == schoolId)
            .FirstOrDefaultAsync(cancellationToken);

    public void AddAttendance(DailyStudentAttendance attendance) =>
        _context.DailyStudentAttendances.Add(attendance);

    public void AddExcuse(AbsenceExcuse excuse) => _context.AbsenceExcuses.Add(excuse);

    public void SetExpectedRowVersion(AbsenceExcuse excuse, byte[] rowVersion) =>
        _context.Entry(excuse).Property(entity => entity.RowVersion).OriginalValue = rowVersion;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new AttendanceConcurrencyException(exception);
        }
        catch (DbUpdateException exception)
        {
            throw new AttendancePersistenceConflictException(exception);
        }
    }

    public async Task<StudentAttendanceSheetDto?> GetAttendanceSheetDtoAsync(
        int schoolId,
        int classroomId,
        DateOnly attendanceDate,
        string rosterRevision,
        CancellationToken cancellationToken)
    {
        var classroom = await _context.Classrooms
            .AsNoTracking()
            .Where(item => item.Id == classroomId && item.SchoolId == schoolId && item.IsActive)
            .Select(item => new ClassroomSummaryDto(
                item.Id,
                item.ClassLabel,
                item.Stage.ToString(),
                item.GradeLevel,
                item.Section))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (classroom is null) return null;

        var rows = await _context.DailyStudentAttendances
            .AsNoTracking()
            .Where(attendance => attendance.SchoolId == schoolId
                && attendance.ClassroomId == classroomId
                && attendance.AttendanceDate == attendanceDate)
            .OrderBy(attendance => attendance.Student.StudentNumber)
            .Select(attendance => new
            {
                attendance.Id,
                attendance.StudentId,
                attendance.Student.StudentNumber,
                StudentDisplayName = (attendance.Student.FirstName + " "
                    + (attendance.Student.MiddleName ?? string.Empty) + " "
                    + attendance.Student.LastName).Trim(),
                attendance.Student.IsActive,
                attendance.Status,
                attendance.ExcuseStatus,
                attendance.RecordedByUserId,
                RecorderDisplayName = (attendance.RecordedByUser.FirstName + " "
                    + attendance.RecordedByUser.LastName).Trim(),
                attendance.RecordedAt,
                attendance.AcademicTermId,
                attendance.RowVersion
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var studentIds = rows.Select(row => row.StudentId).ToArray();
        var metrics = await _context.StudentTermMetrics
            .AsNoTracking()
            .Where(metric => metric.SchoolId == schoolId
                && studentIds.Contains(metric.StudentId)
                && metric.MetricCode == StudentTermMetricCode.PenaltyAbsenceDay)
            .Select(metric => new
            {
                metric.StudentId,
                metric.AcademicTermId,
                metric.Count,
                metric.RecalculatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var metricByStudentAndTerm = metrics.ToDictionary(
            metric => (metric.StudentId, metric.AcademicTermId));

        var now = DateTimeOffset.UtcNow;
        var dtoRows = rows.Select(row =>
        {
            metricByStudentAndTerm.TryGetValue((row.StudentId, row.AcademicTermId), out var metric);
            return new StudentAttendanceSheetRowDto(
                row.Id,
                new StudentSummaryDto(
                    row.StudentId,
                    row.StudentNumber,
                    row.StudentDisplayName,
                    classroomId,
                    classroom.Label,
                    row.IsActive,
                    null),
                row.Status,
                row.ExcuseStatus,
                new ActorSummaryDto(row.RecordedByUserId, row.RecorderDisplayName, RoleNames.Secretary),
                row.RecordedAt,
                new MetricBadgeDto(
                    StudentTermMetricCode.PenaltyAbsenceDay,
                    metric?.Count ?? 0,
                    0,
                    null,
                    "None",
                    null,
                    metric?.RecalculatedAt ?? now),
                Convert.ToBase64String(row.RowVersion));
        }).ToArray();

        return new StudentAttendanceSheetDto(
            attendanceDate,
            classroom,
            rosterRevision,
            dtoRows.Length > 0,
            dtoRows);
    }

    public async Task<AbsenceExcuseDto?> GetExcuseDtoAsync(
        int schoolId,
        int excuseId,
        CancellationToken cancellationToken)
    {
        var projection = await _context.AbsenceExcuses
            .AsNoTracking()
            .Where(excuse => excuse.Id == excuseId && excuse.SchoolId == schoolId)
            .Select(excuse => new
            {
                excuse.Id,
                excuse.ExcuseType,
                excuse.Status,
                excuse.GuardianProfileId,
                GuardianDisplayName = (excuse.GuardianProfile.ApplicationUser.FirstName + " "
                    + excuse.GuardianProfile.ApplicationUser.LastName).Trim(),
                GuardianLink = excuse.GuardianProfile.Students
                    .Where(link => link.SchoolId == schoolId
                        && link.StudentId == excuse.DailyStudentAttendance.StudentId)
                    .Select(link => new
                    {
                        link.RelationshipType,
                        link.IsPrimary,
                        link.ReceivesNotifications
                    })
                    .FirstOrDefault(),
                excuse.SubmittedAt,
                excuse.ReviewedByUserId,
                ReviewerDisplayName = excuse.ReviewedByUser == null
                    ? null
                    : (excuse.ReviewedByUser.FirstName + " " + excuse.ReviewedByUser.LastName).Trim(),
                excuse.ReviewedAt,
                excuse.ReviewReason,
                excuse.RowVersion,
                Attachments = excuse.Attachments
                    .OrderBy(attachment => attachment.Id)
                    .Select(attachment => new
                    {
                        attachment.Id,
                        attachment.OriginalFileName,
                        attachment.ContentType,
                        attachment.SizeBytes,
                        attachment.UploadedAt,
                        attachment.UploadedByUserId,
                        UploaderDisplayName = (attachment.UploadedByUser.FirstName + " "
                            + attachment.UploadedByUser.LastName).Trim()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (projection is null) return null;

        var guardianLink = projection.GuardianLink;
        var guardian = new GuardianSummaryDto(
            projection.GuardianProfileId,
            projection.GuardianDisplayName,
            guardianLink?.RelationshipType ?? 0,
            guardianLink?.IsPrimary ?? false,
            guardianLink?.ReceivesNotifications ?? false);
        var reviewer = projection.ReviewedByUserId is null
            ? null
            : new ActorSummaryDto(
                projection.ReviewedByUserId,
                projection.ReviewerDisplayName ?? string.Empty,
                RoleNames.StudentAffairsOfficer);
        var attachments = projection.Attachments.Select(attachment => new AttachmentDto(
            attachment.Id,
            attachment.OriginalFileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.UploadedAt,
            new ActorSummaryDto(
                attachment.UploadedByUserId,
                attachment.UploaderDisplayName,
                RoleNames.Guardian),
            $"/api/v1/student-attendance/excuses/{projection.Id}/attachments/{attachment.Id}"))
            .ToArray();

        return new AbsenceExcuseDto(
            projection.Id,
            projection.ExcuseType,
            projection.Status,
            guardian,
            projection.SubmittedAt,
            reviewer,
            projection.ReviewedAt,
            projection.ReviewReason,
            attachments,
            Convert.ToBase64String(projection.RowVersion));
    }
}
