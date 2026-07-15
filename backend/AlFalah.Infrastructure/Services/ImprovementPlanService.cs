using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.ImprovementPlans;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

public class ImprovementPlanService : IImprovementPlanService
{
    private readonly AlFalahDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly ILogger<ImprovementPlanService> _logger;

    public ImprovementPlanService(
        AlFalahDbContext context,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard,
        ILogger<ImprovementPlanService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _logger = logger;
    }

    // ─── Queries ──────────────────────────────────────────────────────────────

    public async Task<List<ImprovementPlanDto>> GetPlansForVisitAsync(int visitId, CancellationToken cancellationToken = default)
    {
        var visit = await _context.Visits
            .Include(v => v.School)
            .Include(v => v.Instructor)
            .Include(v => v.CreatedByUser)
            .FirstOrDefaultAsync(v => v.Id == visitId, cancellationToken)
            ?? throw new KeyNotFoundException("الزيارة غير موجودة.");

        EnsureUserCanAccessVisit(visit);

        var plans = await _context.ImprovementPlans
            .Include(p => p.School)
            .Include(p => p.Instructor)
            .Include(p => p.Domain)
            .Include(p => p.CreatedByUser)
            .Where(p => p.VisitId == visitId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = new List<ImprovementPlanDto>();
        foreach (var plan in plans)
        {
            dtos.Add(await MapPlanAsync(plan, cancellationToken));
        }

        return dtos;
    }

    public async Task<ImprovementPlanDto> GetPlanByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var plan = await _context.ImprovementPlans
            .Include(p => p.School)
            .Include(p => p.Instructor)
            .Include(p => p.Domain)
            .Include(p => p.CreatedByUser)
            .Include(p => p.Visit)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("خطة التحسين غير موجودة.");

        EnsureUserCanAccessVisit(plan.Visit);

        return await MapPlanAsync(plan, cancellationToken);
    }

