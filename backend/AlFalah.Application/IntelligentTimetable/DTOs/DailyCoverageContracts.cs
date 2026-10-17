namespace AlFalah.Application.IntelligentTimetable.DTOs;

public sealed record CoverageChoice(int NeedId, int TeacherId);
public sealed record CoveragePreviewRequest(DateOnly Date, string Mode, IReadOnlyList<CoverageChoice> Choices, DateTimeOffset ExpiresAt);
public sealed record CoverageApproveRequest(Guid RequestId, CoveragePreviewRequest Preview, string ProposalId, string? Reason);
public sealed record CoverageTeacherDto(int Id, string Name, int ExecutedPeriods, int PendingPeriods);
public sealed record CoverageOptionDto(int TeacherId, string Name, string Color, string[] Errors, string[] Warnings, int ExecutedPeriods, int PendingPeriods);
public sealed record CoverageNeedDto(int Id, int Revision, int TimetableId, int SourceEntryId, DateOnly Date, string OriginalTeacher,
    string Classroom, string Subject, int[] Periods, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string TimeZoneId,
    string State, bool IsRequired, int? SubstitutionId, int? AssignedTeacherId, string? AssignedTeacher, string? ReviewReason,
    DateTimeOffset? AcknowledgedAt, DateTimeOffset? SubmittedAt, string Narrative, string Notes, string? CorrectionReason);
public sealed record CoverageBoardDto(DateOnly Date, int? TimetableId, int? TimetableRevision, bool CanManage, bool CanOverride,
    string? Issue, DateTimeOffset Now, IReadOnlyList<CoverageNeedDto> Needs, IReadOnlyList<CoverageTeacherDto> Teachers);
public sealed record CoverageProposalItemDto(int NeedId, int Revision, int TeacherId, string TeacherName, string Color, string[] Warnings);
public sealed record CoveragePreviewDto(string ProposalId, DateTimeOffset ExpiresAt, string Mode,
    IReadOnlyList<CoverageProposalItemDto> Assignments, IReadOnlyList<int> UncoveredNeedIds);
public sealed record CoverageActionRequest(Guid RequestId, int Revision, int SubstitutionId, string? Reason = null);
public sealed record CoverageDraftRequest(int Revision, int SubstitutionId, string Narrative, string Notes);
public sealed record CoverageRecordDto(int Id, string Kind, string Actor, DateTimeOffset At, string Narrative, string Notes,
    string? Reason, string SnapshotHash, string? SignatureHash, string? SignatureData);
public sealed record CoverageTaskDetailDto(CoverageNeedDto Need, bool CanAcknowledge, bool CanExecute, bool CanEditDraft, string? BlockedReason,
    string? AccountSignature, IReadOnlyList<CoverageRecordDto> History);
