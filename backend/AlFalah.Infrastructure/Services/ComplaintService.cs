using System.Text.Json;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Complaints;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Phase 8 — complaints workflow. See <see cref="IComplaintService"/> for the
/// contract. Every rule is enforced HERE (never trust the frontend — D-24 class):
///  - Submission: instructor-own + visit Approved + report viewed (ReportViewLog).
///  - Visibility: SM = school, Instructor = own, SuperAdmin = global.
///    **MainManager = HARD 403** AND **Moderator = HARD 403** (D-75) on every
///    read/write — even if a Complaint.* permission were ever leaked to those
///    roles. SuperAdmin support is the only Moderator-role exception.
///  - Status machine: Open → InReview → Resolved | Rejected → Closed.
///  - Reopen-from-complaint delegates to Phase 5 <see cref="IVisitService.ReopenAsync"/>
///    so the state machine / audit / same-RubricVersionId recompute are reused
///    verbatim (never rewrite working code).
/// </summary>
public class ComplaintService : IComplaintService
{
    private readonly AlFalahDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly IVisitService _visitService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ComplaintService> _logger;

    public ComplaintService(
        AlFalahDbContext context,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard,
        IVisitService visitService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ComplaintService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _visitService = visitService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    // ─── State machine ──────────────────────────────────────────────

    private static readonly Dictionary<ComplaintStatus, ComplaintStatus[]> AllowedTransitions = new()
    {
        [ComplaintStatus.Open]     = new[] { ComplaintStatus.InReview },
        [ComplaintStatus.InReview] = new[] { ComplaintStatus.Resolved, ComplaintStatus.Rejected },
        [ComplaintStatus.Resolved] = new[] { ComplaintStatus.Closed },
        [ComplaintStatus.Rejected] = new[] { ComplaintStatus.Closed },
        [ComplaintStatus.Closed]   = Array.Empty<ComplaintStatus>()
    };

    internal static string StatusLabelAr(ComplaintStatus s) => s switch
    {
        ComplaintStatus.Open     => "مفتوحة",
        ComplaintStatus.InReview => "قيد المراجعة",
        ComplaintStatus.Resolved => "تم حلها",
        ComplaintStatus.Rejected => "مرفوضة",
        ComplaintStatus.Closed   => "مغلقة",
        _ => s.ToString()
    };

    // ─── Commands ────────────────────────────────────────────────────

    public async Task<ComplaintDto> CreateAsync(int visitId, CreateComplaintRequestDto request, CancellationToken cancellationToken = default)
    {
        EnsureNotMainManager();
        EnsureNotModerator();
        if (!_currentUser.IsInRole(RoleNames.Instructor))
            throw new UnauthorizedSchoolAccessException("تقديم الشكوى متاح للمعلم صاحب التقرير فقط.");

        var currentUserId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول لتقديم شكوى.");

        var visit = await _context.Visits
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == visitId, cancellationToken)
            ?? throw new KeyNotFoundException("الزيارة غير موجودة.");

        // D-36-consistent: ONLY the visit's own Instructor may complain.
        if (visit.InstructorId != currentUserId)
            throw new UnauthorizedSchoolAccessException("لا يمكنك تقديم شكوى على زيارة لا تخصك.");

        if (visit.Status != VisitStatus.Approved)
            throw new InvalidOperationException("لا يمكن تقديم شكوى إلا على زيارة معتمدة.");

        // The instructor must have VIEWED the approved report first (Phase 5
        // ReportViewLog is the proof — the /report + /report/pdf endpoints
        // write it on every successful instructor view).
        var hasViewed = await _context.ReportViewLogs
            .AsNoTracking()
            .AnyAsync(l => l.VisitId == visitId && l.InstructorUserId == currentUserId, cancellationToken);
        if (!hasViewed)
            throw new InvalidOperationException("لا يمكن تقديم شكوى قبل الاطلاع على تقرير الزيارة.");

        // School-scope (defense in depth — the instructor's ActiveSchoolId must
        // match the visit's school; D-24/D-28 class).
        await _scopeGuard.EnsureCanMutateSchoolAsync(visit.SchoolId, cancellationToken);

        var complaint = new Complaint
        {
            SchoolId = visit.SchoolId,
            VisitId = visit.Id,
            InstructorUserId = currentUserId,
            // Snapshot the visit's evaluator for complaint audit/handling context.
            // This identifier never grants the Moderator complaint visibility (D-75).
            ModeratorUserId = visit.CreatedByUserId,
            Subject = request.Subject.Trim(),
            Body = request.Body.Trim(),
            Status = ComplaintStatus.Open,
            CreatedByUserId = currentUserId
        };

        _context.Complaints.Add(complaint);
        await _context.SaveChangesAsync(cancellationToken); // materialize Id for the audit row

        WriteAudit(complaint, "Complaint.Create", "تقديم شكوى / طلب مراجعة",
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { complaint.Id, complaint.VisitId, complaint.Subject, status = (int)complaint.Status }));
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Complaint created: id={ComplaintId} visit={VisitId} by={UserId}", complaint.Id, visitId, currentUserId);

