using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs.DTOs.Dashboards;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.GatePasses;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class GatePassWorkflowRepository : IGatePassWorkflowRepository
{
    private readonly AlFalahDbContext _context;

    private readonly IBellScheduleRepository _timings;
    public GatePassWorkflowRepository(AlFalahDbContext context, IBellScheduleRepository timings)
    {
        _context = context;
        _timings = timings;
    }

    public async Task<DateOnly?> GetPublishedStudyDateAsync(int schoolId, DateTimeOffset instant, CancellationToken ct)
    {
        try
        {
            var schedule = await _timings.GetPublishedAsync(schoolId, instant, ct);
            if (schedule is null) return null;
            var local = BellScheduleResolver.LocalTime(schedule, instant);
            return BellScheduleResolver.EffectivePeriods(schedule, BellScheduleResolver.ToDay(local.DayOfWeek)).Count > 0
                ? DateOnly.FromDateTime(local.DateTime) : null;
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    public Task<bool> IsActiveGuardianAsync(
        int schoolId,
        string guardianUserId,
        CancellationToken cancellationToken) =>
        _context.GuardianProfiles.AsNoTracking().AnyAsync(profile =>
            profile.SchoolId == schoolId
            && profile.ApplicationUserId == guardianUserId
            && profile.IsActive
            && !profile.IsDeleted
            && profile.ApplicationUser.IsActive,
            cancellationToken);

    public Task<bool> IsOwnedByGuardianAsync(
        int schoolId,
        int gatePassId,
        string guardianUserId,
        CancellationToken cancellationToken) =>
        _context.GatePasses.AsNoTracking().AnyAsync(gatePass =>
            gatePass.Id == gatePassId
            && gatePass.SchoolId == schoolId
            && !gatePass.IsDeleted
            && gatePass.RequestedByGuardianProfile.SchoolId == schoolId
            && gatePass.RequestedByGuardianProfile.ApplicationUserId == guardianUserId
            && gatePass.RequestedByGuardianProfile.IsActive
            && !gatePass.RequestedByGuardianProfile.IsDeleted
            && gatePass.RequestedByGuardianProfile.ApplicationUser.IsActive,
            cancellationToken);

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
                && link.GuardianProfile.ApplicationUserId == guardianUserId
                && link.GuardianProfile.IsActive
                && !link.GuardianProfile.IsDeleted
                && link.GuardianProfile.ApplicationUser.IsActive
                && link.Student.IsActive
                && !link.Student.IsDeleted)
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
            .Include(gatePass => gatePass.Transitions)
            .Where(gatePass => gatePass.Id == gatePassId && gatePass.SchoolId == schoolId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int?> GetInstructorProfileIdAsync(
        int schoolId,
        string teacherUserId,
        CancellationToken cancellationToken) =>
        _context.InstructorProfiles
            .AsNoTracking()
            .Where(profile => profile.SchoolId == schoolId
                && profile.UserId == teacherUserId
                && profile.IsActive
                && profile.User.IsActive)
            .Select(profile => (int?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<GatePassTimetableSnapshot?> ResolvePublishedTimetableAsync(
        int schoolId,
        int academicYearId,
        TimetableSemester semester,
        int classroomId,
        string classroomLabel,
        DateTimeOffset instant,
        CancellationToken cancellationToken)
    {
        var schedule = await _timings.GetPublishedAsync(schoolId, instant, cancellationToken);
        var period = BellScheduleResolver.CurrentPeriod(schedule, instant);
        if (schedule is null || period is null || schedule.AcademicYearId != academicYearId || schedule.Semester != semester) return null;
        var day = BellScheduleResolver.ToDay(BellScheduleResolver.LocalTime(schedule, instant).DayOfWeek);
        var localDate = DateOnly.FromDateTime(BellScheduleResolver.LocalTime(schedule, instant).DateTime);
        var exactMatches = await PublishedEntries(schoolId, academicYearId, semester, day)
            .Where(entry => entry.Period == period.Sequence && entry.SchoolTimetable.BellScheduleRevisionId == schedule.RevisionId)
            .Where(entry => entry.ClassroomId == classroomId)
            .Select(entry => new GatePassTimetableSnapshot(
                entry.SchoolTimetableId,
                entry.Id,
                _context.Set<AlFalah.Domain.Entities.TimetableSubstitutionMovement>().Where(m => m.SchoolId == schoolId &&
                    m.SchoolTimetableEntryId == entry.Id && m.Substitution.LocalDate == localDate && m.Substitution.Kind == "Substitution")
                    .OrderByDescending(m => m.TimetableSubstitutionId).Select(m => (int?)m.ToTeacherId).FirstOrDefault() ?? entry.InstructorProfileId,
                entry.Period))
            .Take(2)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (exactMatches.Count == 1) return exactMatches[0];
        if (exactMatches.Count > 1) return null;

        var fallbackMatches = await PublishedEntries(schoolId, academicYearId, semester, day)
            .Where(entry => entry.Period == period.Sequence && entry.SchoolTimetable.BellScheduleRevisionId == schedule.RevisionId)
            .Where(entry => entry.ClassroomId == null && entry.ClassLabel == classroomLabel)
            .Select(entry => new GatePassTimetableSnapshot(
                entry.SchoolTimetableId,
                entry.Id,
                _context.Set<AlFalah.Domain.Entities.TimetableSubstitutionMovement>().Where(m => m.SchoolId == schoolId &&
                    m.SchoolTimetableEntryId == entry.Id && m.Substitution.LocalDate == localDate && m.Substitution.Kind == "Substitution")
                    .OrderByDescending(m => m.TimetableSubstitutionId).Select(m => (int?)m.ToTeacherId).FirstOrDefault() ?? entry.InstructorProfileId,
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

    public async Task<PagedResult<GatePassDto>> GetGatePassesAsync(
        int schoolId,
        GatePassListQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var dbQuery = _context.GatePasses
            .AsNoTracking()
            .Where(gp => gp.SchoolId == schoolId);

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(gp => gp.Status == query.Status.Value);
        }

        if (query.Date.HasValue)
        {
            var date = query.Date.Value;
            var startUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var endUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            dbQuery = dbQuery.Where(gp => gp.RequestedExitAt >= startUtc && gp.RequestedExitAt <= endUtc);
        }

        if (query.ClassroomId.HasValue)
        {
            dbQuery = dbQuery.Where(gp => gp.CurrentClassroomId == query.ClassroomId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            dbQuery = dbQuery.Where(gp =>
                gp.Student.FirstName.Contains(term)
                || (gp.Student.MiddleName != null && gp.Student.MiddleName.Contains(term))
                || gp.Student.LastName.Contains(term)
                || gp.Student.StudentNumber.Contains(term)
                || gp.PickupPersonName.Contains(term)
                || gp.Reason.Contains(term));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var projections = await dbQuery
            .OrderByDescending(gp => gp.RequestedExitAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = projections.Select(p =>
        {
            var classroom = p.CurrentClassroomId is null
                ? null
                : new ClassroomSummaryDto(
                    p.CurrentClassroomId.Value,
                    p.ClassroomLabel ?? string.Empty,
                    p.ClassroomStage ?? string.Empty,
                    p.ClassroomGrade ?? 0,
                    p.ClassroomSection ?? string.Empty);
            var teacher = p.TeacherUserId is null
                ? null
                : new ActorSummaryDto(
                    p.TeacherUserId,
                    $"{p.TeacherFirstName} {p.TeacherLastName}".Trim(),
                    RoleNames.Instructor);

            return new GatePassDto(
                p.Id,
                new StudentSummaryDto(
                    p.StudentId,
                    p.StudentNumber,
                    p.StudentDisplayName,
                    p.CurrentClassroomId,
                    p.ClassroomLabel,
                    p.IsActive,
                    null),
                p.RequestedAt,
                p.RequestedExitAt,
                p.Reason,
                new PickupPersonDto(
                    p.PickupPersonName,
                    p.PickupRelationship,
                    p.PickupIdentityHint),
                p.Status,
                p.ApprovedWindowStartsAt,
                p.ApprovedWindowEndsAt,
                p.ReviewedAt,
                p.ExitedAt,
                classroom,
                teacher,
                Array.Empty<NotificationDeliveryDto>(),
                Convert.ToBase64String(p.RowVersion));
        }).ToList();

        return new PagedResult<GatePassDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<GatePassDto>> GetMyGatePassesAsync(
        int schoolId,
        string guardianUserId,
        GatePassListQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var dbQuery = _context.GatePasses
            .AsNoTracking()
            .Where(gp => gp.SchoolId == schoolId
                && gp.RequestedByGuardianProfile.SchoolId == schoolId
                && gp.RequestedByGuardianProfile.ApplicationUserId == guardianUserId
                && gp.RequestedByGuardianProfile.IsActive
                && !gp.RequestedByGuardianProfile.IsDeleted
                && gp.RequestedByGuardianProfile.ApplicationUser.IsActive
                && !gp.IsDeleted);

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(gp => gp.Status == query.Status.Value);
        }

        if (query.Date.HasValue)
        {
            var date = query.Date.Value;
            var startUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var endUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            dbQuery = dbQuery.Where(gp => gp.RequestedExitAt >= startUtc && gp.RequestedExitAt <= endUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            dbQuery = dbQuery.Where(gp =>
                gp.Student.FirstName.Contains(term)
                || (gp.Student.MiddleName != null && gp.Student.MiddleName.Contains(term))
                || gp.Student.LastName.Contains(term)
                || gp.PickupPersonName.Contains(term)
                || gp.Reason.Contains(term));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var projections = await dbQuery
            .OrderByDescending(gp => gp.RequestedExitAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = projections.Select(p =>
        {
            var classroom = p.CurrentClassroomId is null
                ? null
                : new ClassroomSummaryDto(
                    p.CurrentClassroomId.Value,
                    p.ClassroomLabel ?? string.Empty,
                    p.ClassroomStage ?? string.Empty,
                    p.ClassroomGrade ?? 0,
                    p.ClassroomSection ?? string.Empty);
            var teacher = p.TeacherUserId is null
                ? null
                : new ActorSummaryDto(
                    p.TeacherUserId,
                    $"{p.TeacherFirstName} {p.TeacherLastName}".Trim(),
                    RoleNames.Instructor);

            return new GatePassDto(
                p.Id,
                new StudentSummaryDto(
                    p.StudentId,
                    p.StudentNumber,
                    p.StudentDisplayName,
                    p.CurrentClassroomId,
                    p.ClassroomLabel,
                    p.IsActive,
                    null),
                p.RequestedAt,
                p.RequestedExitAt,
                p.Reason,
                new PickupPersonDto(
                    p.PickupPersonName,
                    p.PickupRelationship,
                    p.PickupIdentityHint),
                p.Status,
                p.ApprovedWindowStartsAt,
                p.ApprovedWindowEndsAt,
                p.ReviewedAt,
                p.ExitedAt,
                classroom,
                teacher,
                Array.Empty<NotificationDeliveryDto>(),
                Convert.ToBase64String(p.RowVersion));
        }).ToList();

        return new PagedResult<GatePassDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<SecurityGatePassQueuePageDto> GetSecurityGatePassQueueAsync(
        int schoolId,
        GatePassListQuery query,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);

        var dbQuery = _context.GatePasses
            .AsNoTracking()
            .Where(gp => gp.SchoolId == schoolId
                && (gp.Status == GatePassStatus.Approved || gp.Status == GatePassStatus.SecurityAcknowledged)
                && gp.ApprovedWindowStartsAt.HasValue
                && gp.ApprovedWindowEndsAt.HasValue
                && gp.ReviewedAt.HasValue);

        // Security may search the authorized queue, but client filters never widen it
        // to other states, dates, or classrooms.

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            dbQuery = dbQuery.Where(gp =>
                gp.Student.FirstName.Contains(term)
                || (gp.Student.MiddleName != null && gp.Student.MiddleName.Contains(term))
                || gp.Student.LastName.Contains(term)
                || gp.Student.StudentNumber.Contains(term)
                || gp.PickupPersonName.Contains(term));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var projections = await dbQuery
            .OrderByDescending(gp => gp.ApprovedWindowStartsAt <= now && now < gp.ApprovedWindowEndsAt)
            .ThenBy(gp => gp.ApprovedWindowEndsAt)
            .ThenBy(gp => gp.ReviewedAt)
            .ThenBy(gp => gp.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(gatePass => new
            {
                gatePass.Id,
                gatePass.StudentId,
                gatePass.ApprovedWindowStartsAt,
                gatePass.ApprovedWindowEndsAt,
                gatePass.ReviewedAt,
                gatePass.SecurityAcknowledgedAt,
                gatePass.PickupPersonName,
                gatePass.PickupRelationship,
                gatePass.PickupIdentityHint,
                gatePass.Status,
                gatePass.RowVersion,
                gatePass.Student.StudentNumber,
                gatePass.Student.IsActive,
                StudentDisplayName = (gatePass.Student.FirstName + " "
                    + (gatePass.Student.MiddleName ?? string.Empty) + " "
                    + gatePass.Student.LastName).Trim(),
                ClassLabel = gatePass.CurrentClassroom == null ? "N/A" : gatePass.CurrentClassroom.ClassLabel,
                OfficerName = _context.Users
                    .Where(user => user.Id == gatePass.ReviewedByUserId)
                    .Select(user => (user.FirstName + " " + user.LastName).Trim())
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = projections.Select(p => new SecurityGatePassQueueItemDto(
                p.Id,
                new StudentSummaryDto(
                    p.StudentId,
                    p.StudentNumber,
                    p.StudentDisplayName,
                    null,
                    p.ClassLabel,
                    p.IsActive,
                    null),
                p.ClassLabel,
                p.ApprovedWindowStartsAt!.Value,
                p.ApprovedWindowEndsAt!.Value,
                new PickupPersonDto(
                    p.PickupPersonName,
                    p.PickupRelationship,
                    p.PickupIdentityHint),
                string.IsNullOrWhiteSpace(p.OfficerName) ? "Student Affairs Officer" : p.OfficerName,
                p.ReviewedAt!.Value,
                p.SecurityAcknowledgedAt,
                p.Status,
                Convert.ToBase64String(p.RowVersion)))
            .ToList();

        return new SecurityGatePassQueuePageDto(items, totalCount, page, pageSize, now);
    }

    public async Task<SecurityGatePassDetailDto?> GetSecurityDetailAsync(
        int schoolId,
        int gatePassId,
        CancellationToken cancellationToken)
    {
        var projection = await _context.GatePasses
            .AsNoTracking()
            .Where(gatePass => gatePass.Id == gatePassId
                && gatePass.SchoolId == schoolId
                && (gatePass.Status == GatePassStatus.Approved
                    || gatePass.Status == GatePassStatus.SecurityAcknowledged
                    || gatePass.Status == GatePassStatus.Exited))
            .Select(gatePass => new
            {
                gatePass.Id,
                gatePass.StudentId,
                gatePass.Student.StudentNumber,
                gatePass.Student.IsActive,
                StudentDisplayName = (gatePass.Student.FirstName + " "
                    + (gatePass.Student.MiddleName ?? string.Empty) + " "
                    + gatePass.Student.LastName).Trim(),
                ClassLabel = gatePass.CurrentClassroom == null ? "N/A" : gatePass.CurrentClassroom.ClassLabel,
                gatePass.ApprovedWindowStartsAt,
                gatePass.ApprovedWindowEndsAt,
                gatePass.PickupPersonName,
                gatePass.PickupRelationship,
                gatePass.PickupIdentityHint,
                OfficerName = _context.Users
                    .Where(user => user.Id == gatePass.ReviewedByUserId)
                    .Select(user => (user.FirstName + " " + user.LastName).Trim())
                    .FirstOrDefault(),
                gatePass.ReviewedAt,
                gatePass.SecurityAcknowledgedAt,
                gatePass.ExitedAt,
                gatePass.Status,
                gatePass.RowVersion
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return projection is null
            ? null
            : new SecurityGatePassDetailDto(
                projection.Id,
                new StudentSummaryDto(
                    projection.StudentId,
                    projection.StudentNumber,
                    projection.StudentDisplayName,
                    null,
                    projection.ClassLabel,
                    projection.IsActive,
                    null),
                projection.ClassLabel,
                projection.ApprovedWindowStartsAt,
                projection.ApprovedWindowEndsAt,
                new PickupPersonDto(
                    projection.PickupPersonName,
                    projection.PickupRelationship,
                    projection.PickupIdentityHint),
                string.IsNullOrWhiteSpace(projection.OfficerName)
                    ? "Student Affairs Officer"
                    : projection.OfficerName,
                projection.ReviewedAt,
                projection.SecurityAcknowledgedAt,
                projection.ExitedAt,
                projection.Status,
                Convert.ToBase64String(projection.RowVersion));
    }

    public async Task<SecurityStudentAffairsDashboardDto> GetSecurityDashboardAsync(
        int schoolId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var queue = await GetSecurityGatePassQueueAsync(
            schoolId,
            new GatePassListQuery { PageNumber = 1, PageSize = 12 },
            now,
            cancellationToken).ConfigureAwait(false);

        var counts = await _context.GatePasses
            .AsNoTracking()
            .Where(gatePass => gatePass.SchoolId == schoolId
                && (gatePass.Status == GatePassStatus.Approved
                    || gatePass.Status == GatePassStatus.SecurityAcknowledged)
                && gatePass.ApprovedWindowStartsAt.HasValue
                && gatePass.ApprovedWindowEndsAt.HasValue
                && gatePass.ReviewedAt.HasValue)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Approved = group.Count(gatePass => gatePass.Status == GatePassStatus.Approved),
                Acknowledged = group.Count(gatePass => gatePass.Status == GatePassStatus.SecurityAcknowledged),
                ActiveWindow = group.Count(gatePass => gatePass.ApprovedWindowStartsAt <= now
                    && now < gatePass.ApprovedWindowEndsAt),
                OverdueAcknowledged = group.Count(gatePass => gatePass.Status == GatePassStatus.SecurityAcknowledged
                    && gatePass.ApprovedWindowEndsAt <= now)
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SecurityStudentAffairsDashboardDto(
            queue.Items,
            new[]
            {
                new DashboardCountDto("Approved", "بانتظار مطابقة المستلم", counts?.Approved ?? 0, "Info"),
                new DashboardCountDto("SecurityAcknowledged", "بانتظار الخروج الفعلي", counts?.Acknowledged ?? 0, "Warning"),
                new DashboardCountDto("ActiveWindow", "داخل نافذة التنفيذ الآن", counts?.ActiveWindow ?? 0, "Success"),
                new DashboardCountDto("OverdueAcknowledged", "مطابقة دون خروج في المهلة", counts?.OverdueAcknowledged ?? 0, "Danger")
            },
            now);
    }

    public async Task<GatePassHistoryDto?> GetHistoryAsync(
        int schoolId,
        int gatePassId,
        CancellationToken cancellationToken)
    {
        var exists = await _context.GatePasses
            .AsNoTracking()
            .AnyAsync(gp => gp.Id == gatePassId && gp.SchoolId == schoolId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists) return null;

        var transitions = await _context.GatePassTransitions
            .AsNoTracking()
            .Where(t => t.SchoolId == schoolId && t.GatePassId == gatePassId)
            .OrderBy(t => t.OccurredAt)
            .Select(t => new
            {
                FromState = t.FromStatus.HasValue ? t.FromStatus.Value.ToString() : null,
                ToState = t.ToStatus.ToString(),
                t.ActorUserId,
                ActorName = (t.ActorUser.FirstName + " " + t.ActorUser.LastName).Trim(),
                t.ActorRole,
                t.OccurredAt,
                t.Reason
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var transitionDtos = transitions.Select(t => new TransitionDto(
            t.FromState,
            t.ToState,
            new ActorSummaryDto(t.ActorUserId, string.IsNullOrWhiteSpace(t.ActorName) ? t.ActorRole : t.ActorName, t.ActorRole),
            t.OccurredAt,
            t.Reason)).ToList();

        return new GatePassHistoryDto(transitionDtos, Array.Empty<NotificationDeliveryDto>());
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
