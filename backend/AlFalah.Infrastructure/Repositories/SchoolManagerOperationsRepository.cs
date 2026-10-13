using AlFalah.Application.DTOs.Dashboards;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class SchoolManagerOperationsRepository(AlFalahDbContext context) : ISchoolManagerOperationsRepository
{
    private static readonly string[] StaffRoles = [RoleNames.SchoolManager, RoleNames.Moderator, RoleNames.Instructor];

    public async Task<bool> IsActiveManagerAsync(int schoolId, string userId, CancellationToken cancellationToken) =>
        await context.UserSchoolRoles.AsNoTracking().AnyAsync(x => x.SchoolId == schoolId
            && x.UserId == userId && x.IsActive && !x.IsDeleted && x.User.IsActive && x.Role.Name == RoleNames.SchoolManager
            && x.School.IsActive && !x.School.IsDeleted, cancellationToken).ConfigureAwait(false);

    public async Task<SchoolManagerOperationsDto> ReadAsync(SchoolManagerOperationsQuery query, CancellationToken ct)
    {
        // Queries are deliberately sequential: this repository shares one scoped DbContext.
        var staff = await ReadStaffAsync(query, ct).ConfigureAwait(false);
        var students = query.IncludeStudents ? await ReadStudentsAsync(query, ct).ConfigureAwait(false) : null;
        var summons = query.IncludeStudents ? await ReadSummonsAsync(query, ct).ConfigureAwait(false) : null;
        var visits = await context.Visits.AsNoTracking()
            .Where(x => x.SchoolId == query.SchoolId && !x.IsDeleted && x.ExperienceVersion == ExperienceVersion.PrototypeV2)
            .OrderByDescending(x => x.VisitDate).ThenByDescending(x => x.Id).Take(5)
            .Select(x => new RecentSchoolVisitDto(x.Id, x.Instructor.FirstName + " " + x.Instructor.LastName,
                x.EvaluatorNameSnapshot ?? x.CreatedByUser.FirstName + " " + x.CreatedByUser.LastName,
                x.Subject, x.VisitDate, (int)x.Status,
                x.Status == VisitStatus.Approved && x.Analysis != null ? x.Analysis.OverallScore : null))
            .ToListAsync(ct).ConfigureAwait(false);
        var activities = await ReadActivitiesAsync(query, ct).ConfigureAwait(false);
        int? gatePasses = query.IncludeGatePasses
            ? await context.GatePasses.AsNoTracking().CountAsync(x => x.SchoolId == query.SchoolId && !x.IsDeleted, ct) : null;
        int? conversations = query.IncludeConversations
            ? await context.ConversationThreads.AsNoTracking().CountAsync(x => x.SchoolId == query.SchoolId && !x.IsDeleted, ct) : null;
        var dismissalDate = query.IncludeStudents ? await DutyGuardianScope.CurrentDate(context, query.SchoolId, ct) : query.Date;
        int? dismissal = query.IncludeStudents ? await context.Set<AlFalah.Domain.Entities.Duty.StudentDismissalLateness>().AsNoTracking()
            .Where(x => x.SchoolId == query.SchoolId && x.DutyDate == dismissalDate && !x.IsRetracted).Select(x => x.StudentId).Distinct().CountAsync(ct) : null;
        return new(default, query.Date, "", students, staff, summons, null, activities, gatePasses, conversations, visits, dismissal);
    }

    private async Task<StaffPulseDto> ReadStaffAsync(SchoolManagerOperationsQuery query, CancellationToken ct)
    {
        var roster = context.UserSchoolRoles.AsNoTracking().Where(x => x.SchoolId == query.SchoolId
            && x.IsActive && !x.IsDeleted && x.User.IsActive && StaffRoles.Contains(x.Role.Name!));
        var users = roster.Select(x => x.UserId).Distinct();
        var rows = from user in context.Users.AsNoTracking()
                   where users.Contains(user.Id)
                   join attendance in context.AttendanceRecords.AsNoTracking().Where(x => x.SchoolId == query.SchoolId
                       && x.AttendanceDate == query.Date) on user.Id equals attendance.UserId into records
                   from attendance in records.DefaultIfEmpty()
                   select new { Name = user.FirstName + " " + user.LastName,
                       Role = roster.Where(x => x.UserId == user.Id).OrderBy(x => x.Role.Name).Select(x => x.Role.Name).First(),
                       Status = attendance == null ? (int?)null : (int)attendance.Status };
        var counts = await rows.GroupBy(_ => 1).Select(g => new AttendanceCountsDto(g.Count(),
            g.Count(x => x.Status == (int)AttendanceStatus.Present),
            g.Count(x => x.Status == (int)AttendanceStatus.Absent),
            g.Count(x => x.Status == (int)AttendanceStatus.Excused))).SingleOrDefaultAsync(ct).ConfigureAwait(false);
        var absences = await rows.Where(x => x.Status == (int)AttendanceStatus.Absent || x.Status == (int)AttendanceStatus.Excused)
            .OrderBy(x => x.Status).ThenBy(x => x.Name).Take(8)
            .Select(x => new StaffAbsenceDto(x.Name, x.Role!, x.Status!.Value)).ToListAsync(ct).ConfigureAwait(false);
        var from = query.Date.AddDays(-30);
        var trend = await context.AttendanceRecords.AsNoTracking().Where(x => x.SchoolId == query.SchoolId
                && users.Contains(x.UserId) && x.AttendanceDate >= from && x.AttendanceDate <= query.Date)
            .GroupBy(x => x.AttendanceDate).OrderByDescending(g => g.Key).Take(14)
            .Select(g => new AttendanceTrendDto(g.Key, g.Count(x => x.Status == AttendanceStatus.Present), g.Count()))
            .ToListAsync(ct).ConfigureAwait(false);
        return new(counts ?? new(0, 0, 0, 0), absences, trend.OrderBy(x => x.Date).ToArray());
    }

    private async Task<StudentPulseDto> ReadStudentsAsync(SchoolManagerOperationsQuery query, CancellationToken ct)
    {
        var enrollments = context.StudentEnrollments.AsNoTracking().Where(x => x.SchoolId == query.SchoolId
            && !x.IsDeleted && x.Status == StudentEnrollmentStatus.Active && x.EnrolledOn <= query.Date
            && (x.WithdrawnOn == null || x.WithdrawnOn >= query.Date)
            && x.Student.IsActive && !x.Student.IsDeleted && x.Classroom.IsActive && !x.Classroom.IsDeleted
            && x.AcademicTerm.IsActive && !x.AcademicTerm.IsDeleted
            && x.AcademicTerm.StartsOn <= query.Date && x.AcademicTerm.EndsOn >= query.Date);
        var rows = from enrollment in enrollments
                   join daily in context.DailyStudentAttendances.AsNoTracking().Where(x => !x.IsDeleted && x.AttendanceDate == query.Date)
                       on new { enrollment.SchoolId, enrollment.StudentId, enrollment.ClassroomId, enrollment.AcademicTermId }
                       equals new { daily.SchoolId, daily.StudentId, daily.ClassroomId, daily.AcademicTermId } into records
                   from daily in records.DefaultIfEmpty()
                   group daily by new { enrollment.ClassroomId, enrollment.Classroom.ClassLabel } into g
                   orderby g.Key.ClassLabel, g.Key.ClassroomId
                   select new ClassroomPulseDto(g.Key.ClassroomId, g.Key.ClassLabel, g.Count(),
                       g.Count(x => x != null && x.Status == StudentAttendanceStatus.Present),
                       g.Count(x => x != null && x.Status == StudentAttendanceStatus.Absent),
                       g.Count(x => x != null && x.Status == StudentAttendanceStatus.AbsentExcused));
        var classrooms = await rows.ToListAsync(ct).ConfigureAwait(false);
        var counts = new AttendanceCountsDto(classrooms.Sum(x => x.Total), classrooms.Sum(x => x.Present),
            classrooms.Sum(x => x.Absent), classrooms.Sum(x => x.Excused));
        var from = query.Date.AddDays(-30);
        var trend = await context.DailyStudentAttendances.AsNoTracking().Where(x => x.SchoolId == query.SchoolId
                && !x.IsDeleted && x.AttendanceDate >= from && x.AttendanceDate <= query.Date)
            .GroupBy(x => x.AttendanceDate).OrderByDescending(g => g.Key).Take(14)
            .Select(g => new AttendanceTrendDto(g.Key, g.Count(x => x.Status == StudentAttendanceStatus.Present), g.Count()))
            .ToListAsync(ct).ConfigureAwait(false);
        return new(counts, classrooms, trend.OrderBy(x => x.Date).ToArray());
    }

    private async Task<SummonPulseDto> ReadSummonsAsync(SchoolManagerOperationsQuery query, CancellationToken ct)
    {
        var rows = context.GuardianSummons.AsNoTracking().Where(x => x.SchoolId == query.SchoolId && !x.IsDeleted);
        var counts = await rows.GroupBy(_ => 1).Select(g => new {
            Pending = g.Count(x => x.Status == GuardianSummonStatus.Pending),
            Attended = g.Count(x => x.Status == GuardianSummonStatus.Attended),
            Observation = g.Count(x => x.Status == GuardianSummonStatus.UnderObservation),
            Improved = g.Count(x => x.Status == GuardianSummonStatus.Improved)
        }).SingleOrDefaultAsync(ct).ConfigureAwait(false);
        var appointments = await rows.Where(x => x.Status == GuardianSummonStatus.Pending && x.ScheduledAt >= query.DayStartsAt)
            .OrderBy(x => x.ScheduledAt).ThenBy(x => x.Id).Take(4)
            .Select(x => new SummonAppointmentDto(x.Id, x.Student.FirstName + " " + x.Student.LastName,
                x.ScheduledAt!.Value, (int)x.Priority)).ToListAsync(ct).ConfigureAwait(false);
        return new(counts?.Pending ?? 0, counts?.Attended ?? 0, counts?.Observation ?? 0, counts?.Improved ?? 0, appointments);
    }

    private async Task<IReadOnlyList<SchoolActivityDto>> ReadActivitiesAsync(SchoolManagerOperationsQuery query, CancellationToken ct)
    {
        // Read safe metadata from source records instead of serializing audit payloads.
        var visits = await context.Visits.AsNoTracking()
            .Where(x => x.SchoolId == query.SchoolId && !x.IsDeleted && x.ExperienceVersion == ExperienceVersion.PrototypeV2)
            .OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id).Take(4)
            .Select(x => new SchoolActivityDto("visits", "تحديث زيارة " + x.Instructor.FirstName + " " + x.Instructor.LastName,
                x.UpdatedAt, "/visits")).ToListAsync(ct).ConfigureAwait(false);
        var staffAt = await context.AttendanceRecords.AsNoTracking().Where(x => x.SchoolId == query.SchoolId)
            .MaxAsync(x => (DateTimeOffset?)x.UpdatedAt, ct).ConfigureAwait(false);
        if (staffAt is { } staff) visits.Add(new("staff", "تحديث سجل حضور الموظفين", staff, "/attendance"));
        if (query.IncludeStudents)
        {
            var studentsAt = await context.DailyStudentAttendances.AsNoTracking()
                .Where(x => x.SchoolId == query.SchoolId && !x.IsDeleted)
                .MaxAsync(x => (DateTimeOffset?)x.UpdatedAt, ct).ConfigureAwait(false);
            if (studentsAt is { } students) visits.Add(new("students", "تحديث رصد حضور الطلاب", students, "/student-affairs/oversight"));
            var summonAt = await context.GuardianSummons.AsNoTracking().Where(x => x.SchoolId == query.SchoolId && !x.IsDeleted)
                .MaxAsync(x => (DateTimeOffset?)x.UpdatedAt, ct).ConfigureAwait(false);
            if (summonAt is { } summon) visits.Add(new("summons", "تحديث سجل استدعاءات أولياء الأمور", summon, "/student-affairs/oversight"));
        }
        var publishedAt = await context.SchoolTimetables.AsNoTracking().Where(x => x.SchoolId == query.SchoolId && !x.IsDeleted && x.IsPublished)
            .MaxAsync(x => x.PublishedAt, ct).ConfigureAwait(false);
        if (publishedAt is { } published) visits.Add(new("timetable", "نشر الجدول المدرسي", published, "/timetable"));
        return visits.OrderByDescending(x => x.At).ThenBy(x => x.Category).Take(6).ToArray();
    }
}
