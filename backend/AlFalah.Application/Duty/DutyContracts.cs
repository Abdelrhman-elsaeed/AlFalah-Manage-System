using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Duty;

public static class DutyPermissions
{
    public const string View = "Duty.View";
    public const string Manage = "Duty.Manage";
}
public sealed class DutyException(string code, string message, int status = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
public sealed record DutyTeacher(int ProfileId, string UserId, string Name, bool IsEligible = true);
public sealed record DutyTerm(int Id, int YearId, string Year, int Semester, DateOnly From, DateOnly To);
public sealed record DutyExclusion(string UserId, DateOnly From, DateOnly To, string Reason);
public sealed record DutyClosure(DateOnly From, DateOnly To, string Reason);
public sealed record DutySettings(int TermId, DateOnly From, DateOnly To, IReadOnlyList<int> Weekdays,
    IReadOnlyList<string> TeacherIds, IReadOnlyList<DutyExclusion> Exclusions, IReadOnlyList<DutyClosure> Closures,
    DateOnly RegenerateFrom, DateOnly RegenerateTo, bool AcceptAttendanceCalendarMismatch = false);
public sealed record DutyPreviewRequest(DutySettings Settings, int ExpectedRevision);
public sealed record DutyPublishRequest(DutySettings Settings, int ExpectedRevision, string PreviewDigest, Guid RequestId);
public sealed record DutyChangeRequest(int AssignmentId, int ExpectedRevision, string TeacherId, int? OtherAssignmentId,
    string Reason, string Mode, bool AcceptException, int? ResolveChangeId, Guid RequestId);
public sealed record DutyMutationResult(int PlanId, int Revision);
public sealed record DutyDay(int Id, DateOnly Date, string OriginalTeacherId, string OriginalTeacher,
    string TeacherId, string Teacher, int Revision, bool Protected, bool Locked, string? ExceptionCode,
    string? Attendance, bool IsMine, string? ChangeKind);
public sealed record DutyHistory(int Id, string Kind, DateOnly Date, DateOnly? OtherDate, string Before,
    string After, string? OtherBefore, string? OtherAfter, string Reason, string Actor, DateTimeOffset At,
    int? RelatedChangeId, bool Unresolved, string BeforeTeacherId, string AfterTeacherId);
public sealed record DutyFairness(string UserId, string Name, int Planned, int Effective, int Covered,
    int Completed, int PendingCompensation);
public sealed record DutyPreviewDay(DateOnly Date, string TeacherId, string Teacher, string? Before,
    bool Preserved, string? ExceptionCode);
public sealed record DutyPreview(string Digest, IReadOnlyList<DutyPreviewDay> Days, IReadOnlyList<DutyFairness> Fairness,
    IReadOnlyList<string> Warnings, int ChangedDates);
public sealed record DutyWorkspace(bool CanManage, string School, DateOnly Today, string UserId,
    IReadOnlyList<DutyTerm> Terms, int? TermId, int PlanRevision, DutySettings? Settings,
    IReadOnlyList<DutyTeacher> Teachers, IReadOnlyList<int> SuggestedWeekdays, IReadOnlyList<DutyDay> Days,
    IReadOnlyList<DutyHistory> History, IReadOnlyList<DutyFairness> Fairness, int PendingRetries,
    IReadOnlyList<DutyClosure> Closures);
public sealed record DutyAccess(string School, bool Manager, bool Teacher);
public sealed record DutyTiming(int? RevisionId, string Zone, IReadOnlyList<int> Weekdays);
public sealed class DutyState
{
    public List<DutyPlan> Plans { get; init; } = [];
    public List<DutyAssignment> Assignments { get; init; } = [];
    public List<DutyAssignmentChange> Changes { get; init; } = [];
    public List<AttendanceRecord> Attendance { get; init; } = [];
    public List<DutyTeacher> Teachers { get; init; } = [];
    public Dictionary<string, string> Names { get; init; } = [];
}

public interface IDutyRepository
{
    Task<DutyAccess?> AccessAsync(int schoolId, string userId, CancellationToken ct);
    Task<List<DutyTerm>> TermsAsync(int schoolId, CancellationToken ct);
    Task<DutyTiming> TimingAsync(int schoolId, DutyTerm term, CancellationToken ct);
    Task<DutyState> LoadAsync(int schoolId, CancellationToken ct);
    Task<T> AtomicAsync<T>(int schoolId, Func<Task<T>> action, CancellationToken ct);
    Task<DutyRequestReceipt?> ReceiptAsync(int schoolId, Guid requestId, CancellationToken ct);
    Task<List<DutyReconciliationOperation>> PendingAsync(CancellationToken ct);
    Task<DutyReconciliationOperation?> OperationAsync(long id, CancellationToken ct);
    Task<int> PendingCountAsync(int schoolId, CancellationToken ct);
    void Add(object entity);
    void Remove(object entity);
    void Notify(int schoolId, string userId, string key, string message);
    Task SaveAsync(CancellationToken ct);
}
public interface IDutyService
{
    Task<DutyWorkspace> WorkspaceAsync(int? termId, CancellationToken ct);
    Task<DutyPreview> PreviewAsync(DutyPreviewRequest request, CancellationToken ct);
    Task<DutyMutationResult> PublishAsync(DutyPublishRequest request, CancellationToken ct);
    Task<DutyPreview> ChangePreviewAsync(DutyChangeRequest request, CancellationToken ct);
    Task<DutyMutationResult> ChangeAsync(DutyChangeRequest request, CancellationToken ct);
    Task RetryAsync(CancellationToken ct);
}
