using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable;

public static class StatementCategories
{
    public static bool Valid(string? value) => value is null or "Specialization" or "LifeSkills" or "CriticalThinking" or "Activity" or "Other";
}
public sealed record StatementHeader(string School, string Year, int Semester, string Setup, string? Header, string? Footer, string? Manager);
public sealed record StatementClass(int Id, int Stage, int Grade, string Label, string Code);
public sealed record StatementRequirement(int Id, int SubjectId, int ClassroomId, int Individual, int Pairs);
public sealed record StatementSource(int Revision, StatementHeader Header, IReadOnlyList<StatementClass> Classes,
    IReadOnlyList<SubjectDto> Subjects, IReadOnlyList<StatementRequirement> Requirements,
    IReadOnlyList<TeachingAssignmentDto> Assignments, IReadOnlyList<TeachingTeacherDto> Teachers);
public sealed record StatementAllocation(int RequirementId, int TeacherId, string Subject, int Stage, int Grade,
    string Classroom, string Code, string Category, string Mode, int Periods, int Pairs);
public sealed record StatementTeacher(int Id, string Name, string Specialization, IReadOnlyList<StatementAllocation> Allocations);
public sealed record StatementGrade(int Stage, int Grade);
public sealed record TeachingStatementSnapshot(int Revision, string Scope, string ScopeLabel, StatementHeader Header,
    DateTimeOffset CreatedAt, IReadOnlyList<StatementTeacher> Teachers, int PendingRequirements)
{
    public IReadOnlyList<StatementGrade> Grades { get; init; } = [];
}
public sealed record TeachingStatementFile(byte[] Content, string FileName, string Hash, int Revision, string Scope);
public sealed record GetTeachingStatementQuery(int SetupId, int Revision, string Scope) : IRequest<ApiResponse<TeachingStatementFile>>;

public interface ITeachingStatementRepository
{
    Task<TeachingAssignmentStatement?> FindAsync(int school, int setup, int revision, string scope, string renderer, CancellationToken ct);
    Task<StatementSource> CaptureAsync(int school, int setup, CancellationToken ct);
    Task<TeachingAssignmentStatement> StoreAsync(TeachingAssignmentStatement statement, CancellationToken ct);
}
public interface ITeachingStatementRenderer
{
    string Version { get; }
    byte[] Render(TeachingStatementSnapshot snapshot);
}
