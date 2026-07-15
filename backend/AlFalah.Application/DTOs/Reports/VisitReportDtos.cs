using AlFalah.Shared.Models;

namespace AlFalah.Application.DTOs.Reports;

// ─── Phase 6 / Stage 1: report PDF aggregate DTO ─────────────────────────────
//
// Assembled by the visit/report service from the visit's immutable snapshot
// (VisitAnalysis) and the related data (Visit, VisitScores, School, Users).
// Consumed ONLY by IPdfReportService to build the document — never sent as
// a JSON HTTP response (the controller returns application/pdf bytes, not
// ApiResponse<T>).
//
// All Arabic fields are populated from the persisted snapshot to guarantee
// the PDF matches the stored analysis byte-for-byte (D-26 carry-over).
// No client-supplied data is mixed in.
//
// Phase 6 / Stage 2: extended with branding (logo bytes, color, header/footer
// text), real signatures (image bytes + display name + date), and QR payload.
// Image bytes are populated by the report service after it loads the
// SchoolReportSettings + UserSignature rows; if an asset is missing or
// unreachable the corresponding bytes are null and the PDF service renders
// the safe fallback (initials box, blank signature line, no QR).

/// <summary>
/// Read-only aggregate passed to the PDF builder. Every field already has its
/// final Arabic label per docs/09 / docs/11 — the builder does NOT consult
/// i18n or enums.
/// </summary>
public class VisitReportDto
{
    // ─── Header / school context ─────────────────────────────────────────
    public int VisitId { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;

    // ─── Teacher / moderator / approver ──────────────────────────────────
    public string InstructorFullName { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? GradeClass { get; set; }
    public string? LessonTitle { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public string? Notes { get; set; }
    public string CreatedByFullName { get; set; } = string.Empty;
    public string? ApprovedByFullName { get; set; }

    // ─── Visit meta ──────────────────────────────────────────────────────
    public string VisitCategoryLabelAr { get; set; } = string.Empty;
    public string VisitSequenceLabelAr { get; set; } = string.Empty;
    public DateTimeOffset VisitDate { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    // ─── Snapshot (verbatim from docs/09) ────────────────────────────────
    public int RubricVersionNumber { get; set; }

    public List<ReportDomainBlockDto> Domains { get; set; } = new();

    // Snapshot fields — exactly the persisted JSON, no recompute.
    public decimal OverallScore { get; set; }
    public string PerformanceLevelAr { get; set; } = string.Empty;
    public List<ReportStrengthDto> Strengths { get; set; } = new();
    public List<ReportImprovementDto> ImprovementAreas { get; set; } = new();
    public List<ReportPriorityStandardDto> PriorityStandards { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public List<ReportPlanFollowUpDto> PlanFollowUps { get; set; } = new();

    // ─── Phase 6 / Stage 2: branding + signatures + QR ───────────────────

    /// <summary>Resolved school initials (e.g. "م.ف") for the logo fallback.</summary>
    public string SchoolInitials { get; set; } = string.Empty;

    /// <summary>Raw image bytes for the school logo (PNG/JPG/GIF). Null when missing.</summary>
    public byte[]? SchoolLogoBytes { get; set; }

    /// <summary>Image format hint ("png", "jpeg", "gif"). Drives the QuestPDF image binding.</summary>
    public string SchoolLogoFormat { get; set; } = "png";

    /// <summary>Header text (resolved — falls back to school name when missing).</summary>
    public string HeaderText { get; set; } = string.Empty;

    /// <summary>Footer text (resolved — falls back to a generated timestamp when missing).</summary>
    public string FooterText { get; set; } = string.Empty;

    /// <summary>Validated brand color (#RRGGBB). Falls back to the Saudi brand green when missing.</summary>
    public string PrimaryColor { get; set; } = "#0F7132";

    /// <summary>Whether to render the moderator's signature box (from SchoolReportSettings.ShowModeratorSignature).</summary>
    public bool ShowModeratorSignature { get; set; } = true;

    /// <summary>Whether to render the manager's signature box (from SchoolReportSettings.ShowManagerSignature).</summary>
    public bool ShowManagerSignature { get; set; } = true;

    /// <summary>Whether to render the QR code (from SchoolReportSettings.ShowQrCode).</summary>
    public bool ShowQrCode { get; set; }

    /// <summary>Raw image bytes for the moderator/creator signature. Null when missing.</summary>
    public byte[]? ModeratorSignatureBytes { get; set; }
    public string ModeratorSignatureFormat { get; set; } = "png";

    /// <summary>Raw image bytes for the evaluated instructor's signature. Null renders a blank line.</summary>
    public byte[]? InstructorSignatureBytes { get; set; }
    public string InstructorSignatureFormat { get; set; } = "png";

    /// <summary>Raw image bytes for the manager/approver signature. Null when missing.</summary>
    public byte[]? ManagerSignatureBytes { get; set; }
    public string ManagerSignatureFormat { get; set; } = "png";

    /// <summary>
    /// Compact verification payload encoded into the QR (informational only):
    /// <c>visit-{id}|school-{schoolId}|ref-{shortHash}</c> — NO scores, NO PII.
    /// Public verification page is deferred (out of Stage-2 scope).
    /// </summary>
    public string QrPayload { get; set; } = string.Empty;

    /// <summary>
    /// Legacy status flag populated by the visit service. Non-approved reports
    /// render normally; the PDF builder uses this only to avoid displaying an
    /// inaccurate "Approved" badge.
    /// </summary>
    public bool IsDraftWatermark { get; set; }
}

/// <summary>One rubric domain's header + standards with scores (for the PDF).</summary>
public class ReportDomainBlockDto
{
    public string DomainCode { get; set; } = string.Empty;
    public string DomainNameAr { get; set; } = string.Empty;
    public decimal AverageScore { get; set; }
    public List<ReportStandardScoreDto> Standards { get; set; } = new();
}

/// <summary>One standard's score row inside a domain block.</summary>
public class ReportStandardScoreDto
{
    public string StandardCode { get; set; } = string.Empty;
    public string StandardTextAr { get; set; } = string.Empty;
    public int? Score { get; set; }
    public string ScoreLabelAr { get; set; } = string.Empty;
    public string? EvidenceNote { get; set; }
}

public class ReportStrengthDto
{
    public string DomainCode { get; set; } = string.Empty;
    public string DomainNameAr { get; set; } = string.Empty;
    public decimal AverageScore { get; set; }
}

public class ReportImprovementDto
{
    public string DomainCode { get; set; } = string.Empty;
    public string DomainNameAr { get; set; } = string.Empty;
    public decimal AverageScore { get; set; }
}

public class ReportPriorityStandardDto
{
    public string DomainCode { get; set; } = string.Empty;
    public string StandardCode { get; set; } = string.Empty;
    public string StandardTextAr { get; set; } = string.Empty;
    public int Score { get; set; }
}

public class ReportPlanFollowUpDto
{
    public string? DomainNameAr { get; set; }
    public string Goal { get; set; } = string.Empty;
    public DateTimeOffset FollowDate { get; set; }
    public string ProgressNote { get; set; } = string.Empty;
    public string? EvidenceNote { get; set; }
    public int? ProgressScore { get; set; }
    public string CreatedByFullName { get; set; } = string.Empty;
}
