using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;

namespace AlFalah.Application.IntelligentTimetable;

public sealed record TimetableValidationContext(SchoolTimetable Timetable, TimetableSetupProfile? Setup,
    BellScheduleRevision? Schedule, IReadOnlyList<TeacherTimetableProfile> Teachers,
    IReadOnlyList<ClassSubjectRequirement> Requirements, IReadOnlyList<TeachingAssignment> Assignments,
    IReadOnlyList<Classroom> Classrooms, IReadOnlyList<SubjectDefinition> Subjects, IReadOnlyList<TimetableRoom> Rooms);

public interface ITimetableReviewRepository
{
    Task<IReadOnlyList<ReviewTimetableOption>> GetTimetablesAsync(int school, CancellationToken ct);
    Task<TimetableValidationContext?> GetValidationContextAsync(int school, int timetableId, CancellationToken ct);
    Task<TimetableAnalysisRun?> GetLatestAnalysisRunAsync(int school, int timetableId, CancellationToken ct);
    Task<TimetableAnalysisFinding?> GetFindingByIdAsync(int school, int findingId, CancellationToken ct);
    void AddAnalysis(TimetableAnalysisRun run);
    void AddVersion(SchoolTimetableVersion version);
    Task<int> NextVersionAsync(int timetableId, CancellationToken ct);
    void AddAudit(AuditLog audit);
    Task SaveAsync(CancellationToken ct);
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task StageMovementsAsync(SchoolTimetable timetable, IReadOnlyList<RepairMovementDto> movements, CancellationToken ct);
}
