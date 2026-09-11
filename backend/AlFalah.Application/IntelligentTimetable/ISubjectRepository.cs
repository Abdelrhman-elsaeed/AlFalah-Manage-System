using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;

namespace AlFalah.Application.IntelligentTimetable;

public interface ISubjectRepository
{
    Task<TimetableSetupProfile?> GetSetupAsync(int schoolId, int setupId, CancellationToken ct);
    Task<IReadOnlyList<SubjectDefinition>> GetSubjectsAsync(int schoolId, CancellationToken ct);
    Task<IReadOnlyList<RoomDto>> GetRoomsAsync(int schoolId, CancellationToken ct);
    Task<IReadOnlyList<SubjectClassroomDto>> GetClassroomsAsync(int schoolId, int yearId, CancellationToken ct);
    Task<IReadOnlyList<ClassSubjectRequirement>> GetRequirementsAsync(int schoolId, int setupId, CancellationToken ct);
    Task<bool> IsUsedAsync(int schoolId, int requirementId, CancellationToken ct);
    Task<IReadOnlyList<TeachingAssignment>> GetTeachingAssignmentsAsync(int schoolId, int setupId, CancellationToken ct);
    void AddSubject(SubjectDefinition subject);
    void AddRoom(TimetableRoom room);
    void AddRequirement(ClassSubjectRequirement requirement);
    void SetRules(ClassSubjectRequirement requirement, SubjectRulesRequest rules);
    Task SaveAsync(int schoolId, int setupId, string userId, string action, object details, CancellationToken ct);
}
