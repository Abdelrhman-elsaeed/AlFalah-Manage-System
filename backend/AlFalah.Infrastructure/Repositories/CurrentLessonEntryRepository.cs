using AlFalah.Application.IntelligentTimetable;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class CurrentLessonEntryRepository(AlFalahDbContext context) : ICurrentLessonEntryRepository
{
    public async Task<IReadOnlyList<CurrentLessonEntry>> GetEntriesAsync(
        int schoolId,
        int schoolTimetableId,
        DateOnly localDate,
        TimetableDay day,
        int periodSequence,
        int? classroomId,
        string? classroomLabel,
        CancellationToken cancellationToken)
    {
        var query = context.SchoolTimetableEntries
            .AsNoTracking()
            .Where(entry => entry.SchoolId == schoolId
                && entry.SchoolTimetableId == schoolTimetableId
                && entry.SchoolTimetable.IsPublished
                && !entry.SchoolTimetable.IsDeleted
                && !entry.IsDeleted
                && entry.EntryType == TimetableEntryType.Lesson
                && entry.Day == day
                && entry.Period == periodSequence);

        if (classroomId.HasValue)
        {
            var normalizedLabel = classroomLabel?.Trim();
            query = query.Where(entry => entry.ClassroomId == classroomId.Value
                || (entry.ClassroomId == null
                    && normalizedLabel != null
                    && entry.ClassLabel == normalizedLabel));
        }

        var baseEntries = await query
            .Select(entry => new EntryProjection(
                entry.Id,
                entry.ClassroomId,
                entry.Classroom == null ? entry.ClassLabel ?? string.Empty : entry.Classroom.ClassLabel,
                entry.Classroom == null ? null : entry.Classroom.Stage,
                entry.Classroom == null ? null : entry.Classroom.GradeLevel,
                entry.Classroom == null ? null : entry.Classroom.Section,
                entry.Subject ?? entry.InstructorProfile.SubjectSpecialization ?? string.Empty,
                entry.InstructorProfileId,
                entry.InstructorProfile.UserId,
                (entry.InstructorProfile.User.FirstName + " " + entry.InstructorProfile.User.LastName).Trim()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (baseEntries.Count == 0) return Array.Empty<CurrentLessonEntry>();

        var entryIds = baseEntries.Select(x => x.Id).ToArray();
        var substitutions = await context.TimetableSubstitutions
            .AsNoTracking()
            .Where(substitution => substitution.SchoolId == schoolId
                && substitution.SchoolTimetableId == schoolTimetableId
                && substitution.LocalDate == localDate
                && substitution.Kind == "Substitution")
            .SelectMany(substitution => substitution.Movements
                .Where(movement => entryIds.Contains(movement.SchoolTimetableEntryId))
                .Select(movement => new
                {
                    SubstitutionId = substitution.Id,
                    movement.SchoolTimetableEntryId,
                    movement.ToTeacherId
                }))
            .OrderByDescending(x => x.SubstitutionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var effectiveByEntry = substitutions
            .GroupBy(x => x.SchoolTimetableEntryId)
            .ToDictionary(x => x.Key, x => x.First());
        var effectiveInstructorIds = effectiveByEntry.Values
            .Select(x => x.ToTeacherId)
            .Distinct()
            .ToArray();
        var effectiveInstructors = effectiveInstructorIds.Length == 0
            ? new Dictionary<int, CurrentLessonInstructor>()
            : await context.InstructorProfiles
                .AsNoTracking()
                .Where(profile => profile.SchoolId == schoolId
                    && profile.IsActive
                    && !profile.IsDeleted
                    && effectiveInstructorIds.Contains(profile.Id))
                .Select(profile => new CurrentLessonInstructor(
                    profile.Id,
                    profile.UserId,
                    (profile.User.FirstName + " " + profile.User.LastName).Trim()))
                .ToDictionaryAsync(x => x.Id, cancellationToken)
                .ConfigureAwait(false);

        return baseEntries.Select(entry =>
        {
            var original = new CurrentLessonInstructor(
                entry.OriginalInstructorId,
                entry.OriginalInstructorUserId,
                entry.OriginalInstructorDisplayName);
            if (!effectiveByEntry.TryGetValue(entry.Id, out var movement)
                || !effectiveInstructors.TryGetValue(movement.ToTeacherId, out var effective))
                return Map(entry, original, original, null);
            return Map(entry, original, effective, movement.SubstitutionId);
        }).ToArray();
    }

    private static CurrentLessonEntry Map(
        EntryProjection entry,
        CurrentLessonInstructor original,
        CurrentLessonInstructor effective,
        int? substitutionId) => new(
            entry.Id,
            entry.ClassroomId,
            entry.ClassroomLabel,
            entry.ClassroomStage,
            entry.ClassroomGradeLevel,
            entry.ClassroomSection,
            entry.Subject,
            original,
            effective,
            substitutionId);

    private sealed record EntryProjection(
        int Id,
        int? ClassroomId,
        string ClassroomLabel,
        SchoolStage? ClassroomStage,
        byte? ClassroomGradeLevel,
        string? ClassroomSection,
        string Subject,
        int OriginalInstructorId,
        string OriginalInstructorUserId,
        string OriginalInstructorDisplayName);
}
