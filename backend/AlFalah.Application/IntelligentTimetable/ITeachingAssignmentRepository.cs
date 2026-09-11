using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;

namespace AlFalah.Application.IntelligentTimetable;

public interface ITeachingAssignmentRepository
{
    Task<TimetableSetupProfile?> GetSetupAsync(int school, int setup, CancellationToken ct);
    Task<IReadOnlyList<ClassSubjectRequirement>> GetRequirementsAsync(int school, int setup, CancellationToken ct);
    Task<IReadOnlyList<TeachingAssignment>> GetAssignmentsAsync(int school, int setup, CancellationToken ct);
    Task<IReadOnlyList<TeachingTeacherDto>> GetTeachersAsync(int school, int setup, CancellationToken ct);
    Task<TeachingTeacherPage> SearchTeachersAsync(int school, int setup, TeachingTeacherSearch search, CancellationToken ct);
    void Add(TeachingAssignment assignment);
    void SetMembers(TeachingAssignment assignment, IReadOnlyList<TeachingMemberRequest> members);
    Task SaveAsync(TimetableSetupProfile setup, string actor, object before, object after, CancellationToken ct);
}
