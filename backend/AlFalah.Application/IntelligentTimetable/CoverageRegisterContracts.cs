using AlFalah.Domain.Entities;
using AlFalah.Shared.Models;

namespace AlFalah.Application.IntelligentTimetable;

public sealed record CoverageRegisterHeader(string School, string Year, int Semester, string? Letterhead, string TimeZoneId = "Africa/Cairo");
public sealed record CoverageRegisterSignature(int RecordId, string ActorId, DateTimeOffset SignedAt, string Sha256, byte[] Bytes);
public sealed record CoverageRegisterRow(int NeedId, int NeedRevision, int OriginalTeacherId, string OriginalTeacher, int Period,
    int? SubstitutionId, string Classroom, string Subject, string? Teacher, string State, bool CorrectionOpen,
    string Narrative, string Notes, CoverageRegisterSignature? Receipt, CoverageRegisterSignature? Execution);
public sealed record CoverageRegisterSnapshot(CoverageRegisterHeader Header, DateOnly Date, bool IsComplete, IReadOnlyList<CoverageRegisterRow> Rows);
public sealed record CoverageRegisterWeek(DateOnly Start, DateOnly End, IReadOnlyList<CoverageRegisterSnapshot> Days);
public sealed record CoverageRegisterInfo(int Id, DateOnly Date, int Revision, string SourceRevision, bool IsComplete,
    string FileName, string Sha256, DateTimeOffset IssuedAt, string Scope = "Day", DateOnly? EndDate = null);
public sealed record CoverageRegisterContext(DateOnly Date, string? SourceRevision, string? Issue, int TeacherCount, int PeriodCount,
    int ExecutedPeriods, bool IsComplete, PagedResult<CoverageRegisterInfo> History, string Scope = "Day", DateOnly? EndDate = null);
public sealed record IssueCoverageRegisterRequest(DateOnly Date, string SourceRevision, string Scope = "Day");
public sealed record CoverageRegisterFile(byte[] Bytes, string FileName, string Sha256, int Revision);
public interface ICoverageRegisterRenderer
{
    string Version { get; }
    byte[] Render(CoverageRegisterSnapshot snapshot, int revision, DateTimeOffset issuedAt);
    byte[] RenderWeek(CoverageRegisterWeek snapshot, int revision, DateTimeOffset issuedAt);
}
public interface ICoverageRegisterRepository
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task<IReadOnlyList<DailyCoverageNeed>> NeedsAsync(int school, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<DailyCoverageNeed>> RegisterNeedsAsync(int school, DateOnly start, DateOnly end, CancellationToken ct);
    Task<CoverageRegisterHeader> RegisterHeaderAsync(int school, DateOnly date, IReadOnlyList<int> timetableIds, CancellationToken ct);
    Task<CoverageRegisterArtifact?> RegisterAsync(int school, int id, CancellationToken ct);
    Task<CoverageRegisterArtifact?> RegisterBySourceAsync(int school, DateOnly date, string source, string renderer, CancellationToken ct, string scope = "Day");
    Task<PagedResult<CoverageRegisterArtifact>> RegisterHistoryAsync(int school, DateOnly date, int page, int size, CancellationToken ct, string scope = "Day");
    Task<int> NextRegisterRevisionAsync(int school, DateOnly date, CancellationToken ct, string scope = "Day");
    void AddRegister(CoverageRegisterArtifact artifact);
    void AddAudit(AuditLog audit);
    Task SaveAsync(CancellationToken ct);
}