        return await GetByIdAsync(complaint.Id, cancellationToken);
    }

    public async Task<List<ComplaintDto>> ListAsync(int? status, CancellationToken cancellationToken = default)
    {
        EnsureNotMainManager();
        EnsureNotModerator();

        var q = _context.Complaints
            .AsNoTracking()
            .Include(c => c.School)
            .Include(c => c.Visit)
            .Include(c => c.Instructor)
            .Include(c => c.Moderator)
            .Include(c => c.HandledByUser)
            .AsQueryable();

        if (!_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            var active = _currentUser.ActiveSchoolId
                ?? throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة مرتبطة بحسابك. يرجى إعادة تسجيل الدخول.");
            q = q.Where(c => c.SchoolId == active);

            var currentUserId = _currentUser.UserId
                ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول لعرض الشكاوى.");

            if (_currentUser.IsInRole(RoleNames.SchoolManager))
            {
                // School Manager: ALL complaints in HIS school — no extra filter.
            }
            else if (_currentUser.IsInRole(RoleNames.Instructor))
            {
                q = q.Where(c => c.InstructorUserId == currentUserId);
            }
            else
            {
                throw new UnauthorizedSchoolAccessException("دور المستخدم غير معروف ضمن سياقات الأمان.");
            }
        }

        if (status.HasValue)
            q = q.Where(c => c.Status == (ComplaintStatus)status.Value);

        var rows = await q
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(MapDto).ToList();
    }

    public async Task<ComplaintDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureNotMainManager();
        EnsureNotModerator();
        var complaint = await LoadComplaintAsync(id, cancellationToken);
        EnsureCanAccess(complaint);
        return MapDto(complaint);
    }

    public async Task<ComplaintDto> UpdateStatusAsync(int id, UpdateComplaintStatusRequestDto request, CancellationToken cancellationToken = default)
    {
        var complaint = await LoadComplaintAsync(id, cancellationToken, track: true);
        EnsureCanHandle(complaint);

        var target = (ComplaintStatus)request.Status;
        if (!Enum.IsDefined(typeof(ComplaintStatus), target))
            throw new InvalidOperationException("حالة الشكوى غير صالحة.");

        if (!AllowedTransitions.TryGetValue(complaint.Status, out var allowed) || !allowed.Contains(target))
            throw new InvalidOperationException(
                $"لا يمكن نقل الشكوى من حالة «{StatusLabelAr(complaint.Status)}» إلى حالة «{StatusLabelAr(target)}».");

        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول.");
        var now = DateTimeOffset.UtcNow;
        var oldStatus = complaint.Status;

        complaint.Status = target;
        complaint.HandledByUserId = userId;
        complaint.HandledAt = now;
        complaint.UpdatedByUserId = userId;
        if (!string.IsNullOrWhiteSpace(request.ResolutionNote))
            complaint.ResolutionNote = request.ResolutionNote.Trim();

        WriteAudit(complaint, "Complaint.StatusChange",
            reason: complaint.ResolutionNote ?? $"تغيير حالة الشكوى إلى {StatusLabelAr(target)}",
            oldValues: JsonSerializer.Serialize(new { status = (int)oldStatus }),
            newValues: JsonSerializer.Serialize(new { status = (int)target, handledByUserId = userId, handledAt = now, resolutionNote = complaint.ResolutionNote }));

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Complaint status changed: id={ComplaintId} {Old}->{New} by={UserId}", id, oldStatus, target, userId);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ComplaintDto> ReopenVisitAsync(int id, ReopenVisitFromComplaintRequestDto request, CancellationToken cancellationToken = default)
    {
        var complaint = await LoadComplaintAsync(id, cancellationToken, track: true);
        EnsureCanHandle(complaint);

        if (complaint.Status == ComplaintStatus.Closed)
            throw new InvalidOperationException("لا يمكن إعادة فتح الزيارة من شكوى مغلقة.");

        var reason = request.Reason.Trim();
        if (reason.Length == 0)
            throw new InvalidOperationException("سبب إعادة الفتح مطلوب.");

        // Reuse Phase 5 reopen verbatim: enforces Approved-only + SM scope +
        // writes the Visit.Reopen audit row. The resubmit later recomputes the
        // analysis snapshot on the SAME RubricVersionId (Phase 5 SubmitAsync).
        // The reason is prefixed with the complaint reference so the reopen is
        // permanently linked to this complaint in the visit history.
        await _visitService.ReopenAsync(complaint.VisitId, $"شكوى رقم {complaint.Id}: {reason}", cancellationToken);

        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول.");
        var now = DateTimeOffset.UtcNow;
        var oldStatus = complaint.Status;

        complaint.VisitReopenedAt = now;
        complaint.VisitReopenReason = reason;
        if (complaint.Status == ComplaintStatus.Open)
            complaint.Status = ComplaintStatus.InReview;
        complaint.HandledByUserId = userId;
        complaint.HandledAt = now;
        complaint.UpdatedByUserId = userId;

        WriteAudit(complaint, "Complaint.ReopenVisit",
            reason: reason,
            oldValues: JsonSerializer.Serialize(new { status = (int)oldStatus }),
            newValues: JsonSerializer.Serialize(new { status = (int)complaint.Status, visitId = complaint.VisitId, visitReopenedAt = now }));

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Visit reopened from complaint: complaint={ComplaintId} visit={VisitId} by={UserId}", id, complaint.VisitId, userId);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var complaint = await LoadComplaintAsync(id, cancellationToken, track: true);
        EnsureCanHandle(complaint); // SM (own school) / SuperAdmin; MainManager and Moderator hard-blocked.

        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول.");
        var now = DateTimeOffset.UtcNow;

        complaint.IsDeleted = true;
        complaint.DeletedAt = now;
        complaint.DeletedByUserId = userId;

        WriteAudit(complaint, "Complaint.Delete", "حذف الشكوى (حذف منطقي)",
            oldValues: JsonSerializer.Serialize(new { isDeleted = false }),
            newValues: JsonSerializer.Serialize(new { isDeleted = true, deletedAt = now }));

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Complaint soft-deleted: id={ComplaintId} by={UserId}", id, userId);
    }

    // ─── Authorization helpers ──────────────────────────────────────────

    /// <summary>
    /// HEADLINE Phase-8 rule: Main Manager must NEVER see complaint details —
    /// hard 403 even though the role is otherwise global, and even if a
    /// Complaint.* permission were ever leaked to it. SuperAdmin (support) is
    /// exempt; a user carrying BOTH roles is treated as SuperAdmin.
    /// </summary>
    private void EnsureNotMainManager()
    {
        if (_currentUser.IsInRole(RoleNames.MainManager) && !_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            _logger.LogWarning("MainManager blocked from complaints: user={UserId}", _currentUser.UserId);
            throw new UnauthorizedSchoolAccessException(
                "الاطلاع على تفاصيل الشكاوى غير متاح لمدير المدارس العام — الشكاوى خاصة بالمدرسة المعنية.");
        }
    }

    /// <summary>
    /// D-75 — Moderator must NEVER see complaint details either. The Moderator
    /// had a partial "own-visit" scope (visit creator) but the product decision
    /// is now a hard block: a Moderator has no business with complaints at all
    /// (he does not handle them — that is the School Manager — and showing him
    /// counts/subjects/summaries was leaking complaint content into a role that
    /// is otherwise focused on visits/plans). This mirrors the Main-Manager
    /// block: belt (no Complaint.* permission is seeded for Moderator) +
    /// suspenders (this hard 403 even if a permission ever leaks). SuperAdmin
    /// is the only exception because it is the documented support role.
    /// </summary>
    private void EnsureNotModerator()
    {
        if (!_currentUser.IsInRole(RoleNames.Moderator)) return;
        if (_currentUser.IsInRole(RoleNames.SuperAdmin)) return;

        _logger.LogWarning("Moderator blocked from complaints: user={UserId}", _currentUser.UserId);
        throw new UnauthorizedSchoolAccessException(
            "الاطلاع على الشكاوى غير متاح للمشرف — الشكاوى خاصة بإدارة المدرسة والمعلم صاحب الشكوى.");
    }

    private void EnsureCanAccess(Complaint complaint)
    {
        if (_currentUser.IsInRole(RoleNames.SuperAdmin))
            return;

        var active = _currentUser.ActiveSchoolId
            ?? throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة مرتبطة بحسابك. يرجى إعادة تسجيل الدخول.");
        if (active != complaint.SchoolId)
            throw UnauthorizedSchoolAccessException.OutsideScope(active, complaint.SchoolId);

        var currentUserId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول لعرض الشكوى.");

        if (_currentUser.IsInRole(RoleNames.SchoolManager))
            return;

        if (_currentUser.IsInRole(RoleNames.Instructor))
        {
            if (complaint.InstructorUserId != currentUserId)
                throw new UnauthorizedSchoolAccessException("لا تملك صلاحية الوصول إلى شكاوى المعلمين الآخرين.");
            return;
        }

        throw new UnauthorizedSchoolAccessException("دور المستخدم غير معروف ضمن سياقات الأمان.");
    }

    /// <summary>Handle/Delete/Reopen authorization: SuperAdmin OR the complaint's
    /// own School Manager. MainManager hard-blocked (D-Phase8); Moderator also
    /// hard-blocked (D-75); Instructor rejected.</summary>
    private void EnsureCanHandle(Complaint complaint)
    {
        EnsureNotMainManager();
        EnsureNotModerator();

        if (_currentUser.IsInRole(RoleNames.SuperAdmin))
            return;

        if (!_currentUser.IsInRole(RoleNames.SchoolManager))
            throw new UnauthorizedSchoolAccessException("معالجة الشكاوى متاحة لمدير المدرسة المعني فقط.");

        var active = _currentUser.ActiveSchoolId
            ?? throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة مرتبطة بحسابك. يرجى إعادة تسجيل الدخول.");
        if (active != complaint.SchoolId)
            throw UnauthorizedSchoolAccessException.OutsideScope(active, complaint.SchoolId);
    }

    // ─── Internals ───────────────────────────────────────────────────

    private async Task<Complaint> LoadComplaintAsync(int id, CancellationToken cancellationToken, bool track = false)
    {
        IQueryable<Complaint> q = _context.Complaints
            .Include(c => c.School)
            .Include(c => c.Visit)
            .Include(c => c.Instructor)
            .Include(c => c.Moderator)
            .Include(c => c.HandledByUser);

        if (!track) q = q.AsNoTracking();

        return await q.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("الشكوى غير موجودة.");
    }

    private static ComplaintDto MapDto(Complaint c) => new()
    {
        Id = c.Id,
        SchoolId = c.SchoolId,
        SchoolName = c.School?.Name ?? string.Empty,
        VisitId = c.VisitId,
        VisitSubject = c.Visit?.Subject,
        VisitDate = c.Visit?.VisitDate ?? default,
        InstructorUserId = c.InstructorUserId,
        InstructorFullName = c.Instructor?.FullName ?? string.Empty,
        ModeratorUserId = c.ModeratorUserId,
        ModeratorFullName = c.Moderator?.FullName ?? string.Empty,
        Subject = c.Subject,
        Body = c.Body,
        Status = (int)c.Status,
        StatusLabelAr = StatusLabelAr(c.Status),
        AllowedNextStatuses = AllowedTransitions.TryGetValue(c.Status, out var next)
            ? next.Select(s => (int)s).ToList()
            : new List<int>(),
        ResolutionNote = c.ResolutionNote,
        HandledByUserId = c.HandledByUserId,
        HandledByFullName = c.HandledByUser?.FullName,
        HandledAt = c.HandledAt,
        VisitReopenedAt = c.VisitReopenedAt,
        VisitReopenReason = c.VisitReopenReason,
        CreatedAt = c.CreatedAt
    };

    private void WriteAudit(Complaint complaint, string action, string reason, string? oldValues, string? newValues)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            SchoolId = complaint.SchoolId,
            UserId = _currentUser.UserId,
            Action = action,
            EntityName = nameof(Complaint),
            EntityId = complaint.Id.ToString(),
            OldValues = oldValues,
            NewValues = newValues,
            Reason = reason,
            CreatedAt = DateTimeOffset.UtcNow,
            IpAddress = TryResolveClientIp()
        });
    }

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
}
