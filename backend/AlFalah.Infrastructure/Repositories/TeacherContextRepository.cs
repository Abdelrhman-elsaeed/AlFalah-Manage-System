using AlFalah.Application.StudentAffairs.TeacherContext;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class TeacherContextRepository : ITeacherContextRepository
{
    private readonly AlFalahDbContext _context;

    public TeacherContextRepository(AlFalahDbContext context) => _context = context;

    public async Task<TeacherContextSnapshot?> GetTopPriorityAsync(
        TeacherContextLookup lookup,
        CancellationToken cancellationToken)
    {
        var teacher = await _context.InstructorProfiles
            .AsNoTracking()
            .Where(profile => profile.SchoolId == lookup.SchoolId
                && profile.UserId == lookup.TeacherUserId
                && profile.IsActive
                && profile.User.IsActive)
            .Select(profile => new TeacherIdentitySnapshot(
                profile.Id,
                profile.UserId,
                (profile.User.FirstName + " " + profile.User.LastName).Trim()))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (teacher is null)
        {
            return null;
        }

        var timetable = await _context.SchoolTimetables
            .AsNoTracking()
            .Where(candidate => candidate.SchoolId == lookup.SchoolId
                && candidate.IsPublished
                && candidate.AcademicYear.StartsOn <= lookup.SchoolLocalDate
                && candidate.AcademicYear.EndsOn >= lookup.SchoolLocalDate)
            .OrderByDescending(candidate => candidate.PublishedAt)
            .ThenByDescending(candidate => candidate.Revision)
            .Select(candidate => new PublishedTimetableSnapshot(
                candidate.Id,
                candidate.AcademicYearId,
                candidate.Semester,
                candidate.Revision))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        TeacherTimetablePeriodSnapshot? period = null;
        IReadOnlyList<TeacherRosterStudentSnapshot> roster = Array.Empty<TeacherRosterStudentSnapshot>();
        if (timetable is not null)
        {
            period = await ResolvePeriodAsync(
                lookup,
                teacher.InstructorProfileId,
                timetable.Id,
                cancellationToken).ConfigureAwait(false);

            if (period is not null)
            {
                roster = await GetRosterAsync(
                    lookup,
                    timetable,
                    period.Classroom.Id,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        var pendingGatePasses = await _context.GatePasses
            .AsNoTracking()
            .CountAsync(gatePass => gatePass.SchoolId == lookup.SchoolId
                && gatePass.CurrentInstructorProfileId == teacher.InstructorProfileId
                && gatePass.Status == GatePassStatus.Approved
                && gatePass.ApprovedWindowEndsAt >= lookup.UtcNow,
                cancellationToken)
            .ConfigureAwait(false);

        var pendingEntryPermits = await _context.ClassroomEntryPermits
            .AsNoTracking()
            .CountAsync(permit => permit.SchoolId == lookup.SchoolId
                && permit.TargetInstructorProfileId == teacher.InstructorProfileId
                && permit.Status == ClassroomEntryPermitStatus.Issued
                && permit.ValidUntil >= lookup.UtcNow,
                cancellationToken)
            .ConfigureAwait(false);

        return new TeacherContextSnapshot(
            teacher,
            timetable?.Revision ?? 0,
            period,
            roster,
            pendingGatePasses,
            pendingEntryPermits);
    }

    private async Task<TeacherTimetablePeriodSnapshot?> ResolvePeriodAsync(
        TeacherContextLookup lookup,
        int instructorProfileId,
        int timetableId,
        CancellationToken cancellationToken)
    {
        var entries = _context.SchoolTimetableEntries
            .AsNoTracking()
            .Where(entry => entry.SchoolId == lookup.SchoolId
                && entry.SchoolTimetableId == timetableId
                && entry.InstructorProfileId == instructorProfileId
                && entry.EntryType == TimetableEntryType.Lesson
                && entry.ClassroomId != null
                && entry.Classroom!.IsActive);

        if (lookup.SchoolLocalDay is not null && lookup.CurrentPeriod is not null)
        {
            var current = await ProjectPeriod(entries
                    .Where(entry => entry.Day == lookup.SchoolLocalDay
                        && entry.Period == lookup.CurrentPeriod))
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (current is not null || !lookup.AllowOffHoursFallback)
            {
                return current;
            }
        }
        else if (!lookup.AllowOffHoursFallback)
        {
            return null;
        }

        if (lookup.SchoolLocalDay is not null)
        {
            var sameDay = await ProjectPeriod(entries
                    .Where(entry => entry.Day == lookup.SchoolLocalDay)
                    .OrderBy(entry => entry.Period == lookup.FallbackPeriod ? 0 : 1)
                    .ThenBy(entry => entry.Period))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (sameDay is not null)
            {
                return sameDay;
            }
        }

        return await ProjectPeriod(entries
                .OrderBy(entry => entry.Day)
                .ThenBy(entry => entry.Period))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<TeacherRosterStudentSnapshot>> GetRosterAsync(
        TeacherContextLookup lookup,
        PublishedTimetableSnapshot timetable,
        int classroomId,
        CancellationToken cancellationToken) =>
        await _context.StudentEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.SchoolId == lookup.SchoolId
                && enrollment.ClassroomId == classroomId
                && enrollment.Status == StudentEnrollmentStatus.Active
                && enrollment.EnrolledOn <= lookup.SchoolLocalDate
                && (enrollment.WithdrawnOn == null
                    || enrollment.WithdrawnOn >= lookup.SchoolLocalDate)
                && enrollment.Student.IsActive
                && enrollment.Classroom.IsActive
                && enrollment.AcademicTerm.IsActive
                && enrollment.AcademicTerm.StartsOn <= lookup.SchoolLocalDate
                && enrollment.AcademicTerm.EndsOn >= lookup.SchoolLocalDate
                && enrollment.AcademicTerm.AcademicYearId == timetable.AcademicYearId
                && enrollment.AcademicTerm.Semester == timetable.Semester)
            .OrderBy(enrollment => enrollment.RollNumber)
            .ThenBy(enrollment => enrollment.Student.FirstName)
            .ThenBy(enrollment => enrollment.Student.LastName)
            .Select(enrollment => new TeacherRosterStudentSnapshot(
                enrollment.StudentId,
                enrollment.Student.StudentNumber,
                (enrollment.Student.FirstName + " "
                    + (enrollment.Student.MiddleName ?? string.Empty) + " "
                    + enrollment.Student.LastName).Trim(),
                enrollment.ClassroomId,
                enrollment.Classroom.ClassLabel,
                enrollment.Student.IsActive,
                enrollment.Student.ProfilePhotoStorageKey))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static IQueryable<TeacherTimetablePeriodSnapshot> ProjectPeriod(
        IQueryable<AlFalah.Domain.Entities.SchoolTimetableEntry> entries) =>
        entries.Select(entry => new TeacherTimetablePeriodSnapshot(
            entry.Id,
            entry.Period,
            entry.Subject ?? entry.InstructorProfile.SubjectSpecialization ?? string.Empty,
            new TeacherClassroomSnapshot(
                entry.Classroom!.Id,
                entry.Classroom.ClassLabel,
                entry.Classroom.Stage,
                entry.Classroom.GradeLevel,
                entry.Classroom.Section)));

    private sealed record PublishedTimetableSnapshot(
        int Id,
        int AcademicYearId,
        TimetableSemester Semester,
        int Revision);
}
