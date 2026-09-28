using AlFalah.Application.StudentAffairs.Attendance;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace AlFalah.Infrastructure.Repositories;

public sealed class AttendanceWorkflowRepository : IAttendanceWorkflowRepository
{
    private const string AttendanceSubmissionMessageType = "AttendanceSheet:";
    private readonly AlFalahDbContext _context;

    public AttendanceWorkflowRepository(AlFalahDbContext context) => _context = context;

    public Task<string?> GetAttendanceSubmissionFingerprintAsync(
        int schoolId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var messageId = BuildIdempotencyMessageId(idempotencyKey);
        return _context.InboxMessages
            .AsNoTracking()
            .Where(message => message.SchoolId == schoolId
                && message.MessageId == messageId
                && message.MessageType.StartsWith(AttendanceSubmissionMessageType))
            .Select(message => message.MessageType.Substring(AttendanceSubmissionMessageType.Length))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public void AddAttendanceSubmissionReceipt(
        int schoolId,
        string idempotencyKey,
        string requestFingerprint,
        DateTimeOffset processedAt) =>
        _context.InboxMessages.Add(new InboxMessage
        {
            SchoolId = schoolId,
            MessageId = BuildIdempotencyMessageId(idempotencyKey),
            MessageType = AttendanceSubmissionMessageType + requestFingerprint,
            ReceivedAt = processedAt,
            ProcessedAt = processedAt
        });

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
                && !enrollment.IsDeleted
                && enrollment.EnrolledOn <= attendanceDate
                && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= attendanceDate)
                && enrollment.Student.SchoolId == schoolId
                && enrollment.Student.IsActive
                && !enrollment.Student.IsDeleted
                && enrollment.AcademicTerm.SchoolId == schoolId
                && enrollment.AcademicTerm.IsActive
                && !enrollment.AcademicTerm.IsDeleted
                && enrollment.AcademicTerm.StartsOn <= attendanceDate
                && enrollment.AcademicTerm.EndsOn >= attendanceDate
                && enrollment.Classroom.SchoolId == schoolId
                && enrollment.Classroom.IsActive
                && !enrollment.Classroom.IsDeleted)
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
        string _,
        CancellationToken cancellationToken)
    {
        var classroom = await _context.Classrooms
            .AsNoTracking()
            .Where(item => item.Id == classroomId
                && item.SchoolId == schoolId
                && item.IsActive
                && !item.IsDeleted)
            .Select(item => new ClassroomSummaryDto(
                item.Id,
                item.ClassLabel,
                item.Stage.ToString(),
                item.GradeLevel,
                item.Section))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (classroom is null) return null;

        var roster = await GetActiveRosterAsync(schoolId, classroomId, attendanceDate, cancellationToken)
            .ConfigureAwait(false);
        var rosterStudentIds = roster.Select(item => item.StudentId).ToArray();

        var rows = await _context.DailyStudentAttendances
            .AsNoTracking()
            .Where(attendance => attendance.SchoolId == schoolId
                && attendance.ClassroomId == classroomId
                && attendance.AttendanceDate == attendanceDate
                && rosterStudentIds.Contains(attendance.StudentId))
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

        var rosterRevision = BuildRosterRevision(roster, rows.Select(row =>
            $"{row.Id}:{row.StudentId}:{(int)row.Status}:{(int?)row.ExcuseStatus}:{Convert.ToHexString(row.RowVersion)}"));

        var now = DateTimeOffset.UtcNow;

        var students = await _context.Students
            .AsNoTracking()
            .Where(student => student.SchoolId == schoolId
                && rosterStudentIds.Contains(student.Id)
                && student.IsActive
                && !student.IsDeleted)
            .OrderBy(student => student.StudentNumber)
            .Select(student => new
            {
                student.Id,
                student.StudentNumber,
                DisplayName = (student.FirstName + " " + (student.MiddleName ?? string.Empty) + " " + student.LastName).Trim(),
                student.IsActive
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var studentIds = students.Select(student => student.Id).ToArray();
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

        var rowByStudent = rows.GroupBy(row => row.StudentId).ToDictionary(group => group.Key, group => group.First());
        var termByStudent = roster.GroupBy(item => item.StudentId).ToDictionary(group => group.Key, group => group.First().AcademicTermId);
        var dtoRows = students.Select(student =>
        {
            rowByStudent.TryGetValue(student.Id, out var row);
            var academicTermId = row?.AcademicTermId ?? termByStudent[student.Id];
            metricByStudentAndTerm.TryGetValue((student.Id, academicTermId), out var metric);
            return new StudentAttendanceSheetRowDto(
                row?.Id,
                new StudentSummaryDto(
                    student.Id,
                    student.StudentNumber,
                    student.DisplayName,
                    classroomId,
                    classroom.Label,
                    student.IsActive,
                    null),
                row?.Status ?? StudentAttendanceStatus.Present,
                row?.ExcuseStatus,
                row is null ? null : new ActorSummaryDto(row.RecordedByUserId, row.RecorderDisplayName, RoleNames.Secretary),
                row?.RecordedAt,
                new MetricBadgeDto(
                    StudentTermMetricCode.PenaltyAbsenceDay,
                    metric?.Count ?? 0,
                    0,
                    null,
                    "None",
                    null,
                    metric?.RecalculatedAt ?? now),
                row is null ? null : Convert.ToBase64String(row.RowVersion));
        }).ToArray();

        return new StudentAttendanceSheetDto(
            attendanceDate,
            classroom,
            rosterRevision,
            true,
            dtoRows);
    }

    private static string BuildRosterRevision(
        IReadOnlyList<AttendanceRosterStudentSnapshot> roster,
        IEnumerable<string> attendanceRows)
    {
        var payload = string.Join('|', roster
            .OrderBy(item => item.StudentId)
            .ThenBy(item => item.AcademicTermId)
            .Select(item => $"r:{item.StudentId}:{item.AcademicTermId}"))
            + "||"
            + string.Join('|', attendanceRows.OrderBy(value => value, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static Guid BuildIdempotencyMessageId(string idempotencyKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey.Trim()));
        return new Guid(hash.AsSpan(0, 16));
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
                excuse.GuardianNotes,
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
            Convert.ToBase64String(projection.RowVersion),
            projection.GuardianNotes);
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
                excuse.GuardianNotes,
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
                Convert.ToBase64String(projection.RowVersion),
                projection.GuardianNotes);
        }).ToList();
    }

    public async Task<PagedResult<OfficerAbsenceExcuseQueueItemDto>> GetPendingExcusesAsync(
        int schoolId,
        OfficerAbsenceExcuseQueueQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);
        var dbQuery = _context.AbsenceExcuses.AsNoTracking()
            .Where(x => x.SchoolId == schoolId && !x.IsDeleted
                && x.Status == AbsenceExcuseStatus.Pending
                && !x.DailyStudentAttendance.IsDeleted);

        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(x => x.DailyStudentAttendance.AttendanceDate >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(x => x.DailyStudentAttendance.AttendanceDate <= query.ToDate.Value);
        if (query.ClassroomId.HasValue)
            dbQuery = dbQuery.Where(x => x.DailyStudentAttendance.ClassroomId == query.ClassroomId.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            dbQuery = dbQuery.Where(x =>
                x.DailyStudentAttendance.Student.StudentNumber.Contains(search)
                || x.DailyStudentAttendance.Student.FirstName.Contains(search)
                || (x.DailyStudentAttendance.Student.MiddleName != null
                    && x.DailyStudentAttendance.Student.MiddleName.Contains(search))
                || x.DailyStudentAttendance.Student.LastName.Contains(search));
        }

        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await dbQuery
            .OrderBy(x => x.SubmittedAt)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                Excuse = x,
                Attendance = x.DailyStudentAttendance,
                Student = x.DailyStudentAttendance.Student,
                ClassroomLabel = x.DailyStudentAttendance.Classroom.ClassLabel,
                RecorderName = (x.DailyStudentAttendance.RecordedByUser.FirstName + " "
                    + x.DailyStudentAttendance.RecordedByUser.LastName).Trim(),
                GuardianName = (x.GuardianProfile.ApplicationUser.FirstName + " "
                    + x.GuardianProfile.ApplicationUser.LastName).Trim(),
                GuardianLink = x.GuardianProfile.Students
                    .Where(link => link.SchoolId == schoolId
                        && link.StudentId == x.DailyStudentAttendance.StudentId)
                    .Select(link => new { link.RelationshipType, link.IsPrimary, link.ReceivesNotifications })
                    .FirstOrDefault(),
                Attachments = x.Attachments
                    .Where(a => !a.IsDeleted)
                    .OrderBy(a => a.Id)
                    .Select(a => new
                    {
                        a.Id,
                        a.OriginalFileName,
                        a.ContentType,
                        a.SizeBytes,
                        a.UploadedAt,
                        a.UploadedByUserId,
                        UploaderName = (a.UploadedByUser.FirstName + " " + a.UploadedByUser.LastName).Trim()
                    }).ToList()
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var items = rows.Select(row =>
        {
            var studentName = $"{row.Student.FirstName} {row.Student.MiddleName} {row.Student.LastName}".Trim();
            var attendance = new StudentAttendanceRecordDto(
                row.Attendance.Id,
                new StudentSummaryDto(
                    row.Student.Id,
                    row.Student.StudentNumber,
                    row.Student.IdentityNumber,
                    studentName,
                    row.Attendance.ClassroomId,
                    row.ClassroomLabel,
                    row.Student.IsActive,
                    row.Student.ProfilePhotoStorageKey),
                row.Attendance.AttendanceDate,
                row.Attendance.Status,
                row.Attendance.ExcuseStatus,
                new ActorSummaryDto(row.Attendance.RecordedByUserId, row.RecorderName, RoleNames.Secretary),
                row.Attendance.RecordedAt,
                Convert.ToBase64String(row.Attendance.RowVersion));

            var guardian = new GuardianSummaryDto(
                row.Excuse.GuardianProfileId,
                row.GuardianName,
                row.GuardianLink?.RelationshipType ?? 0,
                row.GuardianLink?.IsPrimary ?? false,
                row.GuardianLink?.ReceivesNotifications ?? false);
            var attachments = row.Attachments.Select(a => new AttachmentDto(
                a.Id,
                a.OriginalFileName,
                a.ContentType,
                a.SizeBytes,
                a.UploadedAt,
                new ActorSummaryDto(a.UploadedByUserId, a.UploaderName, RoleNames.Guardian),
                $"/api/v1/student-attendance/excuses/{row.Excuse.Id}/attachments/{a.Id}"))
                .ToArray();
            var excuse = new AbsenceExcuseDto(
                row.Excuse.Id,
                row.Excuse.ExcuseType,
                row.Excuse.Status,
                guardian,
                row.Excuse.SubmittedAt,
                null,
                null,
                row.Excuse.ReviewReason,
                attachments,
                Convert.ToBase64String(row.Excuse.RowVersion),
                row.Excuse.GuardianNotes);
            return new OfficerAbsenceExcuseQueueItemDto(attendance, excuse);
        }).ToList();

        return new PagedResult<OfficerAbsenceExcuseQueueItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
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

