using AlFalah.Shared.Models;
using AlFalah.Domain.Enums;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.DTOs;

public enum SwapSearchScope
{
    SameDay = 1,
    WholeTimetable = 2
}

public sealed record SwapLessonDto(int EntryId, int TeacherId, string TeacherName, string Classroom,
    string Subject, int[] Periods, int[] EntryIds, string Start, string End, int? RoomId);
public sealed record SwapPreviewDto(int EntryId, string TeacherName, string Classroom, string Subject,
    int FromPeriod, int ToPeriod, string FromTime, string ToTime, string ToTeacherName, int? RoomId,
    TimetableDay FromDay, TimetableDay ToDay);
public sealed record SwapCandidateDto(string Id, string Kind, string Color, string Label,
    string[] Errors, string[] Warnings, IReadOnlyList<RepairMovementDto> Movements, IReadOnlyList<SwapPreviewDto> Preview);
public sealed record SwapCandidatesDto(int TimetableId, int Revision, DateOnly Date, int SourceEntryId,
    string Mode, DateTimeOffset ExpiresAt, bool CanOverride, IReadOnlyList<SwapCandidateDto> Candidates);
public sealed record InlineCandidateCellDto(int AnchorEntryId, IReadOnlyList<int> EntryIds, int TeacherId,
    TimetableDay Day, int Period, string Color, string? DirectProposalId,
    IReadOnlyList<string> AlternativeProposalIds, string? ReasonSummary);
public sealed record InlineSwapCandidatesDto(int TimetableId, int Revision, DateOnly Date, int SourceEntryId,
    IReadOnlyList<int> SourceEntryIds, SwapSearchScope Scope, DateTimeOffset ExpiresAt, bool CanOverride,
    IReadOnlyList<InlineCandidateCellDto> Cells, IReadOnlyList<SwapCandidateDto> Proposals);
public sealed record SubstitutionHistoryDto(int Id, string Kind, DateOnly Date, int BeforeRevision, int AfterRevision,
    string RequestedBy, string ApprovedBy, string? Reason, DateTimeOffset ConfirmedAt);
public sealed record DailySubstitutionDto(int TimetableId, string Title, int Revision, bool IsPublished,
    bool CanManage, bool CanOverride, IReadOnlyList<SwapLessonDto> Lessons, IReadOnlyList<SubstitutionHistoryDto> History);
public sealed record ExecuteSwapRequest(Guid RequestId, int Revision, DateOnly Date, int SourceEntryId,
    string Mode, string ProposalId, DateTimeOffset ExpiresAt, string? OverrideReason,
    SwapSearchScope Scope = SwapSearchScope.SameDay);
public sealed record GetDailySubstitutionsQuery(int TimetableId, DateOnly Date) : IRequest<ApiResponse<DailySubstitutionDto>>;
public sealed record GetSubstitutionTimetablesQuery : IRequest<ApiResponse<IReadOnlyList<ReviewTimetableOption>>>;
public sealed record GetSwapCandidatesQuery(int TimetableId, DateOnly Date, int SourceEntryId, string Mode) : IRequest<ApiResponse<SwapCandidatesDto>>;
public sealed record GetInlineSwapCandidatesQuery(int TimetableId, DateOnly Date, int SourceEntryId, SwapSearchScope Scope) : IRequest<ApiResponse<InlineSwapCandidatesDto>>;
public sealed record ExecuteSwapCommand(int TimetableId, ExecuteSwapRequest Request) : IRequest<ApiResponse<SubstitutionHistoryDto>>;
