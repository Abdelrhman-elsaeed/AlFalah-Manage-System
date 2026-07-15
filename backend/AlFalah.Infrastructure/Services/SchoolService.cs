using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Schools;
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
/// School CRUD + lifecycle service. Enforces Phase 2 business rules:
///  - Exactly one active SchoolManager per school (assigning a new one demotes the previous).
///  - Activation blocked when ManagerUserId is null.
///  - Same Name allowed only when City/LocationDetails differ.
///  - Soft delete via IsDeleted/DeletedAt/DeletedByUserId (enforced by global query filter).
/// Security fix (D-24): every list/detail/mutation is force-filtered by the
/// caller's <see cref="ICurrentUserService.ActiveSchoolId"/> via
/// <see cref="SchoolScopeGuard"/>. Global admins (SuperAdmin/MainManager)
/// bypass the filter.
/// </summary>
public class SchoolService : ISchoolService
{
    private readonly AlFalahDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly ISchoolLocationRepository _locationRepository;
    private readonly ILogger<SchoolService> _logger;

    public SchoolService(
        AlFalahDbContext context,
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard,
        ISchoolLocationRepository locationRepository,
        ILogger<SchoolService> logger)
    {
        _context = context;
        _userManager = userManager;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _locationRepository = locationRepository;
        _logger = logger;
    }

