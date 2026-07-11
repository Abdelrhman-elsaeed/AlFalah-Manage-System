using System.Text.Json;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Reports;
using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Phase 4 visits + scoring + analysis service.
/// Implements <see cref="IVisitService"/>.
/// All rules are enforced server-side: school-scoping, snapshot-on-create,
/// the 25/25 submit gate, and the docs/09 analysis engine.
/// </summary>
public class VisitService : IVisitService
{
    private readonly AlFalahDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly ILogger<VisitService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ImageAssetLoader _imageLoader;

    public VisitService(
        AlFalahDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard,
        ILogger<VisitService> logger,
        IHttpContextAccessor httpContextAccessor,
        ImageAssetLoader imageLoader)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _imageLoader = imageLoader;
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    public async Task<VisitDetailDto> CreateAsync(CreateVisitRequestDto request, CancellationToken cancellationToken = default)
    {
        // School comes from the JWT — global admins may pass null to leave it free.
        var schoolId = await ResolveTargetSchoolIdAsync(request.InstructorId, cancellationToken);

        await _scopeGuard.EnsureCanMutateSchoolAsync(schoolId, cancellationToken);

        // Instructor must belong to this school (active UserSchoolRole with role=Instructor).
        await EnsureInstructorInSchoolAsync(request.InstructorId, schoolId, cancellationToken);

        // Snapshot the currently active rubric version.
        var activeRubric = await _context.RubricVersions
            .AsNoTracking()
            .Include(v => v.Domains).ThenInclude(d => d.Standards)
            .FirstOrDefaultAsync(v => v.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("لا يوجد إصدار نشط من أداة التقييم.");

        var standards = activeRubric.Domains
            .SelectMany(d => d.Standards.Select(s => new { Domain = d, Standard = s }))
            .OrderBy(x => x.Domain.SortOrder).ThenBy(x => x.Standard.SortOrder)
            .ToList();

        if (standards.Count != 25)
            throw new InvalidOperationException(
                $"عدد المعايير في الإصدار النشط ({standards.Count}) لا يساوي 25. يرجى مراجعة الإصدار.");

        var now = DateTimeOffset.UtcNow;

        var visit = new Visit
        {
            SchoolId = schoolId,
            InstructorId = request.InstructorId,
            CreatedByUserId = _currentUser.UserId ?? throw new InvalidOperationException("لم يتم العثور على المستخدم الحالي."),
            RubricVersionId = activeRubric.Id,
            VisitCategory = (VisitCategory)request.VisitCategory,
            VisitSequence = (VisitSequence)request.VisitSequence,
            Status = VisitStatus.Draft,
            VisitDate = request.VisitDate,
            Subject = request.Subject,
            GradeClass = request.GradeClass,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Pre-generate the 25 score rows (all null) — keeps the "exactly 25 rows per visit" invariant.
        foreach (var s in standards)
        {
            visit.Scores.Add(new VisitScore
            {
                RubricStandardId = s.Standard.Id,
                Score = null,
                EvidenceNote = null,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // Apply any optional initial scores from the request (validated to be 0..4 or null).
        if (request.Scores != null && request.Scores.Count > 0)
        {
            var stdIds = standards.Select(x => x.Standard.Id).ToHashSet();
            foreach (var input in request.Scores)
            {
                if (!stdIds.Contains(input.RubricStandardId))
                    throw new InvalidOperationException(
                        $"المعيار رقم {input.RubricStandardId} غير موجود في الإصدار النشط.");

                if (input.Score is < 0 or > 4)
                    throw new InvalidOperationException("درجة المعيار يجب أن تكون بين 0 و 4.");

                var row = visit.Scores.First(x => x.RubricStandardId == input.RubricStandardId);
                row.Score = input.Score;
                row.EvidenceNote = input.EvidenceNote;
            }
        }

        _context.Visits.Add(visit);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Visit created: id={VisitId} school={SchoolId} instructor={InstructorId} rubricVersion={RubricVersionId} by={UserId}",
            visit.Id, visit.SchoolId, visit.InstructorId, visit.RubricVersionId, _currentUser.UserId);

        return await GetByIdAsync(visit.Id, cancellationToken);
    }

    public async Task<VisitDetailDto> UpdateAsync(int id, UpdateVisitRequestDto request, CancellationToken cancellationToken = default)
    {
        var visit = await LoadVisitAsync(id, cancellationToken: cancellationToken);

        // School-scope enforcement.
        await _scopeGuard.EnsureCanMutateSchoolAsync(visit.SchoolId, cancellationToken);

        // Phase 5: editable states — Draft (Phase 4) + RejectedForChanges (returned by SM)
        // + Reopened (after SM reopen). PendingApproval is editable ONLY by the visit's
        // School Manager (direct-edit path); Approved is read-only except via Reopen.
        EnsureCanEdit(visit);

        // Capture before-image for audit (Phase 5 — only emitted when status != Draft,
        // since Draft edits were never audited in Phase 4).
        VisitStatus oldStatus = visit.Status;
        var oldScoresSnapshot = visit.Scores.Select(s => new
        {
            s.RubricStandardId,
            s.Score,
            s.EvidenceNote
        }).ToList();

        // Update visit metadata.
        visit.VisitCategory = (VisitCategory)request.VisitCategory;
        visit.VisitSequence = (VisitSequence)request.VisitSequence;
        visit.VisitDate = request.VisitDate;
        visit.Subject = request.Subject;
        visit.GradeClass = request.GradeClass;
        visit.Notes = request.Notes;
        visit.UpdatedAt = DateTimeOffset.UtcNow;

        // Upsert all 25 scores (request.Scores must have exactly 25 — validator enforces).
        var scoreDict = visit.Scores.ToDictionary(s => s.RubricStandardId);
        var stdIdsInVersion = await _context.RubricStandards
            .Where(s => visit.Scores.Select(vs => vs.RubricStandardId).Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        var stdSet = stdIdsInVersion.ToHashSet();

        foreach (var input in request.Scores)
        {
            if (!stdSet.Contains(input.RubricStandardId))
                throw new InvalidOperationException(
                    $"المعيار رقم {input.RubricStandardId} لا ينتمي لإصدار الزيارة.");

            if (input.Score is < 0 or > 4)
                throw new InvalidOperationException("درجة المعيار يجب أن تكون بين 0 و 4.");

            var row = scoreDict[input.RubricStandardId];
            row.Score = input.Score;
            row.EvidenceNote = input.EvidenceNote;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // Audit edit-after-submit (Phase 5): any update on a non-Draft visit is
        // recorded so the SM's direct-edit path and the moderator's fix-after-reject
        // path leave a trail.
        if (oldStatus != VisitStatus.Draft)
        {
            await WriteAuditAsync(
                visit,
                action: "Visit.Edit",
                reason: oldStatus == VisitStatus.RejectedForChanges
                    ? "تعديل بعد الرفض"
                    : oldStatus == VisitStatus.Reopened
                        ? "تعديل بعد إعادة الفتح"
                        : "تعديل مباشر من مدير المدرسة",
                oldValues: JsonSerializer.Serialize(new
                {
                    status = (int)oldStatus,
                    scores = oldScoresSnapshot
                }),
                newValues: JsonSerializer.Serialize(new
                {
                    status = (int)visit.Status,
                    scores = visit.Scores.Select(s => new
                    {
                        s.RubricStandardId,
                        s.Score,
                        s.EvidenceNote
                    }).ToList()
                }),
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<VisitDetailDto> SubmitAsync(int id, CancellationToken cancellationToken = default)
    {
        var visit = await LoadVisitAsync(id, includeAnalysis: true, cancellationToken);

        await _scopeGuard.EnsureCanMutateSchoolAsync(visit.SchoolId, cancellationToken);

        // Phase 5: submission is valid from:
        //   - Draft (Phase 4 — first submission).
        //   - RejectedForChanges (creator fixed the SM's feedback and is re-submitting).
        //   - Reopened (the SM reopened an approved visit and the creator is re-submitting).
        // All three → PendingApproval; Reopened recomputes a NEW snapshot.
        if (visit.Status != VisitStatus.Draft
            && visit.Status != VisitStatus.RejectedForChanges
            && visit.Status != VisitStatus.Reopened)
            throw new InvalidOperationException(
                $"لا يمكن إرسال الزيارة في حالتها الحالية ({visit.Status}).");

        // 25/25 gate: every score must be present (no null).
        var missing = visit.Scores.Count(s => !s.Score.HasValue);
        if (missing > 0)
            throw new InvalidOperationException(
                $"لا يمكن إرسال الزيارة قبل تقييم جميع المعايير. تبقى {missing} من 25 معياراً بدون درجة.");

        // Capture audit state (Phase 5): before-image of analysis if we're recomputing
        var previousAnalysis = visit.Analysis;
        var isResubmitAfterReopen = visit.Status == VisitStatus.Reopened;

        // Compute + persist the analysis snapshot.
        // Phase 5: the recompute always uses the visit's SNAPSHOTTED RubricVersionId
        // (never the active version), so historical visits stay bound to the rubric
        // that was in effect when they were created.
        var analysis = ComputeAnalysis(visit);

        if (visit.Analysis == null)
        {
            // First submit — Phase 4 path.
            var analysisEntity = new VisitAnalysis
            {
                VisitId = visit.Id,
                OverallScore = analysis.OverallScore,
                PerformanceLevelAr = analysis.PerformanceLevelAr,
                StrengthsJson = JsonSerializer.Serialize(analysis.Strengths),
                ImprovementAreasJson = JsonSerializer.Serialize(analysis.ImprovementAreas),
                PriorityStandardsJson = JsonSerializer.Serialize(analysis.PriorityStandards),
                ComputedAt = DateTimeOffset.UtcNow,
                DomainAverages = analysis.DomainAverages.Select(d => new VisitDomainAverage
                {
                    RubricDomainId = d.RubricDomainId,
                    DomainCode = d.DomainCode,
                    DomainNameAr = d.DomainNameAr,
                    AverageScore = d.AverageScore
                }).ToList<VisitDomainAverage>()
            };
            _context.VisitAnalyses.Add(analysisEntity);
        }
        else
        {
            // Reopen→resubmit: REPLACE the snapshot in place (1:1 holds via UX_VisitAnalysis_Visit).
            // Soft-delete the old domain-averages; the old VisitAnalysis row will be removed
            // after the new one is inserted (see below) — we keep the old row alive until
            // SaveChanges so the FK from Visit.Analysis (1:1) stays valid.
            foreach (var da in visit.Analysis.DomainAverages.ToList())
            {
                _context.VisitDomainAverages.Remove(da);
            }
            _context.VisitAnalyses.Remove(visit.Analysis);
            await _context.SaveChangesAsync(cancellationToken);

            var analysisEntity = new VisitAnalysis
            {
                VisitId = visit.Id,
                OverallScore = analysis.OverallScore,
                PerformanceLevelAr = analysis.PerformanceLevelAr,
                StrengthsJson = JsonSerializer.Serialize(analysis.Strengths),
                ImprovementAreasJson = JsonSerializer.Serialize(analysis.ImprovementAreas),
                PriorityStandardsJson = JsonSerializer.Serialize(analysis.PriorityStandards),
                ComputedAt = DateTimeOffset.UtcNow,
                DomainAverages = analysis.DomainAverages.Select(d => new VisitDomainAverage
                {
                    RubricDomainId = d.RubricDomainId,
                    DomainCode = d.DomainCode,
                    DomainNameAr = d.DomainNameAr,
                    AverageScore = d.AverageScore
                }).ToList<VisitDomainAverage>()
            };
            _context.VisitAnalyses.Add(analysisEntity);
        }

        // Transition status + set SubmittedAt.
        visit.Status = VisitStatus.PendingApproval;
        visit.SubmittedAt = DateTimeOffset.UtcNow;
        visit.UpdatedAt = DateTimeOffset.UtcNow;

        // Phase 5: on reopen→resubmit, write an audit row with the previous + new snapshot.
        if (isResubmitAfterReopen)
        {
            await WriteAuditAsync(
                visit,
                action: "Visit.Resubmit",
                reason: "إعادة إرسال بعد إعادة الفتح",
                oldValues: JsonSerializer.Serialize(new
                {
                    status = (int)VisitStatus.Reopened,
                    overallScore = (decimal?)previousAnalysis?.OverallScore,
                    performanceLevelAr = previousAnalysis?.PerformanceLevelAr,
                    computedAt = previousAnalysis?.ComputedAt
                }),
                newValues: JsonSerializer.Serialize(new
                {
                    status = (int)VisitStatus.PendingApproval,
                    overallScore = analysis.OverallScore,
                    performanceLevelAr = analysis.PerformanceLevelAr,
                    rubricVersionId = visit.RubricVersionId
                }),
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Visit submitted: id={VisitId} rubricVersion={RubricVersionId} overall={Overall} level={Level} resubmit={Resubmit}",
            visit.Id, visit.RubricVersionId, analysis.OverallScore, analysis.PerformanceLevelAr, isResubmitAfterReopen);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var visit = await LoadVisitAsync(id, cancellationToken: cancellationToken);

        await _scopeGuard.EnsureCanMutateSchoolAsync(visit.SchoolId, cancellationToken);

        if (visit.Status != VisitStatus.Draft)
            throw new InvalidOperationException("لا يمكن حذف زيارة تم إرسالها للاعتماد.");

        var userId = _currentUser.UserId ?? "system";
        var now = DateTimeOffset.UtcNow;

        visit.IsDeleted = true;
        visit.DeletedAt = now;
        visit.DeletedByUserId = userId;
        visit.UpdatedAt = now;

        // Cascade-soft-delete children for symmetry.
        foreach (var s in visit.Scores)
        {
            s.IsDeleted = true;
            s.DeletedAt = now;
            s.DeletedByUserId = userId;
        }
        if (visit.Analysis != null)
        {
            visit.Analysis.IsDeleted = true;
            visit.Analysis.DeletedAt = now;
            visit.Analysis.DeletedByUserId = userId;
            foreach (var da in visit.Analysis.DomainAverages)
            {
                da.IsDeleted = true;
                da.DeletedAt = now;
                da.DeletedByUserId = userId;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Visit soft-deleted: id={VisitId} by={UserId}", id, userId);
    }

    // ─── Queries ──────────────────────────────────────────────────────────────

    public async Task<VisitDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var visit = await LoadVisitAsync(id, includeAnalysis: true, cancellationToken);

        var effectiveSchoolId = _scopeGuard.ResolveAllowedSchoolId(visit.SchoolId);
        if (effectiveSchoolId.HasValue && effectiveSchoolId.Value != visit.SchoolId)
            throw new UnauthorizedSchoolAccessException(
                $"لا تملك صلاحية الوصول إلى بيانات خارج المدرسة الحالية ({effectiveSchoolId}).");

        // D-37: Moderator can only access visits HE created.
        EnsureModeratorCanAccessCreatedByVisit(visit);

        // D-36 close (security): instructors are NOT allowed to peek at scores/analysis
        // via the manager endpoint. If the caller is an Instructor (only role carrying
        // Visit.View but no manager capability), they must EITHER be looking at their
        // own visit (Visit.InstructorId == currentUserId) — and if so, they only get the
        // full detail when status == Approved. Any other instructor visit → 403.
        // School managers / moderators / global admins are unaffected (they pass through).
        if (_currentUser.IsInRole(RoleNames.Instructor)
            && !_currentUser.IsInRole(RoleNames.SchoolManager)
            && !_currentUser.IsInRole(RoleNames.Moderator)
            && !_currentUser.IsGlobalAdmin())
        {
            var currentUserId = _currentUser.UserId
                ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول لعرض الزيارة.");

            if (visit.InstructorId != currentUserId)
            {
                _logger.LogWarning(
                    "Instructor cross-record access denied: caller {UserId} attempted to GET /visits/{VisitId} belonging to instructor {OwnerId}.",
                    currentUserId, visit.Id, visit.InstructorId);
                throw new UnauthorizedSchoolAccessException(
                    "لا تملك صلاحية الوصول إلى تقارير المعلمين الآخرين.");
            }

            // Own visit: return a safe PENDING payload when status != Approved.
            // The dedicated /report endpoint is the only path that returns the full
            // result to an instructor (and is the only path that writes ReportViewLog).
            if (visit.Status != VisitStatus.Approved)
            {
                _logger.LogInformation(
                    "Instructor pending view: caller {UserId} opened non-approved visit {VisitId} (status={Status}); Scores/Analysis suppressed.",
                    currentUserId, visit.Id, visit.Status);
                return MapPendingForInstructor(visit);
            }

            // Approved + own: the dedicated /report endpoint is the canonical path
            // (it also writes a ReportViewLog). Hit this endpoint directly via the
            // manager path here would NOT log a view; we still return the full detail
            // so the UI can render, but log a warning so this path is traceable.
            _logger.LogInformation(
                "Instructor used manager endpoint GET /visits/{VisitId} on an approved own visit; redirect to /report recommended for view-log tracking.",
                visit.Id);
        }

        return await MapDetailAsync(visit);
    }

    /// <summary>
    /// Builds the safe "awaiting approval" payload returned to an Instructor
    /// when they GET a visit that is not yet approved (their own or otherwise).
    /// NO Scores, NO Analysis, NO evidence notes — only the meta that the
    /// pending-notice UI needs.
    /// </summary>
    private static VisitDetailDto MapPendingForInstructor(Visit visit)
    {
        return new VisitDetailDto
        {
            Id = visit.Id,
            SchoolId = visit.SchoolId,
            SchoolName = visit.School.Name,
            InstructorId = visit.InstructorId,
            InstructorFullName = visit.Instructor.FullName,
            CreatedByUserId = visit.CreatedByUserId,
            CreatedByFullName = visit.CreatedByUser.FullName,
            RubricVersionId = visit.RubricVersionId,
            RubricVersionNumber = visit.RubricVersion.VersionNumber,
            VisitCategory = ((int)visit.VisitCategory).ToString(),
            VisitCategoryLabelAr = visit.VisitCategory.ToArabicString(),
            VisitSequence = ((int)visit.VisitSequence).ToString(),
            VisitSequenceLabelAr = visit.VisitSequence.ToArabicString(),
            Status = ((int)visit.Status).ToString(),
            StatusLabelAr = StatusLabelAr(visit.Status),
            VisitDate = visit.VisitDate,
            Subject = visit.Subject,
            GradeClass = visit.GradeClass,
            // Notes / CreatedByFullName / approval metadata intentionally still
            // returned (they are not the "result" — they are visit-level context).
            Notes = visit.Notes,
            CreatedAt = visit.CreatedAt,
            UpdatedAt = visit.UpdatedAt,
            SubmittedAt = visit.SubmittedAt,
            ApprovedByUserId = visit.ApprovedByUserId,
            ApprovedByFullName = visit.ApprovedByUser?.FullName,
            ApprovedAt = visit.ApprovedAt,
            RejectionReason = visit.RejectionReason,
            ReopenReason = visit.ReopenReason,
            ReopenedByUserId = visit.ReopenedByUserId,
            ReopenedByFullName = visit.ReopenedByUser?.FullName,
            ReopenedAt = visit.ReopenedAt,
            // Instructors can never edit — IsReadOnly stays true.
            IsReadOnly = true,
            // Hardening: NO Scores / NO Analysis before approval.
            Scores = new List<VisitScoreDto>(),
            Analysis = null
        };
    }

    public async Task<VisitAnalysisDto?> GetAnalysisAsync(int id, CancellationToken cancellationToken = default)
    {
        var visit = await _context.Visits
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("الزيارة غير موجودة.");

        var effectiveSchoolId = _scopeGuard.ResolveAllowedSchoolId(visit.SchoolId);
        if (effectiveSchoolId.HasValue && effectiveSchoolId.Value != visit.SchoolId)
            throw new UnauthorizedSchoolAccessException(
                $"لا تملك صلاحية الوصول إلى بيانات خارج المدرسة الحالية ({effectiveSchoolId}).");

        // D-37: Moderator own-visits-only.
        EnsureModeratorCanAccessCreatedByVisit(visit);

        var analysis = await _context.VisitAnalyses
            .AsNoTracking()
            .Include(a => a.DomainAverages)
            .FirstOrDefaultAsync(a => a.VisitId == id, cancellationToken);

        if (analysis == null) return null;

        return new VisitAnalysisDto
        {
            Id = analysis.Id,
            VisitId = analysis.VisitId,
            OverallScore = analysis.OverallScore,
            PerformanceLevelAr = analysis.PerformanceLevelAr,
            Strengths = DeserializeList<VisitStrengthDto>(analysis.StrengthsJson),
            ImprovementAreas = DeserializeList<VisitImprovementDto>(analysis.ImprovementAreasJson),
            PriorityStandards = DeserializeList<VisitPriorityStandardDto>(analysis.PriorityStandardsJson),
            DomainAverages = analysis.DomainAverages
                .OrderBy(d => d.DomainCode)
                .Select(d => new VisitDomainAverageDto
                {
                    Id = d.Id,
                    RubricDomainId = d.RubricDomainId,
                    DomainCode = d.DomainCode,
                    DomainNameAr = d.DomainNameAr,
                    AverageScore = d.AverageScore
                }).ToList(),
            ComputedAt = analysis.ComputedAt
        };
    }

    public async Task<PagedResult<VisitListItemDto>> ListAsync(VisitListQuery query, CancellationToken cancellationToken = default)
    {
        // School-scope resolve: client-supplied schoolId is ignored for school-scoped callers.
        var effectiveSchoolId = _scopeGuard.ResolveAllowedSchoolId(null);

        var q = _context.Visits
            .AsNoTracking()
            .Include(v => v.School)
            .Include(v => v.Instructor)
            .Include(v => v.CreatedByUser)
            .Include(v => v.RubricVersion)
            .Include(v => v.Scores)
            .AsQueryable();

        if (effectiveSchoolId.HasValue)
            q = q.Where(v => v.SchoolId == effectiveSchoolId.Value);

        // D-37: per docs/03 §4 ("Moderator must not see private visit activity of
        // other moderators"), a Moderator (and ONLY a Moderator — SchoolManager +
        // global admins + Instructors are unaffected) sees only visits HE created.
        // Without this filter a Moderator would receive every visit in his school,
        // including those created by other moderators or the School Manager.
        if (IsModeratorOnlyCaller())
        {
            var currentUserId = _currentUser.UserId
                ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول لعرض الزيارات.");
            q = q.Where(v => v.CreatedByUserId == currentUserId);
        }

        if (query.Status.HasValue)
            q = q.Where(v => v.Status == (VisitStatus)query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.InstructorId))
            q = q.Where(v => v.InstructorId == query.InstructorId);

        if (query.VisitCategory.HasValue)
            q = q.Where(v => v.VisitCategory == (VisitCategory)query.VisitCategory.Value);

        if (query.FromDate.HasValue)
            q = q.Where(v => v.VisitDate >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(v => v.VisitDate <= query.ToDate.Value);

        var totalCount = await q.CountAsync(cancellationToken);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => query.PageSize
        };

        var rows = await q
            .OrderByDescending(v => v.VisitDate)
            .ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(v => new VisitListItemDto
        {
            Id = v.Id,
            SchoolId = v.SchoolId,
            SchoolName = v.School.Name,
            InstructorId = v.InstructorId,
            InstructorFullName = v.Instructor.FullName,
            CreatedByUserId = v.CreatedByUserId,
            CreatedByFullName = v.CreatedByUser.FullName,
            RubricVersionId = v.RubricVersionId,
            RubricVersionNumber = v.RubricVersion.VersionNumber,
            VisitCategory = ((int)v.VisitCategory).ToString(),
            VisitCategoryLabelAr = v.VisitCategory.ToArabicString(),
            VisitSequence = ((int)v.VisitSequence).ToString(),
            VisitSequenceLabelAr = v.VisitSequence.ToArabicString(),
            Status = ((int)v.Status).ToString(),
            StatusLabelAr = StatusLabelAr(v.Status),
            VisitDate = v.VisitDate,
            Subject = v.Subject,
            GradeClass = v.GradeClass,
            CreatedAt = v.CreatedAt,
            SubmittedAt = v.SubmittedAt,
            ScoredStandardsCount = v.Scores.Count(s => s.Score.HasValue),
            TotalStandardsCount = v.Scores.Count
        }).ToList();

        return new PagedResult<VisitListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    // ─── Analysis engine (docs/09 verbatim) ───────────────────────────────────

    /// <summary>
    /// Computes the analysis snapshot following docs/09 exactly:
    ///  - Domain average = mean of standard scores in that domain (UNEVEN distribution respected).
    ///  - Overall score = mean of all 25 scored standards.
    ///  - Performance level thresholds: متميز >=3.5, جيد جداً >=3.0, جيد >=2.5,
    ///    متحقق جزئياً >=2.0, يحتاج تحسين >=1.0, غير مشاهد <1.0.
    ///  - Strengths = domains with average >= 3.0.
    ///  - Improvement areas = domains with average < 2.5.
    ///  - Priority standards = individual standards with score <= 1.5.
    /// </summary>
    private (decimal OverallScore, string PerformanceLevelAr,
             List<VisitStrengthDto> Strengths, List<VisitImprovementDto> ImprovementAreas,
             List<VisitPriorityStandardDto> PriorityStandards,
             List<VisitDomainAverageDto> DomainAverages)
        ComputeAnalysis(Visit visit)
    {
        // Pull standards grouped by domain (load standards navigation eagerly here since
        // LoadVisitAsync includes them on the Visit).
        var standardsWithDomain = visit.Scores
            .Where(s => s.Score.HasValue)
            .Select(s => new
            {
                Score = s,
                DomainId = s.RubricStandard.Domain.Id,
                DomainCode = s.RubricStandard.Domain.Code,
                DomainNameAr = s.RubricStandard.Domain.NameAr
            })
            .ToList();

        if (standardsWithDomain.Count != 25)
            throw new InvalidOperationException(
                $"عدد المعايير المُقيَّمة ({standardsWithDomain.Count}) لا يساوي 25.");

        // Domain averages — respect uneven distribution (D1=6 / D2=4 / D3=6 / D4=3 / D5=6).
        var domainGroups = standardsWithDomain
            .GroupBy(x => new { x.DomainId, x.DomainCode, x.DomainNameAr })
            .OrderBy(g => g.Key.DomainCode)
            .ToList();

        var domainAverages = domainGroups.Select(g => new VisitDomainAverageDto
        {
            RubricDomainId = g.Key.DomainId,
            DomainCode = g.Key.DomainCode,
            DomainNameAr = g.Key.DomainNameAr,
            AverageScore = Math.Round(g.Average(x => (decimal)x.Score.Score!.Value), 3)
        }).ToList();

        // Overall = mean of all 25 standard scores.
        var overall = Math.Round(
            standardsWithDomain.Average(x => (decimal)x.Score.Score!.Value),
            3);

        // Performance level — apply highest-first thresholds from docs/09.
        var level = MapPerformanceLevel(overall);

        // Strengths = domains with avg >= 3.0
        var strengths = domainAverages
            .Where(d => d.AverageScore >= 3.0m)
            .Select(d => new VisitStrengthDto
            {
                DomainCode = d.DomainCode,
                DomainNameAr = d.DomainNameAr,
                AverageScore = d.AverageScore
            }).ToList();

        // Improvement = domains with avg < 2.5
        var improvements = domainAverages
            .Where(d => d.AverageScore < 2.5m)
            .Select(d => new VisitImprovementDto
            {
                DomainCode = d.DomainCode,
                DomainNameAr = d.DomainNameAr,
                AverageScore = d.AverageScore
            }).ToList();

        // Priority standards = individual standards with score <= 1.5
        var priorities = standardsWithDomain
            .Where(x => x.Score.Score!.Value <= 1)
            .Select(x => new VisitPriorityStandardDto
            {
                DomainCode = x.DomainCode,
                StandardCode = x.Score.RubricStandard.Code,
                StandardTextAr = x.Score.RubricStandard.TextAr,
                Score = x.Score.Score!.Value
            })
            .OrderBy(p => p.Score)
            .ThenBy(p => p.StandardCode)
            .ToList();

        return (overall, level, strengths, improvements, priorities, domainAverages);
    }

    private static string MapPerformanceLevel(decimal overall)
    {
        // Ordered highest → lowest per docs/09.
        if (overall >= 3.5m) return "متميز";
        if (overall >= 3.0m) return "جيد جداً";
        if (overall >= 2.5m) return "جيد";
        if (overall >= 2.0m) return "متحقق جزئياً";
        if (overall >= 1.0m) return "يحتاج تحسين";
        return "غير مشاهد";
    }

    private static string StatusLabelAr(VisitStatus s) => s switch
    {
        VisitStatus.Draft => "مسودة",
        VisitStatus.Submitted => "مُرسلة",
        VisitStatus.PendingApproval => "بانتظار الاعتماد",
        VisitStatus.Approved => "معتمدة",
        VisitStatus.RejectedForChanges => "مرفوضة للتعديل",
        VisitStatus.Reopened => "مُعاد فتحها",
        VisitStatus.UnderReviewAfterComplaint => "قيد المراجعة بعد شكوى",
        VisitStatus.Cancelled => "ملغاة",
        _ => s.ToString()
    };

    // ─── Phase 5: Approval workflow ───────────────────────────────────────────

    /// <summary>
    /// PendingApproval → Approved. Caller MUST be the visit's School Manager
    /// (or SuperAdmin / MainManager); enforced by <see cref="EnsureCanActAsSchoolManagerAsync"/>.
    /// </summary>
    public async Task<VisitDetailDto> ApproveAsync(int id, CancellationToken cancellationToken = default)
    {
        var visit = await LoadVisitAsync(id, cancellationToken: cancellationToken);
        await EnsureCanActAsSchoolManagerAsync(visit, cancellationToken);

        if (visit.Status != VisitStatus.PendingApproval)
            throw new InvalidOperationException(
                "لا يمكن اعتماد الزيارة في حالتها الحالية. يجب أن تكون بانتظار الاعتماد.");

        var oldStatus = visit.Status;
        var userId = _currentUser.UserId ?? throw new InvalidOperationException("لم يتم العثور على المستخدم الحالي.");
        var now = DateTimeOffset.UtcNow;

        visit.Status = VisitStatus.Approved;
        visit.ApprovedByUserId = userId;
        visit.ApprovedAt = now;
        visit.UpdatedAt = now;
        // Clear rejection/reopen transient fields — the visit is now in a clean approved state.
        visit.RejectionReason = null;
        visit.ReopenReason = null;
        visit.ReopenedByUserId = null;
        visit.ReopenedAt = null;

        await WriteAuditAsync(
            visit,
            action: "Visit.Approve",
            reason: "اعتماد الزيارة",
            oldValues: JsonSerializer.Serialize(new { status = (int)oldStatus }),
            newValues: JsonSerializer.Serialize(new
            {
                status = (int)visit.Status,
                approvedByUserId = userId,
                approvedAt = now
            }),
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Visit approved: id={VisitId} by={UserId}", visit.Id, userId);

        return await GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// PendingApproval → RejectedForChanges. <paramref name="reason"/> is required
    /// (validator + service both enforce non-empty). Writes an AuditLog row.
    /// </summary>
    public async Task<VisitDetailDto> RejectAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("سبب الرفض مطلوب.");

        var visit = await LoadVisitAsync(id, cancellationToken: cancellationToken);
        await EnsureCanActAsSchoolManagerAsync(visit, cancellationToken);

        if (visit.Status != VisitStatus.PendingApproval)
            throw new InvalidOperationException(
                "لا يمكن رفض الزيارة في حالتها الحالية. يجب أن تكون بانتظار الاعتماد.");

        var oldStatus = visit.Status;
        var now = DateTimeOffset.UtcNow;

        visit.Status = VisitStatus.RejectedForChanges;
        visit.RejectionReason = reason.Trim();
        // On reject: clear the approval/reopen state — the visit is back in the creator's hands.
        visit.ApprovedByUserId = null;
        visit.ApprovedAt = null;
        visit.ReopenReason = null;
        visit.ReopenedByUserId = null;
        visit.ReopenedAt = null;
        visit.UpdatedAt = now;

        await WriteAuditAsync(
            visit,
            action: "Visit.Reject",
            reason: reason.Trim(),
            oldValues: JsonSerializer.Serialize(new { status = (int)oldStatus }),
            newValues: JsonSerializer.Serialize(new
            {
                status = (int)visit.Status,
                rejectionReason = visit.RejectionReason
            }),
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Visit rejected: id={VisitId} reason={Reason}", visit.Id, reason);

        return await GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// Approved → Reopened. Reason required. The visit becomes editable again;
    /// the analysis snapshot stays in place until the next resubmit recomputes a
    /// new one on the same RubricVersionId (Phase 5 — historical accuracy).
    /// </summary>
    public async Task<VisitDetailDto> ReopenAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("سبب إعادة الفتح مطلوب.");

        var visit = await LoadVisitAsync(id, cancellationToken: cancellationToken);
        await EnsureCanActAsSchoolManagerAsync(visit, cancellationToken);

        if (visit.Status != VisitStatus.Approved)
            throw new InvalidOperationException(
                "لا يمكن إعادة فتح الزيارة في حالتها الحالية. يجب أن تكون معتمدة.");

        var oldStatus = visit.Status;
        var userId = _currentUser.UserId ?? throw new InvalidOperationException("لم يتم العثور على المستخدم الحالي.");
        var now = DateTimeOffset.UtcNow;

        visit.Status = VisitStatus.Reopened;
        visit.ReopenReason = reason.Trim();
        visit.ReopenedByUserId = userId;
        visit.ReopenedAt = now;
        visit.UpdatedAt = now;
        // Clear rejection transient field — reject and reopen are mutually exclusive.
        visit.RejectionReason = null;

        await WriteAuditAsync(
            visit,
            action: "Visit.Reopen",
            reason: reason.Trim(),
            oldValues: JsonSerializer.Serialize(new { status = (int)oldStatus }),
            newValues: JsonSerializer.Serialize(new
            {
                status = (int)visit.Status,
                reopenReason = visit.ReopenReason,
                reopenedByUserId = userId,
                reopenedAt = now
            }),
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Visit reopened: id={VisitId} by={UserId} reason={Reason}", visit.Id, userId, reason);

        return await GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// Instructor-only report view. Hard-gated:
    ///  - status MUST be Approved;
    ///  - current user id MUST equal Visit.InstructorId;
    ///  - global admins are NOT auto-allowed — they view through the manager's
    ///    detail page (the global-admin instructor-aliasing would be a Phase 6+
    ///    feature).
    /// Records a <c>ReportViewLog</c> on success.
    /// </summary>
    public async Task<InstructorReportDto> GetInstructorReportAsync(int id, CancellationToken cancellationToken = default)
    {
        var visit = await LoadVisitAsync(id, includeAnalysis: true, cancellationToken);

        var currentUserId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول لعرض التقرير.");

        // Hard gate — instructors cannot peek at other instructors' approved visits.
        if (visit.Status != VisitStatus.Approved)
            throw new UnauthorizedAccessException(
                "لم يتم اعتماد هذه الزيارة بعد. لا يمكن للمعلم الاطلاع على نتيجتها قبل اعتماد مدير المدرسة.");

        if (visit.InstructorId != currentUserId)
            throw new UnauthorizedSchoolAccessException(
                "لا تملك صلاحية الوصول إلى تقارير المعلمين الآخرين.");

        // Snapshot the visit before logging the view (so the log captures a stable FK).
        var ip = TryResolveClientIp();

        _context.ReportViewLogs.Add(new ReportViewLog
        {
            VisitId = visit.Id,
            InstructorUserId = currentUserId,
            ViewedAt = DateTimeOffset.UtcNow,
            IpAddress = ip
        });

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Instructor report viewed: visit={VisitId} instructor={InstructorId}", visit.Id, currentUserId);

        // Build the instructor-facing DTO — carries the same shape as the manager's
        // detail (visit meta + 25 scores + analysis), minus the notes / creator fields
        // that the instructor is not authorized to see.
        VisitAnalysisDto? analysisDto = null;
        if (visit.Analysis != null)
        {
            analysisDto = new VisitAnalysisDto
            {
                Id = visit.Analysis.Id,
                VisitId = visit.Analysis.VisitId,
                OverallScore = visit.Analysis.OverallScore,
                PerformanceLevelAr = visit.Analysis.PerformanceLevelAr,
                Strengths = DeserializeList<VisitStrengthDto>(visit.Analysis.StrengthsJson),
                ImprovementAreas = DeserializeList<VisitImprovementDto>(visit.Analysis.ImprovementAreasJson),
                PriorityStandards = DeserializeList<VisitPriorityStandardDto>(visit.Analysis.PriorityStandardsJson),
                DomainAverages = visit.Analysis.DomainAverages
                    .OrderBy(d => d.DomainCode)
                    .Select(d => new VisitDomainAverageDto
                    {
                        Id = d.Id,
                        RubricDomainId = d.RubricDomainId,
                        DomainCode = d.DomainCode,
                        DomainNameAr = d.DomainNameAr,
                        AverageScore = d.AverageScore
                    }).ToList(),
                ComputedAt = visit.Analysis.ComputedAt
            };
        }

        return new InstructorReportDto
        {
            VisitId = visit.Id,
            InstructorId = visit.InstructorId,
            InstructorFullName = visit.Instructor.FullName,
            SchoolId = visit.SchoolId,
            SchoolName = visit.School.Name,
            RubricVersionId = visit.RubricVersionId,
            RubricVersionNumber = visit.RubricVersion.VersionNumber,
            VisitCategory = ((int)visit.VisitCategory).ToString(),
            VisitCategoryLabelAr = visit.VisitCategory.ToArabicString(),
            VisitSequence = ((int)visit.VisitSequence).ToString(),
            VisitSequenceLabelAr = visit.VisitSequence.ToArabicString(),
            Status = ((int)visit.Status).ToString(),
            StatusLabelAr = StatusLabelAr(visit.Status),
            VisitDate = visit.VisitDate,
            Subject = visit.Subject,
            GradeClass = visit.GradeClass,
            SubmittedAt = visit.SubmittedAt,
            ApprovedAt = visit.ApprovedAt,
            ApprovedByFullName = visit.ApprovedByUser?.FullName,
            Scores = visit.Scores
                .OrderBy(s => s.RubricStandard.Domain.SortOrder)
                .ThenBy(s => s.RubricStandard.SortOrder)
                .Select(s => new VisitScoreDto
                {
                    Id = s.Id,
                    VisitId = s.VisitId,
                    RubricStandardId = s.RubricStandardId,
                    StandardCode = s.RubricStandard.Code,
                    StandardTextAr = s.RubricStandard.TextAr,
                    RubricDomainId = s.RubricStandard.Domain.Id,
                    DomainCode = s.RubricStandard.Domain.Code,
                    DomainNameAr = s.RubricStandard.Domain.NameAr,
                    Score = s.Score,
                    EvidenceNote = s.EvidenceNote
                }).ToList(),
            Analysis = analysisDto
        };
    }

    /// <summary>
    /// Aggregated report-view status for the manager / moderator detail view.
    /// School-scope enforced via the visit lookup; instructors cannot call this
    /// because the controller gates it on <c>Visit.View</c>.
    /// </summary>
    public async Task<ReportViewStatusDto> GetReportViewStatusAsync(int id, CancellationToken cancellationToken = default)
    {
        var visit = await _context.Visits
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("الزيارة غير موجودة.");

        var effectiveSchoolId = _scopeGuard.ResolveAllowedSchoolId(visit.SchoolId);
        if (effectiveSchoolId.HasValue && effectiveSchoolId.Value != visit.SchoolId)
            throw new UnauthorizedSchoolAccessException(
                $"لا تملك صلاحية الوصول إلى بيانات خارج المدرسة الحالية ({effectiveSchoolId}).");

        // D-37: Moderator own-visits-only.
        EnsureModeratorCanAccessCreatedByVisit(visit);

        var logs = await _context.ReportViewLogs
            .AsNoTracking()
            .Where(l => l.VisitId == id)
            .OrderBy(l => l.ViewedAt)
            .Select(l => l.ViewedAt)
            .ToListAsync(cancellationToken);

        if (logs.Count == 0)
        {
            return new ReportViewStatusDto
            {
                VisitId = id,
                HasBeenViewed = false,
                FirstViewedAt = null,
                LastViewedAt = null,
                ViewCount = 0
            };
        }

        return new ReportViewStatusDto
        {
            VisitId = id,
            HasBeenViewed = true,
            FirstViewedAt = logs.First(),
            LastViewedAt = logs.Last(),
            ViewCount = logs.Count
        };
    }

    // ─── Phase 6 / Stage 1: PDF report payload ─────────────────────────────────

    /// <summary>
    /// Phase 6 Stage 1 — assembles the immutable payload that <see cref="AlFalah.Application.Interfaces.IPdfReportService"/>
    /// consumes to render the server-side PDF.
    ///
    /// Authorization (reused from existing gates — D-24 / D-28 / D-36 / D-37 intact):
    ///  - Status MUST be Approved (otherwise InvalidOperationException → 400 Arabic).
    ///  - Instructor: ONLY their OWN approved visit (D-36 close); on success a
    ///    ReportViewLog is inserted (PDF download counts as a view, mirroring
    ///    the existing /report endpoint semantics per the prompt's recommendation).
    ///  - School Manager: any approved visit in HIS school (school-scoping via
    ///    SchoolScopeGuard); cross-school → 403.
    ///  - Moderator: ONLY approved visits HE created (D-37); cross-moderator → 403.
    ///  - SuperAdmin / MainManager: global.
    /// </summary>
    public async Task<VisitReportDto> GetVisitReportAsync(int id, CancellationToken cancellationToken = default)
    {
        var visit = await LoadVisitAsync(id, includeAnalysis: true, cancellationToken);

        // D-41 / Task 3: REMOVED the "Status MUST be Approved" gate. Non-Approved
        // visits may now produce a PDF; the renderer stamps a clear
        // "مسودة — غير معتمدة" watermark so it cannot be mistaken for an
        // official report. Visibility gates (school-scope / moderator own-only /
        // instructor own-only) remain intact.

        // School-scoping (D-24 / D-28) for school-scoped callers.
        // Global admins bypass; school-scoped callers must match.
        if (!_currentUser.IsGlobalAdmin())
        {
            if (!_currentUser.IsSchoolScopedRole())
                throw new UnauthorizedSchoolAccessException("دور المستخدم غير معروف ضمن سياقات الأمان.");

            var active = _currentUser.ActiveSchoolId;
            if (active is null)
                throw new UnauthorizedSchoolAccessException(
                    "لا توجد مدرسة نشطة مرتبطة بحسابك. يرجى إعادة تسجيل الدخول.");
            if (active.Value != visit.SchoolId)
                throw UnauthorizedSchoolAccessException.OutsideScope(active, visit.SchoolId);
        }

        // D-37: Moderator-only callers are restricted to visits THEY created.
        EnsureModeratorCanAccessCreatedByVisit(visit);

        // D-36 close: instructors can only get a PDF of their OWN visit.
        // (PDF download is treated as a "view" — a ReportViewLog is written,
        // mirroring the existing /report endpoint semantics.)
        var isInstructorOnlyCaller =
            _currentUser.IsInRole(RoleNames.Instructor)
            && !_currentUser.IsInRole(RoleNames.SchoolManager)
            && !_currentUser.IsInRole(RoleNames.Moderator)
            && !_currentUser.IsGlobalAdmin();

        if (isInstructorOnlyCaller)
        {
            var currentUserId = _currentUser.UserId
                ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول لإنشاء تقرير الزيارة.");
            if (visit.InstructorId != currentUserId)
                throw new UnauthorizedSchoolAccessException(
                    "لا تملك صلاحية إنشاء تقرير لزيارات المعلمين الآخرين.");
        }

        // Record a view log (matches the existing /report endpoint semantics;
        // the prompt recommends logging PDF downloads).
        _context.ReportViewLogs.Add(new ReportViewLog
        {
            VisitId = visit.Id,
            InstructorUserId = _currentUser.UserId ?? visit.InstructorId,
            ViewedAt = DateTimeOffset.UtcNow,
            IpAddress = TryResolveClientIp()
        });
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Visit PDF report requested: visit={VisitId} status={Status} by={UserId}",
            visit.Id, visit.Status, _currentUser.UserId);

        var dto = await BuildReportDtoAsync(visit, cancellationToken);

        // D-41 / Task 3: stamp a watermark when the visit is not Approved.
        // The renderer reads this flag and draws a clear Arabic banner so the
        // output cannot be mistaken for an official report.
        dto.IsDraftWatermark = visit.Status != VisitStatus.Approved;

        return dto;
    }

    /// <summary>
    /// D-41 / Task 6 — bulk export support. Reuses the SAME scoped query as the
    /// list endpoint so the caller only ever receives visit ids they are allowed
    /// to see (school-scope, moderator own-only, global admin bypass). The
    /// controller iterates these ids, builds a PDF for each, and packages them
    /// into a single ZIP.
    ///
    /// NOTE: This intentionally does NOT cap pageSize — the bulk export must
    /// include EVERY visit visible to the caller, regardless of how many. We
    /// materialize the list in memory so a single ZIP stream contains exactly
    /// the scoped visits with no surprise omissions.
    /// </summary>
    public async Task<List<int>> ListScopedVisitIdsForExportAsync(VisitListQuery query, CancellationToken cancellationToken = default)
    {
        var effectiveSchoolId = _scopeGuard.ResolveAllowedSchoolId(null);

        var q = _context.Visits
            .AsNoTracking()
            .AsQueryable();

        if (effectiveSchoolId.HasValue)
            q = q.Where(v => v.SchoolId == effectiveSchoolId.Value);

        if (IsModeratorOnlyCaller())
        {
            var currentUserId = _currentUser.UserId
                ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول لتصدير الزيارات.");
            q = q.Where(v => v.CreatedByUserId == currentUserId);
        }

        if (query.Status.HasValue)
            q = q.Where(v => v.Status == (VisitStatus)query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.InstructorId))
            q = q.Where(v => v.InstructorId == query.InstructorId);

        if (query.VisitCategory.HasValue)
            q = q.Where(v => v.VisitCategory == (VisitCategory)query.VisitCategory.Value);

        if (query.FromDate.HasValue)
            q = q.Where(v => v.VisitDate >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(v => v.VisitDate <= query.ToDate.Value);

        // Soft-delete filter is applied via the global query filter on Visit;
        // no need to re-apply it here.

        var ids = await q
            .OrderByDescending(v => v.VisitDate)
            .ThenByDescending(v => v.Id)
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Bulk export requested: ids={Count} by={UserId}", ids.Count, _currentUser.UserId);

        return ids;
    }

    /// <summary>
    /// Assembles the immutable <see cref="VisitReportDto"/> from the visit + its
    /// snapshot. Pure projection — no DB queries other than the visit load that
    /// already happened in <see cref="GetVisitReportAsync"/>.
    ///
    /// Phase 6 / Stage 2 — also enriches the DTO with:
    ///  - SchoolReportSettings (header/footer text, primary color, flags).
    ///  - School.LogoUrl (resolved into image bytes — fallback to initials).
    ///  - Moderator (creator) + Manager (approver) signatures from
    ///    <see cref="UserSignature"/> — fallback to a printed name + line.
    ///  - QR payload (compact reference, no scores/PII — verification page deferred).
    /// Every external load is wrapped in try/catch so a missing image never
    /// breaks the PDF pipeline.
    /// </summary>
    private async Task<VisitReportDto> BuildReportDtoAsync(Visit visit, CancellationToken cancellationToken)
    {
        var dto = new VisitReportDto
        {
            VisitId = visit.Id,
            SchoolId = visit.SchoolId,
            SchoolName = visit.School.Name,
            InstructorFullName = visit.Instructor.FullName,
            Subject = visit.Subject,
            GradeClass = visit.GradeClass,
            CreatedByFullName = visit.CreatedByUser.FullName,
            ApprovedByFullName = visit.ApprovedByUser?.FullName,
            VisitCategoryLabelAr = visit.VisitCategory.ToArabicString(),
            VisitSequenceLabelAr = visit.VisitSequence.ToArabicString(),
            VisitDate = visit.VisitDate,
            SubmittedAt = visit.SubmittedAt,
            ApprovedAt = visit.ApprovedAt,
            RubricVersionNumber = visit.RubricVersion.VersionNumber,
        };

        // Snapshot fields — exactly the persisted JSON, no recompute.
        if (visit.Analysis != null)
        {
            dto.OverallScore = visit.Analysis.OverallScore;
            dto.PerformanceLevelAr = visit.Analysis.PerformanceLevelAr;

            var strengths = DeserializeList<VisitStrengthDto>(visit.Analysis.StrengthsJson);
            dto.Strengths = strengths.Select(s => new ReportStrengthDto
            {
                DomainCode = s.DomainCode,
                DomainNameAr = s.DomainNameAr,
                AverageScore = s.AverageScore
            }).ToList();

            var improvements = DeserializeList<VisitImprovementDto>(visit.Analysis.ImprovementAreasJson);
            dto.ImprovementAreas = improvements.Select(i => new ReportImprovementDto
            {
                DomainCode = i.DomainCode,
                DomainNameAr = i.DomainNameAr,
                AverageScore = i.AverageScore
            }).ToList();

            var priorities = DeserializeList<VisitPriorityStandardDto>(visit.Analysis.PriorityStandardsJson);
            dto.PriorityStandards = priorities.Select(p => new ReportPriorityStandardDto
            {
                DomainCode = p.DomainCode,
                StandardCode = p.StandardCode,
                StandardTextAr = p.StandardTextAr,
                Score = p.Score
            }).ToList();
        }

        // Standards grouped by domain — driven by the snapshotted rubric,
        // not the currently-active one (historical accuracy; D-21 carry-over).
        // Use RubricStandard.Domain.SortOrder to keep the original domain order.
        var domainGroups = visit.Scores
            .OrderBy(s => s.RubricStandard.Domain.SortOrder)
            .ThenBy(s => s.RubricStandard.SortOrder)
            .GroupBy(s => new {
                s.RubricStandard.Domain.Id,
                s.RubricStandard.Domain.Code,
                s.RubricStandard.Domain.NameAr
            })
            .ToList();

        foreach (var g in domainGroups)
        {
            // Domain average from the persisted snapshot (VisitDomainAverage rows),
            // NOT recomputed from scores — guarantees the PDF matches the stored
            // snapshot byte-for-byte (docs/09 invariant).
            var persistedAverage = visit.Analysis?.DomainAverages
                .FirstOrDefault(d => d.RubricDomainId == g.Key.Id);

            var block = new ReportDomainBlockDto
            {
                DomainCode = g.Key.Code,
                DomainNameAr = g.Key.NameAr,
                AverageScore = persistedAverage?.AverageScore ?? 0m,
                Standards = g.Select(s => new ReportStandardScoreDto
                {
                    StandardCode = s.RubricStandard.Code,
                    StandardTextAr = s.RubricStandard.TextAr,
                    Score = s.Score,
                    ScoreLabelAr = MapScoreLabelAr(s.Score),
                    EvidenceNote = s.EvidenceNote
                }).ToList()
            };
            dto.Domains.Add(block);
        }

        // ─── Phase 6 / Stage 2: branding + signatures + QR payload ─────────
        // Best-effort load of every external asset. None of these can break
        // the report pipeline — every load is guarded by ImageAssetLoader +
        // a per-asset try/catch. The PDF service decides what to render when
        // a particular field is null/empty.
        await EnrichReportDtoWithBrandingAsync(dto, visit, cancellationToken);

        return dto;
    }

    /// <summary>
    /// Phase 6 / Stage 2 — populates the branding, logo, signatures, and QR
    /// payload on the DTO. All asset fetches are best-effort; a failure here
    /// logs a warning and leaves the corresponding field null so the PDF
    /// renders its fallback (initials box / blank signature line / no QR).
    /// </summary>
    private async Task EnrichReportDtoWithBrandingAsync(VisitReportDto dto, Visit visit, CancellationToken cancellationToken)
    {
        // 1) SchoolReportSettings — defaults when null.
        var settings = visit.School?.ReportSettings;
        dto.HeaderText = !string.IsNullOrWhiteSpace(settings?.ReportHeaderText)
            ? settings!.ReportHeaderText!.Trim()
            : visit.School?.Name ?? dto.SchoolName;

        dto.FooterText = !string.IsNullOrWhiteSpace(settings?.ReportFooterText)
            ? settings!.ReportFooterText!.Trim()
            : string.Empty; // PDF service composes a timestamp fallback.

        dto.PrimaryColor = NormalizeHexColor(settings?.PrimaryColor)
            ?? "#0F7132"; // Saudi brand green (Stage-1 default).

        dto.ShowModeratorSignature = settings?.ShowModeratorSignature ?? true;
        dto.ShowManagerSignature = settings?.ShowManagerSignature ?? true;
        dto.ShowQrCode = settings?.ShowQrCode ?? false;

        // 2) School logo — prefer ReportSettings.LogoUrl (per-school branding),
        //    fall back to School.LogoUrl, fall back to initials.
        dto.SchoolInitials = ResolveSchoolInitials(dto.SchoolName);

        var logoSource = !string.IsNullOrWhiteSpace(settings?.LogoUrl)
            ? settings!.LogoUrl
            : visit.School?.LogoUrl;

        if (!string.IsNullOrWhiteSpace(logoSource))
        {
            try
            {
                var loaded = await _imageLoader.TryLoadAsync(logoSource, cancellationToken: cancellationToken);
                if (loaded.HasValue && !loaded.Value.IsEmpty)
                {
                    dto.SchoolLogoBytes = loaded.Value.Bytes;
                    dto.SchoolLogoFormat = loaded.Value.Format;
                }
                else
                {
                    _logger.LogInformation(
                        "VisitReport: school logo source unreachable or invalid for school={SchoolId}; using initials fallback.",
                        visit.SchoolId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "VisitReport: school logo load threw for school={SchoolId}; using initials fallback.",
                    visit.SchoolId);
            }
        }

        // 3) Moderator (creator) signature — only if the flag is on.
        if (dto.ShowModeratorSignature && !string.IsNullOrEmpty(visit.CreatedByUserId))
        {
            var signature = await TryLoadUserSignatureAsync(visit.CreatedByUserId, cancellationToken);
            if (signature.HasValue)
            {
                dto.ModeratorSignatureBytes = signature.Value.bytes;
                dto.ModeratorSignatureFormat = signature.Value.format;
            }
        }

        // 4) Manager (approver) signature — only if the flag is on AND the
        //    visit has been approved (otherwise there's no approver).
        if (dto.ShowManagerSignature && !string.IsNullOrEmpty(visit.ApprovedByUserId))
        {
            var signature = await TryLoadUserSignatureAsync(visit.ApprovedByUserId!, cancellationToken);
            if (signature.HasValue)
            {
                dto.ManagerSignatureBytes = signature.Value.bytes;
                dto.ManagerSignatureFormat = signature.Value.format;
            }
        }

        // 5) QR payload — compact reference only (NO scores, NO PII).
        //    Verification page is deferred (out of Stage-2 scope).
        dto.QrPayload = BuildQrPayload(dto);
    }

    /// <summary>
    /// Loads the <see cref="UserSignature"/> row for a user (if any) and
    /// resolves the signature image (URL or base64) into bytes. Soft-deleted
    /// users are excluded by the global query filter on ApplicationUser.
    /// </summary>
    private async Task<(byte[] bytes, string format)?> TryLoadUserSignatureAsync(
        string userId, CancellationToken cancellationToken)
    {
        try
        {
            var sig = await _context.UserSignatures
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

            if (sig is null) return null;

            // Prefer the uploaded image URL (could itself be a base64 payload
            // or a remote URL); fall back to the drawn base64.
            var source = !string.IsNullOrWhiteSpace(sig.SignatureImageUrl)
                ? sig.SignatureImageUrl
                : sig.SignatureDrawnData;

            if (string.IsNullOrWhiteSpace(source)) return null;

            var loaded = await _imageLoader.TryLoadAsync(source, cancellationToken: cancellationToken);
            if (loaded.HasValue && !loaded.Value.IsEmpty)
                return (loaded.Value.Bytes, loaded.Value.Format);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "VisitReport: UserSignature load failed for user={UserId}; using printed-name fallback.",
                userId);
            return null;
        }
    }

    /// <summary>
    /// Builds a compact, non-sensitive QR payload:
    /// <c>alfalah:visit-{id}|school-{schoolId}|ref-{8-char-base32}</c>.
    /// The hash mixes the visit id, school id, and ApprovedAt (if any) so the
    /// reference is unique per report without leaking scores / teacher names.
    /// </summary>
    internal static string BuildQrPayload(VisitReportDto dto)
    {
        var input = $"{dto.VisitId}:{dto.SchoolId}:{dto.ApprovedAt?.UtcTicks ?? 0}";
        var hash = StableShortHash(input);
        return $"alfalah:visit-{dto.VisitId}|school-{dto.SchoolId}|ref-{hash}";
    }

    private static string StableShortHash(string input)
    {
        // FNV-1a 32-bit → 8-char base32 (Crockford-ish). Deterministic; no crypto.
        unchecked
        {
            uint hash = 2166136261u;
            foreach (var c in input)
            {
                hash ^= c;
                hash *= 16777619u;
            }

            const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford
            Span<char> buf = stackalloc char[8];
            for (var i = 7; i >= 0; i--)
            {
                buf[i] = alphabet[(int)(hash & 0x1Fu)];
                hash >>= 5;
            }
            return new string(buf);
        }
    }

    /// <summary>Validates a stored hex color (#RGB / #RRGGBB) and returns the
    /// canonical <c>#RRGGBB</c> form. Returns null on any invalid input so
    /// the PDF service can use its Saudi-green default.</summary>
    internal static string? NormalizeHexColor(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (!s.StartsWith('#')) return null;
        var hex = s[1..];
        if (hex.Length == 3)
        {
            // expand #abc → #AABBCC (case-insensitive validation)
            if (!IsHex(hex)) return null;
            return $"#{char.ToUpperInvariant(hex[0])}{char.ToUpperInvariant(hex[0])}" +
                   $"{char.ToUpperInvariant(hex[1])}{char.ToUpperInvariant(hex[1])}" +
                   $"{char.ToUpperInvariant(hex[2])}{char.ToUpperInvariant(hex[2])}";
        }
        if (hex.Length == 6 && IsHex(hex))
            return $"#{hex.ToUpperInvariant()}";
        return null;
    }

    private static bool IsHex(string s)
    {
        foreach (var c in s)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        return true;
    }

    /// <summary>
    /// Builds the school initials used as a logo placeholder (e.g.
    /// "مدرسة الفلاح النموذجية" → "م.ف"). Falls back to the first character
    /// when the name has no spaces.
    /// </summary>
    internal static string ResolveSchoolInitials(string schoolName)
    {
        if (string.IsNullOrWhiteSpace(schoolName)) return "؟";
        var tokens = schoolName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return "؟";
        if (tokens.Length == 1) return tokens[0]!.Substring(0, Math.Min(2, tokens[0]!.Length));
        var sb = new System.Text.StringBuilder();
        foreach (var t in tokens.Take(3))
            if (!string.IsNullOrEmpty(t)) sb.Append(t[0]);
        return sb.Length == 0 ? "؟" : sb.ToString();
    }

    /// <summary>Maps an int score to its Arabic label verbatim from docs/09.</summary>
    private static string MapScoreLabelAr(int? score) => score switch
    {
        0 => "غير مشاهد",
        1 => "يحتاج تحسين",
        2 => "متحقق جزئياً",
        3 => "متحقق بدرجة جيدة",
        4 => "متميز",
        _ => "—"
    };

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Visit> LoadVisitAsync(int id, bool includeAnalysis = false, CancellationToken cancellationToken = default)
    {
        IQueryable<Visit> q = _context.Visits
            .Include(v => v.School)
                .ThenInclude(s => s.ReportSettings)
            .Include(v => v.Instructor)
            .Include(v => v.CreatedByUser)
            .Include(v => v.ApprovedByUser)
            .Include(v => v.RubricVersion)
            .Include(v => v.Scores).ThenInclude(s => s.RubricStandard).ThenInclude(std => std.Domain);

        if (includeAnalysis)
            q = q.Include(v => v.Analysis!).ThenInclude(a => a.DomainAverages);

        var visit = await q.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("الزيارة غير موجودة.");

        return visit;
    }

    private async Task<VisitDetailDto> MapDetailAsync(Visit visit)
    {
        VisitAnalysisDto? analysisDto = null;
        if (visit.Analysis != null)
        {
            analysisDto = new VisitAnalysisDto
            {
                Id = visit.Analysis.Id,
                VisitId = visit.Analysis.VisitId,
                OverallScore = visit.Analysis.OverallScore,
                PerformanceLevelAr = visit.Analysis.PerformanceLevelAr,
                Strengths = DeserializeList<VisitStrengthDto>(visit.Analysis.StrengthsJson),
                ImprovementAreas = DeserializeList<VisitImprovementDto>(visit.Analysis.ImprovementAreasJson),
                PriorityStandards = DeserializeList<VisitPriorityStandardDto>(visit.Analysis.PriorityStandardsJson),
                DomainAverages = visit.Analysis.DomainAverages
                    .OrderBy(d => d.DomainCode)
                    .Select(d => new VisitDomainAverageDto
                    {
                        Id = d.Id,
                        RubricDomainId = d.RubricDomainId,
                        DomainCode = d.DomainCode,
                        DomainNameAr = d.DomainNameAr,
                        AverageScore = d.AverageScore
                    }).ToList(),
                ComputedAt = visit.Analysis.ComputedAt
            };
        }

        return new VisitDetailDto
        {
            Id = visit.Id,
            SchoolId = visit.SchoolId,
            SchoolName = visit.School.Name,
            InstructorId = visit.InstructorId,
            InstructorFullName = visit.Instructor.FullName,
            CreatedByUserId = visit.CreatedByUserId,
            CreatedByFullName = visit.CreatedByUser.FullName,
            RubricVersionId = visit.RubricVersionId,
            RubricVersionNumber = visit.RubricVersion.VersionNumber,
            VisitCategory = ((int)visit.VisitCategory).ToString(),
            VisitCategoryLabelAr = visit.VisitCategory.ToArabicString(),
            VisitSequence = ((int)visit.VisitSequence).ToString(),
            VisitSequenceLabelAr = visit.VisitSequence.ToArabicString(),
            Status = ((int)visit.Status).ToString(),
            StatusLabelAr = StatusLabelAr(visit.Status),
            VisitDate = visit.VisitDate,
            Subject = visit.Subject,
            GradeClass = visit.GradeClass,
            Notes = visit.Notes,
            CreatedAt = visit.CreatedAt,
            UpdatedAt = visit.UpdatedAt,
            SubmittedAt = visit.SubmittedAt,
            ApprovedByUserId = visit.ApprovedByUserId,
            ApprovedByFullName = visit.ApprovedByUser?.FullName,
            ApprovedAt = visit.ApprovedAt,
            RejectionReason = visit.RejectionReason,
            ReopenReason = visit.ReopenReason,
            ReopenedByUserId = visit.ReopenedByUserId,
            ReopenedByFullName = visit.ReopenedByUser?.FullName,
            ReopenedAt = visit.ReopenedAt,
            IsReadOnly = ComputeIsReadOnly(visit),
            Scores = visit.Scores
                .OrderBy(s => s.RubricStandard.Domain.SortOrder)
                .ThenBy(s => s.RubricStandard.SortOrder)
                .Select(s => new VisitScoreDto
                {
                    Id = s.Id,
                    VisitId = s.VisitId,
                    RubricStandardId = s.RubricStandardId,
                    StandardCode = s.RubricStandard.Code,
                    StandardTextAr = s.RubricStandard.TextAr,
                    RubricDomainId = s.RubricStandard.Domain.Id,
                    DomainCode = s.RubricStandard.Domain.Code,
                    DomainNameAr = s.RubricStandard.Domain.NameAr,
                    Score = s.Score,
                    EvidenceNote = s.EvidenceNote
                }).ToList(),
            Analysis = analysisDto
        };
    }

    private async Task<int> ResolveTargetSchoolIdAsync(string instructorId, CancellationToken cancellationToken)
    {
        if (_currentUser.IsGlobalAdmin())
        {
            // Global admins must still pick a school — derive from the instructor.
            var adminInstructorAssignment = await _context.UserSchoolRoles
                .AsNoTracking()
                .Where(usr => usr.UserId == instructorId && usr.IsActive)
                .OrderBy(usr => usr.SchoolId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("المعلم المُختار غير مُعيَّن في أي مدرسة.");

            return adminInstructorAssignment.SchoolId;
        }

        // School-scoped callers: force to ActiveSchoolId.
        var active = _currentUser.ActiveSchoolId;
        if (active is null)
            throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة مرتبطة بحسابك.");

        return active.Value;
    }

    private async Task EnsureInstructorInSchoolAsync(string instructorId, int schoolId, CancellationToken cancellationToken)
    {
        var assignment = await _context.UserSchoolRoles
            .AsNoTracking()
            .Include(usr => usr.Role)
            .Where(usr => usr.UserId == instructorId
                       && usr.SchoolId == schoolId
                       && usr.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment == null)
            throw new InvalidOperationException(
                "المعلم المُختار غير مُعيَّن في هذه المدرسة بدور معلم نشط.");

        var roleName = assignment.Role.Name;
        if (roleName != RoleNames.Instructor)
            throw new InvalidOperationException(
                $"المستخدم المُختار ليس بدور معلم (الدور الحالي: {roleName}).");
    }

    private static List<T> DeserializeList<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<T>();
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    // ─── Phase 5 helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Phase 5 edit gate:
    ///  - Draft — always editable (Phase 4).
    ///  - RejectedForChanges — editable by the creator (Moderator / School Manager).
    ///  - Reopened — editable by the creator.
    ///  - PendingApproval — editable ONLY by the visit's School Manager (direct-edit
    ///    path) or by a global admin. Moderators cannot edit at this stage.
    ///  - Approved — read-only (use Reopen to make it editable).
    ///  - UnderReviewAfterComplaint / Cancelled — read-only.
    /// </summary>
    private void EnsureCanEdit(Visit visit)
    {
        switch (visit.Status)
        {
            case VisitStatus.Draft:
                return;

            case VisitStatus.RejectedForChanges:
            case VisitStatus.Reopened:
                // Editable by anyone who passed EnsureCanMutateSchoolAsync — that's
                // the visit's School Manager (or SuperAdmin / MainManager) and the
                // Moderator of that school (since their user-scope was already verified
                // by the school-scope guard).
                return;

            case VisitStatus.PendingApproval:
                // Direct-edit by the visit's School Manager (or a global admin).
                // Moderators cannot edit at PendingApproval — they must wait for the
                // SM to either approve or reject.
                if (_currentUser.IsGlobalAdmin())
                    return;

                if (_currentUser.IsInRole(RoleNames.SchoolManager))
                    return;

                throw new InvalidOperationException(
                    "لا يمكن تعديل الزيارة في حالة بانتظار الاعتماد إلا من قبل مدير المدرسة عبر المسار المباشر.");

            case VisitStatus.Approved:
                throw new InvalidOperationException(
                    "الزيارة معتمدة. استخدم إجراء إعادة الفتح لتعديلها.");

            default:
                throw new InvalidOperationException(
                    $"لا يمكن تعديل الزيارة في حالتها الحالية ({visit.Status}).");
        }
    }

    /// <summary>
    /// Authorization for Approve / Reject / Reopen:
    ///  - Global admins (SuperAdmin / MainManager) may always act.
    ///  - School Managers may act ONLY when their ActiveSchoolId matches the visit's school
    ///    (cross-school approvals are rejected with 403).
    ///  - Moderators / Instructors may never approve / reject / reopen.
    /// </summary>
    private async Task EnsureCanActAsSchoolManagerAsync(Visit visit, CancellationToken cancellationToken)
    {
        if (_currentUser.IsGlobalAdmin())
            return;

        if (!_currentUser.IsInRole(RoleNames.SchoolManager))
            throw new UnauthorizedSchoolAccessException(
                "هذا الإجراء متاح فقط لمدير المدرسة المعني بالزيارة.");

        await _scopeGuard.EnsureCanMutateSchoolAsync(visit.SchoolId, cancellationToken);
    }

    /// <summary>
    /// D-37: True when the caller is a Moderator AND NOT also a School Manager /
    /// Super Admin / Main Manager (defensive — a user could in theory carry
    /// multiple role claims via <c>UserSchoolRole</c> rows). Used by the read
    /// endpoints to scope results to "visits created by THIS moderator" only.
    /// </summary>
    private bool IsModeratorOnlyCaller()
    {
        if (!_currentUser.IsInRole(RoleNames.Moderator))
            return false;
        if (_currentUser.IsInRole(RoleNames.SchoolManager))
            return false;
        if (_currentUser.IsGlobalAdmin())
            return false;
        return true;
    }

    /// <summary>
    /// D-37: enforce Moderator own-visits-only visibility on a single visit
    /// (used by detail / analysis / view-status). The Moderator must be the
    /// visit's creator; cross-moderator access is rejected with 403 even
    /// within the same school. School Manager / Super Admin / Main Manager
    /// are exempt; Instructors use their own D-36 path.
    /// </summary>
    private void EnsureModeratorCanAccessCreatedByVisit(Visit visit)
    {
        if (!IsModeratorOnlyCaller())
            return;

        var currentUserId = _currentUser.UserId;
        if (string.IsNullOrEmpty(currentUserId))
            throw new UnauthorizedAccessException("يجب تسجيل الدخول لعرض الزيارة.");

        if (visit.CreatedByUserId != currentUserId)
        {
            _logger.LogWarning(
                "Moderator cross-record access denied: caller {UserId} attempted to access visit {VisitId} created by {OwnerId}.",
                currentUserId, visit.Id, visit.CreatedByUserId);
            throw new UnauthorizedSchoolAccessException(
                "لا تملك صلاحية الوصول إلى زيارات المشرفين الآخرين في مدرستك.");
        }
    }

    /// <summary>
    /// Writes an <see cref="AuditLog"/> row capturing the before/after state of an action.
    /// Caller is responsible for calling <c>SaveChangesAsync</c> afterward.
    /// </summary>
    private async Task WriteAuditAsync(
        Visit visit,
        string action,
        string reason,
        string? oldValues,
        string? newValues,
        CancellationToken cancellationToken)
    {
        var entry = new AuditLog
        {
            SchoolId = visit.SchoolId,
            UserId = _currentUser.UserId,
            Action = action,
            EntityName = nameof(Visit),
            EntityId = visit.Id.ToString(),
            OldValues = oldValues,
            NewValues = newValues,
            Reason = reason,
            CreatedAt = DateTimeOffset.UtcNow,
            IpAddress = TryResolveClientIp()
        };
        _context.AuditLogs.Add(entry);

        // AuditLog is also in the same SaveChanges unit-of-work, but we don't await
        // SaveChangesAsync here — the caller batches it. Adding it to the context is enough.
        await Task.CompletedTask;
    }

    /// <summary>
    /// Best-effort capture of the caller's IP. Reads X-Forwarded-For first (when behind a proxy),
    /// then falls back to the connection's remote IP. Returns null on any failure.
    /// Failing to capture an IP is NOT a hard failure for audit — the AuditLog row is
    /// still written with a null IP.
    /// </summary>
    private string? TryResolveClientIp()
    {
        try
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null) return null;
            var xff = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(xff))
                return xff.Split(',')[0].Trim();
            return ctx.Connection?.RemoteIpAddress?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Phase 5 update of IsReadOnly semantics:
    ///  - Draft → false (Phase 4 behaviour).
    ///  - RejectedForChanges / Reopened → false (creator can edit again).
    ///  - PendingApproval → false ONLY for the visit's School Manager / global admin
    ///    (direct-edit path); true otherwise.
    ///  - Approved / UnderReviewAfterComplaint / Cancelled → true (read-only).
    /// </summary>
    private bool ComputeIsReadOnly(Visit visit)
    {
        switch (visit.Status)
        {
            case VisitStatus.Draft:
            case VisitStatus.RejectedForChanges:
            case VisitStatus.Reopened:
                return false;

            case VisitStatus.PendingApproval:
                return !(_currentUser.IsGlobalAdmin() || _currentUser.IsInRole(RoleNames.SchoolManager));

            default:
                return true;
        }
    }
}