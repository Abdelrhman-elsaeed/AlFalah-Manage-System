using AlFalah.Application.StudentAffairs.DTOs.Behaviors;
using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.TeacherActions;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class TeacherActionWorkflowRepository : ITeacherActionWorkflowRepository
{
    private readonly AlFalahDbContext _context;

    public TeacherActionWorkflowRepository(AlFalahDbContext context) => _context = context;

    public Task<TeacherActionScopeSnapshot?> ResolveScopeAsync(
        int schoolId,
        string teacherUserId,
        int studentId,
        int timetableEntryId,
        bool allowOverride,
        TimetableDay day,
        DateOnly occurrenceDate,
        CancellationToken cancellationToken) =>
        _context.InstructorProfiles
            .AsNoTracking()
            .Where(reporter => reporter.SchoolId == schoolId
                && reporter.UserId == teacherUserId
                && reporter.IsActive)
            .SelectMany(reporter => _context.SchoolTimetableEntries
                .AsNoTracking()
                .Where(entry => entry.Id == timetableEntryId
                    && entry.SchoolId == schoolId
                    && entry.Day == day
                    && entry.EntryType == TimetableEntryType.Lesson
                    && entry.ClassroomId != null
                    && entry.InstructorProfile.SchoolId == schoolId
                    && entry.InstructorProfile.IsActive
                    && (allowOverride || entry.InstructorProfileId == reporter.Id)
                    && entry.SchoolTimetable.SchoolId == schoolId
                    && entry.SchoolTimetable.IsPublished)
                .SelectMany(entry => _context.StudentEnrollments
                    .AsNoTracking()
                    .Where(enrollment => enrollment.SchoolId == schoolId
                        && enrollment.StudentId == studentId
                        && enrollment.Student.SchoolId == schoolId
                        && enrollment.Student.IsActive
                        && enrollment.ClassroomId == entry.ClassroomId
                        && enrollment.Classroom.SchoolId == schoolId
                        && enrollment.Classroom.IsActive
                        && enrollment.Status == StudentEnrollmentStatus.Active
                        && enrollment.EnrolledOn <= occurrenceDate
                        && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= occurrenceDate)
                        && enrollment.AcademicTerm.SchoolId == schoolId
                        && enrollment.AcademicTerm.IsActive
                        && enrollment.AcademicTerm.StartsOn <= occurrenceDate
                        && enrollment.AcademicTerm.EndsOn >= occurrenceDate
                        && enrollment.AcademicTerm.AcademicYearId == entry.SchoolTimetable.AcademicYearId
                        && enrollment.AcademicTerm.Semester == entry.SchoolTimetable.Semester)
                    .Select(enrollment => new TeacherActionScopeSnapshot(
                        reporter.Id,
                        enrollment.AcademicTermId,
                        enrollment.ClassroomId,
                        entry.SchoolTimetableId,
                        entry.Id,
                        entry.Period))))
            .SingleOrDefaultAsync(cancellationToken);

    public void Add(BehaviorIncident incident) => _context.BehaviorIncidents.Add(incident);
    public void Add(AcademicConcern concern) => _context.AcademicConcerns.Add(concern);
    public void Add(SessionDelay delay) => _context.SessionDelays.Add(delay);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);

    public async Task<BehaviorIncidentDto?> GetBehaviorDtoAsync(
        int schoolId,
        int incidentId,
        CancellationToken cancellationToken)
    {
        var row = await _context.BehaviorIncidents
            .AsNoTracking()
            .Where(incident => incident.Id == incidentId && incident.SchoolId == schoolId)
            .Select(incident => new
            {
                incident.Id,
                incident.StudentId,
                incident.Student.StudentNumber,
                StudentName = (incident.Student.FirstName + " "
                    + (incident.Student.MiddleName ?? string.Empty) + " "
                    + incident.Student.LastName).Trim(),
                incident.Student.IsActive,
                incident.ClassroomId,
                ClassroomLabel = incident.Classroom == null ? null : incident.Classroom.ClassLabel,
                incident.AcademicTermId,
                incident.CategoryCode,
                incident.Severity,
                incident.Description,
                incident.OccurredAt,
                incident.Location,
                incident.ImmediateActionTaken,
                incident.GuardianDispatchDecision,
                incident.ReportedByInstructorProfileId,
                ReporterUserId = incident.ReportedByInstructorProfile == null
                    ? incident.ReportedByStaffUserId
                    : incident.ReportedByInstructorProfile.UserId,
                ReporterFirstName = incident.ReportedByInstructorProfile == null
                    ? incident.ReportedByStaffUser!.FirstName
                    : incident.ReportedByInstructorProfile.User.FirstName,
                ReporterLastName = incident.ReportedByInstructorProfile == null
                    ? incident.ReportedByStaffUser!.LastName
                    : incident.ReportedByInstructorProfile.User.LastName,
                incident.RowVersion
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row is null) return null;

        var metric = await GetMetricAsync(
            schoolId,
            row.StudentId,
            row.AcademicTermId,
            StudentTermMetricCode.CountableBehaviorIncident,
            cancellationToken).ConfigureAwait(false);
        var referralId = await _context.StudentReferrals
            .AsNoTracking()
            .Where(referral => referral.SchoolId == schoolId
                && referral.SourceType == ReferralSourceType.Behavior
                && referral.SourceEntityId == incidentId)
            .Select(referral => (int?)referral.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new BehaviorIncidentDto(
            row.Id,
            Student(row.StudentId, row.StudentNumber, row.StudentName, row.ClassroomId,
                row.ClassroomLabel, row.IsActive),
            row.CategoryCode,
            row.Severity,
            row.Description,
            row.OccurredAt,
            row.Location,
            row.ImmediateActionTaken,
            Actor(row.ReporterUserId, row.ReporterFirstName, row.ReporterLastName),
            row.GuardianDispatchDecision,
            Badge(StudentTermMetricCode.CountableBehaviorIncident, metric, row.Severity.ToString(), row.OccurredAt),
            referralId,
            new[] { "MetricRecalculation", "OfficerDispatchDecision" },
            Convert.ToBase64String(row.RowVersion));
    }

    public async Task<AcademicConcernDto?> GetAcademicConcernDtoAsync(
        int schoolId,
        int concernId,
        CancellationToken cancellationToken)
    {
        var row = await _context.AcademicConcerns
            .AsNoTracking()
            .Where(concern => concern.Id == concernId && concern.SchoolId == schoolId)
            .Select(concern => new
            {
                concern.Id,
                concern.StudentId,
                concern.Student.StudentNumber,
                StudentName = (concern.Student.FirstName + " "
                    + (concern.Student.MiddleName ?? string.Empty) + " "
                    + concern.Student.LastName).Trim(),
                concern.Student.IsActive,
                concern.ClassroomId,
                ClassroomLabel = concern.Classroom == null ? null : concern.Classroom.ClassLabel,
                concern.AcademicTermId,
                concern.Category,
                concern.Description,
                concern.OccurredAt,
                concern.GuardianDispatchDecision,
                ReporterUserId = concern.ReportedByInstructorProfile.UserId,
                ReporterFirstName = concern.ReportedByInstructorProfile.User.FirstName,
                ReporterLastName = concern.ReportedByInstructorProfile.User.LastName
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row is null) return null;

        var metric = await GetMetricAsync(
            schoolId,
            row.StudentId,
            row.AcademicTermId,
            StudentTermMetricCode.AcademicConcern,
            cancellationToken).ConfigureAwait(false);
        var referralId = await _context.StudentReferrals
            .AsNoTracking()
            .Where(referral => referral.SchoolId == schoolId
                && referral.SourceType == ReferralSourceType.AcademicConcern
                && referral.SourceEntityId == concernId)
            .Select(referral => (int?)referral.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AcademicConcernDto(
            row.Id,
            Student(row.StudentId, row.StudentNumber, row.StudentName, row.ClassroomId,
                row.ClassroomLabel, row.IsActive),
            row.Category,
            row.Description,
            row.OccurredAt,
            Actor(row.ReporterUserId, row.ReporterFirstName, row.ReporterLastName),
            row.GuardianDispatchDecision,
            Badge(StudentTermMetricCode.AcademicConcern, metric, "None", row.OccurredAt),
            referralId,
            string.Empty);
    }

    public async Task<SessionDelayDto?> GetSessionDelayDtoAsync(
        int schoolId,
        int delayId,
        CancellationToken cancellationToken)
    {
        var row = await _context.SessionDelays
            .AsNoTracking()
            .Where(delay => delay.Id == delayId && delay.SchoolId == schoolId)
            .Select(delay => new
            {
                delay.Id,
                delay.StudentId,
                delay.Student.StudentNumber,
                StudentName = (delay.Student.FirstName + " "
                    + (delay.Student.MiddleName ?? string.Empty) + " "
                    + delay.Student.LastName).Trim(),
                delay.Student.IsActive,
                delay.ClassroomId,
                delay.Classroom.ClassLabel,
                delay.AcademicTermId,
                TimetableEntryId = delay.SchoolTimetableEntryId!.Value,
                delay.Period,
                delay.OccurredAt,
                delay.DelayMinutes,
                delay.Reason,
                delay.GuardianNotificationStatus,
                ReporterUserId = delay.ReportedByInstructorProfile.UserId,
                ReporterFirstName = delay.ReportedByInstructorProfile.User.FirstName,
                ReporterLastName = delay.ReportedByInstructorProfile.User.LastName,
                delay.RowVersion
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row is null) return null;

        var metric = await GetMetricAsync(
            schoolId,
            row.StudentId,
            row.AcademicTermId,
            StudentTermMetricCode.SessionDelay,
            cancellationToken).ConfigureAwait(false);

        return new SessionDelayDto(
            row.Id,
            Student(row.StudentId, row.StudentNumber, row.StudentName, row.ClassroomId,
                row.ClassLabel, row.IsActive),
            row.TimetableEntryId,
            row.Period,
            row.OccurredAt,
            row.DelayMinutes,
            row.Reason,
            Actor(row.ReporterUserId, row.ReporterFirstName, row.ReporterLastName),
            Badge(StudentTermMetricCode.SessionDelay, metric, "None", row.OccurredAt),
            null,
            Convert.ToBase64String(row.RowVersion));
    }

    private Task<MetricSnapshot?> GetMetricAsync(
        int schoolId,
        int studentId,
        int academicTermId,
        StudentTermMetricCode metricCode,
        CancellationToken cancellationToken) =>
        _context.StudentTermMetrics
            .AsNoTracking()
            .Where(metric => metric.SchoolId == schoolId
                && metric.StudentId == studentId
                && metric.AcademicTermId == academicTermId
                && metric.MetricCode == metricCode)
            .Select(metric => new MetricSnapshot(metric.Count, metric.RecalculatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    private static StudentSummaryDto Student(
        int id,
        string number,
        string name,
        int? classroomId,
        string? classLabel,
        bool isActive) => new(id, number, name, classroomId, classLabel, isActive, null);

    private static ActorSummaryDto Actor(string? userId, string? firstName, string? lastName) =>
        new(userId ?? string.Empty, $"{firstName} {lastName}".Trim(), RoleNames.Instructor);

    private static MetricBadgeDto Badge(
        StudentTermMetricCode code,
        MetricSnapshot? metric,
        string severity,
        DateTimeOffset lastOccurrenceAt) =>
        new(code, metric?.Count ?? 0, 0, null, severity, lastOccurrenceAt,
            metric?.RecalculatedAt ?? lastOccurrenceAt);

    private sealed record MetricSnapshot(int Count, DateTimeOffset RecalculatedAt);
}
