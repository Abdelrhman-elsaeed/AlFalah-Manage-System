using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Interfaces;

public interface ISchoolTimetableRepository
{
    IQueryable<SchoolTimetable> GetAll();
    IQueryable<SchoolTimetableVersion> GetVersions(int timetableId);
    IQueryable<InstructorProfile> GetTeachers(int schoolId);
    IQueryable<AcademicYear> GetAcademicYears();
    IQueryable<School> GetSchools();
    IQueryable<ApplicationUser> GetModerators(int schoolId);
    IQueryable<TimetableEditorGrant> GetGrants(int schoolId);
    Task<List<TimetableEditorGrant>> GetTrackedGrantsAsync(int schoolId, CancellationToken cancellationToken = default);
    Task<SchoolTimetable?> GetTrackedWithEntriesAsync(int timetableId, CancellationToken cancellationToken = default);
    Task<bool> AcademicYearExistsAsync(int academicYearId, CancellationToken cancellationToken = default);
    Task AddAsync(SchoolTimetable timetable, CancellationToken cancellationToken = default);
    Task AddVersionAsync(SchoolTimetableVersion version, CancellationToken cancellationToken = default);
    Task AddEntriesAsync(IEnumerable<SchoolTimetableEntry> entries, CancellationToken cancellationToken = default);
    Task AddGrantsAsync(IEnumerable<TimetableEditorGrant> grants, CancellationToken cancellationToken = default);
    void SoftDeleteEntries(IEnumerable<SchoolTimetableEntry> entries, DateTimeOffset deletedAt);
    void SoftDeleteGrants(IEnumerable<TimetableEditorGrant> grants, string userId, DateTimeOffset deletedAt);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
