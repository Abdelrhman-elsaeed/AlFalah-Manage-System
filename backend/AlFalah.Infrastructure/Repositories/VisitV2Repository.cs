using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class VisitV2Repository(AlFalahDbContext db) : IVisitV2Repository
{
    public async Task AddReportViewAsync(int visitId, string instructorUserId, CancellationToken cancellationToken = default)
    {
        await db.ReportViewLogs.AddAsync(new ReportViewLog
        {
            VisitId = visitId,
            InstructorUserId = instructorUserId,
            ViewedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<VisitV2PdfAssetSources> GetPdfAssetSourcesAsync(int visitId, CancellationToken cancellationToken = default)
    {
        var visit = await db.Visits.AsNoTracking()
            .Where(v => v.Id == visitId && v.ExperienceVersion == ExperienceVersion.PrototypeV2)
            .Select(v => new
            {
                v.InstructorId,
                EvaluatorId = v.CreatedByUserId,
                ManagerId = v.ApprovedByUserId,
                SchoolName = v.School.Name,
                SchoolLogo = v.School.LogoUrl,
                Header = v.School.ReportSettings == null ? null : v.School.ReportSettings.ReportHeaderText,
                Footer = v.School.ReportSettings == null ? null : v.School.ReportSettings.ReportFooterText,
                Logo = v.School.ReportSettings == null ? null : v.School.ReportSettings.LogoUrl,
                Color = v.School.ReportSettings == null ? null : v.School.ReportSettings.PrimaryColor,
                ShowEvaluator = v.School.ReportSettings == null || v.School.ReportSettings.ShowModeratorSignature,
                ShowManager = v.School.ReportSettings == null || v.School.ReportSettings.ShowManagerSignature
            })
            .SingleAsync(cancellationToken);

        var userIds = new[] { visit.InstructorId, visit.EvaluatorId, visit.ManagerId }
            .Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().Distinct().ToArray();
        var signatures = await db.UserSignatures.AsNoTracking()
            .Where(s => userIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, s => !string.IsNullOrWhiteSpace(s.SignatureImageUrl)
                ? s.SignatureImageUrl : s.SignatureDrawnData, cancellationToken);

        return new VisitV2PdfAssetSources(
            string.IsNullOrWhiteSpace(visit.Header) ? visit.SchoolName : visit.Header.Trim(),
            visit.Footer?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(visit.Color) ? "#0F7132" : visit.Color,
            !string.IsNullOrWhiteSpace(visit.Logo) ? visit.Logo : visit.SchoolLogo,
            signatures.GetValueOrDefault(visit.InstructorId),
            signatures.GetValueOrDefault(visit.EvaluatorId),
            visit.ManagerId is null ? null : signatures.GetValueOrDefault(visit.ManagerId),
            visit.ShowEvaluator,
            visit.ShowManager);
    }

    public Task<RubricVersion?> GetRubricAsync(CancellationToken cancellationToken = default) =>
        db.RubricVersions
            .AsNoTracking()
            .Include(v => v.Domains.OrderBy(d => d.SortOrder))
                .ThenInclude(d => d.Standards.OrderBy(s => s.SortOrder))
                    .ThenInclude(s => s.Indicators.OrderBy(i => i.SortOrder))
            .SingleOrDefaultAsync(v => v.VersionNumber == 2, cancellationToken);

    public Task<ApplicationUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

    public Task<bool> IsActiveInstructorAsync(string userId, int schoolId, CancellationToken cancellationToken = default) =>
        db.UserSchoolRoles.AnyAsync(
            x => x.UserId == userId && x.SchoolId == schoolId && x.IsActive &&
                 x.Role.Name == RoleNames.Instructor,
            cancellationToken);

    public async Task<string> GetEvaluatorRoleAsync(string userId, int schoolId, CancellationToken cancellationToken = default)
    {
        var role = await db.UserSchoolRoles.AsNoTracking()
            .Where(x => x.UserId == userId && x.SchoolId == schoolId && x.IsActive)
            .Select(x => x.Role.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return RoleLabel(role);
    }

    public async Task<Visit?> GetAsync(int id, bool tracking, CancellationToken cancellationToken = default)
    {
        var query = db.Visits
            .Include(v => v.School)
            .Include(v => v.Instructor)
            .Include(v => v.CreatedByUser)
            .Include(v => v.RubricVersion)
            .Include(v => v.Scores)
                .ThenInclude(s => s.RubricStandard)
                    .ThenInclude(s => s.Domain)
            .Include(v => v.Scores)
                .ThenInclude(s => s.RubricStandard)
                    .ThenInclude(s => s.Indicators)
            .Include(v => v.Scores)
                .ThenInclude(s => s.ObservedIndicators)
            .Include(v => v.Analysis)
                .ThenInclude(a => a!.DomainAverages)
            .Include(v => v.TreatmentSnapshots)
            .AsSplitQuery()
            .Where(v => v.Id == id && v.ExperienceVersion == ExperienceVersion.PrototypeV2);

        return tracking
            ? await query.SingleOrDefaultAsync(cancellationToken)
            : await query.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Visit visit, CancellationToken cancellationToken = default) =>
        await db.Visits.AddAsync(visit, cancellationToken);

    public async Task<Dictionary<int, List<RubricIndicator>>> GetIndicatorsByStandardAsync(
        int rubricVersionId,
        IReadOnlyCollection<int> standardIds,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.RubricIndicators.AsNoTracking()
            .Where(i => standardIds.Contains(i.RubricStandardId) &&
                        i.Standard.Domain.RubricVersionId == rubricVersionId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(cancellationToken);
        return rows.GroupBy(i => i.RubricStandardId).ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task<PagedResult<VisitV2ArchiveItemDto>> ListAsync(
        VisitV2ArchiveQuery request,
        int? schoolId,
        string? creatorUserId,
        string? instructorUserId,
        bool approvedOnly,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyScopeAndFilters(db.Visits.AsNoTracking(), request, schoolId, creatorUserId)
            .Where(v => instructorUserId == null || v.InstructorId == instructorUserId)
            .Where(v => !approvedOnly || v.Status == VisitStatus.Approved);

        var total = await query.CountAsync(cancellationToken);
        var raw = await query.OrderByDescending(v => v.VisitDate).ThenByDescending(v => v.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(v => new
            {
                v.Id,
                v.VisitDate,
                v.ClassroomPeriod,
                InstructorName = v.Instructor.FirstName + " " + v.Instructor.LastName,
                EmployeeNumber = db.InstructorProfiles
                    .Where(p => p.UserId == v.InstructorId && p.SchoolId == v.SchoolId)
                    .Select(p => p.EmployeeNumber).FirstOrDefault(),
                v.Subject,
                v.GradeClass,
                v.LessonTitle,
                v.VisitCategory,
                v.VisitSequence,
                EvaluatorName = v.EvaluatorNameSnapshot ?? v.CreatedByUser.FirstName + " " + v.CreatedByUser.LastName,
                EvaluatorRole = v.EvaluatorRoleSnapshot ?? string.Empty,
                v.Status,
                OverallPercentage = v.Analysis == null ? null : v.Analysis.OverallPercentage,
                PerformanceLevelAr = v.Analysis == null ? null : v.Analysis.PerformanceLevelAr
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<VisitV2ArchiveItemDto>
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total,
            Items = raw.Select(v => new VisitV2ArchiveItemDto(
                v.Id, v.VisitDate, v.ClassroomPeriod ?? 0, v.InstructorName.Trim(), v.EmployeeNumber,
                v.Subject ?? string.Empty, v.GradeClass ?? string.Empty, v.LessonTitle ?? string.Empty,
                v.VisitCategory.ToArabicString(), v.VisitSequence.ToArabicString(), v.EvaluatorName.Trim(),
                v.EvaluatorRole, (int)v.Status, StatusLabel(v.Status), v.OverallPercentage,
                v.PerformanceLevelAr)).ToList()
        };
    }

    public async Task<IReadOnlyList<VisitV2EvaluatorFilterDto>> ListEvaluatorsAsync(
        int? schoolId,
        string? creatorUserId,
        CancellationToken cancellationToken = default) =>
        await db.Visits.AsNoTracking()
            .Where(v => v.ExperienceVersion == ExperienceVersion.PrototypeV2)
            .Where(v => schoolId == null || v.SchoolId == schoolId)
            .Where(v => creatorUserId == null || v.CreatedByUserId == creatorUserId)
            .GroupBy(v => new
            {
                v.CreatedByUserId,
                Name = v.EvaluatorNameSnapshot ?? v.CreatedByUser.FirstName + " " + v.CreatedByUser.LastName
            })
            .OrderBy(g => g.Key.Name)
            .Select(g => new VisitV2EvaluatorFilterDto(g.Key.CreatedByUserId, g.Key.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Visit>> ListForExportAsync(
        VisitV2ArchiveQuery request,
        int? schoolId,
        string? creatorUserId,
        CancellationToken cancellationToken = default) =>
        await ApplyScopeAndFilters(db.Visits.AsNoTracking(), request, schoolId, creatorUserId)
            .Include(v => v.Instructor)
            .Include(v => v.CreatedByUser)
            .Include(v => v.Analysis)
                .ThenInclude(a => a!.DomainAverages)
            .OrderByDescending(v => v.VisitDate)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public Task<int> CountActiveInstructorsAsync(int? schoolId, CancellationToken cancellationToken = default) =>
        db.UserSchoolRoles.AsNoTracking()
            .Where(x => x.IsActive && x.Role.Name == RoleNames.Instructor)
            .Where(x => schoolId == null || x.SchoolId == schoolId)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, string?>> GetEmployeeNumbersAsync(
        IReadOnlyCollection<string> instructorIds,
        CancellationToken cancellationToken = default) =>
        await db.InstructorProfiles.AsNoTracking()
            .Where(p => instructorIds.Contains(p.UserId))
            .GroupBy(p => p.UserId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(p => p.EmployeeNumber).FirstOrDefault(), cancellationToken);

    public async Task<VisitV2DashboardDto> GetDashboardAsync(
        int? schoolId,
        string? creatorUserId,
        int totalActiveTeachers,
        CancellationToken cancellationToken = default)
    {
        var visits = db.Visits.AsNoTracking()
            .Where(v => v.ExperienceVersion == ExperienceVersion.PrototypeV2 && v.Status == VisitStatus.Approved)
            .Where(v => schoolId == null || v.SchoolId == schoolId)
            .Where(v => creatorUserId == null || v.CreatedByUserId == creatorUserId);

        var totals = await visits.GroupBy(_ => 1).Select(g => new
        {
            Count = g.Count(),
            Average = g.Average(v => (double?)(v.Analysis!.OverallPercentage ?? 0)) ?? 0,
            High = g.Count(v => (v.Analysis!.OverallPercentage ?? 0) >= 65),
            Teachers = g.Select(v => v.InstructorId).Distinct().Count()
        }).SingleOrDefaultAsync(cancellationToken);

        var evaluatorAggregates = await visits
            // Group columns with their native collations. Coalescing the Arabic
            // snapshot with the Identity user id inside SQL causes a collation
            // conflict on existing SQL Server databases.
            .GroupBy(v => v.CreatedByUserId)
            .Select(g => new
            {
                UserId = g.Key,
                DisplayName = g.Max(v => v.EvaluatorNameSnapshot),
                Count = g.Count(),
                Average = g.Average(v => (double?)(v.Analysis!.OverallPercentage ?? 0)) ?? 0
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        var domainAggregates = await db.VisitDomainAverages.AsNoTracking()
            .Where(d => d.PercentageScore != null &&
                        d.VisitAnalysis.Visit.ExperienceVersion == ExperienceVersion.PrototypeV2 &&
                        d.VisitAnalysis.Visit.Status == VisitStatus.Approved)
            .Where(d => schoolId == null || d.VisitAnalysis.Visit.SchoolId == schoolId)
            .Where(d => creatorUserId == null || d.VisitAnalysis.Visit.CreatedByUserId == creatorUserId)
            .GroupBy(d => new { d.DomainCode, d.DomainNameAr })
            .Select(g => new
            {
                Code = g.Key.DomainCode,
                NameAr = g.Key.DomainNameAr,
                Average = g.Average(x => (double)x.PercentageScore!.Value)
            })
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        var standardAggregates = await db.VisitScores.AsNoTracking()
            .Where(s => s.Visit.ExperienceVersion == ExperienceVersion.PrototypeV2 &&
                        s.Visit.Status == VisitStatus.Approved && s.Score != null)
            .Where(s => schoolId == null || s.Visit.SchoolId == schoolId)
            .Where(s => creatorUserId == null || s.Visit.CreatedByUserId == creatorUserId)
            .GroupBy(s => new { s.RubricStandard.Code, s.RubricStandard.TextAr })
            .Select(g => new
            {
                Code = g.Key.Code,
                TextAr = g.Key.TextAr,
                Average = g.Average(x => (double)x.Score!.Value / 4d * 100d),
                VisitCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Keep grouping and averaging in SQL. Only the small aggregate result sets
        // are materialized before rounding because the DTO projection's Math.Round
        // overload is not translated reliably by relational providers.
        var domainRows = domainAggregates
            .Select(x => new VisitV2DomainAggregateDto(
                x.Code, x.NameAr, (decimal)Math.Round(x.Average, 1)))
            .ToList();
        var standards = standardAggregates
            .Select(x => new VisitV2StandardAggregateDto(
                x.Code, x.TextAr, (decimal)Math.Round(x.Average, 1), x.VisitCount))
            .ToList();

        var count = totals?.Count ?? 0;
        return new VisitV2DashboardDto(
            count,
            (decimal)Math.Round(totals?.Average ?? 0, 1),
            count == 0 ? 0 : Math.Round((decimal)(totals?.High ?? 0) / count * 100m, 1),
            totals?.Teachers ?? 0,
            totalActiveTeachers,
            evaluatorAggregates.Select(x => new VisitV2EvaluatorWorkloadDto(
                x.UserId, x.DisplayName ?? x.UserId, x.Count, (decimal)Math.Round(x.Average, 1))).ToList(),
            domainRows,
            standards.OrderByDescending(x => x.AveragePercentage).ThenBy(x => x.Code).Take(3).ToList(),
            standards.OrderBy(x => x.AveragePercentage).ThenBy(x => x.Code).Take(3).ToList());
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await db.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsRelational())
        {
            await action(cancellationToken);
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static IQueryable<Visit> ApplyScopeAndFilters(
        IQueryable<Visit> query,
        VisitV2ArchiveQuery request,
        int? schoolId,
        string? creatorUserId)
    {
        query = query.Where(v => v.ExperienceVersion == ExperienceVersion.PrototypeV2)
            .Where(v => schoolId == null || v.SchoolId == schoolId)
            .Where(v => creatorUserId == null || v.CreatedByUserId == creatorUserId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(v =>
                (v.Instructor.FirstName + " " + v.Instructor.LastName).Contains(search) ||
                (v.Subject ?? "").Contains(search) || (v.GradeClass ?? "").Contains(search) ||
                (v.EvaluatorNameSnapshot ?? v.CreatedByUser.FirstName + " " + v.CreatedByUser.LastName).Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(request.EvaluatorUserId))
            query = query.Where(v => v.CreatedByUserId == request.EvaluatorUserId);
        if (request.Status.HasValue)
            query = query.Where(v => (int)v.Status == request.Status.Value);
        if (request.VisitCategory.HasValue)
            query = query.Where(v => (int)v.VisitCategory == request.VisitCategory.Value);
        if (request.FromDate.HasValue)
            query = query.Where(v => v.VisitDate >= request.FromDate.Value);
        if (request.ToDate.HasValue)
            query = query.Where(v => v.VisitDate <= request.ToDate.Value);
        return query;
    }

    private static string RoleLabel(string? role) => role switch
    {
        RoleNames.SchoolManager => "مدير المدرسة",
        RoleNames.Moderator => "مشرف",
        RoleNames.MainManager => "مدير المدارس العام",
        RoleNames.SuperAdmin => "مدير النظام",
        _ => role ?? string.Empty
    };

    private static string StatusLabel(VisitStatus status) => status switch
    {
        VisitStatus.Draft => "مسودة",
        VisitStatus.PendingApproval => "بانتظار الاعتماد",
        VisitStatus.Approved => "معتمدة",
        VisitStatus.RejectedForChanges => "مرفوضة للتعديل",
        VisitStatus.Reopened => "مُعاد فتحها",
        VisitStatus.UnderReviewAfterComplaint => "قيد المراجعة بعد شكوى",
        VisitStatus.Cancelled => "ملغاة",
        _ => "مُرسلة"
    };
}