    public async Task<PagedResult<SchoolListItemDto>> GetPagedAsync(SchoolListQuery query, CancellationToken cancellationToken = default)
    {
        var q = _context.Schools
            .AsNoTracking()
            .Include(s => s.Manager)
            .AsQueryable();

        // SECURITY (D-24): for school-scoped callers, the result is
        // silently forced to ActiveSchoolId — the client cannot enumerate
        // other schools even by sending crafted filters.
        var allowedSchoolId = _scopeGuard.ResolveAllowedSchoolId(null);
        if (allowedSchoolId.HasValue)
            q = q.Where(s => s.Id == allowedSchoolId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x =>
                x.Name.Contains(s) ||
                x.City.Contains(s) ||
                (x.LocationDetails != null && x.LocationDetails.Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(query.City))
            q = q.Where(x => x.City == query.City);

        if (!string.IsNullOrWhiteSpace(query.Stage) &&
            Enum.TryParse<SchoolStage>(query.Stage, false, out var stage))
            q = q.Where(x => x.Stage == stage);

        if (query.IsActive.HasValue)
            q = q.Where(x => x.IsActive == query.IsActive.Value);

        var total = await q.CountAsync(cancellationToken);

        q = ApplySorting(q, query.SortBy, query.SortDesc);

        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new SchoolListItemDto
            {
                Id = s.Id,
                Name = s.Name,
                Stage = s.Stage.ToString(),
                City = s.City,
                SchoolLocationId = s.SchoolLocationId,
                SchoolLocationName = s.Location != null ? s.Location.NameAr : null,
                RegionName = s.Location != null ? s.Location.RegionNameAr : null,
                Latitude = s.Location != null ? s.Location.Latitude : null,
                Longitude = s.Location != null ? s.Location.Longitude : null,
                LocationDetails = s.LocationDetails,
                LogoUrl = s.LogoUrl,
                IsActive = s.IsActive,
                ManagerUserId = s.ManagerUserId,
                ManagerFullName = s.Manager != null ? s.Manager.FullName : null,
                ActiveUserCount = _context.UserSchoolRoles.Count(usr => usr.SchoolId == s.Id && usr.IsActive),
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SchoolListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<SchoolDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // SECURITY (D-24): reject any GetById for a school outside the caller's scope.
        await _scopeGuard.EnsureCanMutateSchoolAsync(id, cancellationToken);

        var school = await _context.Schools
            .AsNoTracking()
            .Include(s => s.Manager)
            .Include(s => s.Location)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("المدرسة غير موجودة.");

        return new SchoolDetailDto
        {
            Id = school.Id,
            Name = school.Name,
            Stage = school.Stage.ToString(),
            City = school.City,
            SchoolLocationId = school.SchoolLocationId,
            SchoolLocationName = school.Location?.NameAr,
            RegionName = school.Location?.RegionNameAr,
            Latitude = school.Location?.Latitude,
            Longitude = school.Location?.Longitude,
            LocationDetails = school.LocationDetails,
            LogoUrl = school.LogoUrl,
            IsActive = school.IsActive,
            ManagerUserId = school.ManagerUserId,
            ManagerFullName = school.Manager?.FullName,
            ManagerUsername = school.Manager?.UserName,
            CreatedAt = school.CreatedAt,
            UpdatedAt = school.UpdatedAt,
            ActiveUserCount = await _context.UserSchoolRoles.CountAsync(u => u.SchoolId == id && u.IsActive, cancellationToken)
        };
    }

    public async Task<SchoolDetailDto> CreateAsync(SchoolCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        // SECURITY (D-24): only global admins can create schools. School-scoped
        // callers do not have an ActiveSchoolId to scope to, so any create would
        // land outside their allowed scope — reject up-front.
        if (!_currentUser.IsGlobalAdmin())
            throw new UnauthorizedSchoolAccessException("إنشاء المدارس متاح للمدير العام ومدير النظام فقط.");

        var stage = Enum.Parse<SchoolStage>(request.Stage, false);
        var location = await GetRequiredLocationAsync(request.SchoolLocationId, cancellationToken);

        // Rule: same Name allowed only if City/LocationDetails differ.
        // A duplicate on (Name, City, LocationDetails) is rejected.
        var duplicate = await _context.Schools
            .AnyAsync(s => s.Name == request.Name
                       && s.SchoolLocationId == request.SchoolLocationId
                       && (s.LocationDetails ?? string.Empty) == (request.LocationDetails ?? string.Empty),
                cancellationToken);

        if (duplicate)
            throw new InvalidOperationException("توجد مدرسة بنفس الاسم والمدينة والموقع بالفعل.");

        // If a manager is provided at create time, validate it exists, is active, and is in role SchoolManager.
        if (!string.IsNullOrWhiteSpace(request.ManagerUserId))
            await EnsureUserIsSchoolManagerAsync(request.ManagerUserId, cancellationToken);

        var school = new School
        {
            Name = request.Name.Trim(),
            Stage = stage,
            City = location.NameAr,
            SchoolLocationId = location.Id,
            LocationDetails = request.LocationDetails?.Trim(),
            LogoUrl = request.LogoUrl,
            ManagerUserId = request.ManagerUserId,
            // Activation is blocked until a manager is assigned, regardless of request body.
            IsActive = !string.IsNullOrWhiteSpace(request.ManagerUserId) && request.IsActive
        };

        _context.Schools.Add(school);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("School created: {SchoolId} {SchoolName} (manager={ManagerUserId})",
            school.Id, school.Name, school.ManagerUserId ?? "none");

        return await GetByIdAsync(school.Id, cancellationToken);
    }

    public async Task<SchoolDetailDto> UpdateAsync(int id, SchoolUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        await _scopeGuard.EnsureCanMutateSchoolAsync(id, cancellationToken);

        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("المدرسة غير موجودة.");

        var stage = Enum.Parse<SchoolStage>(request.Stage, false);
        var location = await GetRequiredLocationAsync(request.SchoolLocationId, cancellationToken);

        // Uniqueness check excluding this row.
        var duplicate = await _context.Schools
            .AnyAsync(s => s.Id != id
                       && s.Name == request.Name
                       && s.SchoolLocationId == request.SchoolLocationId
                       && (s.LocationDetails ?? string.Empty) == (request.LocationDetails ?? string.Empty),
                cancellationToken);
        if (duplicate)
            throw new InvalidOperationException("توجد مدرسة أخرى بنفس الاسم والمدينة والموقع.");

        // Manager change through update path is also allowed — enforce same rule.
        if (!string.IsNullOrWhiteSpace(request.ManagerUserId) &&
            request.ManagerUserId != school.ManagerUserId)
        {
            await EnsureUserIsSchoolManagerAsync(request.ManagerUserId, cancellationToken);

            // If a previous manager exists, deactivate their UserSchoolRole for this school.
            if (!string.IsNullOrWhiteSpace(school.ManagerUserId))
            {
                var previousManagerRoles = await _context.UserSchoolRoles
                    .Where(usr => usr.SchoolId == id
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

            // Activate (or create) the new manager's UserSchoolRole.
            await EnsureUserSchoolRoleActiveAsync(request.ManagerUserId, id, RoleNames.SchoolManager, cancellationToken);

            school.ManagerUserId = request.ManagerUserId;
        }

        school.Name = request.Name.Trim();
        school.Stage = stage;
        school.City = location.NameAr;
        school.SchoolLocationId = location.Id;
        school.LocationDetails = request.LocationDetails?.Trim();
        school.LogoUrl = request.LogoUrl;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _scopeGuard.EnsureCanMutateSchoolAsync(id, cancellationToken);

        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("المدرسة غير موجودة.");

        school.IsDeleted = true;
        school.DeletedAt = DateTimeOffset.UtcNow;
        school.DeletedByUserId = _currentUser.UserId;
        school.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("School soft-deleted: {SchoolId} {SchoolName} (by {UserId})",
            school.Id, school.Name, _currentUser.UserId);
    }

    public async Task<SchoolDetailDto> AssignManagerAsync(int schoolId, AssignSchoolManagerRequestDto request, CancellationToken cancellationToken = default)
    {
        await _scopeGuard.EnsureCanMutateSchoolAsync(schoolId, cancellationToken);

        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.Id == schoolId, cancellationToken)
            ?? throw new KeyNotFoundException("المدرسة غير موجودة.");

        await EnsureUserIsSchoolManagerAsync(request.UserId, cancellationToken);

        // Deactivate the current manager's UserSchoolRole for this school (if any).
        if (!string.IsNullOrWhiteSpace(school.ManagerUserId) && school.ManagerUserId != request.UserId)
        {
            var previousManagerRoles = await _context.UserSchoolRoles
                .Where(usr => usr.SchoolId == schoolId
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

        // Activate or create the new manager's UserSchoolRole.
        await EnsureUserSchoolRoleActiveAsync(request.UserId, schoolId, RoleNames.SchoolManager, cancellationToken);

        school.ManagerUserId = request.UserId;
        school.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("School manager assigned: school={SchoolId} manager={UserId}", schoolId, request.UserId);

        return await GetByIdAsync(schoolId, cancellationToken);
    }

    public async Task ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        await _scopeGuard.EnsureCanMutateSchoolAsync(id, cancellationToken);

        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("المدرسة غير موجودة.");

        // Business rule: cannot activate without a manager.
        if (string.IsNullOrWhiteSpace(school.ManagerUserId))
            throw new InvalidOperationException("لا يمكن تفعيل مدرسة بدون تعيين مدير لها.");

        school.IsActive = true;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("School activated: {SchoolId} {SchoolName}", school.Id, school.Name);
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        await _scopeGuard.EnsureCanMutateSchoolAsync(id, cancellationToken);

        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("المدرسة غير موجودة.");

        school.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("School deactivated: {SchoolId} {SchoolName}", school.Id, school.Name);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task EnsureUserIsSchoolManagerAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("المستخدم المختار غير موجود.");

        if (!user.IsActive)
            throw new InvalidOperationException("المستخدم المختار غير نشط.");

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains(RoleNames.SchoolManager))
            throw new InvalidOperationException("المستخدم المختار لا يملك دور مدير المدرسة.");
    }

    private async Task<SchoolLocation> GetRequiredLocationAsync(int locationId, CancellationToken cancellationToken) =>
        await _locationRepository.GetByIdAsync(locationId, cancellationToken)
        ?? throw new KeyNotFoundException("موقع المدرسة المحدد غير موجود أو غير نشط.");

    private async Task EnsureUserSchoolRoleActiveAsync(string userId, int schoolId, string roleName, CancellationToken cancellationToken)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken)
            ?? throw new InvalidOperationException($"الدور '{roleName}' غير موجود.");

        var existing = await _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(usr => usr.UserId == userId
                                    && usr.SchoolId == schoolId
                                    && usr.RoleId == role.Id,
                cancellationToken);

        if (existing != null)
        {
            if (existing.IsDeleted)
            {
                // Re-activate a previously deleted assignment.
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.DeletedByUserId = null;
            }
            existing.IsActive = true;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedByUserId = _currentUser.UserId;
        }
        else
        {
            _context.UserSchoolRoles.Add(new UserSchoolRole
            {
                UserId = userId,
                SchoolId = schoolId,
                RoleId = role.Id,
                IsActive = true,
                CreatedByUserId = _currentUser.UserId
            });
        }
    }

    private static IQueryable<School> ApplySorting(IQueryable<School> q, string? sortBy, bool desc)
    {
        sortBy = (sortBy ?? "name").Trim().ToLowerInvariant();
        return sortBy switch
        {
            "city"    => desc ? q.OrderByDescending(x => x.City)    : q.OrderBy(x => x.City),
            "stage"   => desc ? q.OrderByDescending(x => x.Stage)   : q.OrderBy(x => x.Stage),
            "active"  => desc ? q.OrderByDescending(x => x.IsActive): q.OrderBy(x => x.IsActive),
            "created" => desc ? q.OrderByDescending(x => x.CreatedAt) : q.OrderBy(x => x.CreatedAt),
            _         => desc ? q.OrderByDescending(x => x.Name)    : q.OrderBy(x => x.Name)
        };
    }
}
