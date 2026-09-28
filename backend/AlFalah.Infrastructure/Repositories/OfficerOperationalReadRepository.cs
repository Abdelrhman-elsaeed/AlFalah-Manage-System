using AlFalah.Application.StudentAffairs.DTOs.Behaviors;
using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Application.StudentAffairs.DTOs.Recognitions;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.OfficerOperations;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class OfficerOperationalReadRepository : IOfficerOperationalReadRepository
{
    private readonly AlFalahDbContext _context;
    public OfficerOperationalReadRepository(AlFalahDbContext context) => _context = context;

    public async Task<PagedResult<AcademicConcernDto>> GetAcademicConcernsAsync(int schoolId, AcademicConcernListQuery query, CancellationToken ct)
    {
        var page = Page(query.PageNumber); var size = Size(query.PageSize);
        var source = _context.AcademicConcerns.AsNoTracking().Where(x => x.SchoolId == schoolId && !x.IsDeleted);
        if (query.AcademicTermId is { } term) source = source.Where(x => x.AcademicTermId == term);
        if (query.ClassroomId is { } classroom) source = source.Where(x => x.ClassroomId == classroom);
        if (query.StudentId is { } student) source = source.Where(x => x.StudentId == student);
        if (!string.IsNullOrWhiteSpace(query.Category)) source = source.Where(x => x.Category == query.Category);
        if (!string.IsNullOrWhiteSpace(query.Search)) { var search = query.Search.Trim(); source = source.Where(x => x.Student.StudentNumber.Contains(search) || x.Student.FirstName.Contains(search) || x.Student.LastName.Contains(search) || x.Description.Contains(search)); }
        var total = await source.CountAsync(ct).ConfigureAwait(false);
        var rows = await source.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size)
            .Include(x => x.Student).Include(x => x.Classroom)
            .Include(x => x.ReportedByInstructorProfile).ThenInclude(x => x.User)
            .ToListAsync(ct).ConfigureAwait(false);
        var metrics = await MetricsAsync(schoolId, rows.Select(x => x.StudentId), StudentTermMetricCode.AcademicConcern, ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var referrals = await _context.StudentReferrals.AsNoTracking().Where(x => x.SchoolId == schoolId && !x.IsDeleted && x.SourceType == ReferralSourceType.AcademicConcern && x.SourceEntityId != null && ids.Contains(x.SourceEntityId.Value)).ToDictionaryAsync(x => x.SourceEntityId!.Value, x => x.Id, ct).ConfigureAwait(false);
        var items = rows.Select(x => new AcademicConcernDto(x.Id, Student(x.Student, x.ClassroomId, x.Classroom?.ClassLabel), x.Category, x.Description, x.OccurredAt, Instructor(x.ReportedByInstructorProfile), x.GuardianDispatchDecision, Badge(StudentTermMetricCode.AcademicConcern, metrics, x.StudentId, x.OccurredAt), referrals.GetValueOrDefault(x.Id), x.UpdatedAt.ToString("O"))).ToList();
        return Result(items, total, page, size);
    }

    public async Task<PagedResult<BehaviorIncidentDto>> GetBehaviorIncidentsAsync(int schoolId, BehaviorListQuery query, CancellationToken ct)
    {
        var page = Page(query.PageNumber); var size = Size(query.PageSize);
        var source = _context.BehaviorIncidents.AsNoTracking().Where(x => x.SchoolId == schoolId && !x.IsDeleted);
        if (query.AcademicTermId is { } term) source = source.Where(x => x.AcademicTermId == term);
        if (query.ClassroomId is { } classroom) source = source.Where(x => x.ClassroomId == classroom);
        if (query.StudentId is { } student) source = source.Where(x => x.StudentId == student);
        if (!string.IsNullOrWhiteSpace(query.Category)) source = source.Where(x => x.CategoryCode == query.Category);
        if (query.Severity is { } severity) source = source.Where(x => x.Severity == severity);
        if (query.HasReferral is { } hasReferral) source = hasReferral ? source.Where(x => _context.StudentReferrals.Any(r => r.SchoolId == schoolId && !r.IsDeleted && r.SourceType == ReferralSourceType.Behavior && r.SourceEntityId == x.Id)) : source.Where(x => !_context.StudentReferrals.Any(r => r.SchoolId == schoolId && !r.IsDeleted && r.SourceType == ReferralSourceType.Behavior && r.SourceEntityId == x.Id));
        if (!string.IsNullOrWhiteSpace(query.Search)) { var search = query.Search.Trim(); source = source.Where(x => x.Student.StudentNumber.Contains(search) || x.Student.FirstName.Contains(search) || x.Student.LastName.Contains(search) || x.Description.Contains(search)); }
        var total = await source.CountAsync(ct).ConfigureAwait(false);
        var rows = await source.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size)
            .Include(x => x.Student).Include(x => x.Classroom)
            .Include(x => x.ReportedByInstructorProfile!).ThenInclude(x => x.User)
            .Include(x => x.ReportedByStaffUser)
            .ToListAsync(ct).ConfigureAwait(false);
        var metrics = await MetricsAsync(schoolId, rows.Select(x => x.StudentId), StudentTermMetricCode.CountableBehaviorIncident, ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var referrals = await _context.StudentReferrals.AsNoTracking().Where(x => x.SchoolId == schoolId && !x.IsDeleted && x.SourceType == ReferralSourceType.Behavior && x.SourceEntityId != null && ids.Contains(x.SourceEntityId.Value)).ToDictionaryAsync(x => x.SourceEntityId!.Value, x => x.Id, ct).ConfigureAwait(false);
        var items = rows.Select(x => new BehaviorIncidentDto(x.Id, Student(x.Student, x.ClassroomId, x.Classroom?.ClassLabel), x.CategoryCode, x.Severity, x.Description, x.OccurredAt, x.Location, x.ImmediateActionTaken, BehaviorReporter(x), x.GuardianDispatchDecision, Badge(StudentTermMetricCode.CountableBehaviorIncident, metrics, x.StudentId, x.OccurredAt, x.Severity.ToString()), referrals.GetValueOrDefault(x.Id), Array.Empty<string>(), Convert.ToBase64String(x.RowVersion))).ToList();
        return Result(items, total, page, size);
    }

    public async Task<PagedResult<MorningDelayDto>> GetMorningDelaysAsync(int schoolId, MorningDelayListQuery query, CancellationToken ct)
    {
        var page = Page(query.PageNumber); var size = Size(query.PageSize);
        var source = _context.MorningArrivalDelays.AsNoTracking().Where(x => x.SchoolId == schoolId && !x.IsDeleted);
        if (query.Date is { } date) source = source.Where(x => x.SchoolLocalDate == date);
        if (query.AcademicTermId is { } term) source = source.Where(x => x.AcademicTermId == term);
        if (query.StudentId is { } student) source = source.Where(x => x.StudentId == student);
        if (query.ClassroomId is { } classroom) source = source.Where(x => x.Student.Enrollments.Any(e => !e.IsDeleted && e.AcademicTermId == x.AcademicTermId && e.ClassroomId == classroom));
        if (!string.IsNullOrWhiteSpace(query.Search)) { var search = query.Search.Trim(); source = source.Where(x => x.Student.StudentNumber.Contains(search) || x.Student.FirstName.Contains(search) || x.Student.LastName.Contains(search)); }
        var total = await source.CountAsync(ct).ConfigureAwait(false);
        var rows = await source.OrderByDescending(x => x.ArrivalAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size)
            .Include(x => x.Student).ThenInclude(x => x.Enrollments).ThenInclude(x => x.Classroom)
            .ToListAsync(ct).ConfigureAwait(false);
        var metrics = await MetricsAsync(schoolId, rows.Select(x => x.StudentId), StudentTermMetricCode.MorningArrivalDelay, ct);
        var items = rows.Select(x => Morning(x, metrics)).ToList();
        return Result(items, total, page, size);
    }

    public async Task<MorningDelayDto?> GetMorningDelayAsync(int schoolId, int delayId, CancellationToken ct)
    {
        var row = await _context.MorningArrivalDelays.AsNoTracking().Where(x => x.SchoolId == schoolId && x.Id == delayId && !x.IsDeleted)
            .Include(x => x.Student).ThenInclude(x => x.Enrollments).ThenInclude(x => x.Classroom)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (row is null) return null;
        var metrics = await MetricsAsync(schoolId, new[] { row.StudentId }, StudentTermMetricCode.MorningArrivalDelay, ct);
        return Morning(row, metrics);
    }

    public async Task<PagedResult<SessionDelayDto>> GetSessionDelaysAsync(int schoolId, SessionDelayListQuery query, CancellationToken ct)
    {
        var page = Page(query.PageNumber); var size = Size(query.PageSize);
        var source = _context.SessionDelays.AsNoTracking().Where(x => x.SchoolId == schoolId && !x.IsDeleted);
        if (query.AcademicTermId is { } term) source = source.Where(x => x.AcademicTermId == term);
        if (query.ClassroomId is { } classroom) source = source.Where(x => x.ClassroomId == classroom);
        if (query.StudentId is { } student) source = source.Where(x => x.StudentId == student);
        if (query.Date is { } date) { var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); var to = from.AddDays(1); source = source.Where(x => x.OccurredAt >= from && x.OccurredAt < to); }
        if (!string.IsNullOrWhiteSpace(query.Search)) { var search = query.Search.Trim(); source = source.Where(x => x.Student.StudentNumber.Contains(search) || x.Student.FirstName.Contains(search) || x.Student.LastName.Contains(search)); }
        var total = await source.CountAsync(ct).ConfigureAwait(false);
        var rows = await source.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size)
            .Include(x => x.Student).Include(x => x.Classroom).Include(x => x.ReportedByInstructorProfile).ThenInclude(x => x.User)
            .ToListAsync(ct).ConfigureAwait(false);
        var metrics = await MetricsAsync(schoolId, rows.Select(x => x.StudentId), StudentTermMetricCode.SessionDelay, ct);
        var items = rows.Select(x => new SessionDelayDto(x.Id, Student(x.Student, x.ClassroomId, x.Classroom.ClassLabel), x.SchoolTimetableEntryId ?? 0, x.Period, x.OccurredAt, x.DelayMinutes, x.Reason, Instructor(x.ReportedByInstructorProfile), Badge(StudentTermMetricCode.SessionDelay, metrics, x.StudentId, x.OccurredAt), null, Convert.ToBase64String(x.RowVersion))).ToList();
        return Result(items, total, page, size);
    }

    public async Task<PagedResult<RecognitionDto>> GetRecognitionsAsync(int schoolId, RecognitionListQuery query, CancellationToken ct)
    {
        var page = Page(query.PageNumber); var size = Size(query.PageSize);
        var source = _context.StudentRecognitions.AsNoTracking().Where(x => x.SchoolId == schoolId && !x.IsDeleted);
        if (query.AcademicTermId is { } term) source = source.Where(x => x.AcademicTermId == term);
        if (query.ClassroomId is { } classroom) source = source.Where(x => x.ClassroomId == classroom);
        if (query.StudentId is { } student) source = source.Where(x => x.StudentId == student);
        if (query.InstructorProfileId is { } instructor) source = source.Where(x => x.ReportedByInstructorProfileId == instructor);
        if (!string.IsNullOrWhiteSpace(query.RecognitionType)) source = source.Where(x => x.RecognitionType == query.RecognitionType);
        if (query.Month is { } month) source = source.Where(x => x.RecognizedAt.Month == month);
        if (query.WeekOf is { } week) { var to = week.AddDays(7); var fromAt = new DateTimeOffset(week.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); var toAt = new DateTimeOffset(to.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); source = source.Where(x => x.RecognizedAt >= fromAt && x.RecognizedAt < toAt); }
        if (!string.IsNullOrWhiteSpace(query.Search)) { var search = query.Search.Trim(); source = source.Where(x => x.Student.StudentNumber.Contains(search) || x.Student.FirstName.Contains(search) || x.Student.LastName.Contains(search) || x.Title.Contains(search)); }
        var total = await source.CountAsync(ct).ConfigureAwait(false);
        var rows = await source.OrderByDescending(x => x.RecognizedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size)
            .Include(x => x.Student).Include(x => x.Classroom).Include(x => x.ReportedByInstructorProfile).ThenInclude(x => x.User)
            .ToListAsync(ct).ConfigureAwait(false);
        var items = rows.Select(x => new RecognitionDto(x.Id, Student(x.Student, x.ClassroomId, x.Classroom?.ClassLabel), x.RecognitionType, x.Title, x.Description, x.RecognizedAt, Instructor(x.ReportedByInstructorProfile), null, x.UpdatedAt.ToString("O"))).ToList();
        return Result(items, total, page, size);
    }

    private async Task<Dictionary<int, StudentTermMetric>> MetricsAsync(int schoolId, IEnumerable<int> studentIds, StudentTermMetricCode code, CancellationToken ct)
    {
        var ids = studentIds.Distinct().ToArray();
        return await _context.StudentTermMetrics.AsNoTracking().Where(x => x.SchoolId == schoolId && !x.IsDeleted && x.MetricCode == code && ids.Contains(x.StudentId)).OrderByDescending(x => x.RecalculatedAt).GroupBy(x => x.StudentId).Select(group => group.First()).ToDictionaryAsync(x => x.StudentId, ct).ConfigureAwait(false);
    }

    private static MorningDelayDto Morning(MorningArrivalDelay row, IReadOnlyDictionary<int, StudentTermMetric> metrics)
    {
        var enrollment = row.Student.Enrollments.FirstOrDefault(x => !x.IsDeleted && x.AcademicTermId == row.AcademicTermId && x.Status == StudentEnrollmentStatus.Active);
        return new MorningDelayDto(row.Id, Student(row.Student, enrollment?.ClassroomId, enrollment?.Classroom?.ClassLabel), row.ArrivalAt, row.ArrivalAt.ToString("HH:mm"), "School schedule", row.CutoffTimeSnapshot, row.DelayMinutes, row.Reason, Badge(StudentTermMetricCode.MorningArrivalDelay, metrics, row.StudentId, row.ArrivalAt), null, row.UpdatedAt.ToString("O"));
    }

    private static StudentSummaryDto Student(Student student, int? classroomId, string? classLabel) =>
        new(
            student.Id,
            student.StudentNumber,
            $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
            classroomId,
            classLabel,
            student.IsActive,
            null);
    private static ActorSummaryDto Instructor(InstructorProfile profile) => new(profile.UserId, $"{profile.User.FirstName} {profile.User.LastName}".Trim(), RoleNames.Instructor);
    private static ActorSummaryDto BehaviorReporter(BehaviorIncident incident) => incident.ReportedByInstructorProfile is not null ? Instructor(incident.ReportedByInstructorProfile) : new ActorSummaryDto(incident.ReportedByStaffUserId ?? string.Empty, $"{incident.ReportedByStaffUser?.FirstName} {incident.ReportedByStaffUser?.LastName}".Trim(), "Staff");
    private static MetricBadgeDto Badge(StudentTermMetricCode code, IReadOnlyDictionary<int, StudentTermMetric> metrics, int studentId, DateTimeOffset occurredAt, string severity = "info") { metrics.TryGetValue(studentId, out var metric); return new MetricBadgeDto(code, metric?.Count ?? 0, 0, null, severity, occurredAt, metric?.RecalculatedAt ?? occurredAt); }
    private static int Page(int page) => Math.Max(1, page);
    private static int Size(int size) => Math.Clamp(size <= 0 ? 20 : size, 1, 100);
    private static PagedResult<T> Result<T>(List<T> items, int total, int page, int size) => new() { Items = items, TotalCount = total, Page = page, PageSize = size };
}
