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
/// users with role SchoolManager | Secretary | Moderator | Instructor. Security fix
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

        // D-74 — Surface teacher-profile fields when the user has the Instructor
        // role. Resolved from the FIRST active Instructor assignment (mirrors
        // TeacherService.ResolveTeacherInScopeAsync — one profile per user per
        // school; the schools list above carries the school summary).
        InstructorProfile? profile = null;
        List<string> classes = new();
        SchoolStage? stage = null;
        if (roles.Any(r => r == RoleNames.Instructor))
        {
            profile = await _context.InstructorProfiles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.UserId == userId && p.IsActive)
                .OrderBy(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (profile != null)
            {
                classes = await _context.InstructorClasses
                    .AsNoTracking()
                    .Where(c => c.InstructorProfileId == profile.Id)
                    .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                    .Select(c => c.ClassLabel)
                    .ToListAsync(cancellationToken);
                stage = profile.Stage;
            }
        }

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
            LastLoginAt = user.LastLoginAt,
            EmployeeNumber = profile?.EmployeeNumber,
            Subject = profile?.SubjectSpecialization,
            Stage = stage,
            Classes = classes
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

        // Usernames are case-insensitive identifiers; persist them lower-cased so
        // the stored value always matches what the operator types at login.
        var username = (request.Username ?? string.Empty).Trim().ToLowerInvariant();

        var existing = await _userManager.FindByNameAsync(username);
        if (existing != null)
            throw new InvalidOperationException("اسم المستخدم مستخدم بالفعل.");

        var (firstName, lastName) = ResolveNameParts(request.FullName, request.FirstName, request.LastName);

        var user = new ApplicationUser
        {
            UserName = username,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            EmailConfirmed = true, // Phase 2: no email confirmation flow yet
            FirstName = firstName,
            LastName = lastName,
            PreferredLanguage = string.IsNullOrEmpty(request.PreferredLanguage) ? "ar" : request.PreferredLanguage,
            IsActive = true
        };

        // Every role — instructors included — gets the password the operator
        // typed and Identity's normal password policy. The employee number is a
        // business identifier only; it is no longer reused as a credential.
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

            if (request.Role == RoleNames.Secretary)
            {
                var secretaryRole = await _roleManager.FindByNameAsync(RoleNames.Secretary)
                    ?? throw new InvalidOperationException("دور السكرتير غير مهيأ في قاعدة البيانات.");
                var previousSecretaries = await _context.UserSchoolRoles
                    .Where(usr => usr.SchoolId == school.Id
                        && usr.RoleId == secretaryRole.Id
                        && usr.IsActive)
                    .ToListAsync(cancellationToken);
                foreach (var assignment in previousSecretaries)
                {
                    assignment.IsActive = false;
                    assignment.UpdatedAt = DateTimeOffset.UtcNow;
                    assignment.UpdatedByUserId = _currentUser.UserId;
                }
            }

            _context.UserSchoolRoles.Add(new UserSchoolRole
            {
                UserId = user.Id,
                SchoolId = school.Id,
                RoleId = role.Id,
                IsActive = true,
                CreatedByUserId = _currentUser.UserId
            });

            // D-74 — For Instructor creates, upsert the teacher-profile rows
            // (InstructorProfile + InstructorClasses) in the same unit-of-work
            // so a half-failed create cannot leave a User without a profile.
            if (request.Role == RoleNames.Instructor)
            {
                await UpsertInstructorProfileAsync(user.Id, school.Id, request.EmployeeNumber, request.Subject, request.Stage, request.Classes, cancellationToken);
            }

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
        var current = await GetByIdAsync(userId, cancellationToken);

        if (request.SchoolId.HasValue && request.SchoolId.Value > 0)
            await _scopeGuard.EnsureCanMutateSchoolAsync(request.SchoolId.Value, cancellationToken);

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("المستخدم غير موجود.");

        if (!string.IsNullOrWhiteSpace(request.Role) && current.Schools.Count > 0)
        {
            await ChangeRoleAsync(userId, request.Role!, request.SchoolId, current, cancellationToken);
        }

        var (firstName, lastName) = ResolveNameParts(request.FullName, request.FirstName, request.LastName);
        user.FirstName = firstName;
        user.LastName = lastName;
        user.Email = request.Email;
        user.PhoneNumber = request.PhoneNumber;
        user.PreferredLanguage = string.IsNullOrEmpty(request.PreferredLanguage) ? "ar" : request.PreferredLanguage;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new ArgumentException(errors);
        }

        // D-74 — For Instructor updates, upsert the teacher-profile rows. The
        // SchoolId is taken from the user's FIRST active Instructor assignment
        // (mirrors the teacher's first school — matches the rest of the UI).
        // Cross-school safety: a SchoolManager can only edit teachers in his
        // own school — GetByIdAsync's check above already enforces that.
        if (current.Roles.Any(r => r == RoleNames.Instructor))
        {
            var profileSchoolId = await _context.InstructorProfiles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => (int?)p.SchoolId)
                .FirstOrDefaultAsync(cancellationToken);
            var currentSchoolId = profileSchoolId ?? current.Schools.FirstOrDefault()?.SchoolId;
            var effectiveSchoolId = request.SchoolId ?? currentSchoolId;

            if (effectiveSchoolId.HasValue && effectiveSchoolId.Value > 0)
            {
                if (request.SchoolId.HasValue && request.SchoolId.Value != currentSchoolId)
                    await MoveInstructorAssignmentAsync(userId, currentSchoolId, request.SchoolId.Value, cancellationToken);

                await UpsertInstructorProfileAsync(
                    userId,
                    effectiveSchoolId.Value,
                    request.EmployeeNumber,
                    request.Subject,
                    request.Stage,
                    request.Classes,
                    cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return await GetByIdAsync(userId, cancellationToken);
    }

    public async Task ChangePasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsGlobalAdmin() && !_currentUser.IsInRole(RoleNames.SchoolManager))
            throw new UnauthorizedAccessException("تغيير كلمات مرور المستخدمين متاح لمدير المدرسة أو الإدارة العامة فقط.");

        var current = await GetByIdAsync(userId, cancellationToken);
        if (!_currentUser.IsGlobalAdmin() && current.Roles.Contains(RoleNames.SchoolManager))
            throw new UnauthorizedSchoolAccessException("لا يمكن لمدير المدرسة تغيير كلمة مرور مدير مدرسة آخر.");

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("المستخدم غير موجود.");
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
            throw new ArgumentException(string.Join("; ", result.Errors.Select(e => e.Description)));

        _logger.LogInformation("Password changed for user {UserId} by {ByUserId}", userId, _currentUser.UserId);
    }

    private async Task ChangeRoleAsync(
        string userId,
        string newRoleName,
        int? requestedSchoolId,
        UserDetailDto current,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.IsGlobalAdmin()
            ? requestedSchoolId ?? current.Schools.FirstOrDefault()?.SchoolId
            : _currentUser.ActiveSchoolId;
        if (!schoolId.HasValue)
            throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة مرتبطة بالحساب.");
        if (_currentUser.IsGlobalAdmin() && requestedSchoolId.HasValue)
            await _scopeGuard.EnsureCanMutateSchoolAsync(requestedSchoolId.Value, cancellationToken);

        var role = await _roleManager.FindByNameAsync(newRoleName)
            ?? throw new InvalidOperationException($"الدور '{newRoleName}' غير موجود.");
        if (newRoleName is not (RoleNames.SchoolManager or RoleNames.Secretary or RoleNames.Moderator or RoleNames.Instructor))
            throw new InvalidOperationException("الدور غير مسموح.");

        var assignment = await _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.SchoolId == schoolId.Value && x.IsActive && !x.IsDeleted,
                cancellationToken)
            ?? throw new UnauthorizedSchoolAccessException("المستخدم غير مرتبط بالمدرسة الحالية.");
        var oldRole = await _roleManager.FindByIdAsync(assignment.RoleId);
        if (oldRole?.Name == newRoleName) return;

        if (!_currentUser.IsGlobalAdmin() && newRoleName == RoleNames.SchoolManager)
            throw new UnauthorizedSchoolAccessException("لا يمكن لمدير المدرسة تعيين دور مدير مدرسة.");
        if (!_currentUser.IsGlobalAdmin() && userId == _currentUser.UserId)
            throw new UnauthorizedSchoolAccessException("لا يمكن لمدير المدرسة تغيير دوره بنفسه.");

        var school = await _context.Schools.FirstOrDefaultAsync(x => x.Id == schoolId.Value, cancellationToken)
            ?? throw new KeyNotFoundException("المدرسة غير موجودة.");

        if (newRoleName == RoleNames.SchoolManager)
        {
            if (!string.IsNullOrWhiteSpace(school.ManagerUserId) && school.ManagerUserId != userId)
            {
                var previous = await _context.UserSchoolRoles
                    .Where(x => x.SchoolId == school.Id && x.UserId == school.ManagerUserId && x.IsActive)
                    .ToListAsync(cancellationToken);
                foreach (var row in previous) row.IsActive = false;
            }
            school.ManagerUserId = userId;
        }
        else if (oldRole?.Name == RoleNames.SchoolManager && school.ManagerUserId == userId)
        {
            school.ManagerUserId = null;
        }

        if (newRoleName == RoleNames.Secretary)
        {
            var secretaryRole = await _roleManager.FindByNameAsync(RoleNames.Secretary);
            if (secretaryRole != null)
            {
                var others = await _context.UserSchoolRoles
                    .Where(x => x.SchoolId == school.Id && x.RoleId == secretaryRole.Id && x.UserId != userId && x.IsActive)
                    .ToListAsync(cancellationToken);
                foreach (var row in others) row.IsActive = false;
            }
        }

        assignment.RoleId = role.Id;
        assignment.UpdatedAt = DateTimeOffset.UtcNow;
        assignment.UpdatedByUserId = _currentUser.UserId;

        var targetUser = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("المستخدم غير موجود.");
        if (!await _userManager.IsInRoleAsync(targetUser, newRoleName))
            await _userManager.AddToRoleAsync(targetUser, newRoleName);
        if (oldRole?.Name != null
            && oldRole.Name != newRoleName
            && !await _context.UserSchoolRoles.AnyAsync(x => x.UserId == userId && x.RoleId == oldRole.Id && x.IsActive && !x.IsDeleted, cancellationToken))
        {
            await _userManager.RemoveFromRoleAsync(targetUser, oldRole.Name);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(string userId, CancellationToken cancellationToken = default)
    {
        // SECURITY (D-24): same scoping as Update. Reuse GetByIdAsync's check.
        await GetByIdAsync(userId, cancellationToken);

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("المستخدم غير موجود.");

        // A school manager removes the user's assignment from this school only.
        // Global administrators may deactivate the account everywhere.
        var activeRoles = await _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .Where(usr => usr.UserId == userId && usr.IsActive && !usr.IsDeleted)
            .Where(usr => _currentUser.IsGlobalAdmin() || usr.SchoolId == _currentUser.ActiveSchoolId)
            .ToListAsync(cancellationToken);

        foreach (var usr in activeRoles)
        {
            usr.IsActive = false;
            usr.UpdatedAt = DateTimeOffset.UtcNow;
            usr.UpdatedByUserId = _currentUser.UserId;
        }

        if (_currentUser.IsGlobalAdmin()
            || !await _context.UserSchoolRoles.AnyAsync(
                usr => usr.UserId == userId && usr.IsActive && !usr.IsDeleted && usr.SchoolId != _currentUser.ActiveSchoolId,
                cancellationToken))
        {
            user.IsActive = false;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new ArgumentException(errors);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User deactivated: {UserId} (by {ByUserId})", userId, _currentUser.UserId);
    }

    /// <summary>
    /// D-74 — Upsert the <see cref="InstructorProfile"/> + <see cref="InstructorClass"/>
    /// rows for an Instructor user. Always keyed to the user's first active
    /// Instructor assignment's school (one profile per user, consistent with
    /// the existing <c>UX_InstructorProfile_User</c> unique index on UserId).
    ///
    /// Strategy: load the existing profile row (if any), update its scalar
    /// fields, then fully replace the class list with the incoming labels —
    /// delete missing rows + insert new ones in the same unit-of-work.
    /// </summary>
    private async Task UpsertInstructorProfileAsync(
        string userId,
        int schoolId,
        string? employeeNumber,
        string? subject,
        SchoolStage? stage,
        List<string>? classes,
        CancellationToken cancellationToken)
    {
        var profile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null)
        {
            profile = new InstructorProfile
            {
                UserId = userId,
                SchoolId = schoolId,
                EmployeeNumber = string.IsNullOrWhiteSpace(employeeNumber) ? null : employeeNumber.Trim(),
                SubjectSpecialization = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim(),
                Stage = stage,
                IsActive = true
            };
            _context.InstructorProfiles.Add(profile);
            // Save immediately so the new profile gets an Id we can FK against
            // for the class rows below (avoids relying on graph-fixup ordering).
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Only update fields the caller actually sent. EmployeeNumber / Subject
            // are blankable; treat null as "do not touch" so a partial payload
            // doesn't accidentally wipe existing values. Stage: null means "leave
            // it alone" too (the form only sends it on a full edit).
            if (employeeNumber != null)
                profile.EmployeeNumber = string.IsNullOrWhiteSpace(employeeNumber) ? null : employeeNumber.Trim();
            if (subject != null)
                profile.SubjectSpecialization = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim();
            if (stage.HasValue)
                profile.Stage = stage;
            profile.SchoolId = schoolId;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // D-74 — Class labels: only touch the table when the caller sent a
        // non-null list (null means "don't change classes"). Normalize, dedupe
        // (case-insensitive to mirror the DB collation), preserve order.
        if (classes != null)
        {
            var existing = await _context.InstructorClasses
                .Where(c => c.InstructorProfileId == profile.Id)
                .ToListAsync(cancellationToken);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalized = new List<string>();
            foreach (var raw in classes)
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

            // Insert any new labels.
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
    }

    private async Task MoveInstructorAssignmentAsync(
        string userId,
        int? currentSchoolId,
        int targetSchoolId,
        CancellationToken cancellationToken)
    {
        var targetSchoolExists = await _context.Schools
            .AnyAsync(s => s.Id == targetSchoolId, cancellationToken);
        if (!targetSchoolExists)
            throw new KeyNotFoundException("المدرسة المختارة غير موجودة.");

        var instructorRole = await _roleManager.FindByNameAsync(RoleNames.Instructor)
            ?? throw new InvalidOperationException("دور المعلم غير مهيأ في قاعدة البيانات.");

        var assignments = await _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .Where(r => r.UserId == userId && r.RoleId == instructorRole.Id)
            .ToListAsync(cancellationToken);

        var target = assignments.FirstOrDefault(r => r.SchoolId == targetSchoolId);
        var source = currentSchoolId.HasValue
            ? assignments.FirstOrDefault(r => r.SchoolId == currentSchoolId.Value)
            : assignments.FirstOrDefault(r => r.IsActive && !r.IsDeleted);

        if (target != null)
        {
            target.IsDeleted = false;
            target.DeletedAt = null;
            target.DeletedByUserId = null;
            target.IsActive = true;
            target.UpdatedAt = DateTimeOffset.UtcNow;
            target.UpdatedByUserId = _currentUser.UserId;

            if (source != null && source.Id != target.Id)
            {
                source.IsActive = false;
                source.UpdatedAt = DateTimeOffset.UtcNow;
                source.UpdatedByUserId = _currentUser.UserId;
            }
            return;
        }

        if (source == null)
            throw new KeyNotFoundException("لم يتم العثور على تعيين نشط لهذا المعلم.");

        source.SchoolId = targetSchoolId;
        source.IsActive = true;
        source.UpdatedAt = DateTimeOffset.UtcNow;
        source.UpdatedByUserId = _currentUser.UserId;
    }

    private static (string FirstName, string LastName) ResolveNameParts(
        string? fullName,
        string firstName,
        string lastName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return (firstName.Trim(), lastName.Trim());

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return (parts[0], lastName.Trim());

        return (parts[0], string.Join(' ', parts.Skip(1)));
    }
}