    public async Task<List<WeakDomainSuggestionDto>> GetWeakDomainSuggestionsAsync(int visitId, CancellationToken cancellationToken = default)
    {
        var visit = await _context.Visits
            .Include(v => v.School)
            .Include(v => v.Analysis!).ThenInclude(a => a.DomainAverages)
            .FirstOrDefaultAsync(v => v.Id == visitId, cancellationToken)
            ?? throw new KeyNotFoundException("الزيارة غير موجودة.");

        EnsureUserCanAccessVisit(visit);

        if (visit.Analysis == null)
        {
            return new List<WeakDomainSuggestionDto>();
        }

        // Weak domain definition: average < 2.5
        var weakAverages = visit.Analysis.DomainAverages
            .Where(da => da.AverageScore < 2.5m)
            .ToList();

        var suggestions = new List<WeakDomainSuggestionDto>();

        foreach (var da in weakAverages)
        {
            var suggestion = new WeakDomainSuggestionDto
            {
                DomainId = da.RubricDomainId,
                DomainCode = da.DomainCode,
                DomainNameAr = da.DomainNameAr,
                AverageScore = da.AverageScore
            };

            var domainName = da.DomainNameAr.Trim();
            if (domainName == "بيئة التعلم")
            {
                suggestion.PrefilledGoal = "تحسين جودة بيئة التعلم وجعلها أكثر إثراءً وفاعلية للمتعلمين";
                suggestion.PrefilledActions = "- مراجعة توزيع المقاعد وترتيب الغرفة الصفية\n- إضافة مصادر تعلم متنوعة ومناسبة\n- تعزيز جانب القيم والهوية الوطنية في الديكور التعليمي\n- تطبيق استراتيجيات إدارة الوقت الصفي";
                suggestion.PrefilledSuccessIndicators = "ارتفاع متوسط درجات نطاق بيئة التعلم إلى 3.0 أو أعلى في الزيارة القادمة";
            }
            else if (domainName == "التدريس والتعلم")
            {
                suggestion.PrefilledGoal = "تطوير استراتيجيات التدريس وتنويعها لتحقيق نواتج التعلم المستهدفة";
                suggestion.PrefilledActions = "- حضور دورة تدريبية في استراتيجيات التدريس الحديثة\n- تطبيق التعلم التعاوني في الحصص\n- استخدام التقنية الرقمية في شرح المفاهيم\n- ربط المحتوى بحياة الطلاب اليومية";
                suggestion.PrefilledSuccessIndicators = "تنفيذ 3 استراتيجيات تدريس مختلفة خلال شهر والحصول على تغذية راجعة إيجابية من المشرف";
            }
            else if (domainName == "تنمية المهارات")
            {
                suggestion.PrefilledGoal = "تعزيز تنمية مهارات التفكير العليا والمهارات الحياتية لدى المتعلمين";
                suggestion.PrefilledActions = "- تصميم أنشطة تعلم تستهدف مهارات التفكير الناقد\n- إدراج مشاريع بحثية صغيرة ضمن الخطة الدراسية\n- تشجيع المتعلمين على طرح الأسئلة والتساؤل\n- دمج التعلم الذاتي في الأنشطة اليومية";
                suggestion.PrefilledSuccessIndicators = "لاحظ المشرف زيادة ملموسة في مشاركة الطلاب وأسئلتهم التحليلية خلال الزيارة القادمة";
            }
            else if (domainName == "التقويم")
            {
                suggestion.PrefilledGoal = "تنويع أساليب التقويم وتفعيل التغذية الراجعة البنائية";
                suggestion.PrefilledActions = "- إعداد خطة تقويم تشمل التشخيصي والبنائي والختامي\n- استخدام بطاقات الخروج والاستبانات القصيرة\n- تقديم تغذية راجعة فورية وبنائية لكل طالب\n- توثيق نتائج التقويم وتحليلها";
                suggestion.PrefilledSuccessIndicators = "تطبيق ثلاثة أدوات تقويم مختلفة في كل وحدة دراسية وتوثيق نتائجها";
            }
            else if (domainName == "سلوك المتعلمين")
            {
                suggestion.PrefilledGoal = "تعزيز الانضباط الإيجابي وتنمية الاستقلالية والمسؤولية لدى المتعلمين";
                suggestion.PrefilledActions = "- وضع قواعد صفية واضحة بمشاركة الطلاب\n- تطبيق نظام تحفيز إيجابي ومتنوع\n- تعزيز مفهوم التعلم الذاتي والمسؤولية\n- إجراء أنشطة تعزز الهوية الوطنية والانتماء";
                suggestion.PrefilledSuccessIndicators = "انخفاض ملحوظ في سلوكيات الإزعاج وزيادة مشاركة الطلاب الطوعية في الأنشطة";
            }
            else
            {
                suggestion.PrefilledGoal = $"تحسين الأداء في مجال {domainName}";
                suggestion.PrefilledActions = "- تحديد نقاط الضعف المحددة\n- وضع خطة عمل واضحة\n- الالتزام بالتطبيق والمتابعة";
                suggestion.PrefilledSuccessIndicators = $"ارتفاع متوسط درجات نطاق {domainName} في الزيارة القادمة";
            }

            suggestions.Add(suggestion);
        }

        return suggestions;
    }

    public async Task<PlanProgressDto> GetPlanProgressAsync(int planId, CancellationToken cancellationToken = default)
    {
        var plan = await _context.ImprovementPlans
            .Include(p => p.Visit)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken)
            ?? throw new KeyNotFoundException("خطة التحسين غير موجودة.");

        EnsureUserCanAccessVisit(plan.Visit);

        var followUps = await _context.PlanFollowUps
            .Where(f => f.ImprovementPlanId == planId)
            .OrderByDescending(f => f.FollowDate).ThenByDescending(f => f.Id)
            .ToListAsync(cancellationToken);

