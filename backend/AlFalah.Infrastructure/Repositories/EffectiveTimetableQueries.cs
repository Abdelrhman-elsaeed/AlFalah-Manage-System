using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;

namespace AlFalah.Infrastructure.Repositories;

internal static class EffectiveTimetableQueries
{
    public static IQueryable<SchoolTimetableEntry> ForTeacherOn(this IQueryable<SchoolTimetableEntry> entries,
        AlFalahDbContext db, DateOnly date, int teacherId) => entries.Where(entry =>
            (db.Set<TimetableSubstitutionMovement>().Where(m => m.SchoolId == entry.SchoolId && m.SchoolTimetableEntryId == entry.Id &&
                m.Substitution.LocalDate == date && m.Substitution.Kind == "Substitution")
                .OrderByDescending(m => m.TimetableSubstitutionId).Select(m => (int?)m.ToTeacherId).FirstOrDefault() ?? entry.InstructorProfileId) == teacherId);
}
