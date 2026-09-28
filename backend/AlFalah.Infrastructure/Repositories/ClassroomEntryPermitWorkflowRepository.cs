using AlFalah.Application.StudentAffairs.DTOs.Permits;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.Permits;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using System.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class ClassroomEntryPermitWorkflowRepository(AlFalahDbContext context)
    : IClassroomEntryPermitWorkflowRepository
{
    public Task<ClassroomEntryPermitEnrollmentSnapshot?> GetActiveEnrollmentAsync(
        int schoolId,
        int studentId,
        int? classroomId,
        int academicYearId,
        TimetableSemester semester,
        DateOnly onDate,
        CancellationToken cancellationToken) =>
        context.StudentEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.SchoolId == schoolId
                && enrollment.StudentId == studentId
                && enrollment.Student.SchoolId == schoolId
                && enrollment.Student.IsActive
                && (classroomId == null || enrollment.ClassroomId == classroomId)
                && enrollment.Classroom.SchoolId == schoolId
                && enrollment.Classroom.IsActive
                && enrollment.Status == StudentEnrollmentStatus.Active
                && enrollment.EnrolledOn <= onDate
                && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= onDate)
                && enrollment.AcademicTerm.SchoolId == schoolId
                && enrollment.AcademicTerm.IsActive
                && enrollment.AcademicTerm.StartsOn <= onDate
                && enrollment.AcademicTerm.EndsOn >= onDate
                && enrollment.AcademicTerm.AcademicYearId == academicYearId
                && enrollment.AcademicTerm.Semester == semester)
            .Select(enrollment => new ClassroomEntryPermitEnrollmentSnapshot(
                enrollment.AcademicTermId,
                enrollment.AcademicTerm.AcademicYearId,
                enrollment.AcademicTerm.Semester,
                enrollment.ClassroomId))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<int?> FindEquivalentActivePermitIdAsync(
        int schoolId,
        int studentId,
        int timetableEntryId,
        DateTimeOffset validFrom,
        DateTimeOffset validUntil,
        CancellationToken cancellationToken) =>
        context.ClassroomEntryPermits
            .AsNoTracking()
            .Where(permit => permit.SchoolId == schoolId
                && permit.StudentId == studentId
                && permit.SchoolTimetableEntryId == timetableEntryId
                && permit.Status != ClassroomEntryPermitStatus.Revoked
                && permit.Status != ClassroomEntryPermitStatus.Expired
                && permit.ValidFrom == validFrom
                && permit.ValidUntil == validUntil)
            .Select(permit => (int?)permit.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ClassroomEntryPermit?> GetForUpdateAsync(
        int schoolId,
        int permitId,
        CancellationToken cancellationToken) =>
        context.ClassroomEntryPermits
            .AsTracking()
            .Where(permit => permit.Id == permitId && permit.SchoolId == schoolId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int?> GetInstructorProfileIdAsync(
        int schoolId,
        string teacherUserId,
        CancellationToken cancellationToken) =>
        context.InstructorProfiles
            .AsNoTracking()
            .Where(profile => profile.SchoolId == schoolId
                && profile.UserId == teacherUserId
                && profile.IsActive
                && profile.User.IsActive)
            .Select(profile => (int?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ClassroomEntryPermitSaveResult> SaveNewPermitAsync(
        ClassroomEntryPermit permit,
        CancellationToken cancellationToken)
    {
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                .ConfigureAwait(false)
            : null;
        try
        {
            var existingId = await FindEquivalentActivePermitIdAsync(
                permit.SchoolId,
                permit.StudentId,
                permit.SchoolTimetableEntryId!.Value,
                permit.ValidFrom,
                permit.ValidUntil,
                cancellationToken).ConfigureAwait(false);
            if (existingId is not null)
            {
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new ClassroomEntryPermitSaveResult(existingId.Value, false);
            }

            context.ClassroomEntryPermits.Add(permit);
            await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ClassroomEntryPermitSaveResult(permit.Id, true);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public void Add(ClassroomEntryPermit permit) => context.ClassroomEntryPermits.Add(permit);

    public void SetExpectedRowVersion(ClassroomEntryPermit permit, byte[] rowVersion) =>
        context.Entry(permit).Property(entity => entity.RowVersion).OriginalValue = rowVersion;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ClassroomEntryPermitConcurrencyException(exception);
        }
        catch (DbUpdateException exception)
        {
            throw new ClassroomEntryPermitPersistenceConflictException(exception);
        }
    }

    public async Task<ClassroomEntryPermitDto?> GetDtoAsync(
        int schoolId,
        int permitId,
        ClassroomEntryPermitViewerScope scope,
        string userId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var row = await Project(ScopedQuery(schoolId, scope, userId, utcNow)
                .Where(permit => permit.Id == permitId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : Map(row, utcNow);
    }

    private IQueryable<PermitProjection> Project(IQueryable<ClassroomEntryPermit> permits) =>
        permits.Select(permit => new PermitProjection(
                permit.Id,
                permit.StudentId,
                permit.Student.StudentNumber,
                (permit.Student.FirstName + " " + (permit.Student.MiddleName ?? string.Empty) + " " + permit.Student.LastName).Trim(),
                permit.Student.IsActive,
                permit.Student.ProfilePhotoStorageKey,
                permit.Reason,
                permit.IssuedAt,
                permit.ValidFrom,
                permit.ValidUntil,
                permit.SchoolTimetableEntryId,
                permit.ClassroomId,
                permit.Classroom.ClassLabel,
                permit.Classroom.Stage,
                permit.Classroom.GradeLevel,
                permit.Classroom.Section,
                permit.TargetInstructorProfile == null ? null : permit.TargetInstructorProfile.UserId,
                permit.TargetInstructorProfile == null ? null : permit.TargetInstructorProfile.User.FirstName,
                permit.TargetInstructorProfile == null ? null : permit.TargetInstructorProfile.User.LastName,
                permit.Status,
                permit.AcknowledgedByTeacherUserId,
                permit.AcknowledgedAt,
                context.Notifications
                    .Where(notification => notification.SchoolId == permit.SchoolId
                        && notification.RelatedEntityType == nameof(ClassroomEntryPermit)
                        && notification.RelatedEntityId == permit.Id.ToString()
                        && notification.TemplateKey == "student-affairs.classroom-entry-permit.guardian")
                    .OrderByDescending(notification => notification.Id)
                    .Select(notification => (NotificationDeliveryStatus?)notification.DeliveryStatus)
                    .FirstOrDefault(),
                context.Notifications
                    .Where(notification => notification.SchoolId == permit.SchoolId
                        && notification.RelatedEntityType == nameof(ClassroomEntryPermit)
                        && notification.RelatedEntityId == permit.Id.ToString()
                        && notification.TemplateKey == "student-affairs.classroom-entry-permit.guardian")
                    .OrderByDescending(notification => notification.Id)
                    .Select(notification => notification.DeliveredAt)
                    .FirstOrDefault(),
                context.Notifications
                    .Where(notification => notification.SchoolId == permit.SchoolId
                        && notification.RelatedEntityType == nameof(ClassroomEntryPermit)
                        && notification.RelatedEntityId == permit.Id.ToString()
                        && notification.TemplateKey == "student-affairs.classroom-entry-permit.guardian")
                    .OrderByDescending(notification => notification.Id)
                    .Select(notification => notification.ReadAt)
                    .FirstOrDefault(),
                permit.RowVersion,
                context.StudentTermMetrics
                    .Where(metric => metric.SchoolId == permit.SchoolId
                        && metric.StudentId == permit.StudentId
                        && metric.AcademicTermId == permit.AcademicTermId
                        && metric.MetricCode == StudentTermMetricCode.ClassroomEntryPermit)
                    .Select(metric => (int?)metric.Count)
                    .FirstOrDefault() ?? 0,
                context.StudentTermMetrics
                    .Where(metric => metric.SchoolId == permit.SchoolId
                        && metric.StudentId == permit.StudentId
                        && metric.AcademicTermId == permit.AcademicTermId
                        && metric.MetricCode == StudentTermMetricCode.ClassroomEntryPermit)
                    .Select(metric => (DateTimeOffset?)metric.RecalculatedAt)
                    .FirstOrDefault(),
                context.SchoolStudentAffairsSettings
                    .Where(settings => settings.SchoolId == permit.SchoolId)
                    .Select(settings => settings.Version)
                    .FirstOrDefault(),
                context.SchoolStudentAffairsSettings
                    .Where(settings => settings.SchoolId == permit.SchoolId)
                    .Select(settings => settings.ClassroomEntryPermitThresholdPerTerm)
                    .FirstOrDefault()));

    public async Task<PagedResult<ClassroomEntryPermitDto>> GetPermitsAsync(
        int schoolId,
        ClassroomEntryPermitListQuery query,
        ClassroomEntryPermitViewerScope scope,
        string userId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);
        var dbQuery = ScopedQuery(schoolId, scope, userId, utcNow);

        if (query.Status is ClassroomEntryPermitStatus.Expired)
            dbQuery = dbQuery.Where(permit => permit.Status == ClassroomEntryPermitStatus.Expired
                || (permit.Status != ClassroomEntryPermitStatus.Revoked && permit.ValidUntil <= utcNow));
        else if (query.Status is { } status)
            dbQuery = dbQuery.Where(permit => permit.Status == status
                && (status == ClassroomEntryPermitStatus.Revoked || permit.ValidUntil > utcNow));
        if (query.Date is { } date)
        {
            var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var end = start.AddDays(1);
            dbQuery = dbQuery.Where(permit => permit.IssuedAt >= start && permit.IssuedAt < end);
        }
        if (query.StudentId is { } studentId) dbQuery = dbQuery.Where(permit => permit.StudentId == studentId);
        if (query.ClassroomId is { } classroomId) dbQuery = dbQuery.Where(permit => permit.ClassroomId == classroomId);
        if (query.InstructorProfileId is { } instructorId)
            dbQuery = dbQuery.Where(permit => permit.TargetInstructorProfileId == instructorId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            dbQuery = dbQuery.Where(permit => permit.Student.StudentNumber.Contains(term)
                || permit.Student.FirstName.Contains(term)
                || permit.Student.LastName.Contains(term)
                || permit.Reason.Contains(term));
        }

        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await Project(dbQuery
            .OrderByDescending(permit => permit.IssuedAt)
            .ThenByDescending(permit => permit.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var items = rows.Select(row => Map(row, utcNow)).ToList();

        return new PagedResult<ClassroomEntryPermitDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private IQueryable<ClassroomEntryPermit> ScopedQuery(
        int schoolId,
        ClassroomEntryPermitViewerScope scope,
        string userId,
        DateTimeOffset utcNow)
    {
        var query = context.ClassroomEntryPermits
            .AsNoTracking()
            .Where(permit => permit.SchoolId == schoolId);

        return scope switch
        {
            ClassroomEntryPermitViewerScope.Officer => query,
            ClassroomEntryPermitViewerScope.Instructor => query.Where(permit =>
                permit.TargetInstructorProfile != null
                && permit.TargetInstructorProfile.UserId == userId),
            ClassroomEntryPermitViewerScope.Guardian => query.Where(permit =>
                permit.Student.Guardians.Any(link =>
                    link.SchoolId == schoolId
                    && link.GuardianProfile.ApplicationUserId == userId
                    && link.GuardianProfile.IsActive
                    && link.ValidFrom <= DateOnly.FromDateTime(utcNow.UtcDateTime)
                    && (link.ValidTo == null || link.ValidTo >= DateOnly.FromDateTime(utcNow.UtcDateTime)))),
            _ => query.Where(_ => false)
        };
    }

    private static ClassroomEntryPermitDto Map(PermitProjection row, DateTimeOffset utcNow)
    {
        var effectiveStatus = row.Status != ClassroomEntryPermitStatus.Revoked && row.ValidUntil <= utcNow
            ? ClassroomEntryPermitStatus.Expired
            : row.Status;
        var teacher = row.TargetTeacherUserId is null
            ? null
            : new ActorSummaryDto(
                row.TargetTeacherUserId,
                $"{row.TargetTeacherFirstName} {row.TargetTeacherLastName}".Trim(),
                RoleNames.Instructor);
        var acknowledgedBy = row.AcknowledgedByTeacherUserId is null
            ? null
            : new ActorSummaryDto(
                row.AcknowledgedByTeacherUserId,
                teacher?.DisplayName ?? string.Empty,
                RoleNames.Instructor);
        int? nextThreshold = row.Threshold > 0 && row.MetricCount < row.Threshold ? row.Threshold : null;

        return new ClassroomEntryPermitDto(
            row.Id,
            new StudentSummaryDto(row.StudentId, row.StudentNumber, row.StudentName, row.ClassroomId,
                row.ClassroomLabel, row.StudentIsActive, row.StudentPhotoUrl),
            row.Reason,
            row.IssuedAt,
            row.ValidFrom,
            row.ValidUntil,
            row.SchoolTimetableEntryId,
            new ClassroomSummaryDto(row.ClassroomId, row.ClassroomLabel, row.ClassroomStage.ToString(),
                row.ClassroomGradeLevel, row.ClassroomSection),
            teacher,
            effectiveStatus,
            acknowledgedBy,
            row.AcknowledgedAt,
            row.GuardianDeliveryStatus is { } guardianStatus
                ? new NotificationDeliveryDto(
                    "Guardian",
                    RoleNames.Guardian,
                    guardianStatus,
                    row.GuardianDeliveredAt,
                    row.GuardianReadAt)
                : null,
            new MetricBadgeDto(
                StudentTermMetricCode.ClassroomEntryPermit,
                row.MetricCount,
                row.SettingsVersion,
                nextThreshold,
                row.MetricCount >= row.Threshold && row.Threshold > 0 ? "High" : "None",
                row.IssuedAt,
                row.MetricRecalculatedAt ?? row.IssuedAt),
            Convert.ToBase64String(row.RowVersion));
    }

    private sealed record PermitProjection(
        int Id,
        int StudentId,
        string StudentNumber,
        string StudentName,
        bool StudentIsActive,
        string? StudentPhotoUrl,
        string Reason,
        DateTimeOffset IssuedAt,
        DateTimeOffset ValidFrom,
        DateTimeOffset ValidUntil,
        int? SchoolTimetableEntryId,
        int ClassroomId,
        string ClassroomLabel,
        SchoolStage ClassroomStage,
        byte ClassroomGradeLevel,
        string ClassroomSection,
        string? TargetTeacherUserId,
        string? TargetTeacherFirstName,
        string? TargetTeacherLastName,
        ClassroomEntryPermitStatus Status,
        string? AcknowledgedByTeacherUserId,
        DateTimeOffset? AcknowledgedAt,
        NotificationDeliveryStatus? GuardianDeliveryStatus,
        DateTimeOffset? GuardianDeliveredAt,
        DateTimeOffset? GuardianReadAt,
        byte[] RowVersion,
        int MetricCount,
        DateTimeOffset? MetricRecalculatedAt,
        int SettingsVersion,
        int Threshold);
}
