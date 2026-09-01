using AlFalah.Application.StudentAffairs.Attendance;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
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

        var now = DateTimeOffset.UtcNow;

        if (rows.Count == 0)
        {
            var roster = await GetActiveRosterAsync(schoolId, classroomId, attendanceDate, cancellationToken)
                .ConfigureAwait(false);
            var rosterStudentIds = roster.Select(r => r.StudentId).ToArray();
            var students = await _context.Students
                .AsNoTracking()
                .Where(s => s.SchoolId == schoolId && rosterStudentIds.Contains(s.Id))
                .OrderBy(s => s.StudentNumber)
                .Select(s => new
                {
                    s.Id,
                    s.StudentNumber,
                    DisplayName = (s.FirstName + " " + (s.MiddleName ?? string.Empty) + " " + s.LastName).Trim(),
                    s.IsActive
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var emptyRows = students.Select(s => new StudentAttendanceSheetRowDto(
                null,
                new StudentSummaryDto(
                    s.Id,
                    s.StudentNumber,
                    s.DisplayName,
                    classroomId,
                    classroom.Label,
                    s.IsActive,
                    null),
                StudentAttendanceStatus.Present,
                null,
                null,
                null,
                new MetricBadgeDto(
                    StudentTermMetricCode.PenaltyAbsenceDay,
                    0,
                    0,
                    null,
                    "None",
                    null,
                    now),
                null)).ToArray();

            return new StudentAttendanceSheetDto(
                attendanceDate,
                classroom,
                rosterRevision,
                false,
                emptyRows);
        }

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
            true,
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

    public async Task<IReadOnlyList<AbsenceExcuseDto>> GetExcusesByAttendanceIdAsync(
        int schoolId,
        int attendanceId,
        CancellationToken cancellationToken)
    {
        var projections = await _context.AbsenceExcuses
            .AsNoTracking()
            .Where(excuse => excuse.DailyStudentAttendanceId == attendanceId && excuse.SchoolId == schoolId)
            .OrderByDescending(excuse => excuse.SubmittedAt)
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
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return projections.Select(projection =>
        {
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
        }).ToList();
    }

    public async Task<PagedResult<StudentAttendanceRecordDto>> GetAttendanceRecordsAsync(
        int schoolId,
        StudentAttendanceRecordsQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? 25 : query.PageSize;

        var dbQuery = _context.DailyStudentAttendances
            .AsNoTracking()
            .Where(a => a.SchoolId == schoolId);

        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(a => a.AttendanceDate >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(a => a.AttendanceDate <= query.ToDate.Value);

        if (query.ClassroomId.HasValue)
            dbQuery = dbQuery.Where(a => a.ClassroomId == query.ClassroomId.Value);

        if (query.StudentId.HasValue)
            dbQuery = dbQuery.Where(a => a.StudentId == query.StudentId.Value);

        if (query.Status.HasValue)
            dbQuery = dbQuery.Where(a => a.Status == query.Status.Value);

        if (query.ExcuseStatus.HasValue)
            dbQuery = dbQuery.Where(a => a.ExcuseStatus == query.ExcuseStatus.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            dbQuery = dbQuery.Where(a =>
                a.Student.FirstName.Contains(search)
                || (a.Student.MiddleName != null && a.Student.MiddleName.Contains(search))
                || a.Student.LastName.Contains(search)
                || a.Student.StudentNumber.Contains(search));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var projections = await dbQuery
            .OrderByDescending(a => a.AttendanceDate)
            .ThenBy(a => a.Student.StudentNumber)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.StudentId,
                a.Student.StudentNumber,
                StudentDisplayName = (a.Student.FirstName + " "
                    + (a.Student.MiddleName ?? string.Empty) + " "
                    + a.Student.LastName).Trim(),
                a.Student.IsActive,
                a.ClassroomId,
                ClassroomLabel = a.Classroom.ClassLabel,
                a.AttendanceDate,
                a.Status,
                a.ExcuseStatus,
                a.RecordedByUserId,
                RecorderDisplayName = (a.RecordedByUser.FirstName + " "
                    + a.RecordedByUser.LastName).Trim(),
                a.RecordedAt,
                a.RowVersion
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = projections.Select(p => new StudentAttendanceRecordDto(
            p.Id,
            new StudentSummaryDto(
                p.StudentId,
                p.StudentNumber,
                p.StudentDisplayName,
                p.ClassroomId,
                p.ClassroomLabel,
                p.IsActive,
                null),
            p.AttendanceDate,
            p.Status,
            p.ExcuseStatus,
            new ActorSummaryDto(
                p.RecordedByUserId,
                p.RecorderDisplayName,
                RoleNames.Secretary),
            p.RecordedAt,
            Convert.ToBase64String(p.RowVersion)
        )).ToList();

        return new PagedResult<StudentAttendanceRecordDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<StudentAttendanceRecordDto?> GetAttendanceRecordDtoAsync(
        int schoolId,
        int attendanceId,
        CancellationToken cancellationToken)
    {
        var p = await _context.DailyStudentAttendances
            .AsNoTracking()
            .Where(a => a.Id == attendanceId && a.SchoolId == schoolId)
            .Select(a => new
            {
                a.Id,
                a.StudentId,
                a.Student.StudentNumber,
                StudentDisplayName = (a.Student.FirstName + " "
                    + (a.Student.MiddleName ?? string.Empty) + " "
                    + a.Student.LastName).Trim(),
                a.Student.IsActive,
                a.ClassroomId,
                ClassroomLabel = a.Classroom.ClassLabel,
                a.AttendanceDate,
                a.Status,
                a.ExcuseStatus,
                a.RecordedByUserId,
                RecorderDisplayName = (a.RecordedByUser.FirstName + " "
                    + a.RecordedByUser.LastName).Trim(),
                a.RecordedAt,
                a.RowVersion
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (p is null) return null;

        return new StudentAttendanceRecordDto(
            p.Id,
            new StudentSummaryDto(
                p.StudentId,
                p.StudentNumber,
                p.StudentDisplayName,
                p.ClassroomId,
                p.ClassroomLabel,
                p.IsActive,
                null),
            p.AttendanceDate,
            p.Status,
            p.ExcuseStatus,
            new ActorSummaryDto(
                p.RecordedByUserId,
                p.RecorderDisplayName,
                RoleNames.Secretary),
            p.RecordedAt,
            Convert.ToBase64String(p.RowVersion));
    }

    public async Task<StudentAttendanceHistoryDto?> GetStudentAttendanceHistoryAsync(
        int schoolId,
        int studentId,
        int? academicTermId,
        CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .AsNoTracking()
            .Where(s => s.Id == studentId && s.SchoolId == schoolId)
            .Select(s => new
            {
                s.Id,
                s.StudentNumber,
                DisplayName = (s.FirstName + " " + (s.MiddleName ?? string.Empty) + " " + s.LastName).Trim(),
                s.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (student is null) return null;

        AcademicTerm? term = null;
        if (academicTermId.HasValue)
        {
            term = await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == academicTermId.Value && t.SchoolId == schoolId, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            term = await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.SchoolId == schoolId && t.IsActive && t.StartsOn <= today && t.EndsOn >= today, cancellationToken)
                .ConfigureAwait(false)
                ?? await _context.AcademicTerms
                    .AsNoTracking()
                    .Where(t => t.SchoolId == schoolId && t.IsActive)
                    .OrderByDescending(t => t.StartsOn)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
        }

        if (term is null) return null;

        var enrollment = await _context.StudentEnrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.SchoolId == schoolId && e.AcademicTermId == term.Id && e.Status == StudentEnrollmentStatus.Active)
            .Select(e => new { e.ClassroomId, e.Classroom.ClassLabel })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var recordsRaw = await _context.DailyStudentAttendances
            .AsNoTracking()
            .Where(a => a.SchoolId == schoolId && a.StudentId == studentId && a.AcademicTermId == term.Id)
            .OrderByDescending(a => a.AttendanceDate)
            .Select(a => new
            {
                a.Id,
                a.StudentId,
                StudentNumber = student.StudentNumber,
                StudentDisplayName = student.DisplayName,
                StudentIsActive = student.IsActive,
                a.ClassroomId,
                ClassroomLabel = a.Classroom.ClassLabel,
                a.AttendanceDate,
                a.Status,
                a.ExcuseStatus,
                a.RecordedByUserId,
                RecorderDisplayName = (a.RecordedByUser.FirstName + " " + a.RecordedByUser.LastName).Trim(),
                a.RecordedAt,
                a.RowVersion
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var records = recordsRaw.Select(p => new StudentAttendanceRecordDto(
            p.Id,
            new StudentSummaryDto(
                p.StudentId,
                p.StudentNumber,
                p.StudentDisplayName,
                p.ClassroomId,
                p.ClassroomLabel,
                p.StudentIsActive,
                null),
            p.AttendanceDate,
            p.Status,
            p.ExcuseStatus,
            new ActorSummaryDto(
                p.RecordedByUserId,
                p.RecorderDisplayName,
                RoleNames.Secretary),
            p.RecordedAt,
            Convert.ToBase64String(p.RowVersion)
        )).ToList();

        var metric = await _context.StudentTermMetrics
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.SchoolId == schoolId && m.StudentId == studentId && m.AcademicTermId == term.Id && m.MetricCode == StudentTermMetricCode.PenaltyAbsenceDay, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var metricBadge = new MetricBadgeDto(
            StudentTermMetricCode.PenaltyAbsenceDay,
            metric?.Count ?? 0,
            0,
            null,
            "None",
            null,
            metric?.RecalculatedAt ?? now);

        return new StudentAttendanceHistoryDto(
            new StudentSummaryDto(
                student.Id,
                student.StudentNumber,
                student.DisplayName,
                enrollment?.ClassroomId,
                enrollment?.ClassLabel,
                student.IsActive,
                null),
            new AcademicTermSummaryDto(
                term.Id,
                term.Semester.ToString(),
                term.StartsOn,
                term.EndsOn,
                term.IsActive),
            records,
            metricBadge);
    }

    public async Task<(AbsenceExcuseAttachment Attachment, AbsenceExcuse Excuse)?> GetExcuseAttachmentAsync(
        int schoolId,
        int excuseId,
        int attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await _context.AbsenceExcuseAttachments
            .AsNoTracking()
            .Include(a => a.AbsenceExcuse)
            .ThenInclude(e => e.DailyStudentAttendance)
            .FirstOrDefaultAsync(a => a.Id == attachmentId
                && a.AbsenceExcuseId == excuseId
                && a.SchoolId == schoolId, cancellationToken)
            .ConfigureAwait(false);

        if (attachment is null || attachment.AbsenceExcuse is null)
            return null;

        return (attachment, attachment.AbsenceExcuse);
    }
}

