using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Infrastructure.Data;

namespace AlFalah.Infrastructure.Repositories;

public sealed class ResolvedTimetableClassroom
{
    public SchoolTimetableEntry Entry { get; init; } = null!;
    public Classroom? Classroom { get; init; }
}

/// <summary>Read-only legacy resolution. Published entries and serialized versions are never rewritten.</summary>
public static class TimetableClassroomReferences
{
    public static IQueryable<ResolvedTimetableClassroom> WithResolvedClassroom(this IQueryable<SchoolTimetableEntry> source,
        AlFalahDbContext db) => source.Select(entry => new ResolvedTimetableClassroom
        {
            Entry = entry,
            Classroom = db.Classrooms.Where(c => c.SchoolId == entry.SchoolId && c.AcademicYearId == entry.SchoolTimetable.AcademicYearId &&
                (entry.ClassroomId != null ? c.Id == entry.ClassroomId : c.ClassLabel == entry.ClassLabel ||
                    db.ClassroomLabelHistory.Any(a => a.SchoolId == c.SchoolId && a.AcademicYearId == c.AcademicYearId &&
                        a.ClassroomId == c.Id && a.Label == entry.ClassLabel)))
                .Where(c => entry.ClassroomId != null || db.Classrooms.Count(other => other.SchoolId == entry.SchoolId &&
                    other.AcademicYearId == entry.SchoolTimetable.AcademicYearId && (other.ClassLabel == entry.ClassLabel ||
                        db.ClassroomLabelHistory.Any(a => a.SchoolId == other.SchoolId && a.AcademicYearId == other.AcademicYearId &&
                            a.ClassroomId == other.Id && a.Label == entry.ClassLabel))) == 1).FirstOrDefault()
        });
}
