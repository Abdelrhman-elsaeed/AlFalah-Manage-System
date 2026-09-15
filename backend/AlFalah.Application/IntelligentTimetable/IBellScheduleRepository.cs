using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

public interface IBellScheduleRepository
{
    Task<IReadOnlyList<BellScheduleDto>> ListAsync(int schoolId, int yearId, TimetableSemester semester, CancellationToken ct);
    Task<BellScheduleTemplate?> FindAsync(int schoolId, int id, CancellationToken ct);
    Task<bool> NameExistsAsync(int schoolId, SaveBellScheduleRequest request, int? exceptId, CancellationToken ct);
    Task<BellScheduleDependencies> GetDependenciesAsync(int schoolId, int? templateId, int? profileId, CancellationToken ct);
    Task SaveRevisionAsync(BellScheduleTemplate template, BellScheduleRevision revision, bool isNew, CancellationToken ct);
    Task SelectAsync(TimetableSetupProfile profile, BellScheduleTemplate template, CancellationToken ct);
    Task<BellScheduleDto?> GetRevisionAsync(int schoolId, int revisionId, CancellationToken ct);
    Task<BellScheduleDto?> GetSelectedAsync(int schoolId, int yearId, TimetableSemester semester, int? profileId, CancellationToken ct);
    Task<BellScheduleDto?> GetPublishedAsync(int schoolId, DateTimeOffset instant, CancellationToken ct);
}

public sealed record BellScheduleDependencies(IReadOnlyList<TimetableSetupProfile> Profiles, IReadOnlyList<SchoolTimetable> Timetables)
{
    public void Invalidate(DateTimeOffset changedAt, string userId)
    {
        foreach (var profile in Profiles)
        {
            profile.Status = TimetableSetupStatus.Draft;
            profile.Revision++;
            profile.UpdatedAt = changedAt;
            profile.UpdatedByUserId = userId;
        }
        foreach (var timetable in Timetables.Where(x => !x.IsPublished))
        {
            timetable.TimingsRequireRevalidation = true;
            timetable.Revision++;
        }
    }
}

public sealed class BellScheduleConflictException(string message, Exception? inner = null) : Exception(message, inner);