        // Latest score: first scored follow-up in FollowDate descending order
        var latestScoredFollowUp = followUps.FirstOrDefault(f => f.ProgressScore.HasValue);
        int? latestScore = latestScoredFollowUp?.ProgressScore;
        string? color = null;
        if (latestScore.HasValue)
        {
            if (latestScore.Value >= 75)
                color = "success";
            else if (latestScore.Value >= 50)
                color = "warning";
            else
                color = "danger";
        }

        // Chart data: chronological (FollowDate ascending), only scored rows
        var chartData = followUps
            .Where(f => f.ProgressScore.HasValue)
            .OrderBy(f => f.FollowDate).ThenBy(f => f.Id)
            .Select(f => new ChartPointDto
            {
                FollowDate = f.FollowDate,
                ProgressScore = f.ProgressScore!.Value
            })
            .ToList();

        return new PlanProgressDto
        {
            LatestProgressScore = latestScore,
            LatestProgressColor = color,
            ChartData = chartData
        };
    }

    // ─── Mutations ────────────────────────────────────────────────────────────

    public async Task<ImprovementPlanDto> CreatePlanAsync(CreatePlanRequestDto request, CancellationToken cancellationToken = default)
    {
        var visit = await _context.Visits
            .Include(v => v.School)
            .FirstOrDefaultAsync(v => v.Id == request.VisitId, cancellationToken)
            ?? throw new KeyNotFoundException("الزيارة غير موجودة.");

        await EnsureUserCanMutateVisitAsync(visit, cancellationToken);

        if (request.DomainId.HasValue)
        {
            var domainBelongsToVisitRubric = await _context.RubricDomains
                .AnyAsync(
                    domain => domain.Id == request.DomainId.Value
                        && domain.RubricVersionId == visit.RubricVersionId,
                    cancellationToken);

            if (!domainBelongsToVisitRubric)
            {
                throw new InvalidOperationException(
                    "النطاق المحدد لا ينتمي إلى نسخة أداة التقييم الخاصة بهذه الزيارة.");
            }
        }

        var plan = new ImprovementPlan
        {
            SchoolId = visit.SchoolId,
            InstructorId = visit.InstructorId,
            VisitId = visit.Id,
            DomainId = request.DomainId,
            Goal = request.Goal,
            Actions = request.Actions,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            SuccessIndicators = request.SuccessIndicators,
            Status = PlanStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = _currentUser.UserId ?? throw new InvalidOperationException("المستخدم الحالي غير موجود.")
        };

        _context.ImprovementPlans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Plan created: id={PlanId} school={SchoolId} instructor={InstructorId} visit={VisitId} by={UserId}",
            plan.Id, plan.SchoolId, plan.InstructorId, plan.VisitId, _currentUser.UserId);

        return await GetPlanByIdAsync(plan.Id, cancellationToken);
    }

    public async Task<ImprovementPlanDto> UpdatePlanAsync(int id, UpdatePlanRequestDto request, CancellationToken cancellationToken = default)
    {
        var plan = await _context.ImprovementPlans
            .Include(p => p.Visit)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("خطة التحسين غير موجودة.");

        await EnsureUserCanMutateVisitAsync(plan.Visit, cancellationToken);
        EnsurePlanIsActive(plan);

        plan.Goal = request.Goal;
        plan.Actions = request.Actions;
        plan.StartDate = request.StartDate;
        plan.EndDate = request.EndDate;
        plan.SuccessIndicators = request.SuccessIndicators;
        
        if (Enum.TryParse<PlanStatus>(request.Status, true, out var parsedStatus))
        {
            plan.Status = parsedStatus;
        }

        plan.UpdatedByUserId = _currentUser.UserId;
        plan.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Plan updated: id={PlanId} school={SchoolId} visit={VisitId} by={UserId}",
            plan.Id, plan.SchoolId, plan.VisitId, _currentUser.UserId);

        return await GetPlanByIdAsync(plan.Id, cancellationToken);
    }

    public async Task<ImprovementPlanDto> ReactivatePlanAsync(int id, CancellationToken cancellationToken = default)
    {
        var plan = await _context.ImprovementPlans
            .Include(p => p.Visit)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("خطة التحسين غير موجودة.");

        await EnsureUserCanMutateVisitAsync(plan.Visit, cancellationToken);
        if (plan.Status == PlanStatus.Active)
            throw new InvalidOperationException("خطة التحسين نشطة بالفعل.");

        plan.Status = PlanStatus.Active;
        plan.UpdatedByUserId = _currentUser.UserId;
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Plan explicitly reactivated: id={PlanId} school={SchoolId} visit={VisitId} by={UserId}",
            plan.Id, plan.SchoolId, plan.VisitId, _currentUser.UserId);

        return await GetPlanByIdAsync(plan.Id, cancellationToken);
    }

    public async Task SoftDeletePlanAsync(int id, CancellationToken cancellationToken = default)
    {
        var plan = await _context.ImprovementPlans
            .Include(p => p.Visit)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("خطة التحسين غير موجودة.");

        await EnsureUserCanMutateVisitAsync(plan.Visit, cancellationToken);
        EnsurePlanIsActive(plan);

        var now = DateTimeOffset.UtcNow;
        var userId = _currentUser.UserId ?? "system";

        plan.IsDeleted = true;
        plan.DeletedAt = now;
        plan.DeletedByUserId = userId;
        plan.UpdatedAt = now;

        // Cascade soft delete follow-ups to keep data consistent (they survive in DB with IsDeleted=true)
        var followUps = await _context.PlanFollowUps
            .Where(f => f.ImprovementPlanId == id)
            .ToListAsync(cancellationToken);

        foreach (var f in followUps)
        {
            f.IsDeleted = true;
            f.DeletedAt = now;
            f.DeletedByUserId = userId;
            f.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Plan soft-deleted: id={PlanId} school={SchoolId} visit={VisitId} by={UserId}",
            plan.Id, plan.SchoolId, plan.VisitId, userId);
    }

    public async Task<PlanFollowUpDto> AddFollowUpAsync(int planId, CreateFollowUpRequestDto request, CancellationToken cancellationToken = default)
    {
        var plan = await _context.ImprovementPlans
            .Include(p => p.Visit)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken)
            ?? throw new KeyNotFoundException("خطة التحسين غير موجودة.");

        await EnsureUserCanMutateVisitAsync(plan.Visit, cancellationToken);
        EnsurePlanIsActive(plan);

        var followUp = new PlanFollowUp
        {
            ImprovementPlanId = planId,
            FollowDate = request.FollowDate,
            ProgressNote = request.ProgressNote,
            EvidenceNote = request.EvidenceNote,
            ProgressScore = request.ProgressScore,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = _currentUser.UserId ?? throw new InvalidOperationException("المستخدم الحالي غير موجود.")
        };

        _context.PlanFollowUps.Add(followUp);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Follow-up added: id={FollowUpId} plan={PlanId} by={UserId}",
            followUp.Id, planId, _currentUser.UserId);

        var freshFollowUp = await _context.PlanFollowUps
            .Include(f => f.CreatedByUser)
            .FirstOrDefaultAsync(f => f.Id == followUp.Id, cancellationToken)
            ?? throw new KeyNotFoundException("المتابعة غير موجودة.");

        return MapFollowUpDto(freshFollowUp);
    }

    public async Task<PlanFollowUpDto> UpdateFollowUpAsync(int id, UpdateFollowUpRequestDto request, CancellationToken cancellationToken = default)
    {
        var followUp = await _context.PlanFollowUps
            .Include(f => f.ImprovementPlan).ThenInclude(p => p.Visit)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("المتابعة غير موجودة.");

        await EnsureUserCanMutateVisitAsync(followUp.ImprovementPlan.Visit, cancellationToken);
        EnsurePlanIsActive(followUp.ImprovementPlan);

        followUp.FollowDate = request.FollowDate;
        followUp.ProgressNote = request.ProgressNote;
        followUp.EvidenceNote = request.EvidenceNote;
        followUp.ProgressScore = request.ProgressScore;
        followUp.UpdatedByUserId = _currentUser.UserId;
        followUp.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Follow-up updated: id={FollowUpId} plan={PlanId} by={UserId}",
            followUp.Id, followUp.ImprovementPlanId, _currentUser.UserId);

        var freshFollowUp = await _context.PlanFollowUps
            .Include(f => f.CreatedByUser)
            .FirstOrDefaultAsync(f => f.Id == followUp.Id, cancellationToken)
            ?? throw new KeyNotFoundException("المتابعة غير موجودة.");

        return MapFollowUpDto(freshFollowUp);
    }

    public async Task SoftDeleteFollowUpAsync(int id, CancellationToken cancellationToken = default)
    {
        var followUp = await _context.PlanFollowUps
            .Include(f => f.ImprovementPlan).ThenInclude(p => p.Visit)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("المتابعة غير موجودة.");

        await EnsureUserCanMutateVisitAsync(followUp.ImprovementPlan.Visit, cancellationToken);
        EnsurePlanIsActive(followUp.ImprovementPlan);

        var now = DateTimeOffset.UtcNow;
        var userId = _currentUser.UserId ?? "system";

        followUp.IsDeleted = true;
        followUp.DeletedAt = now;
        followUp.DeletedByUserId = userId;
        followUp.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Follow-up soft-deleted: id={FollowUpId} plan={PlanId} by={UserId}",
            followUp.Id, followUp.ImprovementPlanId, userId);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private void EnsureUserCanAccessVisit(Visit visit)
    {
        // 1. Cross-school check (D-24/D-28)
        var effectiveSchoolId = _scopeGuard.ResolveAllowedSchoolId(visit.SchoolId);
        if (effectiveSchoolId.HasValue && effectiveSchoolId.Value != visit.SchoolId)
        {
            throw new UnauthorizedSchoolAccessException(
                $"لا تملك صلاحية الوصول إلى بيانات خارج المدرسة الحالية ({effectiveSchoolId}).");
        }

        // 2. Moderator own-visits-only (D-37)
        if (IsModeratorOnlyCaller())
        {
            var currentUserId = _currentUser.UserId;
            if (string.IsNullOrEmpty(currentUserId))
                throw new UnauthorizedAccessException("يجب تسجيل الدخول لعرض البيانات.");

            if (visit.CreatedByUserId != currentUserId)
            {
                throw new UnauthorizedSchoolAccessException(
                    "لا تملك صلاحية الوصول إلى خطط تحسين زيارات مشرفين آخرين.");
            }
        }

        // 3. Instructor own-visits-only and must be Approved (D-36)
        if (_currentUser.IsInRole(RoleNames.Instructor)
            && !_currentUser.IsInRole(RoleNames.SchoolManager)
            && !_currentUser.IsInRole(RoleNames.Moderator)
            && !_currentUser.IsGlobalAdmin())
        {
            var currentUserId = _currentUser.UserId;
            if (string.IsNullOrEmpty(currentUserId))
                throw new UnauthorizedAccessException("يجب تسجيل الدخول لعرض البيانات.");

            if (visit.InstructorId != currentUserId)
            {
                throw new UnauthorizedSchoolAccessException(
                    "لا تملك صلاحية الوصول إلى سجلات المعلمين الآخرين.");
            }

            if (visit.Status != VisitStatus.Approved)
            {
                throw new UnauthorizedSchoolAccessException(
                    "لا يمكنك الاطلاع على خطط التحسين لزيارة غير معتمدة بعد.");
            }
        }
    }

    private async Task EnsureUserCanMutateVisitAsync(Visit visit, CancellationToken cancellationToken)
    {
        // 1. Can mutate school
        await _scopeGuard.EnsureCanMutateSchoolAsync(visit.SchoolId, cancellationToken);

        // 2. Moderator can only mutate visits HE created (D-37)
        if (IsModeratorOnlyCaller())
        {
            var currentUserId = _currentUser.UserId;
            if (visit.CreatedByUserId != currentUserId)
            {
                throw new UnauthorizedSchoolAccessException(
                    "لا تملك صلاحية تعديل بيانات مشرفين آخرين.");
            }
        }
        
        // 3. Instructor cannot mutate
        if (_currentUser.IsInRole(RoleNames.Instructor)
            && !_currentUser.IsInRole(RoleNames.SchoolManager)
            && !_currentUser.IsInRole(RoleNames.Moderator)
            && !_currentUser.IsGlobalAdmin())
        {
            throw new UnauthorizedSchoolAccessException("المعلمين لا يملكون صلاحية تعديل خطط التحسين.");
        }
    }

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

    private async Task<ImprovementPlanDto> MapPlanAsync(ImprovementPlan plan, CancellationToken cancellationToken)
    {
        var followUps = await _context.PlanFollowUps
            .Include(f => f.CreatedByUser)
            .Where(f => f.ImprovementPlanId == plan.Id)
            .OrderByDescending(f => f.FollowDate).ThenByDescending(f => f.Id)
            .ToListAsync(cancellationToken);

        var followUpDtos = followUps.Select(MapFollowUpDto).ToList();

        return new ImprovementPlanDto
        {
            Id = plan.Id,
            SchoolId = plan.SchoolId,
            SchoolName = plan.School?.Name ?? string.Empty,
            InstructorId = plan.InstructorId,
            InstructorFullName = plan.Instructor != null ? $"{plan.Instructor.FirstName} {plan.Instructor.LastName}" : string.Empty,
            VisitId = plan.VisitId,
            DomainId = plan.DomainId,
            DomainNameAr = plan.Domain?.NameAr,
            Goal = plan.Goal,
            Actions = plan.Actions,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            SuccessIndicators = plan.SuccessIndicators,
            Status = plan.Status.ToString().ToLower(),
            CreatedAt = plan.CreatedAt,
            CreatedByUserId = plan.CreatedByUserId,
            CreatedByFullName = plan.CreatedByUser != null ? $"{plan.CreatedByUser.FirstName} {plan.CreatedByUser.LastName}" : string.Empty,
            UpdatedAt = plan.UpdatedAt,
            IsReadOnly = ComputeIsReadOnly(plan),
            FollowUps = followUpDtos
        };
    }

    private PlanFollowUpDto MapFollowUpDto(PlanFollowUp f)
    {
        return new PlanFollowUpDto
        {
            Id = f.Id,
            ImprovementPlanId = f.ImprovementPlanId,
            FollowDate = f.FollowDate,
            ProgressNote = f.ProgressNote,
            EvidenceNote = f.EvidenceNote,
            ProgressScore = f.ProgressScore,
            CreatedAt = f.CreatedAt,
            CreatedByUserId = f.CreatedByUserId,
            CreatedByFullName = f.CreatedByUser != null ? $"{f.CreatedByUser.FirstName} {f.CreatedByUser.LastName}" : string.Empty
        };
    }

    private bool ComputeIsReadOnly(ImprovementPlan plan)
    {
        if (plan.Status != PlanStatus.Active) return true;

        if (_currentUser.IsInRole(RoleNames.Instructor)
            && !_currentUser.IsInRole(RoleNames.SchoolManager)
            && !_currentUser.IsInRole(RoleNames.Moderator)
            && !_currentUser.IsGlobalAdmin())
        {
            return true;
        }

        if (IsModeratorOnlyCaller())
        {
            return plan.CreatedByUserId != _currentUser.UserId;
        }

        if (_currentUser.IsGlobalAdmin()) return false;

        var active = _currentUser.ActiveSchoolId;
        if (active is null || active.Value != plan.SchoolId) return true;

        return false;
    }

    private static void EnsurePlanIsActive(ImprovementPlan plan)
    {
        if (plan.Status != PlanStatus.Active)
            throw new InvalidOperationException(
                "الخطة المكتملة أو الملغاة للقراءة فقط. أعد تنشيط الخطة صراحةً قبل تعديلها أو إضافة متابعة.");
    }
}
