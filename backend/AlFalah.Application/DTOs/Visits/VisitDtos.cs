using AlFalah.Shared.Models;

namespace AlFalah.Application.DTOs.Visits;

// ─── Read DTOs ───────────────────────────────────────────────────────────────

/// <summary>List row returned by GET /api/v1/visits.</summary>
public class VisitListItemDto
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string InstructorId { get; set; } = string.Empty;
    public string InstructorFullName { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByFullName { get; set; } = string.Empty;
    public int RubricVersionId { get; set; }
    public int RubricVersionNumber { get; set; }
    public string VisitCategory { get; set; } = string.Empty;     // enum int as string
    public string VisitCategoryLabelAr { get; set; } = string.Empty;
    public string VisitSequence { get; set; } = string.Empty;     // enum int as string
    public string VisitSequenceLabelAr { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;            // enum int as string
    public string StatusLabelAr { get; set; } = string.Empty;
    public DateTimeOffset VisitDate { get; set; }
    public string? Subject { get; set; }
    public string? GradeClass { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public int ScoredStandardsCount { get; set; } // for list — partial drafts show scored/total
    public int TotalStandardsCount { get; set; }
}

/// <summary>A single standard's score in the visit detail response.</summary>
public class VisitScoreDto
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public int RubricStandardId { get; set; }
    public string StandardCode { get; set; } = string.Empty;
    public string StandardTextAr { get; set; } = string.Empty;
    public int RubricDomainId { get; set; }
    public string DomainCode { get; set; } = string.Empty;
    public string DomainNameAr { get; set; } = string.Empty;
    public int? Score { get; set; }
    public string? EvidenceNote { get; set; }
}

/// <summary>Per-domain average row in the analysis snapshot.</summary>
public class VisitDomainAverageDto
{
    public int Id { get; set; }
    public int RubricDomainId { get; set; }
    public string DomainCode { get; set; } = string.Empty;
    public string DomainNameAr { get; set; } = string.Empty;
    public decimal AverageScore { get; set; }
    /// <summary>Domain average converted to the 0-100 visit grading scale.</summary>
    public decimal PercentageScore { get; set; }
}

/// <summary>Analysis snapshot returned alongside a submitted visit.</summary>
public class VisitAnalysisDto
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public decimal OverallScore { get; set; }
    public decimal TotalScore { get; set; }
    public decimal MaximumScore { get; set; }
    public string PerformanceLevelAr { get; set; } = string.Empty;
    public List<VisitStrengthDto> Strengths { get; set; } = new();
    public List<VisitImprovementDto> ImprovementAreas { get; set; } = new();
    public List<VisitPriorityStandardDto> PriorityStandards { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public List<VisitDomainAverageDto> DomainAverages { get; set; } = new();
    public DateTimeOffset ComputedAt { get; set; }
}

public class VisitStrengthDto
{
    public string DomainCode { get; set; } = string.Empty;
    public string DomainNameAr { get; set; } = string.Empty;
    public decimal AverageScore { get; set; }
}

public class VisitImprovementDto
{
    public string DomainCode { get; set; } = string.Empty;
    public string DomainNameAr { get; set; } = string.Empty;
    public decimal AverageScore { get; set; }
}

public class VisitPriorityStandardDto
{
    public string DomainCode { get; set; } = string.Empty;
    public string StandardCode { get; set; } = string.Empty;
    public string StandardTextAr { get; set; } = string.Empty;
    public int Score { get; set; }
}

/// <summary>
/// Live follow-up data for an improvement plan created from this visit.
/// These rows are projected at read time because they can be added after the
/// immutable visit analysis snapshot was created.
/// </summary>
public class VisitPlanFollowUpDto
{
    public int PlanId { get; set; }
    public string? DomainNameAr { get; set; }
    public string Goal { get; set; } = string.Empty;
    public int FollowUpId { get; set; }
    public DateTimeOffset FollowDate { get; set; }
    public string ProgressNote { get; set; } = string.Empty;
    public string? EvidenceNote { get; set; }
    public int? ProgressScore { get; set; }
    public string CreatedByFullName { get; set; } = string.Empty;
}

/// <summary>Full detail returned by GET /api/v1/visits/{id}.</summary>
public class VisitDetailDto
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string InstructorId { get; set; } = string.Empty;
    public string InstructorFullName { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByFullName { get; set; } = string.Empty;
    public int RubricVersionId { get; set; }
    public int RubricVersionNumber { get; set; }
    public string VisitCategory { get; set; } = string.Empty;
    public string VisitCategoryLabelAr { get; set; } = string.Empty;
    public string VisitSequence { get; set; } = string.Empty;
    public string VisitSequenceLabelAr { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabelAr { get; set; } = string.Empty;
    public DateTimeOffset VisitDate { get; set; }
    public string? Subject { get; set; }
    public string? GradeClass { get; set; }
    public string? LessonTitle { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }

    // ─── Phase 5: approval / reopen fields ─────────────────────────────────
    public string? ApprovedByUserId { get; set; }
    public string? ApprovedByFullName { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? ReopenReason { get; set; }
    public string? ReopenedByUserId { get; set; }
    public string? ReopenedByFullName { get; set; }
    public DateTimeOffset? ReopenedAt { get; set; }

    /// <summary>
    /// Phase 4 derived flag (status != Draft). Phase 5 additionally returns
    /// <c>false</c> for School Managers when status == PendingApproval so the
    /// "Direct edit" path can be surfaced on the detail page.
    /// </summary>
    public bool IsReadOnly { get; set; }

    public List<VisitScoreDto> Scores { get; set; } = new();
    public VisitAnalysisDto? Analysis { get; set; }
    public List<VisitPlanFollowUpDto> PlanFollowUps { get; set; } = new();
}

// ─── Write DTOs ──────────────────────────────────────────────────────────────

/// <summary>
/// Request body for POST /api/v1/visits. SchoolId is taken from the JWT
/// (ActiveSchoolId) — clients cannot choose.
/// </summary>
public class CreateVisitRequestDto
{
    public string InstructorId { get; set; } = string.Empty;
    public int VisitCategory { get; set; }        // VisitCategory enum int
    public int VisitSequence { get; set; }        // VisitSequence enum int
    public DateTimeOffset VisitDate { get; set; }
    public string? Subject { get; set; }
    public string? GradeClass { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public int PresentCount { get; set; }
    public int? AbsentCount { get; set; }
    public string? Notes { get; set; }
    /// <summary>Optional initial scores (partial). Validated: each score ∈ [0,4] or null.</summary>
    public List<VisitScoreInputDto>? Scores { get; set; }
}

/// <summary>
/// Request body for PUT /api/v1/visits/{id}. Draft visits only — submitted visits are read-only.
/// SchoolId / InstructorId / RubricVersionId are immutable after creation.
/// </summary>
public class UpdateVisitRequestDto
{
    public int VisitCategory { get; set; }
    public int VisitSequence { get; set; }
    public DateTimeOffset VisitDate { get; set; }
    public string? Subject { get; set; }
    public string? GradeClass { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public int PresentCount { get; set; }
    public int? AbsentCount { get; set; }
    public string? Notes { get; set; }
    /// <summary>All snapshotted scores are upserted (a null Score means "not yet scored").</summary>
    public List<VisitScoreInputDto> Scores { get; set; } = new();
}

/// <summary>One score entry inside create/update requests.</summary>
public class VisitScoreInputDto
{
    public int RubricStandardId { get; set; }
    public int? Score { get; set; }
    public string? EvidenceNote { get; set; }
}

// ─── Filter / Query ──────────────────────────────────────────────────────────

/// <summary>Query parameters for GET /api/v1/visits (list, paged).</summary>
public class VisitListQuery : PagedQuery
{
    public int? Status { get; set; }              // VisitStatus enum int
    public string? InstructorId { get; set; }
    public int? VisitCategory { get; set; }       // VisitCategory enum int
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
}

// ─── Phase 5: Approval workflow DTOs ─────────────────────────────────────────

/// <summary>
/// Result returned from the instructor-facing "open the report" endpoint.
/// Carries the visit metadata (without the creator's name etc. that the
/// instructor is not authorized to see), the snapshotted <see cref="VisitScoreDto"/> rows,
/// and the <see cref="VisitAnalysisDto"/> snapshot.
/// </summary>
public class InstructorReportDto
{
    public int VisitId { get; set; }
    public string InstructorId { get; set; } = string.Empty;
    public string InstructorFullName { get; set; } = string.Empty;
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public int RubricVersionId { get; set; }
    public int RubricVersionNumber { get; set; }
    public string VisitCategory { get; set; } = string.Empty;
    public string VisitCategoryLabelAr { get; set; } = string.Empty;
    public string VisitSequence { get; set; } = string.Empty;
    public string VisitSequenceLabelAr { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabelAr { get; set; } = string.Empty;
    public DateTimeOffset VisitDate { get; set; }
    public string? Subject { get; set; }
    public string? GradeClass { get; set; }
    public string? LessonTitle { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedByFullName { get; set; }
    public List<VisitScoreDto> Scores { get; set; } = new();
    public VisitAnalysisDto? Analysis { get; set; }
    public List<VisitPlanFollowUpDto> PlanFollowUps { get; set; } = new();
}

/// <summary>
/// Aggregated report-view status for the manager / moderator detail view.
/// Surfaced on the visit detail as "تمت المشاهدة بتاريخ ..." or "لم تتم المشاهدة".
/// </summary>
public class ReportViewStatusDto
{
    public int VisitId { get; set; }
    public bool HasBeenViewed { get; set; }
    public DateTimeOffset? FirstViewedAt { get; set; }
    public DateTimeOffset? LastViewedAt { get; set; }
    public int ViewCount { get; set; }
}

/// <summary>Request body for POST /api/v1/visits/{id}/reject — reason is REQUIRED.</summary>
public class RejectVisitRequestDto
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Request body for POST /api/v1/visits/{id}/reopen — reason is REQUIRED.</summary>
public class ReopenVisitRequestDto
{
    public string Reason { get; set; } = string.Empty;
}
