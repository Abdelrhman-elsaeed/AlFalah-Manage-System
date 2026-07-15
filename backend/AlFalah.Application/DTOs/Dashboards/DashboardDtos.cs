namespace AlFalah.Application.DTOs.Dashboards;

// ─────────────────────────────────────────────────────────────────────────────
// Phase 9 — Dashboards & Exports
//
// All DTOs are server-shaped (the Arabic labels live in code, mirroring the
// pattern from the visit/report DTOs — the frontend mirrors them via i18n).
//
// Visibility scope is enforced inside the SERVICE: a Main Manager dashboard
// NEVER carries any complaint content; a School Manager dashboard is scoped to
// ActiveSchoolId; a Moderator dashboard is scoped to CreatedByUserId == self;
// an Instructor dashboard is scoped to InstructorId == self + Approved only.
// ─────────────────────────────────────────────────────────────────────────────

// ─── Common shape: visits-by-status counter row ──────────────────────────────

/// <summary>One row of the "visits by status" breakdown.</summary>
public class VisitStatusCountDto
{
    /// <summary>VisitStatus int (1..8) — serialised as the int value.</summary>
    public int Status { get; set; }
    public string StatusLabelAr { get; set; } = string.Empty;
    public int Count { get; set; }
}

// ─── Common shape: per-school row for the Main-Manager comparison chart ─────

