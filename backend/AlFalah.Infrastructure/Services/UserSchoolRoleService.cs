using AlFalah.Application.Common;
using AlFalah.Application.DTOs.UserSchoolRoles;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// UserSchoolRole assignment service. Phase 2 security fix (D-24):
/// every list/detail/create/delete is force-filtered by the caller's
/// <see cref="ICurrentUserService.ActiveSchoolId"/> via
/// <see cref="SchoolScopeGuard"/>. Global admins (SuperAdmin/MainManager)
/// bypass the filter.
/// </summary>
public class UserSchoolRoleService : IUserSchoolRoleService
{
    private readonly AlFalahDbContext _context;
    private readonly UserManager<Domain.Entities.ApplicationUser> _userManager;
    private readonly RoleManager<Domain.Entities.ApplicationRole> _roleManager;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly ILogger<UserSchoolRoleService> _logger;

    public UserSchoolRoleService(
        AlFalahDbContext context,
        UserManager<Domain.Entities.ApplicationUser> userManager,
        RoleManager<Domain.Entities.ApplicationRole> roleManager,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard,
        ILogger<UserSchoolRoleService> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _logger = logger;
    }

    public async Task<UserSchoolRoleDetailDto> CreateAsync(UserSchoolRoleCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        await _scopeGuard.EnsureCanMutateSchoolAsync(request.SchoolId, cancellationToken);

        var user = await _userManager.FindByIdAsync(request.UserId)
            ?? throw new KeyNotFoundException("المستخدم غير موجود.");
        if (!user.IsActive)
            throw new InvalidOperationException("المستخدم غير نشط.");

        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.Id == request.SchoolId, cancellationToken)
            ?? throw new KeyNotFoundException("المدرسة غير موجودة.");

        var role = await _roleManager.FindByNameAsync(request.Role)
            ?? throw new InvalidOperationException($"الدور '{request.Role}' غير موجود.");

        // Phase 2: only Phase-2 roles are allowed here.
        if (request.Role != RoleNames.SchoolManager
            && request.Role != RoleNames.Moderator
            && request.Role != RoleNames.Instructor)
            throw new InvalidOperationException("الدور غير مسموح في المرحلة الثانية.");

        // Reject duplicates (same User + School + Role triple), accounting for soft-deletes.
        var existing = await _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(usr => usr.UserId == request.UserId
                                    && usr.SchoolId == request.SchoolId
                                    && usr.RoleId == role.Id,
                cancellationToken);

        if (existing != null && !existing.IsDeleted && existing.IsActive)
            throw new InvalidOperationException("هذا المستخدم مخصص بالفعل لهذه المدرسة بنفس الدور.");

        // If a SchoolManager assignment, ensure exactly-one rule by demoting the previous manager.
        if (request.Role == RoleNames.SchoolManager && school.ManagerUserId != request.UserId)
        {
            if (!string.IsNullOrWhiteSpace(school.ManagerUserId))
            {
                var previousManagerRoles = await _context.UserSchoolRoles
                    .Where(usr => usr.SchoolId == school.Id
                              && usr.UserId == school.ManagerUserId
                              && usr.IsActive)
                    .ToListAsync(cancellationToken);
                foreach (var usr in previousManagerRoles)
                {
                    usr.IsActive = false;
                    usr.UpdatedAt = DateTimeOffset.UtcNow;
                    usr.UpdatedByUserId = _currentUser.UserId;
                }
            }

            school.ManagerUserId = request.UserId;
        }

