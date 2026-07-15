using System.Globalization;
using System.Text;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Dashboards;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Phase 9 — role-based dashboard aggregator.
///
/// Visibility contract (enforced HERE, never in the controller):
///  - <c>GetMainManagerDashboardAsync</c>    : global, MainManager + SuperAdmin only.
///    **NO complaint content / NO counts-with-detail** (Phase 8 rule). Aggregate
///    visit/eval metrics only.
///  - <c>GetSchoolManagerDashboardAsync</c>   : scoped to caller's ActiveSchoolId
///    via <see cref="SchoolScopeGuard"/> — cross-school impossible.
///  - <c>GetModeratorDashboardAsync</c>       : scoped to ActiveSchoolId AND
///    <c>Visit.CreatedByUserId == self</c> (D-37 pattern), with no complaint
///    count, summary, export sheet, or PDF section (D-75).
///  - <c>GetInstructorDashboardAsync</c>      : scoped to
///    <c>Visit.InstructorId == self</c> AND <c>Visit.Status == Approved</c>
///    (D-36 pattern).
///
/// Filters from the client only NARROW within the allowed scope; they can never
/// widen it. The caller-side <see cref="DashboardFilterDto"/> is silently coerced
/// (e.g. a school-scoped caller's <c>SchoolId</c> is forced to their
/// <c>ActiveSchoolId</c>).
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly AlFalahDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        AlFalahDbContext context,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard,
        ILogger<DashboardService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════════════════
    // 1) Main Manager dashboard (global, NO complaint content)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<MainManagerDashboardDto> GetMainManagerDashboardAsync(
        DashboardFilterDto filter, CancellationToken cancellationToken = default)
    {
        EnsureCallerCanReadMainManagerDashboard();

        // Build the base visit query — global (school filter is widened, narrowed
        // by client filter if provided).
        var visitQuery = _context.Visits.AsNoTracking().AsQueryable();
        if (filter.SchoolId is not null) visitQuery = visitQuery.Where(v => v.SchoolId == filter.SchoolId);
        if (filter.ModeratorUserId is not null) visitQuery = visitQuery.Where(v => v.CreatedByUserId == filter.ModeratorUserId);
        if (filter.Subject is not null && filter.Subject.Length > 0) visitQuery = visitQuery.Where(v => v.Subject == filter.Subject);
        if (filter.FromDate is not null) visitQuery = visitQuery.Where(v => v.VisitDate >= filter.FromDate);
        if (filter.ToDate is not null) visitQuery = visitQuery.Where(v => v.VisitDate <= filter.ToDate);
        if (filter.Stage is not null && int.TryParse(filter.Stage, out var stageInt))
        {
            var stage = (SchoolStage)stageInt;
            var schoolIdsForStage = await _context.Schools.AsNoTracking()
                .Where(s => s.Stage == stage && !s.IsDeleted)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);
            visitQuery = visitQuery.Where(v => schoolIdsForStage.Contains(v.SchoolId));
        }

        // Counts
        var schoolsCount = await _context.Schools.AsNoTracking().CountAsync(s => !s.IsDeleted, cancellationToken);
        var activeSchoolsCount = await _context.Schools.AsNoTracking().CountAsync(s => !s.IsDeleted && s.IsActive, cancellationToken);

        // User counts — derived from active UserSchoolRole rows (not from raw users).
        // This avoids counting a user that has been deactivated in every school.
        var activeUserIds = await _context.UserSchoolRoles.AsNoTracking()
            .Where(usr => usr.IsActive)
            .Select(usr => usr.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var activeUserSchoolRoleMap = await _context.UserSchoolRoles.AsNoTracking()
            .Where(usr => usr.IsActive)
            .GroupBy(usr => usr.UserId)
            .Select(g => new { UserId = g.Key, Role = g.OrderByDescending(x => x.CreatedAt).First().Role!.Name })
            .ToListAsync(cancellationToken);
        var schoolManagersCount = activeUserSchoolRoleMap.Count(x => x.Role == RoleNames.SchoolManager);
        var moderatorsCount = activeUserSchoolRoleMap.Count(x => x.Role == RoleNames.Moderator);
        var instructorsCount = activeUserSchoolRoleMap.Count(x => x.Role == RoleNames.Instructor);

        var visitsCount = await visitQuery.CountAsync(cancellationToken);

        // Approved-evaluations count = approved visits that have an analysis snapshot
        var approvedQuery = visitQuery.Where(v => v.Status == VisitStatus.Approved);
        var approvedVisitsCount = await approvedQuery.CountAsync(cancellationToken);

        // Visits-by-status
        var visitsByStatus = await visitQuery
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var statusCounts = visitsByStatus
            .Select(x => new VisitStatusCountDto
            {
                Status = (int)x.Status,
                StatusLabelAr = VisitStatusLabelAr((VisitStatus)x.Status),
                Count = x.Count
            })
            .OrderBy(x => x.Status)
            .ToList();

        // Average overall score across approved visits
        decimal? avgOverallScore = null;
        string? avgLevelAr = null;
        var overallScores = await approvedQuery
            .Where(v => v.Analysis != null)
            .Select(v => (decimal?)v.Analysis!.OverallScore)
            .ToListAsync(cancellationToken);
        if (overallScores.Count > 0)
        {
            avgOverallScore = Math.Round(overallScores.Average() ?? 0m, 3);
            avgLevelAr = ComputePerformanceLevelAr(avgOverallScore.Value);
        }

        // Per-school comparison — one row per school (even those with 0 visits)
        var schools = await _context.Schools.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.City,
                s.LocationDetails,
                s.SchoolLocationId,
                SchoolLocationName = s.Location != null ? s.Location.NameAr : null,
                RegionName = s.Location != null ? s.Location.RegionNameAr : null,
                Latitude = s.Location != null ? (decimal?)s.Location.Latitude : null,
                Longitude = s.Location != null ? (decimal?)s.Location.Longitude : null
            })
            .ToListAsync(cancellationToken);

        var schoolVisitGroups = await visitQuery
            .GroupBy(v => v.SchoolId)
            .Select(g => new
            {
                SchoolId = g.Key,
                Visits = g.Count(),
                Approved = g.Count(v => v.Status == VisitStatus.Approved),
                AvgScore = g.Where(v => v.Analysis != null && v.Status == VisitStatus.Approved)
                            .Average(v => (decimal?)v.Analysis!.OverallScore)
            })
            .ToListAsync(cancellationToken);

        var schoolUserCountGroups = await _context.UserSchoolRoles.AsNoTracking()
            .Where(usr => usr.IsActive)
            .GroupBy(usr => usr.SchoolId)
            .Select(g => new
            {
                SchoolId = g.Key,
                Moderators = g.Count(x => x.Role!.Name == RoleNames.Moderator),
                Instructors = g.Count(x => x.Role!.Name == RoleNames.Instructor)
            })
            .ToListAsync(cancellationToken);

        var schoolComparison = schools.Select(s =>
        {
            var vg = schoolVisitGroups.FirstOrDefault(x => x.SchoolId == s.Id);
            var ug = schoolUserCountGroups.FirstOrDefault(x => x.SchoolId == s.Id);
            return new SchoolComparisonRowDto
            {
                SchoolId = s.Id,
                SchoolName = s.Name,
                City = s.City,
                LocationDetails = s.LocationDetails,
                SchoolLocationId = s.SchoolLocationId,
                SchoolLocationName = s.SchoolLocationName,
                RegionName = s.RegionName,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                VisitsCount = vg?.Visits ?? 0,
                ApprovedVisitsCount = vg?.Approved ?? 0,
                AverageOverallScore = vg?.AvgScore is null ? null : Math.Round(vg.AvgScore.Value, 3),
                PerformanceLevelAr = vg?.AvgScore is null ? null : ComputePerformanceLevelAr(vg.AvgScore.Value),
                InstructorsCount = ug?.Instructors ?? 0,
                ModeratorsCount = ug?.Moderators ?? 0
            };
        })
        .OrderByDescending(x => x.VisitsCount)
        .ToList();

        // Improvement plan analytics — global
        var improvementPlans = await BuildImprovementPlanAnalyticsAsync(
            visitsBaseQuery: null,
            cancellationToken);

        return new MainManagerDashboardDto
        {
            SchoolsCount = schoolsCount,
            ActiveSchoolsCount = activeSchoolsCount,
            SchoolManagersCount = schoolManagersCount,
            ModeratorsCount = moderatorsCount,
            InstructorsCount = instructorsCount,
            VisitsCount = visitsCount,
            ApprovedEvaluationsCount = approvedVisitsCount,
            VisitsByStatus = statusCounts,
            AverageOverallScore = avgOverallScore,
            AveragePerformanceLevelAr = avgLevelAr,
            SchoolComparison = schoolComparison,
            ImprovementPlans = improvementPlans,
            AppliedFilters = await BuildFilterEchoAsync(filter, cancellationToken)
        };
    }

    // ════════════════════════════════════════════════════════════════════════
    // 2) School Manager dashboard (own school — ActiveSchoolId)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<SchoolManagerDashboardDto> GetSchoolManagerDashboardAsync(
        DashboardFilterDto filter, CancellationToken cancellationToken = default)
    {
        var schoolId = _scopeGuard.ResolveAllowedSchoolId(filter.SchoolId)
            ?? throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة مرتبطة بحسابك.");

        EnsureCallerCanReadSchoolManagerDashboard();

        // Force the filter to the caller's school — they can NEVER widen.
        filter.SchoolId = schoolId;

        var school = await _context.Schools.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == schoolId, cancellationToken)
            ?? throw new KeyNotFoundException("المدرسة غير موجودة.");

        // User counts inside this school
        var schoolRoles = await _context.UserSchoolRoles.AsNoTracking()
            .Where(usr => usr.IsActive && usr.SchoolId == schoolId)
            .Select(usr => usr.Role!.Name)
            .ToListAsync(cancellationToken);
        var instructorsCount = schoolRoles.Count(r => r == RoleNames.Instructor);
        var moderatorsCount = schoolRoles.Count(r => r == RoleNames.Moderator);

        // Visit query for this school
        var visitQuery = _context.Visits.AsNoTracking()
            .Where(v => v.SchoolId == schoolId);
        if (filter.Subject is not null && filter.Subject.Length > 0) visitQuery = visitQuery.Where(v => v.Subject == filter.Subject);
        if (filter.ModeratorUserId is not null) visitQuery = visitQuery.Where(v => v.CreatedByUserId == filter.ModeratorUserId);
        if (filter.FromDate is not null) visitQuery = visitQuery.Where(v => v.VisitDate >= filter.FromDate);
        if (filter.ToDate is not null) visitQuery = visitQuery.Where(v => v.VisitDate <= filter.ToDate);

        // "This month" — using the first day of the current month in UTC.
        var firstOfThisMonth = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var visitsThisMonthCount = await visitQuery
            .CountAsync(v => v.VisitDate >= firstOfThisMonth, cancellationToken);

        var evaluationsPendingApprovalCount = await visitQuery
            .CountAsync(v => v.Status == VisitStatus.PendingApproval, cancellationToken);

        // Visits-by-status
        var visitsByStatus = (await visitQuery
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken))
            .Select(x => new VisitStatusCountDto
            {
                Status = (int)x.Status,
                StatusLabelAr = VisitStatusLabelAr((VisitStatus)x.Status),
                Count = x.Count
            })
            .OrderBy(x => x.Status)
            .ToList();

        // Subject performance
        var subjectPerformance = await visitQuery
            .Where(v => v.Subject != null && v.Subject != "")
            .GroupBy(v => v.Subject!)
            .Select(g => new
            {
                Subject = g.Key,
                Visits = g.Count(),
                Approved = g.Count(v => v.Status == VisitStatus.Approved),
                AvgScore = g.Where(v => v.Analysis != null && v.Status == VisitStatus.Approved)
                            .Average(v => (decimal?)v.Analysis!.OverallScore)
            })
            .ToListAsync(cancellationToken);
        var subjectRows = subjectPerformance
            .Select(s => new SubjectPerformanceRowDto
            {
                Subject = s.Subject,
                VisitsCount = s.Visits,
                ApprovedVisitsCount = s.Approved,
                AverageOverallScore = s.AvgScore is null ? null : Math.Round(s.AvgScore.Value, 3)
            })
            .OrderByDescending(s => s.VisitsCount)
            .ToList();

        // Moderator performance
        var moderatorPerformance = await BuildModeratorPerformanceAsync(visitQuery, schoolId, cancellationToken);

        // Instructors needing improvement — pick instructors whose latest approved
        // visit has a domain average < 2.5
        var instructorsNeeding = await BuildInstructorsNeedingImprovementAsync(visitQuery, cancellationToken);

        // Improvement plan analytics (school-scoped)
        var improvementPlans = await BuildImprovementPlanAnalyticsAsync(visitQuery, cancellationToken);

        // Complaints count (NO content). Phase 8: scope is school-only here.
        var complaintsCount = await _context.Complaints.AsNoTracking()
            .CountAsync(c => c.SchoolId == schoolId && !c.IsDeleted, cancellationToken);

        return new SchoolManagerDashboardDto
        {
            SchoolId = schoolId,
            SchoolName = school.Name,
            InstructorsCount = instructorsCount,
            ModeratorsCount = moderatorsCount,
            VisitsThisMonthCount = visitsThisMonthCount,
            EvaluationsPendingApprovalCount = evaluationsPendingApprovalCount,
            ComplaintsCount = complaintsCount,
            InstructorsNeedingImprovementCount = instructorsNeeding.Count,
            VisitsByStatus = visitsByStatus,
            SubjectPerformance = subjectRows,
            ModeratorPerformance = moderatorPerformance,
            InstructorsNeedingImprovement = instructorsNeeding.Rows,
            ImprovementPlans = improvementPlans,
            AppliedFilters = await BuildFilterEchoAsync(filter, cancellationToken)
        };
    }

    // ════════════════════════════════════════════════════════════════════════
    // 3) Moderator dashboard (own work only — D-37)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<ModeratorDashboardDto> GetModeratorDashboardAsync(
        DashboardFilterDto filter, CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول.");

        var schoolId = _scopeGuard.ResolveAllowedSchoolId(filter.SchoolId)
            ?? throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة مرتبطة بحسابك.");

        EnsureCallerCanReadModeratorDashboard();

        // Force the filter to the caller's school
        filter.SchoolId = schoolId;

        var moderator = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken);
        var school = await _context.Schools.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == schoolId, cancellationToken);

        // D-37 — base query: school-scoped AND CreatedByUserId == self
        var visitQuery = _context.Visits.AsNoTracking()
            .Where(v => v.SchoolId == schoolId && v.CreatedByUserId == currentUserId);
        if (filter.Subject is not null && filter.Subject.Length > 0) visitQuery = visitQuery.Where(v => v.Subject == filter.Subject);
        if (filter.FromDate is not null) visitQuery = visitQuery.Where(v => v.VisitDate >= filter.FromDate);
        if (filter.ToDate is not null) visitQuery = visitQuery.Where(v => v.VisitDate <= filter.ToDate);

        var today = DateTime.UtcNow.Date;
        var todaysVisitsCount = await visitQuery
            .CountAsync(v => v.VisitDate.Date == today, cancellationToken);
        var draftVisitsCount = await visitQuery
            .CountAsync(v => v.Status == VisitStatus.Draft, cancellationToken);
        var openImprovementPlansCount = await _context.ImprovementPlans.AsNoTracking()
            .CountAsync(p => p.SchoolId == schoolId
                          && p.CreatedByUserId == currentUserId
                          && p.Status == PlanStatus.Active
                          && !p.IsDeleted, cancellationToken);
        var evaluationsPendingApprovalCount = await visitQuery
            .CountAsync(v => v.Status == VisitStatus.PendingApproval, cancellationToken);

        var approvedQuery = visitQuery.Where(v => v.Status == VisitStatus.Approved);
        var approvedVisitsCount = await approvedQuery.CountAsync(cancellationToken);

        var overallScores = await approvedQuery
            .Where(v => v.Analysis != null)
            .Select(v => (decimal?)v.Analysis!.OverallScore)
            .ToListAsync(cancellationToken);
        var avgOverallScore = overallScores.Count == 0
            ? (decimal?)null
            : Math.Round(overallScores.Average() ?? 0m, 3);

        // Visits by status (own)
        var visitsByStatus = (await visitQuery
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken))
            .Select(x => new VisitStatusCountDto
            {
                Status = (int)x.Status,
                StatusLabelAr = VisitStatusLabelAr((VisitStatus)x.Status),
                Count = x.Count
            })
            .OrderBy(x => x.Status)
            .ToList();

        // Instructors evaluated by this moderator (approved only)
        var instructorGroups = await approvedQuery
            .Where(v => v.Analysis != null)
            .GroupBy(v => new { v.InstructorId, v.Instructor!.FirstName, v.Instructor!.LastName })
            .Select(g => new
            {
                g.Key.InstructorId,
                g.Key.FirstName,
                g.Key.LastName,
                Approved = g.Count(),
                Avg = g.Average(x => (decimal?)x.Analysis!.OverallScore)
            })
            .ToListAsync(cancellationToken);
        var topInstructors = instructorGroups
            .Select(x => new InstructorPerformanceRowDto
            {
                InstructorUserId = x.InstructorId,
                InstructorFullName = $"{x.FirstName} {x.LastName}".Trim(),
                ApprovedVisitsCount = x.Approved,
                AverageOverallScore = x.Avg is null ? null : Math.Round(x.Avg.Value, 3)
            })
            .OrderByDescending(x => x.AverageOverallScore ?? 0m)
            .Take(10)
            .ToList();

        return new ModeratorDashboardDto
        {
            ModeratorUserId = currentUserId,
            ModeratorFullName = moderator?.FullName ?? string.Empty,
            SchoolId = schoolId,
            SchoolName = school?.Name ?? string.Empty,
            TodaysVisitsCount = todaysVisitsCount,
            DraftVisitsCount = draftVisitsCount,
            OpenImprovementPlansCount = openImprovementPlansCount,
            EvaluationsPendingApprovalCount = evaluationsPendingApprovalCount,
            AverageOverallScore = avgOverallScore,
            InstructorsEvaluatedCount = instructorGroups.Count,
            ApprovedVisitsCount = approvedVisitsCount,
            TopInstructors = topInstructors,
            VisitsByStatus = visitsByStatus,
            AppliedFilters = await BuildFilterEchoAsync(filter, cancellationToken)
        };
    }

    // ════════════════════════════════════════════════════════════════════════
    // 4) Instructor dashboard (own + approved only — D-36)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<InstructorDashboardDto> GetInstructorDashboardAsync(
        DashboardFilterDto filter, CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول.");

        EnsureCallerCanReadInstructorDashboard();

        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken)
            ?? throw new KeyNotFoundException("المستخدم غير موجود.");

        // Determine school through the shared scope guard. For an Instructor,
        // a client-supplied schoolId can only narrow to ActiveSchoolId; it can
        // never select another school. Global support callers may still choose
        // an explicit school and otherwise fall back to an active assignment.
        int schoolId;
        var allowedSchoolId = _scopeGuard.ResolveAllowedSchoolId(filter.SchoolId);
        if (allowedSchoolId is not null)
        {
            schoolId = allowedSchoolId.Value;
        }
        else
        {
            schoolId = await _context.UserSchoolRoles.AsNoTracking()
                .Where(usr => usr.UserId == currentUserId && usr.IsActive
                          && usr.Role!.Name == RoleNames.Instructor)
                .OrderBy(usr => usr.CreatedAt)
                .Select(usr => usr.SchoolId)
                .FirstOrDefaultAsync(cancellationToken);
            if (schoolId == 0)
            {
                return new InstructorDashboardDto
                {
                    InstructorUserId = currentUserId,
                    InstructorFullName = user.FullName,
                    ApprovedVisitsCount = 0
                };
            }
        }
        filter.SchoolId = schoolId;

        var school = await _context.Schools.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == schoolId, cancellationToken);

        // D-36 — Approved visits only, AND InstructorId == self
        var approvedVisits = await _context.Visits.AsNoTracking()
            .Include(v => v.Analysis).ThenInclude(a => a!.DomainAverages)
            .Include(v => v.CreatedByUser)
            .Where(v => v.InstructorId == currentUserId
                     && v.Status == VisitStatus.Approved
                     && v.SchoolId == schoolId)
            .OrderBy(v => v.VisitDate)
            .ToListAsync(cancellationToken);

        // Performance trend
        var trend = approvedVisits
            .Where(v => v.Analysis != null)
            .Select(v => new PerformanceTrendPointDto
            {
                VisitId = v.Id,
                VisitDate = v.VisitDate,
                OverallScore = v.Analysis!.OverallScore,
                PerformanceLevelAr = v.Analysis!.PerformanceLevelAr
            })
            .ToList();

        // Latest evaluation
        LatestEvaluationDto? latest = null;
        if (approvedVisits.Count > 0)
        {
            var last = approvedVisits.OrderByDescending(v => v.VisitDate).First();
            latest = new LatestEvaluationDto
            {
                VisitId = last.Id,
                VisitDate = last.VisitDate,
                VisitCategoryLabelAr = last.VisitCategory.ToArabicString(),
                ModeratorFullName = last.CreatedByUser?.FullName ?? string.Empty,
                OverallScore = last.Analysis?.OverallScore ?? 0m,
                PerformanceLevelAr = last.Analysis?.PerformanceLevelAr ?? string.Empty,
                IsApproved = true
            };
        }

        // Strengths / improvement points (from latest analysis)
        var strengths = new List<string>();
        var improvements = new List<string>();
        var lastAnalysis = approvedVisits.OrderByDescending(v => v.VisitDate).FirstOrDefault()?.Analysis;
        if (lastAnalysis != null)
        {
            try
            {
                var sList = System.Text.Json.JsonSerializer.Deserialize<List<VisitDomainSnapshotDto>>(lastAnalysis.StrengthsJson) ?? new();
                strengths = sList.Select(x => $"{x.DomainNameAr} ({x.AverageScore:0.00})").ToList();
                var iList = System.Text.Json.JsonSerializer.Deserialize<List<VisitDomainSnapshotDto>>(lastAnalysis.ImprovementAreasJson) ?? new();
                improvements = iList.Select(x => $"{x.DomainNameAr} ({x.AverageScore:0.00})").ToList();
            }
            catch { /* ignore JSON parse — fall back to empty lists */ }
        }

        // Improvement plans / follow-ups
        var openPlans = await _context.ImprovementPlans.AsNoTracking()
            .Where(p => p.InstructorId == currentUserId
                     && p.SchoolId == schoolId
                     && p.Status == PlanStatus.Active
                     && !p.IsDeleted)
            .ToListAsync(cancellationToken);
        var openPlanIds = openPlans.Select(p => p.Id).ToList();
        var followUps = await _context.PlanFollowUps.AsNoTracking()
            .Where(f => openPlanIds.Contains(f.ImprovementPlanId) && !f.IsDeleted)
            .ToListAsync(cancellationToken);

        // Report view status
        var viewLogs = await _context.ReportViewLogs.AsNoTracking()
            .Where(l => l.InstructorUserId == currentUserId && !l.IsDeleted)
            .OrderBy(l => l.ViewedAt)
            .ToListAsync(cancellationToken);

        return new InstructorDashboardDto
        {
            InstructorUserId = currentUserId,
            InstructorFullName = user.FullName,
            SchoolId = schoolId,
            SchoolName = school?.Name ?? string.Empty,
            LatestEvaluation = latest,
            PerformanceTrend = trend,
            Strengths = strengths,
            ImprovementPoints = improvements,
            OpenImprovementPlansCount = openPlans.Count,
            ImprovementPlansWithFollowUpsCount = openPlanIds.Count == 0
                ? 0
                : followUps.Select(f => f.ImprovementPlanId).Distinct().Count(),
            TotalFollowUpsCount = followUps.Count,
            LatestFollowUpsCount = 0,
            ReportViewedCount = viewLogs.Count,
            FirstReportViewedAt = viewLogs.FirstOrDefault()?.ViewedAt,
            LastReportViewedAt = viewLogs.LastOrDefault()?.ViewedAt,
            ApprovedVisitsCount = approvedVisits.Count,
            AppliedFilters = await BuildFilterEchoAsync(filter, cancellationToken)
        };
    }

    // ════════════════════════════════════════════════════════════════════════
    // 5) Export endpoints (Excel + PDF) — scope-aware
    // ════════════════════════════════════════════════════════════════════════

    public async Task<DashboardExportResult> ExportExcelAsync(
        DashboardRole role, DashboardFilterDto filter, CancellationToken cancellationToken = default)
    {
        return role switch
        {
            DashboardRole.MainManager => await BuildExcelExportAsync(
                "لوحة المدير العام", await GetMainManagerDashboardAsync(filter, cancellationToken), cancellationToken),
            DashboardRole.SchoolManager => await BuildExcelExportAsync(
                "لوحة مدير المدرسة", await GetSchoolManagerDashboardAsync(filter, cancellationToken), cancellationToken),
            DashboardRole.Moderator => await BuildExcelExportAsync(
                "لوحة المشرف", await GetModeratorDashboardAsync(filter, cancellationToken), cancellationToken),
            DashboardRole.Instructor => await BuildExcelExportAsync(
                "لوحة المعلم", await GetInstructorDashboardAsync(filter, cancellationToken), cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
    }

    public async Task<DashboardExportResult> ExportPdfAsync(
        DashboardRole role, DashboardFilterDto filter, CancellationToken cancellationToken = default)
    {
        return role switch
        {
            DashboardRole.MainManager => await BuildPdfExportAsync(
                "لوحة المدير العام", await GetMainManagerDashboardAsync(filter, cancellationToken), cancellationToken),
            DashboardRole.SchoolManager => await BuildPdfExportAsync(
                "لوحة مدير المدرسة", await GetSchoolManagerDashboardAsync(filter, cancellationToken), cancellationToken),
            DashboardRole.Moderator => await BuildPdfExportAsync(
                "لوحة المشرف", await GetModeratorDashboardAsync(filter, cancellationToken), cancellationToken),
            DashboardRole.Instructor => await BuildPdfExportAsync(
                "لوحة المعلم", await GetInstructorDashboardAsync(filter, cancellationToken), cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
    }

    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    private void EnsureCallerCanReadMainManagerDashboard()
    {
        if (!_currentUser.HasPermission(PermissionNames.DashboardMainManager))
            throw new UnauthorizedAccessException("ليس لديك صلاحية لعرض لوحة المدير العام.");
    }

    private void EnsureCallerCanReadSchoolManagerDashboard()
    {
        if (!_currentUser.HasPermission(PermissionNames.DashboardSchoolManager))
            throw new UnauthorizedAccessException("ليس لديك صلاحية لعرض لوحة مدير المدرسة.");
    }

    private void EnsureCallerCanReadModeratorDashboard()
    {
        if (!_currentUser.HasPermission(PermissionNames.DashboardModerator))
            throw new UnauthorizedAccessException("ليس لديك صلاحية لعرض لوحة المشرف.");
    }

    private void EnsureCallerCanReadInstructorDashboard()
    {
        if (!_currentUser.HasPermission(PermissionNames.DashboardInstructor))
            throw new UnauthorizedAccessException("ليس لديك صلاحية لعرض لوحة المعلم.");
    }

    private async Task<List<ModeratorPerformanceRowDto>> BuildModeratorPerformanceAsync(
        IQueryable<Visit> visitQuery, int schoolId, CancellationToken cancellationToken)
    {
        // Per-moderator breakdown
        var moderatorGroups = await visitQuery
            .GroupBy(v => new { v.CreatedByUserId, v.CreatedByUser!.FirstName, v.CreatedByUser!.LastName })
            .Select(g => new
            {
                g.Key.CreatedByUserId,
                g.Key.FirstName,
                g.Key.LastName,
                Visits = g.Count(),
                Approved = g.Count(v => v.Status == VisitStatus.Approved),
                Pending = g.Count(v => v.Status == VisitStatus.PendingApproval),
                Avg = g.Where(v => v.Analysis != null && v.Status == VisitStatus.Approved)
                       .Average(v => (decimal?)v.Analysis!.OverallScore)
            })
            .ToListAsync(cancellationToken);

        // Open improvement plans created by each moderator (per-school)
        var openPlansByModerator = await _context.ImprovementPlans.AsNoTracking()
            .Where(p => p.SchoolId == schoolId
                     && p.Status == PlanStatus.Active
                     && !p.IsDeleted)
            .GroupBy(p => p.CreatedByUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return moderatorGroups.Select(m => new ModeratorPerformanceRowDto
        {
            ModeratorUserId = m.CreatedByUserId,
            ModeratorFullName = $"{m.FirstName} {m.LastName}".Trim(),
            VisitsCount = m.Visits,
            ApprovedVisitsCount = m.Approved,
            PendingApprovalCount = m.Pending,
            AverageOverallScore = m.Avg is null ? null : Math.Round(m.Avg.Value, 3),
            OpenImprovementPlansCount = openPlansByModerator.FirstOrDefault(x => x.UserId == m.CreatedByUserId)?.Count ?? 0
        })
        .OrderByDescending(m => m.VisitsCount)
        .ToList();
    }

    private async Task<(int Count, List<InstructorPerformanceRowDto> Rows)> BuildInstructorsNeedingImprovementAsync(
        IQueryable<Visit> visitQuery, CancellationToken cancellationToken)
    {
        // For each instructor, find their LATEST approved visit's analysis.
        // "Needs improvement" = at least one domain average < 2.5.
        var approvedVisits = await visitQuery
            .Where(v => v.Status == VisitStatus.Approved && v.Analysis != null)
            .Select(v => new
            {
                v.InstructorId,
                v.Instructor!.FirstName,
                v.Instructor!.LastName,
                v.Id,
                v.VisitDate,
                OverallScore = v.Analysis!.OverallScore,
                MinDomainAvg = v.Analysis!.DomainAverages.Min(d => (decimal?)d.AverageScore) ?? 4m
            })
            .ToListAsync(cancellationToken);

        var latestByInstructor = approvedVisits
            .GroupBy(x => x.InstructorId)
            .Select(g => new
            {
                InstructorId = g.Key,
                FirstName = g.First().FirstName,
                LastName = g.First().LastName,
                Latest = g.OrderByDescending(x => x.VisitDate).First(),
                ApprovedCount = g.Count()
            })
            .ToList();

        var rows = latestByInstructor
            .Select(x => new InstructorPerformanceRowDto
            {
                InstructorUserId = x.InstructorId,
                InstructorFullName = $"{x.FirstName} {x.LastName}".Trim(),
                ApprovedVisitsCount = x.ApprovedCount,
                AverageOverallScore = Math.Round(x.Latest.OverallScore, 3),
                LatestPerformanceLevelAr = ComputePerformanceLevelAr(x.Latest.OverallScore),
                NeedsImprovement = x.Latest.MinDomainAvg < 2.5m
            })
            .Where(x => x.NeedsImprovement)
            .OrderBy(x => x.AverageOverallScore ?? 0m)
            .ToList();

        return (rows.Count, rows);
    }

    private async Task<ImprovementPlanAnalyticsDto> BuildImprovementPlanAnalyticsAsync(
        IQueryable<Visit>? visitsBaseQuery, CancellationToken cancellationToken)
    {
        var plansQuery = _context.ImprovementPlans.AsNoTracking().Where(p => !p.IsDeleted);
        if (visitsBaseQuery is not null)
        {
            var scopedVisitIds = await visitsBaseQuery.Select(v => v.Id).ToListAsync(cancellationToken);
            plansQuery = plansQuery.Where(p => scopedVisitIds.Contains(p.VisitId));
        }

        var allPlans = await plansQuery
            .Select(p => new { p.Id, p.Status })
            .ToListAsync(cancellationToken);

        var planIds = allPlans.Select(p => p.Id).ToList();
        var followUps = await _context.PlanFollowUps.AsNoTracking()
            .Where(f => planIds.Contains(f.ImprovementPlanId) && !f.IsDeleted)
            .Select(f => new { f.ImprovementPlanId, f.ProgressScore, f.FollowDate })
            .ToListAsync(cancellationToken);

        // Latest progress per plan = first scored follow-up in FollowDate DESC order
        var latestProgressByPlan = followUps
            .Where(f => f.ProgressScore.HasValue)
            .GroupBy(f => f.ImprovementPlanId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.FollowDate).First().ProgressScore!.Value);

        return new ImprovementPlanAnalyticsDto
        {
            TotalActive = allPlans.Count(p => p.Status == PlanStatus.Active),
            TotalCompleted = allPlans.Count(p => p.Status == PlanStatus.Completed),
            TotalCancelled = allPlans.Count(p => p.Status == PlanStatus.Cancelled),
            TotalFollowUps = followUps.Count,
            PlansWithAtLeastOneFollowUp = followUps.Select(f => f.ImprovementPlanId).Distinct().Count(),
            AverageLatestProgressScore = latestProgressByPlan.Count == 0
                ? (decimal?)null
                : Math.Round((decimal)latestProgressByPlan.Values.Average(), 1)
        };
    }

    private async Task<DashboardFilterEchoDto> BuildFilterEchoAsync(
        DashboardFilterDto filter, CancellationToken cancellationToken)
    {
        var echo = new DashboardFilterEchoDto
        {
            AcademicYear = filter.AcademicYear,
            Semester = filter.Semester,
            SchoolId = filter.SchoolId,
            Subject = filter.Subject,
            Stage = filter.Stage,
            ModeratorUserId = filter.ModeratorUserId
        };

        if (filter.SchoolId is not null)
        {
            var sName = await _context.Schools.AsNoTracking()
                .Where(s => s.Id == filter.SchoolId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(cancellationToken);
            echo.SchoolName = sName;
        }
        if (filter.ModeratorUserId is not null)
        {
            var mName = await _context.Users.AsNoTracking()
                .Where(u => u.Id == filter.ModeratorUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
            echo.ModeratorFullName = mName;
        }

        return echo;
    }

    // ─── Excel export ───────────────────────────────────────────────────────

    private async Task<DashboardExportResult> BuildExcelExportAsync(
        string title, object dashboard, CancellationToken cancellationToken)
    {
        using var wb = new XLWorkbook();
        BuildExcelVisualSheet(wb, title, dashboard);
        BuildExcelSummarySheet(wb, title, dashboard);

        if (dashboard is MainManagerDashboardDto mm)
        {
            BuildVisitsByStatusSheet(wb, "الزيارات حسب الحالة", mm.VisitsByStatus);
            BuildSchoolComparisonSheet(wb, mm.SchoolComparison);
            BuildImprovementPlanSheet(wb, mm.ImprovementPlans);
        }
        else if (dashboard is SchoolManagerDashboardDto sm)
        {
            BuildVisitsByStatusSheet(wb, "الزيارات حسب الحالة", sm.VisitsByStatus);
            BuildSubjectSheet(wb, sm.SubjectPerformance);
            BuildModeratorPerformanceSheet(wb, sm.ModeratorPerformance);
            BuildInstructorsNeedingImprovementSheet(wb, sm.InstructorsNeedingImprovement);
            BuildImprovementPlanSheet(wb, sm.ImprovementPlans);
        }
        else if (dashboard is ModeratorDashboardDto mod)
        {
            BuildVisitsByStatusSheet(wb, "الزيارات حسب الحالة", mod.VisitsByStatus);
            BuildTopInstructorsSheet(wb, mod.TopInstructors);
        }
        else if (dashboard is InstructorDashboardDto ins)
        {
            BuildPerformanceTrendSheet(wb, ins.PerformanceTrend);
        }

        foreach (var worksheet in wb.Worksheets.Skip(2))
            StyleExcelDataSheet(worksheet);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var bytes = ms.ToArray();

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return await Task.FromResult(new DashboardExportResult
        {
            Bytes = bytes,
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = $"dashboard-{title.Replace(' ', '-')}-{stamp}.xlsx"
        });
    }

    private static void StyleExcelDataSheet(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 1;
        var header = sheet.Range(1, 1, 1, lastColumn);
        header.Style
            .Fill.SetBackgroundColor(XLColor.FromHtml("#15603D"))
            .Font.SetFontColor(XLColor.White)
            .Font.SetBold()
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.Row(1).Height = 24;
        if (lastRow > 1)
        {
            sheet.Range(1, 1, lastRow, lastColumn).SetAutoFilter();
            for (var row = 2; row <= lastRow; row++)
            {
                if (row % 2 == 0)
                    sheet.Range(row, 1, row, lastColumn).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F9F6");
                sheet.Range(row, 1, row, lastColumn).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                sheet.Range(row, 1, row, lastColumn).Style.Border.BottomBorderColor = XLColor.FromHtml("#DDE6E0");
            }
        }
        sheet.SheetView.FreezeRows(1);
        sheet.TabColor = XLColor.FromHtml("#1E8E4E");
        sheet.Columns().AdjustToContents();
        foreach (var column in sheet.ColumnsUsed())
        {
            if (column.Width > 42) column.Width = 42;
            if (column.Width < 12) column.Width = 12;
        }
    }

    private void BuildExcelSummarySheet(XLWorkbook wb, string title, object dashboard)
    {
        var sheet = wb.AddWorksheet("ملخص");
        sheet.RightToLeft = true;
        sheet.Cell(1, 1).Value = title;
        sheet.Range(1, 1, 1, 4).Merge();
        sheet.Range(1, 1, 1, 4).Style
            .Fill.SetBackgroundColor(XLColor.FromHtml("#15603D"))
            .Font.SetFontColor(XLColor.White)
            .Font.SetBold()
            .Font.SetFontSize(16)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.Row(1).Height = 30;
        var row = 3;

        void Add(string label, string value)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = value;
            row++;
        }

        switch (dashboard)
        {
            case MainManagerDashboardDto m:
                Add("عدد المدارس", m.SchoolsCount.ToString());
                Add("المدارس النشطة", m.ActiveSchoolsCount.ToString());
                Add("عدد مديري المدارس", m.SchoolManagersCount.ToString());
                Add("عدد المشرفين", m.ModeratorsCount.ToString());
                Add("عدد المعلمين", m.InstructorsCount.ToString());
                Add("إجمالي الزيارات", m.VisitsCount.ToString());
                Add("الزيارات المعتمدة", m.ApprovedEvaluationsCount.ToString());
                Add("متوسط الدرجة الإجمالية", m.AverageOverallScore?.ToString("0.000") ?? "—");
                Add("مستوى الأداء العام", m.AveragePerformanceLevelAr ?? "—");
                Add("خطط التطوير النشطة", m.ImprovementPlans.TotalActive.ToString());
                Add("خطط التطوير المكتملة", m.ImprovementPlans.TotalCompleted.ToString());
                Add("إجمالي المتابعات", m.ImprovementPlans.TotalFollowUps.ToString());
                break;
            case SchoolManagerDashboardDto s:
                Add("المدرسة", s.SchoolName);
                Add("عدد المعلمين", s.InstructorsCount.ToString());
                Add("عدد المشرفين", s.ModeratorsCount.ToString());
                Add("زيارات هذا الشهر", s.VisitsThisMonthCount.ToString());
                Add("بانتظار الاعتماد", s.EvaluationsPendingApprovalCount.ToString());
                Add("عدد الشكاوى", s.ComplaintsCount.ToString());
                Add("معلمون يحتاجون تحسين", s.InstructorsNeedingImprovementCount.ToString());
                Add("خطط التطوير النشطة", s.ImprovementPlans.TotalActive.ToString());
                break;
            case ModeratorDashboardDto mod:
                Add("المدرسة", mod.SchoolName);
                Add("زيارات اليوم", mod.TodaysVisitsCount.ToString());
                Add("الزيارات المسودة", mod.DraftVisitsCount.ToString());
                Add("خطط التطوير المفتوحة", mod.OpenImprovementPlansCount.ToString());
                Add("بانتظار الاعتماد", mod.EvaluationsPendingApprovalCount.ToString());
                Add("زيارات معتمدة", mod.ApprovedVisitsCount.ToString());
                Add("متوسط الدرجة", mod.AverageOverallScore?.ToString("0.000") ?? "—");
                Add("عدد المعلمين المُقيَّمين", mod.InstructorsEvaluatedCount.ToString());
                break;
            case InstructorDashboardDto i:
                Add("المعلم", i.InstructorFullName);
                Add("المدرسة", i.SchoolName);
                Add("عدد الزيارات المعتمدة", i.ApprovedVisitsCount.ToString());
                Add("خطط التطوير المفتوحة", i.OpenImprovementPlansCount.ToString());
                Add("إجمالي المتابعات", i.TotalFollowUpsCount.ToString());
                Add("عدد مشاهدات التقارير", i.ReportViewedCount.ToString());
                if (i.LatestEvaluation is not null)
                {
                    Add("آخر تقييم — الدرجة", i.LatestEvaluation.OverallScore.ToString("0.000"));
                    Add("آخر تقييم — المستوى", i.LatestEvaluation.PerformanceLevelAr);
                    Add("آخر تقييم — المُقيِّم", i.LatestEvaluation.ModeratorFullName);
                }
                break;
        }

        var summaryRange = sheet.Range(3, 1, Math.Max(3, row - 1), 2);
        summaryRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        summaryRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D9E4DD");
        sheet.Range(3, 1, Math.Max(3, row - 1), 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F5EE");
        sheet.Range(3, 1, Math.Max(3, row - 1), 1).Style.Font.Bold = true;
        sheet.Range(3, 2, Math.Max(3, row - 1), 2).Style.Font.FontColor = XLColor.FromHtml("#15603D");
        sheet.Column(1).Width = 38;
        sheet.Column(2).Width = 24;
        sheet.SheetView.FreezeRows(2);
        sheet.TabColor = XLColor.FromHtml("#D4AF37");
    }

    private static void BuildExcelVisualSheet(XLWorkbook wb, string title, object dashboard)
    {
        var sheet = wb.AddWorksheet("لوحة المؤشرات");
        sheet.RightToLeft = true;
        sheet.TabColor = XLColor.FromHtml("#15603D");
        sheet.Range(1, 1, 2, 8).Merge();
        sheet.Cell(1, 1).Value = title;
        sheet.Range(1, 1, 2, 8).Style
            .Fill.SetBackgroundColor(XLColor.FromHtml("#15603D"))
            .Font.SetFontColor(XLColor.White)
            .Font.SetBold()
            .Font.SetFontSize(20)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.Row(1).Height = 26;
        sheet.Row(2).Height = 18;

        var metrics = ExportMetrics(dashboard).Take(8).ToList();
        for (var i = 0; i < metrics.Count; i++)
        {
            var startRow = 4 + (i / 4) * 3;
            var startColumn = 1 + (i % 4) * 2;
            sheet.Range(startRow, startColumn, startRow, startColumn + 1).Merge();
            sheet.Range(startRow + 1, startColumn, startRow + 1, startColumn + 1).Merge();
            sheet.Cell(startRow, startColumn).Value = metrics[i].Label;
            sheet.Cell(startRow + 1, startColumn).Value = metrics[i].Value;
            sheet.Range(startRow, startColumn, startRow + 1, startColumn + 1).Style
                .Fill.SetBackgroundColor(i % 2 == 0 ? XLColor.FromHtml("#E8F5EE") : XLColor.FromHtml("#FFF8E1"))
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                .Border.SetOutsideBorderColor(XLColor.FromHtml("#D9E4DD"))
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            sheet.Cell(startRow, startColumn).Style.Font.FontColor = XLColor.FromHtml("#64748B");
            sheet.Cell(startRow + 1, startColumn).Style.Font.Bold = true;
            sheet.Cell(startRow + 1, startColumn).Style.Font.FontSize = 16;
            sheet.Cell(startRow + 1, startColumn).Style.Font.FontColor = XLColor.FromHtml("#15603D");
        }

        var statuses = dashboard switch
        {
            MainManagerDashboardDto d => d.VisitsByStatus,
            SchoolManagerDashboardDto d => d.VisitsByStatus,
            ModeratorDashboardDto d => d.VisitsByStatus,
            _ => new List<VisitStatusCountDto>()
        };
        var chartRow = 11;
        sheet.Range(chartRow, 1, chartRow, 8).Merge();
        sheet.Cell(chartRow, 1).Value = "التوزيع التشغيلي للزيارات";
        sheet.Range(chartRow, 1, chartRow, 8).Style
            .Fill.SetBackgroundColor(XLColor.FromHtml("#F0F4F1"))
            .Font.SetBold()
            .Font.SetFontColor(XLColor.FromHtml("#15603D"));
        var maxStatus = Math.Max(1, statuses.Select(x => x.Count).DefaultIfEmpty(0).Max());
        for (var i = 0; i < statuses.Count; i++)
        {
            var row = chartRow + i + 1;
            sheet.Cell(row, 1).Value = statuses[i].StatusLabelAr;
            sheet.Cell(row, 2).Value = statuses[i].Count;
            var filled = (int)Math.Round(statuses[i].Count / (double)maxStatus * 6);
            for (var block = 0; block < 6; block++)
            {
                sheet.Cell(row, block + 3).Value = string.Empty;
                sheet.Cell(row, block + 3).Style.Fill.BackgroundColor = block < filled
                    ? XLColor.FromHtml(block < 4 ? "#1E8E4E" : "#D4AF37")
                    : XLColor.FromHtml("#EDF1EE");
            }
            sheet.Row(row).Height = 20;
        }
        sheet.Columns(1, 8).Width = 15;
        sheet.Column(1).Width = 28;
        sheet.SheetView.FreezeRows(2);
    }

    private void BuildVisitsByStatusSheet(XLWorkbook wb, string name, List<VisitStatusCountDto> rows)
    {
        var sheet = wb.AddWorksheet(name);
        sheet.RightToLeft = true;
        sheet.Cell(1, 1).Value = "الحالة";
        sheet.Cell(1, 2).Value = "العدد";
        sheet.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < rows.Count; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].StatusLabelAr;
            sheet.Cell(i + 2, 2).Value = rows[i].Count;
        }
        sheet.Columns().AdjustToContents();
    }

    private void BuildSchoolComparisonSheet(XLWorkbook wb, List<SchoolComparisonRowDto> rows)
    {
        var sheet = wb.AddWorksheet("مقارنة المدارس");
        sheet.RightToLeft = true;
        sheet.Cell(1, 1).Value = "المدرسة";
        sheet.Cell(1, 2).Value = "المدينة";
        sheet.Cell(1, 3).Value = "تفاصيل الموقع";
        sheet.Cell(1, 4).Value = "الزيارات";
        sheet.Cell(1, 5).Value = "المعتمدة";
        sheet.Cell(1, 6).Value = "متوسط الدرجة";
        sheet.Cell(1, 7).Value = "المستوى";
        sheet.Cell(1, 8).Value = "المعلمون";
        sheet.Cell(1, 9).Value = "المشرفون";
        sheet.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < rows.Count; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].SchoolName;
            sheet.Cell(i + 2, 2).Value = rows[i].City;
            sheet.Cell(i + 2, 3).Value = rows[i].LocationDetails ?? "—";
            sheet.Cell(i + 2, 4).Value = rows[i].VisitsCount;
            sheet.Cell(i + 2, 5).Value = rows[i].ApprovedVisitsCount;
            sheet.Cell(i + 2, 6).Value = rows[i].AverageOverallScore?.ToString("0.000") ?? "—";
            sheet.Cell(i + 2, 7).Value = rows[i].PerformanceLevelAr ?? "—";
            sheet.Cell(i + 2, 8).Value = rows[i].InstructorsCount;
            sheet.Cell(i + 2, 9).Value = rows[i].ModeratorsCount;
        }
        sheet.Columns().AdjustToContents();
    }

    private void BuildSubjectSheet(XLWorkbook wb, List<SubjectPerformanceRowDto> rows)
    {
        var sheet = wb.AddWorksheet("أداء المواد");
        sheet.RightToLeft = true;
        sheet.Cell(1, 1).Value = "المادة";
        sheet.Cell(1, 2).Value = "الزيارات";
        sheet.Cell(1, 3).Value = "المعتمدة";
        sheet.Cell(1, 4).Value = "متوسط الدرجة";
        sheet.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < rows.Count; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Subject;
            sheet.Cell(i + 2, 2).Value = rows[i].VisitsCount;
            sheet.Cell(i + 2, 3).Value = rows[i].ApprovedVisitsCount;
            sheet.Cell(i + 2, 4).Value = rows[i].AverageOverallScore?.ToString("0.000") ?? "—";
        }
        sheet.Columns().AdjustToContents();
    }

    private void BuildModeratorPerformanceSheet(XLWorkbook wb, List<ModeratorPerformanceRowDto> rows)
    {
        var sheet = wb.AddWorksheet("أداء المشرفين");
        sheet.RightToLeft = true;
        sheet.Cell(1, 1).Value = "المشرف";
        sheet.Cell(1, 2).Value = "الزيارات";
        sheet.Cell(1, 3).Value = "المعتمدة";
        sheet.Cell(1, 4).Value = "بانتظار الاعتماد";
        sheet.Cell(1, 5).Value = "متوسط الدرجة";
        sheet.Cell(1, 6).Value = "خطط التطوير النشطة";
        sheet.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < rows.Count; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].ModeratorFullName;
            sheet.Cell(i + 2, 2).Value = rows[i].VisitsCount;
            sheet.Cell(i + 2, 3).Value = rows[i].ApprovedVisitsCount;
            sheet.Cell(i + 2, 4).Value = rows[i].PendingApprovalCount;
            sheet.Cell(i + 2, 5).Value = rows[i].AverageOverallScore?.ToString("0.000") ?? "—";
            sheet.Cell(i + 2, 6).Value = rows[i].OpenImprovementPlansCount;
        }
        sheet.Columns().AdjustToContents();
    }

    private void BuildInstructorsNeedingImprovementSheet(XLWorkbook wb, List<InstructorPerformanceRowDto> rows)
    {
        var sheet = wb.AddWorksheet("معلمون يحتاجون تحسين");
        sheet.RightToLeft = true;
        sheet.Cell(1, 1).Value = "المعلم";
        sheet.Cell(1, 2).Value = "الزيارات المعتمدة";
        sheet.Cell(1, 3).Value = "متوسط الدرجة";
        sheet.Cell(1, 4).Value = "المستوى";
        sheet.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < rows.Count; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].InstructorFullName;
            sheet.Cell(i + 2, 2).Value = rows[i].ApprovedVisitsCount;
            sheet.Cell(i + 2, 3).Value = rows[i].AverageOverallScore?.ToString("0.000") ?? "—";
            sheet.Cell(i + 2, 4).Value = rows[i].LatestPerformanceLevelAr ?? "—";
        }
        sheet.Columns().AdjustToContents();
    }

    private void BuildTopInstructorsSheet(XLWorkbook wb, List<InstructorPerformanceRowDto> rows)
    {
        var sheet = wb.AddWorksheet("أعلى المعلمين تقييماً");
        sheet.RightToLeft = true;
        sheet.Cell(1, 1).Value = "المعلم";
        sheet.Cell(1, 2).Value = "الزيارات المعتمدة";
        sheet.Cell(1, 3).Value = "متوسط الدرجة";
        sheet.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < rows.Count; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].InstructorFullName;
            sheet.Cell(i + 2, 2).Value = rows[i].ApprovedVisitsCount;
            sheet.Cell(i + 2, 3).Value = rows[i].AverageOverallScore?.ToString("0.000") ?? "—";
        }
        sheet.Columns().AdjustToContents();
    }

    private void BuildPerformanceTrendSheet(XLWorkbook wb, List<PerformanceTrendPointDto> rows)
    {
        var sheet = wb.AddWorksheet("اتجاه الأداء");
        sheet.RightToLeft = true;
        sheet.Cell(1, 1).Value = "رقم الزيارة";
        sheet.Cell(1, 2).Value = "تاريخ الزيارة";
        sheet.Cell(1, 3).Value = "الدرجة الإجمالية";
        sheet.Cell(1, 4).Value = "المستوى";
        sheet.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < rows.Count; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].VisitId;
            sheet.Cell(i + 2, 2).Value = rows[i].VisitDate.ToString("yyyy-MM-dd");
            sheet.Cell(i + 2, 3).Value = rows[i].OverallScore.ToString("0.000");
            sheet.Cell(i + 2, 4).Value = rows[i].PerformanceLevelAr;
        }
        sheet.Columns().AdjustToContents();
    }

    private void BuildImprovementPlanSheet(XLWorkbook wb, ImprovementPlanAnalyticsDto ip)
    {
        var sheet = wb.AddWorksheet("خطط التطوير");
        sheet.RightToLeft = true;
        sheet.Cell(1, 1).Value = "المؤشر";
        sheet.Cell(1, 2).Value = "القيمة";
        sheet.Row(1).Style.Font.Bold = true;
        var rows = new (string Label, string Value)[]
        {
            ("خطط نشطة", ip.TotalActive.ToString()),
            ("خطط مكتملة", ip.TotalCompleted.ToString()),
            ("خطط ملغاة", ip.TotalCancelled.ToString()),
            ("إجمالي المتابعات", ip.TotalFollowUps.ToString()),
            ("خطط لديها متابعات", ip.PlansWithAtLeastOneFollowUp.ToString()),
            ("متوسط آخر درجة تقدم", ip.AverageLatestProgressScore?.ToString("0.0") ?? "—")
        };
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Label;
            sheet.Cell(i + 2, 2).Value = rows[i].Value;
        }
        sheet.Columns().AdjustToContents();
    }

    // ─── PDF export ────────────────────────────────────────────────────────

    private static IReadOnlyList<(string Label, string Value)> ExportMetrics(object dashboard) => dashboard switch
    {
        MainManagerDashboardDto d => new (string, string)[]
        {
            ("المدارس", d.SchoolsCount.ToString()),
            ("المدارس النشطة", d.ActiveSchoolsCount.ToString()),
            ("المعلمون", d.InstructorsCount.ToString()),
            ("المشرفون", d.ModeratorsCount.ToString()),
            ("الزيارات", d.VisitsCount.ToString()),
            ("التقييمات المعتمدة", d.ApprovedEvaluationsCount.ToString()),
            ("متوسط الأداء", d.AverageOverallScore?.ToString("0.00") ?? "—"),
            ("خطط التحسين النشطة", d.ImprovementPlans.TotalActive.ToString())
        },
        SchoolManagerDashboardDto d => new (string, string)[]
        {
            ("المعلمون", d.InstructorsCount.ToString()),
            ("المشرفون", d.ModeratorsCount.ToString()),
            ("زيارات الشهر", d.VisitsThisMonthCount.ToString()),
            ("بانتظار الاعتماد", d.EvaluationsPendingApprovalCount.ToString()),
            ("يحتاجون إلى تحسين", d.InstructorsNeedingImprovementCount.ToString()),
            ("الشكاوى", d.ComplaintsCount.ToString()),
            ("الخطط النشطة", d.ImprovementPlans.TotalActive.ToString()),
            ("متوسط تقدم الخطط", d.ImprovementPlans.AverageLatestProgressScore?.ToString("0.0") ?? "—")
        },
        ModeratorDashboardDto d => new (string, string)[]
        {
            ("زيارات اليوم", d.TodaysVisitsCount.ToString()),
            ("المسودات", d.DraftVisitsCount.ToString()),
            ("بانتظار الاعتماد", d.EvaluationsPendingApprovalCount.ToString()),
            ("الزيارات المعتمدة", d.ApprovedVisitsCount.ToString()),
            ("المعلمون المقيمون", d.InstructorsEvaluatedCount.ToString()),
            ("متوسط الأداء", d.AverageOverallScore?.ToString("0.00") ?? "—"),
            ("خطط التحسين المفتوحة", d.OpenImprovementPlansCount.ToString())
        },
        InstructorDashboardDto d => new (string, string)[]
        {
            ("الزيارات المعتمدة", d.ApprovedVisitsCount.ToString()),
            ("آخر تقييم", d.LatestEvaluation?.OverallScore.ToString("0.00") ?? "—"),
            ("خطط التحسين المفتوحة", d.OpenImprovementPlansCount.ToString()),
            ("المتابعات", d.TotalFollowUpsCount.ToString()),
            ("مشاهدات التقارير", d.ReportViewedCount.ToString())
        },
        _ => Array.Empty<(string, string)>()
    };

    private async Task<DashboardExportResult> BuildPdfExportAsync(
        string title, object dashboard, CancellationToken cancellationToken)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(22);
                page.PageColor("#F6F8F6");
                page.DefaultTextStyle(t => t.FontSize(10).DirectionFromRightToLeft());

                page.Header().Element(c => ComposePdfHeader(c, title));
                page.Content().Element(c => ComposePdfBody(c, dashboard));
                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Medium));
                    t.Span("صفحة ");
                    t.CurrentPageNumber();
                    t.Span(" من ");
                    t.TotalPages();
                });
            });
        });

        var bytes = doc.GeneratePdf();
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return await Task.FromResult(new DashboardExportResult
        {
            Bytes = bytes,
            ContentType = "application/pdf",
            FileName = $"dashboard-{title.Replace(' ', '-')}-{stamp}.pdf"
        });
    }

    private void ComposePdfHeader(IContainer container, string title)
    {
        container.Background("#15603D").PaddingVertical(12).PaddingHorizontal(14)
            .Row(row =>
            {
                row.RelativeItem().AlignRight().Text(title)
                    .FontSize(18).Bold().FontColor(Colors.White);
                row.ConstantItem(140).AlignLeft().Text(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                    .FontSize(9).FontColor("#F7E8A7");
            });
    }

    private void ComposePdfBody(IContainer container, object dashboard)
    {
        container.PaddingVertical(8).Column(col =>
        {
            col.Spacing(10);

            col.Item().Element(c => ComposePdfKpiGrid(c, dashboard));

            switch (dashboard)
            {
                case MainManagerDashboardDto mm:
                    col.Item().Element(c => ComposePdfVisitsByStatus(c, mm.VisitsByStatus));
                    col.Item().Element(c => ComposePdfSchoolComparison(c, mm.SchoolComparison));
                    break;
                case SchoolManagerDashboardDto sm:
                    col.Item().Element(c => ComposePdfVisitsByStatus(c, sm.VisitsByStatus));
                    col.Item().Element(c => ComposePdfSubjectPerformance(c, sm.SubjectPerformance));
                    col.Item().Element(c => ComposePdfModeratorPerformance(c, sm.ModeratorPerformance));
                    col.Item().Element(c => ComposePdfInstructorsNeedingImprovement(c, sm.InstructorsNeedingImprovement));
                    break;
                case ModeratorDashboardDto mod:
                    col.Item().Element(c => ComposePdfVisitsByStatus(c, mod.VisitsByStatus));
                    col.Item().Element(c => ComposePdfTopInstructors(c, mod.TopInstructors));
                    break;
                case InstructorDashboardDto ins:
                    col.Item().Element(c => ComposePdfPerformanceTrend(c, ins.PerformanceTrend));
                    col.Item().Element(c => ComposePdfInstructorLatest(c, ins.LatestEvaluation));
                    break;
            }
        });
    }

    private static void ComposePdfKpiGrid(IContainer container, object dashboard)
    {
        var metrics = ExportMetrics(dashboard);
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().AlignRight().Text("الملخص التنفيذي")
                .FontSize(12).Bold().FontColor("#15603D");
            foreach (var group in metrics.Chunk(4))
            {
                column.Item().Row(row =>
                {
                    foreach (var metric in group)
                    {
                        row.RelativeItem().PaddingHorizontal(3).Background(Colors.White)
                            .Border(1).BorderColor("#D9E4DD").Padding(8).Column(card =>
                            {
                                card.Item().AlignRight().Text(metric.Label).FontSize(8).FontColor(Colors.Grey.Medium);
                                card.Item().PaddingTop(2).AlignRight().Text(metric.Value).FontSize(15).Bold().FontColor("#15603D");
                                card.Item().PaddingTop(4).Height(3).Background("#D4AF37");
                            });
                    }
                    for (var i = group.Length; i < 4; i++) row.RelativeItem();
                });
            }
        });
    }

    private void ComposePdfSummary(IContainer container, object dashboard)
    {
        container.Background(Colors.Grey.Lighten4).Padding(8).Column(col =>
        {
            col.Spacing(2);
            switch (dashboard)
            {
                case MainManagerDashboardDto m:
                    col.Item().Text($"المدارس: {m.SchoolsCount} | النشطة: {m.ActiveSchoolsCount}");
                    col.Item().Text($"المعلمون: {m.InstructorsCount} | المشرفون: {m.ModeratorsCount} | مديرو المدارس: {m.SchoolManagersCount}");
                    col.Item().Text($"الزيارات: {m.VisitsCount} | المعتمدة: {m.ApprovedEvaluationsCount}");
                    col.Item().Text($"متوسط الدرجة: {(m.AverageOverallScore?.ToString("0.000") ?? "—")} | المستوى: {m.AveragePerformanceLevelAr ?? "—"}");
                    col.Item().Text($"خطط التطوير النشطة: {m.ImprovementPlans.TotalActive} | المتابعات: {m.ImprovementPlans.TotalFollowUps}");
                    break;
                case SchoolManagerDashboardDto s:
                    col.Item().Text($"المدرسة: {s.SchoolName}");
                    col.Item().Text($"المعلمون: {s.InstructorsCount} | المشرفون: {s.ModeratorsCount}");
                    col.Item().Text($"زيارات هذا الشهر: {s.VisitsThisMonthCount} | بانتظار الاعتماد: {s.EvaluationsPendingApprovalCount}");
                    col.Item().Text($"الشكاوى: {s.ComplaintsCount} | يحتاجون تحسين: {s.InstructorsNeedingImprovementCount}");
                    break;
                case ModeratorDashboardDto mod:
                    col.Item().Text($"المدرسة: {mod.SchoolName} | المُقيِّم: {mod.ModeratorFullName}");
                    col.Item().Text($"زيارات اليوم: {mod.TodaysVisitsCount} | المسودة: {mod.DraftVisitsCount} | بانتظار الاعتماد: {mod.EvaluationsPendingApprovalCount}");
                    col.Item().Text($"المعتمدة: {mod.ApprovedVisitsCount} | متوسط الدرجة: {(mod.AverageOverallScore?.ToString("0.000") ?? "—")}");
                    col.Item().Text($"خطط التطوير المفتوحة: {mod.OpenImprovementPlansCount}");
                    break;
                case InstructorDashboardDto i:
                    col.Item().Text($"المعلم: {i.InstructorFullName} | المدرسة: {i.SchoolName}");
                    col.Item().Text($"الزيارات المعتمدة: {i.ApprovedVisitsCount} | خطط التطوير المفتوحة: {i.OpenImprovementPlansCount}");
                    col.Item().Text($"المتابعات: {i.TotalFollowUpsCount} | مشاهدات التقارير: {i.ReportViewedCount}");
                    if (i.LatestEvaluation is not null)
                        col.Item().Text($"آخر تقييم: {i.LatestEvaluation.OverallScore:0.000} ({i.LatestEvaluation.PerformanceLevelAr}) — المُقيِّم: {i.LatestEvaluation.ModeratorFullName}");
                    break;
            }
        });
    }

    private void ComposePdfVisitsByStatus(IContainer container, List<VisitStatusCountDto> rows)
    {
        var maximum = Math.Max(1, rows.Select(row => row.Count).DefaultIfEmpty(0).Max());
        container.Column(col =>
        {
            col.Spacing(4);
            col.Item().PaddingTop(6).Text("التوزيع التشغيلي للزيارات").SemiBold().FontColor("#15603D");
            foreach (var item in rows)
            {
                col.Item().Background(Colors.White).Border(1).BorderColor("#E3E9E5")
                    .PaddingVertical(5).PaddingHorizontal(8).Row(row =>
                {
                    row.ConstantItem(150).AlignRight().Text(item.StatusLabelAr).FontSize(8);
                    row.ConstantItem(34).AlignCenter().Text(item.Count.ToString()).Bold().FontColor("#15603D");
                    row.RelativeItem().Height(9).AlignMiddle().Row(bar =>
                    {
                        bar.RelativeItem(Math.Max(0.01f, item.Count)).Background("#1E8E4E");
                        bar.RelativeItem(Math.Max(0.01f, maximum - item.Count)).Background("#E8EFEA");
                    });
                });
            }
        });
    }

    private void ComposePdfSchoolComparison(IContainer container, List<SchoolComparisonRowDto> rows)
    {
        container.Column(col =>
        {
            col.Spacing(4);
            col.Item().PaddingTop(6).Text("مقارنة المدارس").SemiBold().FontColor("#15603D");
            foreach (var item in rows.Where(row => row.AverageOverallScore.HasValue)
                         .OrderByDescending(row => row.AverageOverallScore).Take(6))
            {
                var score = Math.Clamp(item.AverageOverallScore!.Value, 0m, 4m);
                col.Item().Row(row =>
                {
                    row.ConstantItem(170).AlignRight().Text(item.SchoolName).FontSize(8);
                    row.ConstantItem(38).AlignCenter().Text(score.ToString("0.00")).Bold().FontColor("#15603D");
                    row.RelativeItem().Height(8).AlignMiddle().Row(bar =>
                    {
                        bar.RelativeItem(Math.Max(0.01f, (float)score)).Background(score >= 3m ? "#1E8E4E" : "#D4AF37");
                        bar.RelativeItem(Math.Max(0.01f, 4f - (float)score)).Background("#E8EFEA");
                    });
                });
            }
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(d =>
                {
                    d.RelativeColumn(3);
                    d.RelativeColumn(2);
                    d.ConstantColumn(60);
                    d.ConstantColumn(60);
                    d.ConstantColumn(60);
                    d.ConstantColumn(80);
                    d.ConstantColumn(60);
                    d.ConstantColumn(60);
                });
                t.Header(h =>
                {
                    h.Cell().Text("المدرسة").SemiBold();
                    h.Cell().Text("المدينة").SemiBold();
                    h.Cell().AlignLeft().Text("الزيارات").SemiBold();
                    h.Cell().AlignLeft().Text("المعتمدة").SemiBold();
                    h.Cell().AlignLeft().Text("متوسط").SemiBold();
                    h.Cell().Text("المستوى").SemiBold();
                    h.Cell().AlignLeft().Text("المعلمون").SemiBold();
                    h.Cell().AlignLeft().Text("المشرفون").SemiBold();
                });
                foreach (var r in rows)
                {
                    t.Cell().Text(r.SchoolName);
                    t.Cell().Text(r.City);
                    t.Cell().AlignLeft().Text(r.VisitsCount.ToString());
                    t.Cell().AlignLeft().Text(r.ApprovedVisitsCount.ToString());
                    t.Cell().AlignLeft().Text(r.AverageOverallScore?.ToString("0.00") ?? "—");
                    t.Cell().Text(r.PerformanceLevelAr ?? "—");
                    t.Cell().AlignLeft().Text(r.InstructorsCount.ToString());
                    t.Cell().AlignLeft().Text(r.ModeratorsCount.ToString());
                }
            });
        });
    }

    private void ComposePdfSubjectPerformance(IContainer container, List<SubjectPerformanceRowDto> rows)
    {
        container.Column(col =>
        {
            col.Spacing(4);
            col.Item().PaddingTop(6).Text("أداء المواد").SemiBold();
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(d => { d.RelativeColumn(3); d.ConstantColumn(60); d.ConstantColumn(60); d.ConstantColumn(60); });
                t.Header(h =>
                {
                    h.Cell().Text("المادة").SemiBold();
                    h.Cell().AlignLeft().Text("الزيارات").SemiBold();
                    h.Cell().AlignLeft().Text("المعتمدة").SemiBold();
                    h.Cell().AlignLeft().Text("المتوسط").SemiBold();
                });
                foreach (var r in rows)
                {
                    t.Cell().Text(r.Subject);
                    t.Cell().AlignLeft().Text(r.VisitsCount.ToString());
                    t.Cell().AlignLeft().Text(r.ApprovedVisitsCount.ToString());
                    t.Cell().AlignLeft().Text(r.AverageOverallScore?.ToString("0.00") ?? "—");
                }
            });
        });
    }

    private void ComposePdfModeratorPerformance(IContainer container, List<ModeratorPerformanceRowDto> rows)
    {
        container.Column(col =>
        {
            col.Spacing(4);
            col.Item().PaddingTop(6).Text("أداء المشرفين").SemiBold();
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(d =>
                {
                    d.RelativeColumn(3);
                    d.ConstantColumn(50);
                    d.ConstantColumn(50);
                    d.ConstantColumn(60);
                    d.ConstantColumn(60);
                    d.ConstantColumn(60);
                });
                t.Header(h =>
                {
                    h.Cell().Text("المشرف").SemiBold();
                    h.Cell().AlignLeft().Text("الزيارات").SemiBold();
                    h.Cell().AlignLeft().Text("المعتمدة").SemiBold();
                    h.Cell().AlignLeft().Text("بانتظار").SemiBold();
                    h.Cell().AlignLeft().Text("المتوسط").SemiBold();
                    h.Cell().AlignLeft().Text("خطط نشطة").SemiBold();
                });
                foreach (var r in rows)
                {
                    t.Cell().Text(r.ModeratorFullName);
                    t.Cell().AlignLeft().Text(r.VisitsCount.ToString());
                    t.Cell().AlignLeft().Text(r.ApprovedVisitsCount.ToString());
                    t.Cell().AlignLeft().Text(r.PendingApprovalCount.ToString());
                    t.Cell().AlignLeft().Text(r.AverageOverallScore?.ToString("0.00") ?? "—");
                    t.Cell().AlignLeft().Text(r.OpenImprovementPlansCount.ToString());
                }
            });
        });
    }

    private void ComposePdfInstructorsNeedingImprovement(IContainer container, List<InstructorPerformanceRowDto> rows)
    {
        container.Column(col =>
        {
            col.Spacing(4);
            col.Item().PaddingTop(6).Text("معلمون يحتاجون تحسين").SemiBold();
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(d => { d.RelativeColumn(3); d.ConstantColumn(60); d.ConstantColumn(60); d.ConstantColumn(100); });
                t.Header(h =>
                {
                    h.Cell().Text("المعلم").SemiBold();
                    h.Cell().AlignLeft().Text("المعتمدة").SemiBold();
                    h.Cell().AlignLeft().Text("المتوسط").SemiBold();
                    h.Cell().Text("المستوى").SemiBold();
                });
                foreach (var r in rows)
                {
                    t.Cell().Text(r.InstructorFullName);
                    t.Cell().AlignLeft().Text(r.ApprovedVisitsCount.ToString());
                    t.Cell().AlignLeft().Text(r.AverageOverallScore?.ToString("0.00") ?? "—");
                    t.Cell().Text(r.LatestPerformanceLevelAr ?? "—");
                }
            });
        });
    }

    private void ComposePdfTopInstructors(IContainer container, List<InstructorPerformanceRowDto> rows)
    {
        container.Column(col =>
        {
            col.Spacing(4);
            col.Item().PaddingTop(6).Text("أعلى المعلمين تقييماً").SemiBold();
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(d => { d.RelativeColumn(3); d.ConstantColumn(60); d.ConstantColumn(80); });
                t.Header(h =>
                {
                    h.Cell().Text("المعلم").SemiBold();
                    h.Cell().AlignLeft().Text("المعتمدة").SemiBold();
                    h.Cell().AlignLeft().Text("المتوسط").SemiBold();
                });
                foreach (var r in rows)
                {
                    t.Cell().Text(r.InstructorFullName);
                    t.Cell().AlignLeft().Text(r.ApprovedVisitsCount.ToString());
                    t.Cell().AlignLeft().Text(r.AverageOverallScore?.ToString("0.00") ?? "—");
                }
            });
        });
    }

    private void ComposePdfPerformanceTrend(IContainer container, List<PerformanceTrendPointDto> rows)
    {
        container.Column(col =>
        {
            col.Spacing(4);
            col.Item().PaddingTop(6).Text("اتجاه الأداء").SemiBold();
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(d => { d.ConstantColumn(60); d.ConstantColumn(100); d.ConstantColumn(80); d.ConstantColumn(100); });
                t.Header(h =>
                {
                    h.Cell().Text("رقم الزيارة").SemiBold();
                    h.Cell().Text("التاريخ").SemiBold();
                    h.Cell().AlignLeft().Text("الدرجة").SemiBold();
                    h.Cell().Text("المستوى").SemiBold();
                });
                foreach (var r in rows)
                {
                    t.Cell().Text(r.VisitId.ToString());
                    t.Cell().Text(r.VisitDate.ToString("yyyy-MM-dd"));
                    t.Cell().AlignLeft().Text(r.OverallScore.ToString("0.000"));
                    t.Cell().Text(r.PerformanceLevelAr);
                }
            });
        });
    }

    private void ComposePdfInstructorLatest(IContainer container, LatestEvaluationDto? latest)
    {
        if (latest is null)
        {
            container.PaddingTop(6).Text("لا يوجد تقييم معتمد بعد.");
            return;
        }
        container.Column(col =>
        {
            col.Spacing(4);
            col.Item().PaddingTop(6).Text("آخر تقييم معتمد").SemiBold();
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(d => { d.ConstantColumn(120); d.RelativeColumn(); });
                t.Cell().Text("التاريخ").SemiBold(); t.Cell().Text(latest.VisitDate.ToString("yyyy-MM-dd"));
                t.Cell().Text("نوع الزيارة").SemiBold(); t.Cell().Text(latest.VisitCategoryLabelAr);
                t.Cell().Text("المُقيِّم").SemiBold(); t.Cell().Text(latest.ModeratorFullName);
                t.Cell().Text("الدرجة الإجمالية").SemiBold(); t.Cell().Text(latest.OverallScore.ToString("0.000"));
                t.Cell().Text("مستوى الأداء").SemiBold(); t.Cell().Text(latest.PerformanceLevelAr);
            });
        });
    }

    // ─── Static helpers ───────────────────────────────────────────────────

    internal static string VisitStatusLabelAr(VisitStatus s) => s switch
    {
        VisitStatus.Draft => "مسودة",
        VisitStatus.Submitted => "مُرسلة",
        VisitStatus.PendingApproval => "بانتظار الاعتماد",
        VisitStatus.Approved => "معتمدة",
        VisitStatus.RejectedForChanges => "مرفوضة للتعديل",
        VisitStatus.Reopened => "معاد فتحها",
        VisitStatus.UnderReviewAfterComplaint => "قيد المراجعة بعد شكوى",
        VisitStatus.Cancelled => "ملغاة",
        _ => s.ToString()
    };

    /// <summary>Mirrors docs/09 §performance-levels verbatim (matched to the live
    /// VisitService analysis engine so a dashboard "متوسط" matches the value shown
    /// on the individual visit report).</summary>
    internal static string ComputePerformanceLevelAr(decimal overall)
    {
        if (overall >= 3.5m) return "متميز";
        if (overall >= 3.0m) return "جيد جداً";
        if (overall >= 2.5m) return "جيد";
        if (overall >= 2.0m) return "متحقق جزئياً";
        if (overall >= 1.0m) return "يحتاج تحسين";
        return "غير مشاهد";
    }

    // Local DTO used to deserialize the persisted analysis JSON (mirrors the
    // shape stored by the analysis engine in Phase 4).
    private class VisitDomainSnapshotDto
    {
        public string DomainCode { get; set; } = string.Empty;
        public string DomainNameAr { get; set; } = string.Empty;
        public decimal AverageScore { get; set; }
    }
}
