using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Teachers;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// D-71 — Teachers Management + Teacher Profile service.
///
/// Reuses the existing ApplicationUser + UserSchoolRole + InstructorProfile
/// entities (no parallel Teacher table). Adds:
///  - Scoped list of "teachers" (ApplicationUsers with the Instructor role +
///    an active UserSchoolRole).
///  - Teacher profile (header data).
///  - Teacher visits (in-scope per D-37 for moderators; school-scope for SM).
///  - Teacher progress (per-visit domain averages from the existing
///    VisitDomainAverage snapshot rows) — feeds the RADAR chart on the
///    profile page.
///
/// Mutations REUSE the existing <c>UserService</c> / <c>UsersController</c>
/// endpoints (<c>POST /users</c>, <c>PUT /users/{id}</c>,
/// <c>POST /users/{id}/deactivate</c>) — passing role=Instructor +
/// schoolId=ActiveSchoolId (for SM) — so no duplicate
/// create/edit/deactivate endpoints are added.
///
/// School-scoping is enforced inside this service via <see cref="SchoolScopeGuard"/>;
/// cross-school access → <see cref="UnauthorizedSchoolAccessException"/> → 403.
/// D-37 (Moderator own-visits-only) is applied to the per-teacher visits +
/// progress endpoints.
/// </summary>
public class TeacherService : ITeacherService
{
    private readonly AlFalahDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly AuditLogWriter _audit;
    private readonly ILogger<TeacherService> _logger;

    public TeacherService(
        AlFalahDbContext context,
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard,
        AuditLogWriter audit,
        ILogger<TeacherService> logger)
    {
        _context = context;
        _userManager = userManager;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _audit = audit;
        _logger = logger;
    }

    // ─── List ─────────────────────────────────────────────────────────────────

    public async Task<PagedResult<TeacherListItemDto>> ListAsync(
        TeacherListQuery query,
        CancellationToken cancellationToken = default)
    {
        // School-scope: callers see teachers only in their ActiveSchoolId
        // (MainManager / SuperAdmin see every school). Client-supplied
        // schoolId is intentionally NOT honoured here — the list is scoped
        // by the JWT's active_school_id claim alone.
        var forcedSchoolId = _scopeGuard.ResolveAllowedSchoolId(null);

        // Resolve the set of user ids that have an active UserSchoolRole
        // with role = Instructor. School-scoped callers are restricted to
        // their own school; global admins see everyone.
        var instructorRole = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == RoleNames.Instructor, cancellationToken)
            ?? throw new InvalidOperationException("دور المعلم غير مُهيَّأ في قاعدة البيانات.");

        var assignmentQuery = _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(usr => usr.RoleId == instructorRole.Id && usr.IsActive);

        if (forcedSchoolId.HasValue)
            assignmentQuery = assignmentQuery.Where(usr => usr.SchoolId == forcedSchoolId.Value);