public class SchoolComparisonRowDto
{
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? LocationDetails { get; set; }
    public int? SchoolLocationId { get; set; }
    public string? SchoolLocationName { get; set; }
    public string? RegionName { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int VisitsCount { get; set; }
    public int ApprovedVisitsCount { get; set; }
    /// <summary>Average overall score across the school's approved visits (null if none).</summary>
    public decimal? AverageOverallScore { get; set; }
    /// <summary>Numeric performance level label (e.g. "متميز"), or null if no approved visits.</summary>
    public string? PerformanceLevelAr { get; set; }
    public int InstructorsCount { get; set; }
    public int ModeratorsCount { get; set; }
}

// ─── Common shape: per-subject performance row (for the School Manager
//    "subject performance" widget and the Main-Manager school comparison) ──

public class SubjectPerformanceRowDto
{
    public string Subject { get; set; } = string.Empty;
    public int VisitsCount { get; set; }
    public int ApprovedVisitsCount { get; set; }
    public decimal? AverageOverallScore { get; set; }
}

// ─── Common shape: per-moderator performance row ────────────────────────────

public class ModeratorPerformanceRowDto
{
    public string ModeratorUserId { get; set; } = string.Empty;
    public string ModeratorFullName { get; set; } = string.Empty;
    public int VisitsCount { get; set; }
    public int ApprovedVisitsCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public decimal? AverageOverallScore { get; set; }
    /// <summary>Active (non-Completed, non-soft-deleted) improvement plans the moderator created.</summary>
    public int OpenImprovementPlansCount { get; set; }
}

// ─── Common shape: per-instructor performance row ──────────────────────────

public class InstructorPerformanceRowDto
{
    public string InstructorUserId { get; set; } = string.Empty;
    public string InstructorFullName { get; set; } = string.Empty;
    public int ApprovedVisitsCount { get; set; }
    public decimal? AverageOverallScore { get; set; }
    public string? LatestPerformanceLevelAr { get; set; }
    public int OpenImprovementPlansCount { get; set; }
    /// <summary>True if the instructor's latest approved visit has any domain average &lt; 2.5 (i.e. needs improvement).</summary>
    public bool NeedsImprovement { get; set; }
}

// ─── Common shape: aggregated improvement-plan analytics ───────────────────

public class ImprovementPlanAnalyticsDto
{
    public int TotalActive { get; set; }
    public int TotalCompleted { get; set; }
    public int TotalCancelled { get; set; }
    public int TotalFollowUps { get; set; }
    public int PlansWithAtLeastOneFollowUp { get; set; }
    /// <summary>Average latest progress score (0..100) across all active plans, null if none.</summary>
    public decimal? AverageLatestProgressScore { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// 1) Main Manager Dashboard (global, NO complaint content)
// ════════════════════════════════════════════════════════════════════════════

public class MainManagerDashboardDto
{
    // Counts (global)
    public int SchoolsCount { get; set; }
    public int ActiveSchoolsCount { get; set; }
    public int SchoolManagersCount { get; set; }
    public int ModeratorsCount { get; set; }
    public int InstructorsCount { get; set; }
    public int VisitsCount { get; set; }
    public int ApprovedEvaluationsCount { get; set; }

    // Visits by status
    public List<VisitStatusCountDto> VisitsByStatus { get; set; } = new();

    // Aggregated approved-evaluation metrics
    public decimal? AverageOverallScore { get; set; }
    public string? AveragePerformanceLevelAr { get; set; }

    // Per-school comparison
    public List<SchoolComparisonRowDto> SchoolComparison { get; set; } = new();

    // Improvement plan analytics (global)
    public ImprovementPlanAnalyticsDto ImprovementPlans { get; set; } = new();

    // Echo of the filter so the frontend can reflect the applied scope
    public DashboardFilterEchoDto AppliedFilters { get; set; } = new();
}

// ════════════════════════════════════════════════════════════════════════════
// 2) School Manager Dashboard (own school — ActiveSchoolId scope)
// ════════════════════════════════════════════════════════════════════════════

public class SchoolManagerDashboardDto
{
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;

    // Counts
    public int InstructorsCount { get; set; }
    public int ModeratorsCount { get; set; }
    public int VisitsThisMonthCount { get; set; }
    public int InstructorsNeedingImprovementCount { get; set; }
    public int EvaluationsPendingApprovalCount { get; set; }
    /// <summary>Phase 8 — counts only (NO content / NO subjects / NO bodies).</summary>
    public int ComplaintsCount { get; set; }

    // Visit-status breakdown for the school
    public List<VisitStatusCountDto> VisitsByStatus { get; set; } = new();

    // Subject performance
    public List<SubjectPerformanceRowDto> SubjectPerformance { get; set; } = new();

    // Moderator performance
    public List<ModeratorPerformanceRowDto> ModeratorPerformance { get; set; } = new();

    // Instructors needing improvement (latest-approved visit has a domain &lt; 2.5)
    public List<InstructorPerformanceRowDto> InstructorsNeedingImprovement { get; set; } = new();

    public ImprovementPlanAnalyticsDto ImprovementPlans { get; set; } = new();
    public DashboardFilterEchoDto AppliedFilters { get; set; } = new();
}

// ════════════════════════════════════════════════════════════════════════════
// 3) Moderator Dashboard (own work only — D-37)
// ════════════════════════════════════════════════════════════════════════════

public class ModeratorDashboardDto
{
    public string ModeratorUserId { get; set; } = string.Empty;
    public string ModeratorFullName { get; set; } = string.Empty;
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;

    // Counts (own visits only)
    public int TodaysVisitsCount { get; set; }
    public int DraftVisitsCount { get; set; }
    public int OpenImprovementPlansCount { get; set; }
    public int EvaluationsPendingApprovalCount { get; set; }

    // Performance of instructors HE evaluated
    public decimal? AverageOverallScore { get; set; }
    public int InstructorsEvaluatedCount { get; set; }
    public int ApprovedVisitsCount { get; set; }
    public List<InstructorPerformanceRowDto> TopInstructors { get; set; } = new();

    // Visits by status (own)
    public List<VisitStatusCountDto> VisitsByStatus { get; set; } = new();

    public DashboardFilterEchoDto AppliedFilters { get; set; } = new();
}

// ════════════════════════════════════════════════════════════════════════════
// 4) Instructor Dashboard (own account, own approved visits only — D-36)
// ════════════════════════════════════════════════════════════════════════════

public class InstructorDashboardDto
{
    public string InstructorUserId { get; set; } = string.Empty;
    public string InstructorFullName { get; set; } = string.Empty;
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;

    /// <summary>Latest approved visit (or null if none).</summary>
    public LatestEvaluationDto? LatestEvaluation { get; set; }

    /// <summary>Performance trend across all of the instructor's approved visits (chronological).</summary>
    public List<PerformanceTrendPointDto> PerformanceTrend { get; set; } = new();

    public List<string> Strengths { get; set; } = new();
    public List<string> ImprovementPoints { get; set; } = new();

    public int OpenImprovementPlansCount { get; set; }
    public int ImprovementPlansWithFollowUpsCount { get; set; }
    public int TotalFollowUpsCount { get; set; }
    public int LatestFollowUpsCount { get; set; }

    public int ReportViewedCount { get; set; }
    public DateTimeOffset? FirstReportViewedAt { get; set; }
    public DateTimeOffset? LastReportViewedAt { get; set; }

    public int ApprovedVisitsCount { get; set; }

    public DashboardFilterEchoDto AppliedFilters { get; set; } = new();
}

public class LatestEvaluationDto
{
    public int VisitId { get; set; }
    public DateTimeOffset VisitDate { get; set; }
    public string VisitCategoryLabelAr { get; set; } = string.Empty;
    public string ModeratorFullName { get; set; } = string.Empty;
    public decimal OverallScore { get; set; }
    public string PerformanceLevelAr { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
}

public class PerformanceTrendPointDto
{
    public int VisitId { get; set; }
    public DateTimeOffset VisitDate { get; set; }
    public decimal OverallScore { get; set; }
    public string PerformanceLevelAr { get; set; } = string.Empty;
}

// ════════════════════════════════════════════════════════════════════════════
// Shared filter echo
// ════════════════════════════════════════════════════════════════════════════

public class DashboardFilterEchoDto
{
    public int? AcademicYear { get; set; }
    public string? Semester { get; set; }
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public string? Subject { get; set; }
    public string? Stage { get; set; }
    public string? ModeratorUserId { get; set; }
    public string? ModeratorFullName { get; set; }
}
