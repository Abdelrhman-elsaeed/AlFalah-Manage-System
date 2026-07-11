using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Users;
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
/// User management service. Phase 2 scope: create / update / soft-deactivate
/// users with role SchoolManager | Moderator | Instructor. Security fix
/// (D-24): every query is force-filtered by the caller's
/// <see cref="ICurrentUserService.ActiveSchoolId"/> via
/// <see cref="SchoolScopeGuard"/> so a School Manager only sees users
/// assigned to his own school — Main Manager / Super Admin see everyone.
/// </summary>
public class UserService : IUserService
{
    private readonly AlFalahDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly ILogger<UserService> _logger;

    public UserService(
        AlFalahDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard,
        ILogger<UserService> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _logger = logger;
    }

    public async Task<PagedResult<UserListItemDto>> GetPagedAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        // SECURITY (D-24): pre-resolve the set of users that the caller may see.
        // For school-scoped callers this is restricted to users with an active
        // UserSchoolRole in their ActiveSchoolId. For global admins this is the
        // full set. The forced school id is also used to override any client-
        // supplied ?schoolId= that doesn't match the caller's scope.
        var forcedSchoolId = _scopeGuard.ResolveAllowedSchoolId(query.SchoolId);
        HashSet<string>? callerVisibleUserIds = null;
        if (forcedSchoolId.HasValue)
        {
            callerVisibleUserIds = (await _context.UserSchoolRoles
                .IgnoreQueryFilters()
                .Where(usr => usr.SchoolId == forcedSchoolId.Value && usr.IsActive)
                .Select(usr => usr.UserId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var q = _context.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(u =>
                (u.UserName != null && u.UserName.Contains(s)) ||
                u.FirstName.Contains(s) ||
                u.LastName.Contains(s) ||
                (u.Email != null && u.Email.Contains(s)));
        }

        if (query.IsActive.HasValue)
            q = q.Where(u => u.IsActive == query.IsActive.Value);

        // Filter by Role via UserManager join (do a role lookup first).
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var role = await _roleManager.FindByNameAsync(query.Role);
            if (role != null)
            {
                var userIdsInRole = await _context.UserRoles
                    .Where(ur => ur.RoleId == role.Id)
                    .Select(ur => ur.UserId)
                    .ToListAsync(cancellationToken);

                q = q.Where(u => userIdsInRole.Contains(u.Id));
            }
            else
            {
                // Unknown role → no users match.
                return new PagedResult<UserListItemDto>
                {
                    Items = new List<UserListItemDto>(),
                    TotalCount = 0,
                    Page = query.Page,
                    PageSize = query.PageSize
                };
            }
        }

        // Apply school-scope restriction AFTER optional role filter so we don't
        // ship role-lookup user-ids for users the caller cannot see.
        if (callerVisibleUserIds is not null)
        {
            if (callerVisibleUserIds.Count == 0)
            {
                return new PagedResult<UserListItemDto>
                {
                    Items = new List<UserListItemDto>(),
                    TotalCount = 0,
                    Page = query.Page,
                    PageSize = query.PageSize
                };
            }
            q = q.Where(u => callerVisibleUserIds.Contains(u.Id));
        }

        var total = await q.CountAsync(cancellationToken);

        var users = await q
            .OrderBy(u => u.UserName)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();

        var roleLookup = await _context.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Roles = g.Select(x => x.Name!).ToList() })
            .ToListAsync(cancellationToken);

        var schoolAssignmentsQuery = _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .Where(usr => userIds.Contains(usr.UserId) && usr.IsActive);

        // SECURITY (D-24): if the caller is school-scoped, the `schools` array
        // on each returned user must NOT include assignments to schools the
        // caller cannot see. Restrict the assignment lookup to ActiveSchoolId.
        if (forcedSchoolId.HasValue)
            schoolAssignmentsQuery = schoolAssignmentsQuery.Where(usr => usr.SchoolId == forcedSchoolId.Value);

        var schoolAssignments = await schoolAssignmentsQuery
            .Join(_context.Schools, usr => usr.SchoolId, s => s.Id, (usr, s) => new { usr.UserId, SchoolId = s.Id, SchoolName = s.Name, usr.RoleId })
            .Join(_context.Roles, x => x.RoleId, r => r.Id, (x, r) => new { x.UserId, x.SchoolId, x.SchoolName, Role = r.Name })
            .ToListAsync(cancellationToken);

        var items = users.Select(u =>
        {
            var roles = roleLookup.FirstOrDefault(r => r.UserId == u.Id)?.Roles ?? new List<string>();
            var schools = schoolAssignments
                .Where(sa => sa.UserId == u.Id)
                .Select(sa => new UserSchoolBriefDto
                {
                    SchoolId = sa.SchoolId,
                    SchoolName = sa.SchoolName,
                    Role = sa.Role!
                })
                .ToList();

            return new UserListItemDto
            {
                UserId = u.Id,
                Username = u.UserName ?? "",
                FullName = u.FullName,
                Email = u.Email,
                IsActive = u.IsActive,
                Roles = roles,
                Schools = schools,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            };
        }).ToList();

        _logger.LogInformation(
            "User list: caller={UserId} ActiveSchoolId={Active} requestedSchoolId={Requested} effectiveSchoolId={Effective} returnedCount={Count}",
            _currentUser.UserId,
            _currentUser.ActiveSchoolId,
            query.SchoolId,
            forcedSchoolId,
            items.Count);

        return new PagedResult<UserListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<UserDetailDto> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("المستخدم غير موجود.");

        // SECURITY (D-24): a school-scoped caller may only view users assigned
        // to his own school. If the target user has zero assignments in the
        // caller's ActiveSchoolId, reject.
        if (!_currentUser.IsGlobalAdmin())
        {
            var allowedSchool = _currentUser.ActiveSchoolId
                ?? throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة مرتبطة بحسابك.");

            var hasAccess = await _context.UserSchoolRoles
                .IgnoreQueryFilters()
                .AnyAsync(usr => usr.UserId == userId
                              && usr.SchoolId == allowedSchool
                              && usr.IsActive,
                    cancellationToken);

            if (!hasAccess)
            {
                _logger.LogWarning(
                    "Cross-school user view denied: caller {UserId} (ActiveSchoolId={Active}) tried to read user {Target}.",
                    _currentUser.UserId, allowedSchool, userId);
                throw new UnauthorizedSchoolAccessException(
                    $"لا تملك صلاحية الوصول إلى بيانات المستخدم {userId}. خارج نطاق المدرسة الحالية.");
            }
        }

        var roles = await _userManager.GetRolesAsync(user);

        // SECURITY (D-24): restrict the `schools` array to the caller's scope so
        // they can only see assignments in their own school. Global admins see
        // all assignments as before.
        var schoolAssignmentsQuery = _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .Where(usr => usr.UserId == userId && usr.IsActive);

        if (!_currentUser.IsGlobalAdmin())
        {
            var allowedSchoolId = _currentUser.ActiveSchoolId
                ?? throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة مرتبطة بحسابك.");
            schoolAssignmentsQuery = schoolAssignmentsQuery.Where(usr => usr.SchoolId == allowedSchoolId);
        }

        var schools = await schoolAssignmentsQuery
            .Join(_context.Schools, usr => usr.SchoolId, s => s.Id, (usr, s) => new { usr.UserId, SchoolId = s.Id, SchoolName = s.Name, usr.RoleId })
            .Join(_context.Roles, x => x.RoleId, r => r.Id, (x, r) => new UserSchoolBriefDto
            {
                SchoolId = x.SchoolId,
                SchoolName = x.SchoolName,
                Role = r.Name!
            })
            .ToListAsync(cancellationToken);

        return new UserDetailDto
        {
            UserId = user.Id,
            Username = user.UserName ?? "",
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            PreferredLanguage = user.PreferredLanguage,
            IsActive = user.IsActive,
            Roles = roles.ToList(),
            Schools = schools,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<UserDetailDto> CreateAsync(UserCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        // SECURITY (D-24): if a school is being assigned, it must be the caller's
        // ActiveSchoolId. A school-scoped caller cannot create staff for another
        // school.
        if (request.SchoolId.HasValue && request.SchoolId.Value > 0)
            await _scopeGuard.EnsureCanMutateSchoolAsync(request.SchoolId.Value, cancellationToken);

        // SECURITY (D-24): adding a new SchoolManager is documented as a Main
        // Manager privilege (docs/03 §2: "Add School Manager"). A School Manager
        // can manage instructors and moderators INSIDE his school, not other
        // school managers.
        if (request.Role == RoleNames.SchoolManager && !_currentUser.IsGlobalAdmin())
            throw new UnauthorizedSchoolAccessException("إضافة مدير مدرسة متاحة للمدير العام ومدير النظام فقط.");

        var existing = await _userManager.FindByNameAsync(request.Username);
        if (existing != null)
            throw new InvalidOperationException("اسم المستخدم مستخدم بالفعل.");

        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            EmailConfirmed = true, // Phase 2: no email confirmation flow yet
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PreferredLanguage = string.IsNullOrEmpty(request.PreferredLanguage) ? "ar" : request.PreferredLanguage,
            IsActive = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            throw new ArgumentException(errors);
        }

        // Assign Phase-2 role.
        if (!await _userManager.IsInRoleAsync(user, request.Role))
            await _userManager.AddToRoleAsync(user, request.Role);

        // Optional initial school assignment.
        if (request.SchoolId.HasValue && request.SchoolId.Value > 0)
        {
            var school = await _context.Schools.FirstOrDefaultAsync(s => s.Id == request.SchoolId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("المدرسة المختارة غير موجودة.");

            var role = await _roleManager.FindByNameAsync(request.Role)
                ?? throw new InvalidOperationException($"الدور '{request.Role}' غير موجود.");

            // If the assigned role is SchoolManager, also wire the school.ManagerUserId.
            if (request.Role == RoleNames.SchoolManager)
            {
                // Demote the previous manager's UserSchoolRole.
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

                school.ManagerUserId = user.Id;
            }

            _context.UserSchoolRoles.Add(new UserSchoolRole
            {
                UserId = user.Id,
                SchoolId = school.Id,
                RoleId = role.Id,
                IsActive = true,
                CreatedByUserId = _currentUser.UserId
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("User created: {Username} ({UserId}) role={Role}",
            user.UserName, user.Id, request.Role);

        return await GetByIdAsync(user.Id, cancellationToken);
    }

    public async Task<UserDetailDto> UpdateAsync(string userId, UserUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        // SECURITY (D-24): a school-scoped caller may only modify users assigned
        // to his school. Reuse GetByIdAsync's check by reading first.
        await GetByIdAsync(userId, cancellationToken);

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("المستخدم غير موجود.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = request.Email;
        user.PhoneNumber = request.PhoneNumber;
        user.PreferredLanguage = string.IsNullOrEmpty(request.PreferredLanguage) ? "ar" : request.PreferredLanguage;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new ArgumentException(errors);
        }

        return await GetByIdAsync(userId, cancellationToken);
    }

    public async Task DeactivateAsync(string userId, CancellationToken cancellationToken = default)
    {
        // SECURITY (D-24): same scoping as Update. Reuse GetByIdAsync's check.
        await GetByIdAsync(userId, cancellationToken);

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("المستخدم غير موجود.");

        user.IsActive = false;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new ArgumentException(errors);
        }

        // Deactivate all active UserSchoolRoles for this user.
        var activeRoles = await _context.UserSchoolRoles
            .Where(usr => usr.UserId == userId && usr.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var usr in activeRoles)
        {
            usr.IsActive = false;
            usr.UpdatedAt = DateTimeOffset.UtcNow;
            usr.UpdatedByUserId = _currentUser.UserId;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User deactivated: {UserId} (by {ByUserId})", userId, _currentUser.UserId);
    }
}