        // One row per (User, School); a teacher may theoretically have multiple
        // active assignments in different schools — we surface the FIRST active
        // school here (the user list page does the same with `schools[0]`).
        var teacherAssignments = await assignmentQuery
            .GroupBy(usr => usr.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                SchoolId = g.OrderBy(x => x.SchoolId).Select(x => x.SchoolId).First()
            })
            .ToListAsync(cancellationToken);

        if (teacherAssignments.Count == 0)
        {
            return new PagedResult<TeacherListItemDto>
            {
                Items = new List<TeacherListItemDto>(),
                TotalCount = 0,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        var teacherUserIds = teacherAssignments.Select(a => a.UserId).ToList();

        // Load the user rows (active ones only — list page does not show
        // deactivated teachers, matching the rest of the admin UI).
        var usersQuery = _context.Users
            .AsNoTracking()
            .Where(u => teacherUserIds.Contains(u.Id) && u.IsActive && !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            usersQuery = usersQuery.Where(u =>
                u.FirstName.Contains(s) ||
                u.LastName.Contains(s) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
        }

        var total = await usersQuery.CountAsync(cancellationToken);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => query.PageSize
        };

        var users = await usersQuery
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var pageUserIds = users.Select(u => u.Id).ToList();

        // Pre-load schools for the page (school-name + stage label).
        var pageAssignments = teacherAssignments
            .Where(a => pageUserIds.Contains(a.UserId))
            .Join(_context.Schools.AsNoTracking(),
                a => a.SchoolId,
                s => s.Id,
                (a, s) => new { a.UserId, s.Id, s.Name, s.Stage })
            .ToList();

        // Pre-load InstructorProfile rows for employee number + subject.
        var profiles = await _context.InstructorProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => pageUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);

        // D-74 — Pre-load class labels for the list page (one query for the page).
        var profileIds = profiles.Select(p => p.Id).ToList();
        var classRows = await _context.InstructorClasses
            .AsNoTracking()
            .Where(c => profileIds.Contains(c.InstructorProfileId))
            .OrderBy(c => c.InstructorProfileId).ThenBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Select(c => new { c.InstructorProfileId, c.ClassLabel })
            .ToListAsync(cancellationToken);
        var classesByProfile = classRows
            .GroupBy(r => r.InstructorProfileId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ClassLabel).ToList());

        // Visit counts — per-teacher, in-scope for this caller.
        var visitCounts = await ComputeInScopeVisitCountsAsync(pageUserIds, forcedSchoolId, cancellationToken);

        // Apply search against employee number / subject AFTER the SQL
        // projection (these fields live on InstructorProfile, not on Users,
        // and post-filtering is acceptable because the page slice is small).
        string? search = query.Search?.Trim();
        var filtered = users.Where(u =>
        {
            if (string.IsNullOrEmpty(search)) return true;
            var prof = profiles.FirstOrDefault(p => p.UserId == u.Id);
            var emp = prof?.EmployeeNumber ?? string.Empty;
            var subj = prof?.SubjectSpecialization ?? string.Empty;
            return u.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || u.LastName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (u.PhoneNumber != null && u.PhoneNumber.Contains(search))
                || emp.Contains(search, StringComparison.OrdinalIgnoreCase)
                || subj.Contains(search, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        var items = filtered.Select(u =>
        {
            var sch = pageAssignments.FirstOrDefault(a => a.UserId == u.Id);
            var prof = profiles.FirstOrDefault(p => p.UserId == u.Id);
            var vc = visitCounts.TryGetValue(u.Id, out var c) ? c : 0;
            // D-74 — Stage resolution: profile.Stage first; fall back to the
            // school's stage when the teacher hasn't set one yet.
            var stage = prof?.Stage ?? sch?.Stage ?? SchoolStage.Primary;
            var classes = prof != null && classesByProfile.TryGetValue(prof.Id, out var list)
                ? list
                : new List<string>();
            return new TeacherListItemDto
            {
                UserId = u.Id,
                FullName = u.FullName,
                EmployeeNumber = prof?.EmployeeNumber,
                SchoolId = sch?.Id ?? 0,
                SchoolName = sch?.Name ?? string.Empty,
                SchoolStage = stage.ToString(),
                SchoolStageLabelAr = stage.ToArabicString(),
                Subject = prof?.SubjectSpecialization,
                Stage = stage,
                Classes = classes,
                VisitCount = vc,
                IsActive = u.IsActive
            };
        }).ToList();

        return new PagedResult<TeacherListItemDto>
        {
            Items = items,
            // After SQL + post-filter the total shrinks to `items.Count` so
            // the p-table shows the right number of pages. (Cheap because
            // the page is small; avoids a second count roundtrip.)
            TotalCount = items.Count == 0 ? total : (total - (users.Count - items.Count)),
            Page = page,
            PageSize = pageSize
        };
    }

    // ─── Profile ──────────────────────────────────────────────────────────────

    public async Task<TeacherProfileDto> GetProfileAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var (assignment, _) = await ResolveTeacherInScopeAsync(userId, cancellationToken);

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("المعلم غير موجود.");

        var profile = await _context.InstructorProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        var school = await _context.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == assignment.SchoolId, cancellationToken);

        var visitCount = await CountInScopeVisitsAsync(userId, assignment.SchoolId, cancellationToken);

        // D-74 — Stage resolution: profile.Stage first, fall back to school.Stage.
        var stage = profile?.Stage ?? school?.Stage ?? SchoolStage.Primary;

        // D-74 — Classes (preserves the order the teacher entered them).
        var classes = profile == null
            ? new List<string>()
            : await _context.InstructorClasses
                .AsNoTracking()
                .Where(c => c.InstructorProfileId == profile.Id)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                .Select(c => c.ClassLabel)
                .ToListAsync(cancellationToken);

        return new TeacherProfileDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            EmployeeNumber = profile?.EmployeeNumber,
            SchoolId = assignment.SchoolId,
            SchoolName = school?.Name ?? string.Empty,
            Subject = profile?.SubjectSpecialization,
            Stage = stage,
            SchoolStageLabelAr = stage.ToArabicString(),
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            IsActive = user.IsActive,
            Classes = classes,
            VisitCount = visitCount
        };
    }

    // ─── Visits (D-37 enforced for moderator callers) ────────────────────────

    public async Task<List<TeacherVisitSummaryDto>> GetVisitsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var (assignment, _) = await ResolveTeacherInScopeAsync(userId, cancellationToken);

        var q = _context.Visits
            .AsNoTracking()
            .Where(v => v.InstructorId == userId && v.SchoolId == assignment.SchoolId);

        // D-37: Moderator own-visits-only — the same predicate the visits list uses.
        if (IsModeratorOnlyCaller())
        {
            var currentUserId = _currentUser.UserId
                ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول لعرض زيارات المعلم.");
            q = q.Where(v => v.CreatedByUserId == currentUserId);
        }

        var rows = await q
            .OrderByDescending(v => v.VisitDate).ThenByDescending(v => v.Id)
            .Select(v => new
            {
                v.Id,
                v.VisitDate,
                v.VisitCategory,
                v.Status,
                v.LessonTitle,
                v.Subject,
                v.CreatedByUserId,
                CreatedByFullName = v.CreatedByUser.FullName
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new TeacherVisitSummaryDto
        {
            Id = r.Id,
            VisitDate = r.VisitDate,
            Lesson = !string.IsNullOrWhiteSpace(r.LessonTitle)
                ? r.LessonTitle!
                : (!string.IsNullOrWhiteSpace(r.Subject)
                    ? r.Subject!
                    : ((VisitCategory)r.VisitCategory).ToArabicString()),
            VisitCategory = (int)r.VisitCategory,
            VisitCategoryLabelAr = ((VisitCategory)r.VisitCategory).ToArabicString(),
            Status = (int)r.Status,
            StatusLabelAr = StatusLabelAr((VisitStatus)r.Status),
            // CreatedByFullName is surfaced only to roles that already see
            // other creators on the visits list (SM / global admins). For
            // Moderator + Instructor the field stays null because D-37 means
            // they're only seeing their own visits anyway.
            CreatedByFullName = _currentUser.IsGlobalAdmin() || _currentUser.IsInRole(RoleNames.SchoolManager)
                ? r.CreatedByFullName
                : null
        }).ToList();
    }

    // ─── Progress (per-visit domain averages for the radar chart) ────────────

    public async Task<TeacherProgressDto> GetProgressAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var (assignment, _) = await ResolveTeacherInScopeAsync(userId, cancellationToken);

        // Visits in scope (D-37 enforced). Only SUBMITTED visits carry an
        // analysis snapshot — Draft / PendingApproval visits have no
        // VisitDomainAverage rows yet, so they contribute nothing to the
        // radar. We do not error on empty progress — the UI renders an
        // empty-state ("لا توجد زيارات مُرسلة بعد").
        var q = _context.Visits
            .AsNoTracking()
            .Where(v => v.InstructorId == userId
                     && v.SchoolId == assignment.SchoolId
                     && v.Analysis != null);

        if (IsModeratorOnlyCaller())
        {
            var currentUserId = _currentUser.UserId
                ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول لعرض بيانات تقدم المعلم.");
            q = q.Where(v => v.CreatedByUserId == currentUserId);
        }

        // Pull each submitted visit + its domain averages. Order = visit
        // creation order (oldest first) so the legend reads chronologically.
        var visits = await q
            .OrderBy(v => v.VisitDate).ThenBy(v => v.Id)
            .Select(v => new
            {
                v.Id,
                v.VisitDate,
                v.Analysis!.ComputedAt,
                DomainAverages = v.Analysis!.DomainAverages
                    .OrderBy(d => d.DomainCode)
                    .Select(d => new
                    {
                        d.DomainCode,
                        d.DomainNameAr,
                        d.AverageScore
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        // Dynamic axis labels — use the LATEST visit's domain list so the
        // radar axes match what the current rubric carries. Older visits may
        // have a different domain count (D-21 carry-over: snapshot is
        // immutable), so we explicitly map per-visit and align axis labels
        // in the DTO; the chart builder reads `AxisLabels` for the ring and
        // the per-visit averages for each polygon, padding with 0 where a
        // snapshot didn't carry that domain.
        var axisLabels = visits.LastOrDefault()?.DomainAverages
            .Select(d => new TeacherDomainAverageDto
            {
                DomainCode = d.DomainCode,
                DomainNameAr = d.DomainNameAr,
                AverageScore = 0m
            })
            .ToList() ?? new List<TeacherDomainAverageDto>();

        // Pre-build a Code → DomainNameAr lookup so older visits that lack a
        // domain name still render its current Arabic label (axis names come
        // from the active rubric, snapshot scores stay bound to their visit).
        var axisLookup = axisLabels.ToDictionary(a => a.DomainCode, a => a.DomainNameAr);

        var sequence = 0;
        var perVisit = visits.Select(v =>
        {
            sequence++;
            var legendDate = v.VisitDate.ToString("yyyy-MM-dd");
            return new TeacherVisitProgressDto
            {
                VisitId = v.Id,
                VisitDate = v.VisitDate,
                LegendLabel = $"الزيارة {sequence} — {legendDate}",
                DomainAverages = axisLabels.Select(axis =>
                {
                    var persisted = v.DomainAverages
                        .FirstOrDefault(d => d.DomainCode == axis.DomainCode);
                    return new TeacherDomainAverageDto
                    {
                        DomainCode = axis.DomainCode,
                        DomainNameAr = axisLookup.TryGetValue(axis.DomainCode, out var n) ? n : axis.DomainNameAr,
                        AverageScore = persisted?.AverageScore ?? 0m
                    };
                }).ToList()
            };
        }).ToList();

        _logger.LogInformation(
            "Teacher progress loaded: userId={UserId} school={SchoolId} visitCount={VisitCount} axisCount={AxisCount} caller={CallerId}",
            userId, assignment.SchoolId, perVisit.Count, axisLabels.Count, _currentUser.UserId);

        return new TeacherProgressDto
        {
            UserId = userId,
            AxisLabels = axisLabels,
            Visits = perVisit
        };
    }

    // ─── Teaching info (D-74) — Subject + Classes for visit-form auto-fill ──

    public async Task<TeacherTeachingDto> GetTeachingAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var (assignment, _) = await ResolveTeacherInScopeAsync(userId, cancellationToken);

        var profile = await _context.InstructorProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive, cancellationToken);

        var classes = profile == null
            ? new List<string>()
            : await _context.InstructorClasses
                .AsNoTracking()
                .Where(c => c.InstructorProfileId == profile.Id)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                .Select(c => c.ClassLabel)
                .ToListAsync(cancellationToken);

        SchoolStage stage;
        if (profile?.Stage.HasValue == true) stage = profile.Stage.Value;
        else
        {
            var school = await _context.Schools.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == assignment.SchoolId, cancellationToken);
            stage = school?.Stage ?? SchoolStage.Primary;
        }

        return new TeacherTeachingDto
        {
            UserId = userId,
            SchoolId = assignment.SchoolId,
            Subject = profile?.SubjectSpecialization,
            Stage = stage,
            Classes = classes
        };
    }

    public async Task<TeacherTeachingDto> UpsertTeachingAsync(
        string userId,
        TeacherTeachingUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var (assignment, _) = await ResolveTeacherInScopeAsync(userId, cancellationToken);

        var profile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null)
        {
            profile = new InstructorProfile
            {
                UserId = userId,
                SchoolId = assignment.SchoolId,
                IsActive = true
            };
            _context.InstructorProfiles.Add(profile);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Subject (null = leave alone, matching the UserService convention).
        if (request.Subject != null)
            profile.SubjectSpecialization = string.IsNullOrWhiteSpace(request.Subject)
                ? null
                : request.Subject.Trim();
        // Stage (null = leave alone).
        if (request.Stage.HasValue)
            profile.Stage = request.Stage;

        // Classes (null = leave alone; the existing list stays untouched).
        if (request.Classes != null)
        {
            var existing = await _context.InstructorClasses
                .Where(c => c.InstructorProfileId == profile.Id)
                .ToListAsync(cancellationToken);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalized = new List<string>();
            foreach (var raw in request.Classes)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var trimmed = raw.Trim();
                if (trimmed.Length > 50) trimmed = trimmed.Substring(0, 50);
                if (seen.Add(trimmed)) normalized.Add(trimmed);
            }

            // Drop rows whose label isn't in the new set.
            foreach (var row in existing)
            {
                if (!normalized.Any(n => string.Equals(n, row.ClassLabel, StringComparison.OrdinalIgnoreCase)))
                {
                    row.IsDeleted = true;
                    row.DeletedAt = DateTimeOffset.UtcNow;
                    row.DeletedByUserId = _currentUser.UserId;
                }
            }

            var existingLabels = existing
                .Where(r => !r.IsDeleted)
                .Select(r => r.ClassLabel)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sortOrder = 0;
            foreach (var label in normalized)
            {
                if (existingLabels.Contains(label)) { sortOrder++; continue; }
                _context.InstructorClasses.Add(new InstructorClass
                {
                    InstructorProfileId = profile.Id,
                    ClassLabel = label,
                    SortOrder = sortOrder
                });
                sortOrder++;
            }
        }

        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _audit.Write(
            schoolId: assignment.SchoolId,
            userId: _currentUser.UserId,
            action: "Teacher.UpsertTeaching",
            entityName: nameof(InstructorProfile),
            entityId: profile.Id.ToString(),
            reason: "تحديث المادة والفصول للمعلم",
            oldValues: null,
            newValues: new
            {
                subject = profile.SubjectSpecialization,
                stage = profile.Stage,
                classes = request.Classes
            });

        _logger.LogInformation(
            "Teacher teaching upserted: userId={UserId} school={SchoolId} subject={Subject} classes={Count} caller={CallerId}",
            userId, assignment.SchoolId, profile.SubjectSpecialization, request.Classes?.Count ?? -1, _currentUser.UserId);

        return await GetTeachingAsync(userId, cancellationToken);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a teacher user-id into an active Instructor assignment row
    /// that the caller is allowed to read. Throws 403/404 on cross-school /
    /// missing / not-an-instructor.
    /// </summary>
    private async Task<(UserSchoolRole Assignment, ApplicationUser User)> ResolveTeacherInScopeAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("المعلم غير موجود.");

        var instructorRole = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == RoleNames.Instructor, cancellationToken)
            ?? throw new InvalidOperationException("دور المعلم غير مُهيَّأ في قاعدة البيانات.");

        // Pick the FIRST active Instructor assignment for this user (matches
        // the desktop reference + the existing users-list `schools[0]`
        // convention).
        var assignments = await _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(usr => usr.UserId == userId
                       && usr.RoleId == instructorRole.Id
                       && usr.IsActive)
            .OrderBy(usr => usr.SchoolId)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            _logger.LogWarning(
                "Teacher profile lookup denied: user {UserId} has no active Instructor assignment.",
                userId);
            throw new KeyNotFoundException("لم يتم العثور على تعيين نشط لهذا المعلم.");
        }

        // SECURITY (D-24 / D-28): for school-scoped callers, the resolved
        // school MUST match the caller's ActiveSchoolId. Otherwise 403.
        if (!_currentUser.IsGlobalAdmin())
        {
            var allowedSchool = _currentUser.ActiveSchoolId
                ?? throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة مرتبطة بحسابك.");

            var inScope = assignments.FirstOrDefault(a => a.SchoolId == allowedSchool);
            if (inScope == null)
            {
                _logger.LogWarning(
                    "Cross-school teacher access denied: caller {CallerId} (ActiveSchoolId={Active}) tried to read teacher {TeacherId}.",
                    _currentUser.UserId, allowedSchool, userId);
                throw new UnauthorizedSchoolAccessException(
                    "لا تملك صلاحية الوصول إلى بيانات هذا المعلم. خارج نطاق المدرسة الحالية.");
            }

            return (inScope, user);
        }

        // Global admins get the first assignment (deterministic order).
        return (assignments.First(), user);
    }

    /// <summary>
    /// Computes per-teacher visit counts using the same scoped query as
    /// the visits list endpoint — so a Moderator's "visit count" column
    /// already reflects "visits I created", matching D-37.
    /// </summary>
    private async Task<Dictionary<string, int>> ComputeInScopeVisitCountsAsync(
        List<string> userIds,
        int? forcedSchoolId,
        CancellationToken cancellationToken)
    {
        var q = _context.Visits
            .AsNoTracking()
            .Where(v => userIds.Contains(v.InstructorId));

        if (forcedSchoolId.HasValue)
            q = q.Where(v => v.SchoolId == forcedSchoolId.Value);

        if (IsModeratorOnlyCaller())
        {
            var currentUserId = _currentUser.UserId
                ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول.");
            q = q.Where(v => v.CreatedByUserId == currentUserId);
        }

        var grouped = await q
            .GroupBy(v => v.InstructorId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return grouped.ToDictionary(g => g.UserId, g => g.Count);
    }

    private async Task<int> CountInScopeVisitsAsync(
        string userId, int schoolId, CancellationToken cancellationToken)
    {
        var q = _context.Visits
            .AsNoTracking()
            .Where(v => v.InstructorId == userId && v.SchoolId == schoolId);

        if (IsModeratorOnlyCaller())
        {
            var currentUserId = _currentUser.UserId
                ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول.");
            q = q.Where(v => v.CreatedByUserId == currentUserId);
        }

        return await q.CountAsync(cancellationToken);
    }

    /// <summary>
    /// D-37: true when the caller is a Moderator AND NOT also a School Manager /
    /// Super Admin / Main Manager. Mirrors the same helper in <see cref="VisitService"/>.
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
}