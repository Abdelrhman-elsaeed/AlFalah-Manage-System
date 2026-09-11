using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;

namespace AlFalah.Application.IntelligentTimetable;

public interface ITeacherAvailabilityRepository
{
    Task<TimetableSetupProfile?> GetSetupAsync(int schoolId, int setupId, CancellationToken ct);
    Task<IReadOnlyList<AvailabilityTeacherDto>> GetTeachersAsync(int schoolId, CancellationToken ct);
    Task<AvailabilityTeacherDto?> GetTeacherAsync(int schoolId, int teacherId, CancellationToken ct);
    Task<BellScheduleRevision?> GetScheduleAsync(int schoolId, int templateId, CancellationToken ct);
    Task<TeacherTimetableProfile?> GetProfileAsync(int schoolId, int setupId, int teacherId, CancellationToken ct);
    Task<IReadOnlyList<TeacherTimetableProfile>> GetProfilesAsync(int schoolId, int setupId, CancellationToken ct);
    Task<IReadOnlyList<SchoolTimetableEntry>> GetAssignmentsAsync(int schoolId, int setupId, int teacherId, CancellationToken ct);
    Task<int> GetTeachingLoadAsync(int schoolId, int setupId, int teacherId, CancellationToken ct);
    Task SaveAsync(TeacherTimetableProfile profile, IReadOnlyList<AvailabilitySlotRequest> slots, object? before, CancellationToken ct);
}