        if (existing != null)
        {
            // Re-activate a previously soft-deleted or deactivated assignment.
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.DeletedByUserId = null;
            existing.IsActive = true;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedByUserId = _currentUser.UserId;
        }
        else
        {
            _context.UserSchoolRoles.Add(new Domain.Entities.UserSchoolRole
            {
                UserId = request.UserId,
                SchoolId = request.SchoolId,
                RoleId = role.Id,
                IsActive = true,
                CreatedByUserId = _currentUser.UserId
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("UserSchoolRole created/activated: user={UserId} school={SchoolId} role={Role}",
            request.UserId, request.SchoolId, request.Role);

        return await GetDetailAsync(request.UserId, request.SchoolId, role.Id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _scopeGuard.EnsureCanMutateAssignmentAsync(id, cancellationToken);

        var usr = await _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("تعيين المستخدم غير موجود.");

        usr.IsActive = false;
        usr.IsDeleted = true;
        usr.DeletedAt = DateTimeOffset.UtcNow;
        usr.DeletedByUserId = _currentUser.UserId;

        // If this was the active SchoolManager of the school, clear the school.ManagerUserId.
        var school = await _context.Schools
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == usr.SchoolId, cancellationToken);
        if (school != null && school.ManagerUserId == usr.UserId)
        {
            school.ManagerUserId = null;
            school.IsActive = false; // Deactivate the school — no longer has a manager.
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("UserSchoolRole soft-deleted: id={Id} user={UserId} school={SchoolId}",
            id, usr.UserId, usr.SchoolId);
    }

    public async Task<IReadOnlyList<UserSchoolRoleDetailDto>> GetBySchoolAsync(int? schoolId, CancellationToken cancellationToken = default)
    {
        // SECURITY (D-24): the client-supplied ?schoolId= is IGNORED for school-scoped
        // callers. ResolveAllowedSchoolId silently coerces to ActiveSchoolId when the
        // request doesn't match the caller's scope (or returns null for global admins).
        var effectiveSchoolId = _scopeGuard.ResolveAllowedSchoolId(schoolId);

        var q = _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .Where(usr => usr.IsActive);

        if (effectiveSchoolId.HasValue)
            q = q.Where(usr => usr.SchoolId == effectiveSchoolId.Value);

        _logger.LogInformation(
            "UserSchoolRole list: caller={UserId} roles={Roles} ActiveSchoolId={Active} requested={Requested} effective={Effective}",
            _currentUser.UserId,
            string.Join(",", _currentUser.GetRoles()),
            _currentUser.ActiveSchoolId,
            schoolId,
            effectiveSchoolId);

        var rows = await q
            .Join(_context.Schools, usr => usr.SchoolId, s => s.Id, (usr, s) => new { usr, s.Name })
            .Join(_context.Roles, x => x.usr.RoleId, r => r.Id, (x, r) => new { x.usr, x.Name, RoleName = r.Name })
            .ToListAsync(cancellationToken);

        var userIds = rows.Select(x => x.usr.UserId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        return rows.Select(x => new UserSchoolRoleDetailDto
        {
            Id = x.usr.Id,
            UserId = x.usr.UserId,
            Username = users.TryGetValue(x.usr.UserId, out var u) ? (u.UserName ?? "") : "",
            FullName = users.TryGetValue(x.usr.UserId, out var u2) ? u2.FullName : "",
            SchoolId = x.usr.SchoolId,
            SchoolName = x.Name,
            RoleId = x.usr.RoleId,
            Role = x.RoleName!,
            IsActive = x.usr.IsActive,
            CreatedAt = x.usr.CreatedAt
        }).ToList();
    }

    private async Task<UserSchoolRoleDetailDto> GetDetailAsync(string userId, int schoolId, string roleId, CancellationToken cancellationToken)
    {
        var usr = await _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .FirstAsync(u => u.UserId == userId && u.SchoolId == schoolId && u.RoleId == roleId, cancellationToken);

        var user = await _userManager.FindByIdAsync(userId);
        var school = await _context.Schools.IgnoreQueryFilters().FirstAsync(s => s.Id == schoolId, cancellationToken);
        var role = await _roleManager.FindByIdAsync(roleId);

        return new UserSchoolRoleDetailDto
        {
            Id = usr.Id,
            UserId = usr.UserId,
            Username = user?.UserName ?? "",
            FullName = user?.FullName ?? "",
            SchoolId = school.Id,
            SchoolName = school.Name,
            RoleId = role!.Id,
            Role = role.Name!,
            IsActive = usr.IsActive,
            CreatedAt = usr.CreatedAt
        };
    }
}