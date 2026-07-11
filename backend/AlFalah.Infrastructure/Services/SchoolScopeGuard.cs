using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Central guard that enforces backend school-scoping for every
/// school-scoped read/mutation. The constitution
/// (docs/08-SECURITY.md) states: "School Manager = only his school;
/// Moderator = only selected ActiveSchoolId; Instructor = own records.
/// Main Manager / Super Admin = global."
///
/// Rules implemented here:
///  - Global admins (SuperAdmin, MainManager) bypass the check entirely.
///  - School-scoped callers MUST have an ActiveSchoolId claim; if missing,
///    every school-scoped access is rejected with 403.
///  - Client-supplied schoolId is IGNORED for school-scoped callers; the
///    guard silently forces the result to the caller's ActiveSchoolId.
///  - Mutations (Create/Update/Delete) against a different school are
///    rejected with <see cref="UnauthorizedSchoolAccessException"/> (403).
///
/// All checks log so that the security boundary is auditable.
/// </summary>
public class SchoolScopeGuard
{
    private readonly AlFalahDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SchoolScopeGuard> _logger;

    public SchoolScopeGuard(
        AlFalahDbContext context,
        ICurrentUserService currentUser,
        ILogger<SchoolScopeGuard> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Returns the only school id the caller is allowed to operate on, or
    /// <c>null</c> when the caller is a global admin and may operate on
    /// any school.
    ///
    /// For school-scoped callers this is ALWAYS the JWT's <c>active_school_id</c>
    /// claim, even if the client supplied a different id (that other id is
    /// ignored; cross-school reads are silently filtered, cross-school
    /// mutations throw).
    /// </summary>
    public int? ResolveAllowedSchoolId(int? requestedSchoolId)
    {
        if (_currentUser.IsGlobalAdmin())
            return requestedSchoolId;

        if (!_currentUser.IsSchoolScopedRole())
        {
            // Unknown role composition (shouldn't happen). Fail closed.
            throw new UnauthorizedSchoolAccessException("دور المستخدم غير معروف ضمن سياقات الأمان.");
        }

        var active = _currentUser.ActiveSchoolId;
        if (active is null)
        {
            _logger.LogWarning(
                "School-scoping denial: caller {UserId} has no ActiveSchoolId claim (roles={Roles}).",
                _currentUser.UserId,
                string.Join(",", _currentUser.GetRoles()));
            throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة مرتبطة بحسابك. يرجى إعادة تسجيل الدخول.");
        }

        if (requestedSchoolId is not null && requestedSchoolId.Value != active.Value)
        {
            // Caller asked for another school's data — silently coerce to their
            // own school for reads. Callers who need strict enforcement should
            // call <see cref="EnsureCanMutateSchoolAsync"/> instead for mutations.
            _logger.LogInformation(
                "School-scoping: caller {UserId} (ActiveSchoolId={Active}) requested schoolId={Requested}. Coerced to ActiveSchoolId.",
                _currentUser.UserId, active, requestedSchoolId);
            return active;
        }

        return active;
    }

    /// <summary>
    /// Ensures the caller is allowed to CREATE/UPDATE/DELETE in the given school.
    /// Returns silently for global admins. Throws <see cref="UnauthorizedSchoolAccessException"/>
    /// for school-scoped callers whose scope does not cover <paramref name="schoolId"/>.
    /// </summary>
    public Task EnsureCanMutateSchoolAsync(int schoolId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsGlobalAdmin())
            return Task.CompletedTask;

        if (!_currentUser.IsSchoolScopedRole())
            throw new UnauthorizedSchoolAccessException("دور المستخدم غير معروف ضمن سياقات الأمان.");

        var active = _currentUser.ActiveSchoolId;
        if (active is null)
            throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة مرتبطة بحسابك. يرجى إعادة تسجيل الدخول.");

        if (active.Value != schoolId)
        {
            _logger.LogWarning(
                "Cross-school mutation denied: caller {UserId} (ActiveSchoolId={Active}) attempted to mutate school {Requested}.",
                _currentUser.UserId, active, schoolId);
            throw UnauthorizedSchoolAccessException.OutsideScope(active, schoolId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Same as <see cref="EnsureCanMutateSchoolAsync"/> but checks by assignment
    /// row id (UserSchoolRole.Id): resolves the row's SchoolId from the DB
    /// (ignoring soft-delete filter so a tombstoned row still triggers the
    /// check) and validates it against the caller's ActiveSchoolId.
    /// </summary>
    public async Task<int> EnsureCanMutateAssignmentAsync(int assignmentId, CancellationToken cancellationToken = default)
    {
        var assignmentSchoolId = await _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .Where(usr => usr.Id == assignmentId)
            .Select(usr => usr.SchoolId)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignmentSchoolId == 0)
            throw new KeyNotFoundException("تعيين المستخدم غير موجود.");

        await EnsureCanMutateSchoolAsync(assignmentSchoolId, cancellationToken);
        return assignmentSchoolId;
    }
}
