using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Shared.Models;

namespace AlFalah.Application.Duty;

public sealed record DutyStudent(int StudentId, int EnrollmentId, int ClassroomId, string Name, string Classroom, int Grade, string StudentNumber);
public sealed record DutyClass(int Id, string Label, int Grade, int Count);
public sealed record DutyRosterQuery(int AssignmentId, string? Search = null, int? Grade = null, int? ClassroomId = null, int Page = 1);
public sealed record DutyRoster(PagedResult<DutyStudent> Students, IReadOnlyList<DutyClass> Classes);
public sealed record DutyReportLine(int StudentId, int EnrollmentId, TimeOnly Time, string Action, string Notes, string? StudentName = null, string? Classroom = null);
public sealed record DutyReportContent(bool NoLateStudents, bool NoViolations, IReadOnlyList<DutyReportLine> Violations,
    string Observations, TimeOnly? LastDeparture, string DutyNotes = "", string SubstituteNotes = "");
public sealed record DutyDraftRequest(int AssignmentRevision, int ExpectedVersion, DutyReportContent Content);
public sealed record DutySignRequest(int AssignmentRevision, Guid RequestId, int? DraftVersion = null);
public sealed record DutyCorrectionRequest(int AssignmentRevision, int ExpectedVersion, string Reason, Guid RequestId);
public sealed record DutyLateRequest(int AssignmentRevision, int StudentId, int EnrollmentId, TimeOnly ObservedAt,
    TimeOnly? DepartureAt, string Action, string Notes, Guid RequestId);
public sealed record DutyLateCorrection(int ExpectedRevision, TimeOnly ObservedAt, TimeOnly? DepartureAt, string Action,
    string Notes, string Reason, bool Retract, Guid RequestId);
public sealed record DutyTimingException(int AssignmentRevision, TimeOnly? DismissalAt, string Reason, Guid RequestId);
public sealed record DutyExecutionHeader(string School, string City, string? Header, string? Footer, string? LogoUrl,
    string Manager, string Year, int Semester, string OriginalTeacher, string ActualTeacher, bool IsSubstitute = false);
public sealed record DutyDocumentSummary(int Id, string Kind, int AssignmentRevision, string AuthorUserId,
    DateTimeOffset SignedAt, string SignatureHash, int? PreviousDocumentId, string? CorrectionReason);
public sealed record DutyDraftDto(int Id, int Version, int AssignmentRevision, string AuthorUserId,
    DutyReportContent Content, DateTimeOffset SavedAt, DateTimeOffset? SubmittedAt, string? CorrectionReason);
public sealed record DutyNoticeStatus(int Revision, string State, int Recipients, int DashboardDelivered, int MessagesDelivered,
    int DashboardRead, int MessagesRead);
public sealed record DutyLateDto(int Id, int Revision, int StudentId, int EnrollmentId, int ClassroomId,
    string StudentName, string Classroom, DateOnly Date, TimeOnly ObservedAt, TimeOnly DismissalAt, TimeOnly? DepartureAt,
    string Action, string Notes, bool IsRetracted, IReadOnlyList<DutyNoticeStatus> Notices);
public sealed record DutyExecution(int AssignmentId, int AssignmentRevision, DateOnly Date, bool CanExecute, bool CanManage,
    DutyExecutionHeader Header, TimeOnly? DismissalAt, string? TimingException, string? SignatureData,
    bool Acknowledged, DutyDraftDto? Draft, IReadOnlyList<DutyLateDto> LateStudents, IReadOnlyList<DutyDocumentSummary> Documents,
    IReadOnlyList<DutyDraftDto> PriorDrafts);
public sealed record DutyDismissalQuery(DateOnly? From = null, DateOnly? To = null, int? TermId = null,
    int? ClassroomId = null, int Page = 1);
public sealed record DutyDismissalStatistics(int UniqueStudents, int Incidents, PagedResult<DutyLateDto> Items);
public sealed record DutyGuardianNotice(int NotificationId, int FactId, int Revision, string StudentName, DateOnly Date,
    string Content, string? MessageRoute, bool IsRead, DateTimeOffset At);
public sealed record DutyLogoSnapshot(string? Data, string? Hash, string State);
public interface IDutyDocumentAssets
{
    Task<DutyLogoSnapshot> FreezeLogoAsync(string? source, CancellationToken ct);
}

