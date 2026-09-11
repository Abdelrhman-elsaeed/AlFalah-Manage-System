using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.DTOs;

public sealed record ReviewTimetableOption(int Id, string Title, int Revision, bool IsPublished);
public sealed record ReviewOption(int Id, string Name);
public sealed record ValidationFindingDto(int Id, ViolationRuleCode RuleCode, string RuleNameAr,
    ViolationSeverity Severity, string MessageAr, int? ClassroomId, string? ClassroomName,
    int? InstructorProfileId, string? TeacherName, int? SubjectId, string? SubjectName,
    TimetableDay? Day, int? Period, bool IsOverridden, string? OverrideReason, string? OverriddenByUserId,
    DateTimeOffset? OverriddenAt, bool CanRepair);
public sealed record ReviewEntryDto(int Id, int InstructorProfileId, int? ClassroomId, int? SubjectId,
    string TeacherName, string? ClassroomName, string? SubjectName, TimetableDay Day, int Period, int? RoomId);
public sealed record ReviewPeriodDto(int Day, int Period, string Start, string End);
public sealed record ReviewUnavailableDto(int InstructorProfileId, int Day, int Period);
public sealed record ReviewBreakDto(int Day, string Name, string Start, string End);
public sealed record TimetableReviewResultDto(int AnalysisRunId, int TimetableId, string Title, int TimetableRevision,
    int? SetupRevision, int? BellScheduleRevisionId, bool IsPublished, DateTimeOffset? CompletedAt,
    int HardViolationCount, int WarningCount, bool CanPublish, bool CanManage, bool CanOverride,
    IReadOnlyList<ValidationFindingDto> Findings, IReadOnlyList<ReviewEntryDto> Entries,
    IReadOnlyList<ReviewPeriodDto> Periods, IReadOnlyList<ReviewUnavailableDto> Unavailable,
    IReadOnlyList<ReviewOption> Teachers, IReadOnlyList<ReviewOption> Classrooms, IReadOnlyList<ReviewOption> Subjects,
    IReadOnlyList<ReviewBreakDto> Breaks);
public sealed record RepairMovementDto(int EntryId, TimetableDay FromDay, int FromPeriod, int FromTeacherId,
    TimetableDay ToDay, int ToPeriod, int ToTeacherId);
public sealed record RepairProposalDto(string Id, int AnalysisRunId, int FindingId, int TimetableRevision,
    RepairProposalKind Kind, int Rank, string DescriptionAr, string Confidence,
    int PredictedErrors, int PredictedWarnings, IReadOnlyList<RepairMovementDto> Movements);
public sealed record OverrideSoftViolationRequest(string Reason);
public sealed record GetReviewTimetablesQuery : IRequest<ApiResponse<IReadOnlyList<ReviewTimetableOption>>>;
public sealed record EvaluateTimetableQuery(int TimetableId) : IRequest<ApiResponse<TimetableReviewResultDto>>;
public sealed record GetRepairProposalsQuery(int TimetableId, int FindingId) : IRequest<ApiResponse<List<RepairProposalDto>>>;
public sealed record OverrideSoftViolationCommand(int FindingId, string Reason) : IRequest<ApiResponse<bool>>;
public sealed record ApplyRepairProposalCommand(int TimetableId, RepairProposalDto Proposal) : IRequest<ApiResponse<TimetableReviewResultDto>>;
