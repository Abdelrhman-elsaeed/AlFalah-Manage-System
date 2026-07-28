using System.Globalization;
using System.Text;
using AlFalah.Application.Analysis;
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
                AverageOverallScore = x.Avg is null ? null : Math.Round(x.Avg.Value, 3),
                // Was left unset, so the exported "المستوى" column printed "—"
                // on every single row.
                LatestPerformanceLevelAr = x.Avg is null
                    ? null
                    : ComputePerformanceLevelAr(Math.Round(x.Avg.Value, 3))
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
                strengths = sList.Select(x => $"{x.DomainNameAr} ({ScoreScale.Format(x.AverageScore)})").ToList();
                var iList = System.Text.Json.JsonSerializer.Deserialize<List<VisitDomainSnapshotDto>>(lastAnalysis.ImprovementAreasJson) ?? new();
                improvements = iList.Select(x => $"{x.DomainNameAr} ({ScoreScale.Format(x.AverageScore)})").ToList();
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
                MinDomainAvg = v.Analysis!.DomainAverages.Min(d => (decimal?)d.AverageScore) ?? 4m,
                // Named so the export can say WHY the teacher is on the list.
                Domains = v.Analysis!.DomainAverages
                    .Select(d => new { d.DomainNameAr, d.AverageScore })
                    .ToList()
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
                NeedsImprovement = x.Latest.MinDomainAvg < 2.5m,
                WeakestDomainNameAr = x.Latest.Domains
                    .OrderBy(d => d.AverageScore)
                    .Select(d => d.DomainNameAr)
                    .FirstOrDefault(),
                WeakestDomainScore = x.Latest.Domains.Count == 0
                    ? null
                    : Math.Round(x.Latest.Domains.Min(d => d.AverageScore), 3)
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
                Add("متوسط الدرجة الإجمالية", Score(m.AverageOverallScore));
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
                Add("متوسط الدرجة", Score(mod.AverageOverallScore));
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
                    Add("آخر تقييم — الدرجة", Score(i.LatestEvaluation.OverallScore));
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
            sheet.Cell(i + 2, 6).Value = Score(rows[i].AverageOverallScore);
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
            sheet.Cell(i + 2, 4).Value = Score(rows[i].AverageOverallScore);
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
            sheet.Cell(i + 2, 5).Value = Score(rows[i].AverageOverallScore);
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
        // The selection reason, so the sheet is readable without knowing the rule.
        sheet.Cell(1, 5).Value = "المحور الأضعف";
        sheet.Cell(1, 6).Value = "درجة المحور الأضعف";
        sheet.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < rows.Count; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].InstructorFullName;
            sheet.Cell(i + 2, 2).Value = rows[i].ApprovedVisitsCount;
            sheet.Cell(i + 2, 3).Value = Score(rows[i].AverageOverallScore);
            sheet.Cell(i + 2, 4).Value = rows[i].LatestPerformanceLevelAr ?? "—";
            sheet.Cell(i + 2, 5).Value = rows[i].WeakestDomainNameAr ?? "—";
            sheet.Cell(i + 2, 6).Value = Score(rows[i].WeakestDomainScore);
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
            sheet.Cell(i + 2, 3).Value = Score(rows[i].AverageOverallScore);
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
            sheet.Cell(i + 2, 3).Value = Score(rows[i].OverallScore);
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
            ("متوسط آخر درجة تقدم", Percent(ip.AverageLatestProgressScore))
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
            ("متوسط الأداء", Score(d.AverageOverallScore)),
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
            ("متوسط تقدم الخطط", Percent(d.ImprovementPlans.AverageLatestProgressScore))
        },
        ModeratorDashboardDto d => new (string, string)[]
        {
            ("زيارات اليوم", d.TodaysVisitsCount.ToString()),
            ("المسودات", d.DraftVisitsCount.ToString()),
            ("بانتظار الاعتماد", d.EvaluationsPendingApprovalCount.ToString()),
            ("الزيارات المعتمدة", d.ApprovedVisitsCount.ToString()),
            ("المعلمون المقيمون", d.InstructorsEvaluatedCount.ToString()),
            ("متوسط الأداء", Score(d.AverageOverallScore)),
            ("خطط التحسين المفتوحة", d.OpenImprovementPlansCount.ToString())
        },
        InstructorDashboardDto d => new (string, string)[]
        {
            ("الزيارات المعتمدة", d.ApprovedVisitsCount.ToString()),
            ("آخر تقييم", Score(d.LatestEvaluation?.OverallScore)),
            ("خطط التحسين المفتوحة", d.OpenImprovementPlansCount.ToString()),
            ("المتابعات", d.TotalFollowUpsCount.ToString()),
            ("مشاهدات التقارير", d.ReportViewedCount.ToString())
        },
        _ => Array.Empty<(string, string)>()
    };

    private async Task<DashboardExportResult> BuildPdfExportAsync(
        string title, object dashboard, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PdfTheme.EnsureFonts();

        var generatedAt = DateTimeOffset.UtcNow;
        var scope = ExportScopeLine(dashboard);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.PageColor(PdfTheme.White);
                page.DefaultTextStyle(t => t
                    .FontFamily(PdfTheme.Font)
                    .FontSize(9.5f)
                    .FontColor(PdfTheme.Text)
                    .DirectionFromRightToLeft());

                page.Header().Element(c => ComposePdfHeader(c, title, scope, generatedAt));
                page.Content().Element(c => ComposePdfBody(c, dashboard));
                page.Footer().Element(c => ComposePdfFooter(c, title));
            });
        });

        var bytes = doc.GeneratePdf();
        var stamp = generatedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return await Task.FromResult(new DashboardExportResult
        {
            Bytes = bytes,
            ContentType = "application/pdf",
            FileName = $"dashboard-{PdfReportService.SanitizeForFilename(title).Replace(' ', '-')}-{stamp}.pdf"
        });
    }

    /// <summary>
    /// Second header line naming what the figures cover (school, moderator,
    /// teacher). A printed sheet that is passed around on paper has to say who
    /// it is about without relying on the file name.
    /// </summary>
    private static string ExportScopeLine(object dashboard) => dashboard switch
    {
        MainManagerDashboardDto d => $"جميع المدارس — {d.SchoolsCount} مدرسة ({d.ActiveSchoolsCount} نشطة)",
        SchoolManagerDashboardDto d => d.SchoolName,
        ModeratorDashboardDto d => string.IsNullOrWhiteSpace(d.SchoolName)
            ? d.ModeratorFullName
            : $"{d.SchoolName} — {d.ModeratorFullName}",
        InstructorDashboardDto d => string.IsNullOrWhiteSpace(d.SchoolName)
            ? d.InstructorFullName
            : $"{d.SchoolName} — {d.InstructorFullName}",
        _ => string.Empty
    };

    /// <summary>
    /// Brand masthead. QuestPDF lays Row children out left-to-right physically,
    /// so the Arabic title block is placed first (rendering on the visual left
    /// is wrong for RTL) — instead the title takes the relative item and is
    /// right-aligned inside it, and the timestamp is pinned to the visual left.
    /// </summary>
    private void ComposePdfHeader(IContainer container, string title, string scope, DateTimeOffset generatedAt)
    {
        container.PaddingBottom(10).Column(outer =>
        {
            outer.Item()
                .Background(PdfTheme.Brand)
                .PaddingVertical(11).PaddingHorizontal(14)
                .Row(row =>
                {
                    row.ConstantItem(150).AlignLeft().AlignMiddle().Column(stamp =>
                    {
                        stamp.Item().Text("تاريخ الإصدار")
                            .FontSize(7.5f).FontColor("#BFE3CD");
                        stamp.Item().Text(generatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                            .FontSize(9).Bold().FontColor(PdfTheme.Gold);
                    });

                    row.RelativeItem().AlignRight().Column(head =>
                    {
                        head.Item().AlignRight().Text(title)
                            .FontSize(17).Bold().FontColor(PdfTheme.White);
                        if (!string.IsNullOrWhiteSpace(scope))
                            head.Item().PaddingTop(2).AlignRight().Text(scope)
                                .FontSize(9.5f).FontColor("#D7F1DF");
                    });
                });

            outer.Item().Height(3).Background(PdfTheme.Gold);
        });
    }

    private void ComposePdfFooter(IContainer container, string title)
    {
        container.PaddingTop(8).BorderTop(PdfTheme.BorderWidth).BorderColor(PdfTheme.Border)
            .PaddingTop(5)
            .Row(row =>
            {
                row.RelativeItem().AlignLeft().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(PdfTheme.Muted));
                    t.Span("صفحة ");
                    t.CurrentPageNumber();
                    t.Span(" من ");
                    t.TotalPages();
                });

                row.RelativeItem().AlignRight().Text(title)
                    .FontSize(8).FontColor(PdfTheme.Muted);
            });
    }

    private void ComposePdfBody(IContainer container, object dashboard)
    {
        container.Column(col =>
        {
            col.Spacing(10);

            // The card-shaped sections below all have a bounded height (a fixed
            // grid of KPI tiles, one bar per visit status), so they are pinned
            // whole with ShowEntire — otherwise a card's border and its title
            // band split across the page fold. The table sections manage their
            // own pagination inside PdfTheme.TableSection.
            col.Item().ShowEntire().Element(c => ComposePdfKpiGrid(c, dashboard));

            // Fixed-height summary blocks are declared BEFORE the tables.
            //
            // With the plan-analytics strip last, it was the block that got
            // pushed over the fold, and because it is pinned whole it took a
            // sheet of its own — roughly 80 % white. A table that spills is
            // fine: PdfTheme.TableSection repeats its column header, so a
            // continuation reads as intended rather than as a layout accident.
            switch (dashboard)
            {
                case MainManagerDashboardDto mm:
                    col.Item().ShowEntire().Element(c => ComposePdfVisitsByStatus(c, mm.VisitsByStatus));
                    col.Item().ShowEntire().Element(c => ComposePdfImprovementPlans(c, mm.ImprovementPlans));
                    col.Item().Element(c => ComposePdfSchoolComparison(c, mm.SchoolComparison));
                    break;
                case SchoolManagerDashboardDto sm:
                    col.Item().ShowEntire().Element(c => ComposePdfVisitsByStatus(c, sm.VisitsByStatus));
                    col.Item().ShowEntire().Element(c => ComposePdfImprovementPlans(c, sm.ImprovementPlans));
                    col.Item().Element(c => ComposePdfSubjectPerformance(c, sm.SubjectPerformance));
                    col.Item().Element(c => ComposePdfModeratorPerformance(c, sm.ModeratorPerformance));
                    col.Item().Element(c => ComposePdfInstructorsNeedingImprovement(c, sm.InstructorsNeedingImprovement));
                    break;
                case ModeratorDashboardDto mod:
                    col.Item().ShowEntire().Element(c => ComposePdfVisitsByStatus(c, mod.VisitsByStatus));
                    col.Item().Element(c => ComposePdfTopInstructors(c, mod.TopInstructors));
                    break;
                case InstructorDashboardDto ins:
                    col.Item().ShowEntire().Element(c => ComposePdfInstructorLatest(c, ins.LatestEvaluation));
                    col.Item().Element(c => ComposePdfPerformanceTrend(c, ins.PerformanceTrend));
                    break;
            }
        });
    }

    /// <summary>
    /// Executive KPI strip — four equal cards per row so the last row of an
    /// odd-sized metric set stays aligned with the rows above it instead of
    /// stretching to fill the page.
    ///
    /// Laid out through <see cref="PdfTheme.RtlRow"/>: the first metric must
    /// land on the visual RIGHT (where an Arabic reader starts) and a partial
    /// final row must leave its gap on the LEFT. Both were the wrong way round.
    /// </summary>
    private static void ComposePdfKpiGrid(IContainer container, object dashboard)
    {
        var metrics = ExportMetrics(dashboard);

        PdfTheme.SectionCard(container, "الملخص التنفيذي", card =>
        {
            if (metrics.Count == 0)
            {
                PdfTheme.EmptyNote(card.Item());
                return;
            }

            card.Spacing(6);
            foreach (var group in metrics.Chunk(4))
            {
                var cells = group
                    .Select(metric => new Action<IContainer>(c => ComposePdfKpiCard(c, metric.Label, metric.Value)))
                    .ToList();
                card.Item().Row(row => PdfTheme.RtlRow(row, cells, slots: 4));
            }
        });
    }

    private static void ComposePdfKpiCard(IContainer container, string label, string value)
    {
        container
            .Background(PdfTheme.ZebraRow)
            .Border(PdfTheme.BorderWidth).BorderColor(PdfTheme.Border)
            .Column(card =>
            {
                card.Item().Padding(7).Column(inner =>
                {
                    inner.Item().AlignRight().Text(label)
                        .FontSize(8).FontColor(PdfTheme.Muted);
                    inner.Item().PaddingTop(3).AlignRight().Text(value)
                        .FontSize(16).Bold().FontColor(PdfTheme.BrandDark);
                });
                card.Item().Height(2.5f).Background(PdfTheme.Gold);
            });
    }

    /// <summary>
    /// Visit distribution. Two fixes over the previous version:
    ///  • the bar is filled with the SAME fraction that is printed beside it
    ///    (share of the total). It used to be filled <c>count / max</c> while
    ///    printing <c>count / total</c>, so a bar at a third sat next to "16.7%".
    ///  • the bar fills from the visual right, matching the sheet's direction.
    /// </summary>
    private void ComposePdfVisitsByStatus(IContainer container, List<VisitStatusCountDto> rows)
    {
        var total = rows.Sum(r => r.Count);

        PdfTheme.SectionCard(container, "التوزيع التشغيلي للزيارات", card =>
        {
            if (rows.Count == 0)
            {
                PdfTheme.EmptyNote(card.Item());
                return;
            }

            card.Spacing(4);
            foreach (var item in rows)
            {
                var share = total == 0 ? 0d : (double)item.Count / total;
                card.Item().Row(row =>
                {
                    // Physical L→R: [share] [bar] [count] [label]
                    // In RTL reading order that is label → count → bar → share.
                    row.ConstantItem(46).AlignCenter().AlignMiddle()
                        .Text($"{share * 100d:0.#}%").FontSize(8).FontColor(PdfTheme.Muted);

                    row.RelativeItem().PaddingHorizontal(6).AlignMiddle()
                        .Element(c => PdfTheme.ProgressBar(c, share));

                    row.ConstantItem(38).AlignCenter().AlignMiddle()
                        .Text(item.Count.ToString()).FontSize(10).Bold().FontColor(PdfTheme.BrandDark);

                    row.ConstantItem(132).AlignRight().AlignMiddle()
                        .Text(item.StatusLabelAr).FontSize(9);
                });
            }

            card.Item().PaddingTop(4).Element(c => PdfTheme.Caption(c,
                $"النسبة محسوبة من إجمالي {total} زيارة."));
        });
    }

    private void ComposePdfSchoolComparison(IContainer container, List<SchoolComparisonRowDto> rows)
    {
        PdfTheme.TableSection(container, "مقارنة المدارس", rows.Count, t =>
        {
            {
                // Physical column order is left-to-right; the Arabic name column
                // is therefore declared LAST so it lands on the RTL start edge.
                t.ColumnsDefinition(d =>
                {
                    d.ConstantColumn(58);   // moderators
                    d.ConstantColumn(58);   // teachers
                    d.ConstantColumn(86);   // level
                    d.ConstantColumn(58);   // average
                    d.ConstantColumn(58);   // approved
                    d.ConstantColumn(58);   // visits
                    d.RelativeColumn(2);    // city
                    d.RelativeColumn(3);    // school
                });

                t.Header(h =>
                {
                    PdfTheme.HeaderCell(h.Cell(), "المشرفون", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المعلمون", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المستوى", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المتوسط", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المعتمدة", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "الزيارات", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المدينة");
                    PdfTheme.HeaderCell(h.Cell(), "المدرسة");
                });

                if (rows.Count == 0)
                {
                    PdfTheme.EmptyRow(t, 8, "لا توجد مدارس مطابقة للفلاتر المحددة.");
                    return;
                }

                var ordered = rows
                    .OrderByDescending(r => r.AverageOverallScore ?? -1m)
                    .ThenBy(r => r.SchoolName)
                    .ToList();

                for (var i = 0; i < ordered.Count; i++)
                {
                    var r = ordered[i];
                    var zebra = i % 2 == 1;
                    PdfTheme.BodyCell(t.Cell(), r.ModeratorsCount.ToString(), PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), r.InstructorsCount.ToString(), PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), r.PerformanceLevelAr ?? "—", PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), Score(r.AverageOverallScore), PdfTheme.CellAlign.Center, zebra,
                        strong: true, color: PdfTheme.ScoreColor(r.AverageOverallScore));
                    PdfTheme.BodyCell(t.Cell(), r.ApprovedVisitsCount.ToString(), PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), r.VisitsCount.ToString(), PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), r.City, zebra: zebra);
                    PdfTheme.BodyCell(t.Cell(), r.SchoolName, zebra: zebra, strong: true);
                }
            }
        }, caption: ScaleNote);
    }

    private void ComposePdfSubjectPerformance(IContainer container, List<SubjectPerformanceRowDto> rows)
    {
        PdfTheme.TableSection(container, "أداء المواد الدراسية", rows.Count, t =>
        {
            {
                t.ColumnsDefinition(d =>
                {
                    d.ConstantColumn(64);   // average
                    d.ConstantColumn(64);   // approved
                    d.ConstantColumn(64);   // visits
                    d.RelativeColumn();     // subject
                });

                t.Header(h =>
                {
                    PdfTheme.HeaderCell(h.Cell(), "المتوسط", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المعتمدة", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "الزيارات", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المادة");
                });

                if (rows.Count == 0)
                {
                    PdfTheme.EmptyRow(t, 4, "لم تُسجَّل زيارات لأي مادة بعد.");
                    return;
                }

                for (var i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    var zebra = i % 2 == 1;
                    PdfTheme.BodyCell(t.Cell(), Score(r.AverageOverallScore), PdfTheme.CellAlign.Center, zebra,
                        strong: true, color: PdfTheme.ScoreColor(r.AverageOverallScore));
                    PdfTheme.BodyCell(t.Cell(), r.ApprovedVisitsCount.ToString(), PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), r.VisitsCount.ToString(), PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), r.Subject, zebra: zebra, strong: true);
                }
            }
        });
    }

    private void ComposePdfModeratorPerformance(IContainer container, List<ModeratorPerformanceRowDto> rows)
    {
        PdfTheme.TableSection(container, "أداء المشرفين", rows.Count, t =>
        {
            {
                t.ColumnsDefinition(d =>
                {
                    d.ConstantColumn(70);   // active plans
                    d.ConstantColumn(64);   // average
                    d.ConstantColumn(70);   // pending
                    d.ConstantColumn(64);   // approved
                    d.ConstantColumn(64);   // visits
                    d.RelativeColumn();     // moderator
                });

                t.Header(h =>
                {
                    PdfTheme.HeaderCell(h.Cell(), "خطط نشطة", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المتوسط", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "بانتظار الاعتماد", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المعتمدة", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "الزيارات", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المشرف");
                });

                if (rows.Count == 0)
                {
                    PdfTheme.EmptyRow(t, 6, "لا يوجد مشرفون لديهم زيارات في هذه الفترة.");
                    return;
                }

                for (var i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    var zebra = i % 2 == 1;
                    PdfTheme.BodyCell(t.Cell(), r.OpenImprovementPlansCount.ToString(), PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), Score(r.AverageOverallScore), PdfTheme.CellAlign.Center, zebra,
                        strong: true, color: PdfTheme.ScoreColor(r.AverageOverallScore));
                    PdfTheme.BodyCell(t.Cell(), r.PendingApprovalCount.ToString(), PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), r.ApprovedVisitsCount.ToString(), PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), r.VisitsCount.ToString(), PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), r.ModeratorFullName, zebra: zebra, strong: true);
                }
            }
        });
    }

    private void ComposePdfInstructorsNeedingImprovement(IContainer container, List<InstructorPerformanceRowDto> rows)
    {
        PdfTheme.TableSection(container, "معلمون يحتاجون إلى تحسين", rows.Count, t =>
        {
            {
                // The list is selected on the WEAKEST DOMAIN, so that domain and
                // its score are named. Showing only the overall average put
                // teachers rated "جيد جداً" under a "needs improvement" heading
                // with nothing on the row to explain why.
                t.ColumnsDefinition(d =>
                {
                    d.RelativeColumn(2);    // weakest domain
                    d.ConstantColumn(78);   // weakest score
                    d.ConstantColumn(84);   // overall level
                    d.ConstantColumn(70);   // overall average
                    d.ConstantColumn(64);   // approved
                    d.RelativeColumn(2);    // instructor
                });

                t.Header(h =>
                {
                    PdfTheme.HeaderCell(h.Cell(), "المحور الأضعف");
                    PdfTheme.HeaderCell(h.Cell(), "درجته", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المستوى العام", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المتوسط العام", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المعتمدة", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المعلم");
                });

                if (rows.Count == 0)
                {
                    PdfTheme.EmptyRow(t, 6, "لا يوجد معلمون بحاجة إلى تحسين — نتيجة إيجابية.");
                    return;
                }

                for (var i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    var zebra = i % 2 == 1;
                    PdfTheme.BodyCell(t.Cell(), r.WeakestDomainNameAr ?? "—", zebra: zebra);
                    PdfTheme.BodyCell(t.Cell(), Score(r.WeakestDomainScore), PdfTheme.CellAlign.Center, zebra,
                        strong: true, color: PdfTheme.ScoreColor(r.WeakestDomainScore));
                    PdfTheme.BodyCell(t.Cell(), r.LatestPerformanceLevelAr ?? "—", PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), Score(r.AverageOverallScore), PdfTheme.CellAlign.Center, zebra,
                        color: PdfTheme.Muted);
                    PdfTheme.BodyCell(t.Cell(), r.ApprovedVisitsCount.ToString(), PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), r.InstructorFullName, zebra: zebra, strong: true);
                }
            }
        }, accent: "#DC2626");
    }

    private void ComposePdfTopInstructors(IContainer container, List<InstructorPerformanceRowDto> rows)
    {
        PdfTheme.TableSection(container, "أعلى المعلمين تقييماً", rows.Count, t =>
        {
            {
                t.ColumnsDefinition(d =>
                {
                    d.ConstantColumn(100);  // level
                    d.ConstantColumn(64);   // average
                    d.ConstantColumn(64);   // approved
                    d.RelativeColumn();     // instructor
                    d.ConstantColumn(34);   // rank
                });

                t.Header(h =>
                {
                    PdfTheme.HeaderCell(h.Cell(), "المستوى", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المتوسط", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المعتمدة", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "المعلم");
                    PdfTheme.HeaderCell(h.Cell(), "م", PdfTheme.CellAlign.Center);
                });

                if (rows.Count == 0)
                {
                    PdfTheme.EmptyRow(t, 5, "لا توجد تقييمات معتمدة بعد.");
                    return;
                }

                for (var i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    var zebra = i % 2 == 1;
                    PdfTheme.BodyCell(t.Cell(), r.LatestPerformanceLevelAr ?? "—", PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), Score(r.AverageOverallScore), PdfTheme.CellAlign.Center, zebra,
                        strong: true, color: PdfTheme.ScoreColor(r.AverageOverallScore));
                    PdfTheme.BodyCell(t.Cell(), r.ApprovedVisitsCount.ToString(), PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), r.InstructorFullName, zebra: zebra, strong: true);
                    PdfTheme.BodyCell(t.Cell(), (i + 1).ToString(), PdfTheme.CellAlign.Center, zebra);
                }
            }
        });
    }

    private void ComposePdfPerformanceTrend(IContainer container, List<PerformanceTrendPointDto> rows)
    {
        PdfTheme.TableSection(container, "اتجاه الأداء", rows.Count, t =>
        {
            {
                // Proportional columns, not fixed ones. With fixed widths the
                // table occupied under half a landscape sheet and the slack had
                // to be parked in a spacer column, which read as a real column
                // nobody had labelled. Relative weights let every column grow
                // together so the table fills the card and the figures stay
                // centred under their own headers.
                t.ColumnsDefinition(d =>
                {
                    d.RelativeColumn(3);    // level
                    d.RelativeColumn(2);    // score
                    d.RelativeColumn(3);    // date
                    d.RelativeColumn(2);    // visit id
                });

                t.Header(h =>
                {
                    PdfTheme.HeaderCell(h.Cell(), "مستوى الأداء", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "الدرجة", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "تاريخ الزيارة", PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), "رقم الزيارة", PdfTheme.CellAlign.Center);
                });

                if (rows.Count == 0)
                {
                    PdfTheme.EmptyRow(t, 4, "لا توجد زيارات معتمدة لرسم الاتجاه.");
                    return;
                }

                for (var i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    var zebra = i % 2 == 1;
                    PdfTheme.BodyCell(t.Cell(), r.PerformanceLevelAr, PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), Score(r.OverallScore),
                        PdfTheme.CellAlign.Center, zebra, strong: true, color: PdfTheme.ScoreColor(r.OverallScore));
                    PdfTheme.BodyCell(t.Cell(), r.VisitDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        PdfTheme.CellAlign.Center, zebra);
                    PdfTheme.BodyCell(t.Cell(), r.VisitId.ToString(), PdfTheme.CellAlign.Center, zebra);
                }
            }
        }, caption: ScaleNote);
    }

    /// <summary>
    /// Latest approved evaluation. Rendered as ruled label/value lines rather
    /// than a two-column table: the table gave the value column the leftover
    /// landscape width, so every value printed floating in the middle of the
    /// sheet with a void between it and its own label.
    /// </summary>
    private void ComposePdfInstructorLatest(IContainer container, LatestEvaluationDto? latest)
    {
        PdfTheme.SectionCard(container, "آخر تقييم معتمد", card =>
        {
            if (latest is null)
            {
                PdfTheme.EmptyNote(card.Item(), "لا يوجد تقييم معتمد بعد.");
                return;
            }

            var scoreColor = PdfTheme.ScoreColor(latest.OverallScore);
            var lines = new (string Label, string Value, string? Color)[]
            {
                ("تاريخ الزيارة", latest.VisitDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null),
                ("نوع الزيارة", latest.VisitCategoryLabelAr, null),
                ("المشرف المُقيِّم", latest.ModeratorFullName, null),
                ("الدرجة الإجمالية", ScoreScale.FormatWithMaximum(latest.OverallScore), scoreColor),
                ("مستوى الأداء", latest.PerformanceLevelAr, scoreColor)
            };

            card.Item().Element(c => PdfTheme.DetailGrid(c, lines));
        });
    }

    /// <summary>
    /// Improvement-plan analytics. Six tiles on ONE row: as two rows of three it
    /// was tall enough to trip <c>ShowEntire</c> onto a fresh sheet, which left
    /// roughly 80 % of the final page blank on a printed report.
    /// </summary>
    private void ComposePdfImprovementPlans(IContainer container, ImprovementPlanAnalyticsDto plans)
    {
        PdfTheme.SectionCard(container, "خطط التحسين", card =>
        {
            var cells = new (string Label, string Value)[]
            {
                ("خطط نشطة", plans.TotalActive.ToString()),
                ("خطط مكتملة", plans.TotalCompleted.ToString()),
                ("خطط ملغاة", plans.TotalCancelled.ToString()),
                ("إجمالي المتابعات", plans.TotalFollowUps.ToString()),
                ("خطط لديها متابعة", plans.PlansWithAtLeastOneFollowUp.ToString()),
                ("متوسط آخر تقدم",
                    plans.AverageLatestProgressScore is null
                        ? "—"
                        : $"{plans.AverageLatestProgressScore.Value.ToString("0.#", CultureInfo.InvariantCulture)}%")
            };

            var renderers = cells
                .Select(cell => new Action<IContainer>(c => ComposePdfKpiCard(c, cell.Label, cell.Value)))
                .ToList();

            card.Item().Row(row => PdfTheme.RtlRow(row, renderers, spacing: 5f));
        });
    }

    /// <summary>D-UI-1 — the one scale note printed under every score table.</summary>
    private const string ScaleNote = "جميع الدرجات من 100.";

    /// <summary>
    /// D-UI-1 — publishes a stored 0–4 average on the single published 0–100
    /// scale, or an em dash when it was never scored. The stored value keeps its
    /// 0–4 scale because the performance-level thresholds are defined on it.
    /// </summary>
    private static string Score(decimal? value) => ScoreScale.Format(value);

    /// <summary>
    /// Plan-progress figures are already recorded 0..100, so they are published
    /// as a percentage rather than run through the rubric conversion.
    /// </summary>
    private static string Percent(decimal? value) =>
        value is null ? "—" : $"{value.Value.ToString("0.#", CultureInfo.InvariantCulture)}%";

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