public interface IDutyExecutionRepository
{
    Task<DutyAssignment?> AssignmentAsync(int school, int id, CancellationToken ct);
    Task<DutyPlan> PlanAsync(int school, int id, CancellationToken ct);
    Task<string> SchoolTimeZoneAsync(int school, CancellationToken ct);
    Task<DutyExecutionHeader> HeaderAsync(int school, DutyAssignment a, DutyPlan plan, CancellationToken ct);
    Task<UserSignature?> SignatureAsync(string user, CancellationToken ct);
    Task<TimeOnly?> DismissalAsync(DutyPlan plan, DateOnly date, CancellationToken ct);
    Task<List<DutyReportDraft>> DraftsAsync(int school, int assignment, CancellationToken ct);
    Task<List<DutySignedDocument>> DocumentsAsync(int school, int assignment, CancellationToken ct);
    Task<DutySignedDocument?> DocumentAsync(int school, int id, CancellationToken ct);
    Task<DutyRoster> RosterAsync(int school, DutyPlan plan, DateOnly date, DutyRosterQuery query, CancellationToken ct);
    Task<DutyStudent?> StudentAsync(int school, DutyPlan plan, DateOnly date, int student, int enrollment, CancellationToken ct);
    Task<List<DutyStudent>> StudentsAsync(int school, DutyPlan plan, DateOnly date, IReadOnlyList<int> studentIds, CancellationToken ct);
    Task<List<StudentDismissalLateness>> FactsAsync(int school, int assignment, CancellationToken ct);
    Task<StudentDismissalLateness?> FactAsync(int school, int id, CancellationToken ct);
    Task<StudentDismissalLateness?> ExistingFactAsync(int school, int student, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<DutyNoticeStatus>> NoticeStatusAsync(int school, int fact, CancellationToken ct);
    Task<Dictionary<int, IReadOnlyList<DutyNoticeStatus>>> NoticeStatusesAsync(int school, IReadOnlyList<int> facts, CancellationToken ct);
    Task<DutyDismissalStatistics> StatisticsAsync(int school, DutyDismissalQuery query, CancellationToken ct);
    Task<List<DismissalNoticeBatch>> PendingNoticesAsync(CancellationToken ct);
    Task<DismissalNoticeBatch?> BatchAsync(int school, int id, CancellationToken ct);
    Task<List<DismissalNoticeBatch>> FactBatchesAsync(int school, int fact, CancellationToken ct);
    Task<List<string>> RecipientsAsync(int school, int student, DateOnly currentDate, CancellationToken ct);
    Task<List<DismissalNoticeRecipient>> NoticeRecipientsAsync(int school, int batch, CancellationToken ct);
    Task DeliverMessageAsync(StudentDismissalLateness fact, DismissalNoticeBatch batch, DismissalNoticeRecipient recipient, CancellationToken ct);
    Task DeliverDashboardAsync(StudentDismissalLateness fact, DismissalNoticeBatch batch, DismissalNoticeRecipient recipient, CancellationToken ct);
    Task<List<DutyGuardianNotice>> GuardianNoticesAsync(int school, string user, DateOnly currentDate, CancellationToken ct);
    Task MarkGuardianNoticeReadAsync(int school, string user, int notification, DateOnly currentDate, CancellationToken ct);
    Task<string?> TimingReasonAsync(int school, int assignment, int revision, CancellationToken ct);
    Task<TimeOnly?> DismissalOverrideAsync(int school, int assignment, int revision, CancellationToken ct);
    Task<bool> UnavailableAsync(int school, string user, DateOnly date, CancellationToken ct);
}
public interface IDutyExecutionService
{
    Task<DutyExecution> GetAsync(int assignment, CancellationToken ct);
    Task<DutyRoster> RosterAsync(DutyRosterQuery query, CancellationToken ct);
    Task<DutyExecution> SaveDraftAsync(int assignment, DutyDraftRequest request, CancellationToken ct);
    Task<DutyExecution> AcknowledgeAsync(int assignment, DutySignRequest request, CancellationToken ct);
    Task<DutyExecution> SubmitAsync(int assignment, DutySignRequest request, CancellationToken ct);
    Task<DutyExecution> CorrectReportAsync(int assignment, DutyCorrectionRequest request, CancellationToken ct);
    Task<DutyLateDto> ConfirmLateAsync(int assignment, DutyLateRequest request, CancellationToken ct);
    Task<DutyLateDto> CorrectLateAsync(int fact, DutyLateCorrection request, CancellationToken ct);
    Task<DutyExecution> TimingExceptionAsync(int assignment, DutyTimingException request, CancellationToken ct);
    Task<DutyDismissalStatistics> StatisticsAsync(DutyDismissalQuery query, CancellationToken ct);
    Task RetryNoticesAsync(int fact, CancellationToken ct);
    Task<List<DutyGuardianNotice>> GuardianNoticesAsync(CancellationToken ct);
    Task MarkGuardianNoticeReadAsync(int notification, CancellationToken ct);
    Task<string> SignedSnapshotAsync(int document, CancellationToken ct);
}
