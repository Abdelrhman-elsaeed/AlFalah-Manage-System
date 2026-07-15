using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;

namespace AlFalah.Application.DTOs.Teachers;

// ─────────────────────────────────────────────────────────────────────────────
// D-71 — Teachers Management + Teacher Profile (additive; no parallel table).
// D-74 — adds Stage + Classes[] (table-backed per migration AddTeacherProfileClasses).
//
// "Teachers" are ApplicationUsers with the Instructor role + an active
// UserSchoolRole + (optionally) an InstructorProfile. All read APIs are
// school-scoped via SchoolScopeGuard; mutations reuse the existing
// /api/v1/users endpoints (User.Create / User.Update / User.Deactivate).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Filters for the teachers list endpoint.</summary>
public class TeacherListQuery : PagedQuery
{
    /// <summary>Free-text search across full name, employee no, and subject.</summary>
    public string? Search { get; set; }
}

/// <summary>One row in the teachers list page (إدارة المعلمين).</summary>
public class TeacherListItemDto
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string SchoolStage { get; set; } = string.Empty;
    public string SchoolStageLabelAr { get; set; } = string.Empty;
    public string? Subject { get; set; }
    /// <summary>D-74 — stage stored on the teacher profile (fallback = school's stage).</summary>
    public SchoolStage Stage { get; set; }
    /// <summary>D-74 — class labels the teacher teaches (e.g. ["3/1", "3/2"]).</summary>
    public List<string> Classes { get; set; } = new();
    /// <summary>Number of visits currently in scope for this caller (per D-37 for moderators).</summary>
    public int VisitCount { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Profile header data returned by GET /api/v1/teachers/{userId}.
/// Carries the 6 fields the desktop reference shows in the header card.
/// </summary>
public class TeacherProfileDto
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public SchoolStage Stage { get; set; }
    public string SchoolStageLabelAr { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public List<string> Classes { get; set; } = new();
    public int VisitCount { get; set; }
}

/// <summary>One row in the teacher's "الزيارات الصفية" table on the profile page.</summary>
public class TeacherVisitSummaryDto
{
    public int Id { get; set; }
    public DateTimeOffset VisitDate { get; set; }
    public int VisitSequence { get; set; }
    public string VisitSequenceLabelAr { get; set; } = string.Empty;
    /// <summary>Lesson / subject text (LessonTitle if set, otherwise Subject, otherwise category label).</summary>
    public string Lesson { get; set; } = string.Empty;
    public int VisitCategory { get; set; }
    public string VisitCategoryLabelAr { get; set; } = string.Empty;
    public int Status { get; set; }
    public string StatusLabelAr { get; set; } = string.Empty;
    /// <summary>Creator's full name — surfaced only to roles that can see other creators (School Manager + global admins).</summary>
    public string? CreatedByFullName { get; set; }
}

/// <summary>
/// Per-visit domain averages feeding the radar chart. Domain axes are
/// dynamic (whatever domains the snapshot carries — D-65 spirit: not
/// hardcoded to 5). Score 0..4.
/// </summary>
public class TeacherVisitProgressDto
{
    public int VisitId { get; set; }
    public DateTimeOffset VisitDate { get; set; }
    /// <summary>Sequence label for the legend (e.g. "الزيارة 1 — 2026-07-13").</summary>
    public string LegendLabel { get; set; } = string.Empty;
    /// <summary>One domain-average row per domain in the visit's snapshot rubric version.</summary>
    public List<TeacherDomainAverageDto> DomainAverages { get; set; } = new();
}

/// <summary>One domain-average row for the radar chart.</summary>
public class TeacherDomainAverageDto
{
    public string DomainCode { get; set; } = string.Empty;
    public string DomainNameAr { get; set; } = string.Empty;
    /// <summary>Domain average, 0..4 (decimal).</summary>
    public decimal? AverageScore { get; set; }
}

/// <summary>One active-rubric domain in the earliest-to-latest Approved-visit comparison.</summary>
public class TeacherDomainDeltaDto
{
    public string DomainCode { get; set; } = string.Empty;
    public string DomainNameAr { get; set; } = string.Empty;
    public decimal? FirstAverageScore { get; set; }
    public decimal? LastAverageScore { get; set; }
    /// <summary>Latest minus earliest. Null when either snapshot lacks this active domain.</summary>
    public decimal? Delta { get; set; }
}

/// <summary>Chronological comparison between the teacher's first and latest in-scope Approved visits.</summary>
public class TeacherLongitudinalComparisonDto
{
    public int FirstVisitId { get; set; }
    public DateTimeOffset FirstVisitDate { get; set; }
    public int LastVisitId { get; set; }
    public DateTimeOffset LastVisitDate { get; set; }
    public List<TeacherDomainDeltaDto> DomainDeltas { get; set; } = new();
}

/// <summary>
/// Aggregate response for GET /api/v1/teachers/{userId}/progress.
/// Carries both the dynamic axis list (so the radar can render its
/// label ring) AND the per-visit datasets (so the chart can paint one
/// colored polygon per visit, legend = visit sequence + date).
/// </summary>
public class TeacherProgressDto
{
    public string UserId { get; set; } = string.Empty;
    /// <summary>Axis labels (one entry per domain in the active rubric). Dynamic — D-65 spirit.</summary>
    public List<TeacherDomainAverageDto> AxisLabels { get; set; } = new();
    /// <summary>One series per visit. Order = visit creation order (oldest first).</summary>
    public List<TeacherVisitProgressDto> Visits { get; set; } = new();
    /// <summary>
    /// Earliest-to-latest Approved-visit comparison. Null until at least two
    /// Approved visits are visible to the caller.
    /// </summary>
    public TeacherLongitudinalComparisonDto? FirstToLastComparison { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// D-74 — Teaching info (Subject + Classes) for the auto-fill on the visit
// form + the "مادتي وفصولي" section in account settings.
//
// GET  /api/v1/account/teaching              → current user's own (SELF-ONLY)
// GET  /api/v1/teachers/{userId}/teaching    → in-scope teacher's (manager-scoped)
// PUT  /api/v1/account/teaching              → current user's own (SELF-ONLY)
// PUT  /api/v1/teachers/{userId}/teaching    → in-scope teacher's (manager-scoped)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Lightweight payload returned by the two GET endpoints — carries just the
/// auto-fill-relevant fields so the visit form can hydrate without the full
/// profile DTO.
/// </summary>
public class TeacherTeachingDto
{
    public string UserId { get; set; } = string.Empty;
    public int SchoolId { get; set; }
    /// <summary>The teacher's subject (may be null when not set yet).</summary>
    public string? Subject { get; set; }
    public SchoolStage Stage { get; set; }
    /// <summary>The class labels this teacher teaches, in the teacher's preferred order.</summary>
    public List<string> Classes { get; set; } = new();
}

/// <summary>
/// Body of PUT /teaching endpoints. Subject is free-text and capped by the
/// validator; Classes is the new (full) set — entries missing from this list
/// are soft-deleted, new entries are inserted (server-side diff).
/// </summary>
public class TeacherTeachingUpsertRequest
{
    public string? Subject { get; set; }
    public SchoolStage? Stage { get; set; }
    public List<string>? Classes { get; set; }
}
