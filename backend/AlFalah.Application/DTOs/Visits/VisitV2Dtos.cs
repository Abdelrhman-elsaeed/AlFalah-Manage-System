using AlFalah.Shared.Models;

namespace AlFalah.Application.DTOs.Visits;

public sealed record VisitV2AvailabilityDto(bool IsEnabled);

public sealed record VisitV2IndicatorDto(int Id, string Code, string TextAr, int SortOrder, bool IsObserved);

public sealed record VisitV2StandardDto(
    int Id,
    string Code,
    string TextAr,
    int SortOrder,
    int Score,
    string? EvidenceNote,
    IReadOnlyList<VisitV2IndicatorDto> Indicators);

public sealed record VisitV2DomainDto(
    int Id,
    string Code,
    string NameAr,
    int SortOrder,
    IReadOnlyList<VisitV2StandardDto> Standards);

public sealed record VisitV2ObservationCardDto(
    int RubricVersionId,
    int RubricVersionNumber,
    IReadOnlyList<VisitV2DomainDto> Domains,
    IReadOnlyList<VisitV2ScoreLabelDto> ScoreLabels);

public sealed record VisitV2ScoreLabelDto(int Score, string LabelAr);

public sealed record CreateVisitV2RequestDto(
    string InstructorId,
    int VisitCategory,
    int VisitSequence,
    DateTimeOffset VisitDate,
    int ClassroomPeriod,
    string Subject,
    string GradeClass,
    string LessonTitle,
    int PresentCount,
    int AbsentCount,
    string? Notes);

public sealed record VisitV2ScoreInputDto(
    int RubricStandardId,
    int Score,
    string? EvidenceNote,
    IReadOnlyList<int> ObservedIndicatorIds);

public sealed record UpdateVisitV2RequestDto(
    int VisitCategory,
    int VisitSequence,
    DateTimeOffset VisitDate,
    int ClassroomPeriod,
    string Subject,
    string GradeClass,
    string LessonTitle,
    int PresentCount,
    int AbsentCount,
    string? Notes,
    IReadOnlyList<VisitV2ScoreInputDto> Scores);

public sealed record VisitV2DomainAnalysisDto(
    int RubricDomainId,
    string DomainCode,
    string DomainNameAr,
    int Sum,
    int MaximumScore,
    int Percentage,
    string LevelAr,
    bool IsStrength,
    bool IsImprovementArea);

public sealed record VisitV2AnalysisDto(
    int TotalScore,
    int MaximumScore,
    int OverallPercentage,
    string PerformanceLevelAr,
    IReadOnlyList<VisitV2DomainAnalysisDto> Domains,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> ImprovementAreas,
    DateTimeOffset ComputedAt);

public sealed record VisitV2TreatmentDto(
    int Id,
    int? RubricDomainId,
    string DomainNameAr,
    string Goal,
    string Actions,
    string SuccessIndicators,
    int Source,
    int SortOrder);

public sealed record VisitV2TreatmentInputDto(
    int? Id,
    int? RubricDomainId,
    string DomainNameAr,
    string Goal,
    string Actions,
    string SuccessIndicators,
    int SortOrder);

public sealed record UpdateVisitV2TreatmentsDto(IReadOnlyList<VisitV2TreatmentInputDto> Items);

public sealed record VisitV2DetailDto(
    int Id,
    int SchoolId,
    string SchoolName,
    string InstructorId,
    string InstructorName,
    string EvaluatorName,
    string EvaluatorRole,
    int VisitCategory,
    string VisitCategoryLabelAr,
    int VisitSequence,
    string VisitSequenceLabelAr,
    int Status,
    string StatusLabelAr,
    DateTimeOffset VisitDate,
    int ClassroomPeriod,
    string Subject,
    string GradeClass,
    string LessonTitle,
    int PresentCount,
    int AbsentCount,
    string? Notes,
    int RubricVersionId,
    IReadOnlyList<VisitV2DomainDto> Domains,
    VisitV2AnalysisDto? Analysis,
    IReadOnlyList<VisitV2TreatmentDto> Treatments,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    string? RejectionReason,
    string? ReopenReason,
    bool IsReadOnly);

public sealed class VisitV2ArchiveQuery : PagedQuery
{
    public string? Search { get; init; }
    public string? EvaluatorUserId { get; init; }
    public int? Status { get; init; }
    public int? VisitCategory { get; init; }
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
}

public sealed record VisitV2ArchiveItemDto(
    int Id,
    DateTimeOffset VisitDate,
    int ClassroomPeriod,
    string InstructorName,
    string? EmployeeNumber,
    string Subject,
    string GradeClass,
    string LessonTitle,
    string VisitCategoryLabelAr,
    string VisitSequenceLabelAr,
    string EvaluatorName,
    string EvaluatorRole,
    int Status,
    string StatusLabelAr,
    int? OverallPercentage,
    string? PerformanceLevelAr);

public sealed record VisitV2EvaluatorFilterDto(string UserId, string DisplayName);

public sealed record VisitV2ArchiveResultDto(
    PagedResult<VisitV2ArchiveItemDto> Page,
    IReadOnlyList<VisitV2EvaluatorFilterDto> Evaluators);

public sealed record VisitV2StandardAggregateDto(string Code, string TextAr, decimal AveragePercentage, int VisitCount);

public sealed record VisitV2DomainAggregateDto(string Code, string NameAr, decimal AveragePercentage);

public sealed record VisitV2EvaluatorWorkloadDto(
    string UserId,
    string DisplayName,
    int VisitCount,
    decimal AveragePercentage);

public sealed record VisitV2DashboardDto(
    int TotalVisits,
    decimal AverageOverallPercentage,
    decimal HighAchievementRate,
    int UniqueVisitedTeachers,
    int TotalActiveTeachers,
    IReadOnlyList<VisitV2EvaluatorWorkloadDto> Evaluators,
    IReadOnlyList<VisitV2DomainAggregateDto> Domains,
    IReadOnlyList<VisitV2StandardAggregateDto> TopStandards,
    IReadOnlyList<VisitV2StandardAggregateDto> BottomStandards);

public sealed record VisitV2CsvExportDto(byte[] Content, string FileName);

public sealed record VisitV2PdfExportDto(byte[] Content, string FileName);
