using AlFalah.Application.DTOs.Auth;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Handles all authentication flows: school login, main manager login, refresh, logout.
/// Enforces school context validation via UserSchoolRole.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AlFalahDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        AlFalahDbContext context,
        IJwtService jwtService,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
    }

    // ─── School Login ─────────────────────────────────────────────────────────

    public async Task<AuthResponseDto> SchoolLoginAsync(
        SchoolLoginRequestDto request,
        string? ipAddress,
        string? userAgent)
    {
        var user = await _userManager.FindByNameAsync(request.Username);

        if (user == null || !user.IsActive)
            throw new UnauthorizedAccessException("اسم المستخدم أو كلمة المرور غير صحيحة.");

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("Failed login attempt for user {Username}", request.Username);
            throw new UnauthorizedAccessException("اسم المستخدم أو كلمة المرور غير صحيحة.");
        }

        // Verify the user is assigned to the selected school
        var userSchoolRole = await _context.UserSchoolRoles
            .Include(x => x.Role)
            .Include(x => x.School)
            .Where(x => x.UserId == user.Id
                     && x.SchoolId == request.SchoolId
                     && x.IsActive)
            .FirstOrDefaultAsync();

        if (userSchoolRole == null)
            throw new UnauthorizedAccessException("المستخدم غير مخصص لهذه المدرسة.");

        // Ensure the resolved School and Role are themselves active.
        if (userSchoolRole.School != null && !userSchoolRole.School.IsActive)
            throw new UnauthorizedAccessException("هذه المدرسة غير نشطة حالياً.");

        if (userSchoolRole.Role != null && !userSchoolRole.Role.IsActive)
            throw new UnauthorizedAccessException("دورك في هذه المدرسة غير نشط.");

        // Block Super Admin and Main Manager from school login endpoint
        var userRoles = await _userManager.GetRolesAsync(user);
        if (userRoles.Contains(RoleNames.SuperAdmin) || userRoles.Contains(RoleNames.MainManager))
            throw new UnauthorizedAccessException("يرجى استخدام صفحة تسجيل الدخول الخاصة بالمدير العام.");

        // Update last login
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        // Get permissions for this user's roles
        var permissions = await GetPermissionsForUserAsync(user.Id);

        return await BuildAuthResponseAsync(
            user, userRoles, permissions,
            request.SchoolId, userSchoolRole.School?.Name,
            ipAddress, userAgent);
    }

    // ─── Main Manager Login ───────────────────────────────────────────────────

    public async Task<AuthResponseDto> MainManagerLoginAsync(
        MainManagerLoginRequestDto request,
        string? ipAddress,
        string? userAgent)
    {
        var user = await _userManager.FindByNameAsync(request.Username);

        if (user == null || !user.IsActive)
            throw new UnauthorizedAccessException("اسم المستخدم أو كلمة المرور غير صحيحة.");

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("Failed main manager login for {Username}", request.Username);
            throw new UnauthorizedAccessException("اسم المستخدم أو كلمة المرور غير صحيحة.");
        }

        var userRoles = await _userManager.GetRolesAsync(user);

        // Only allow SuperAdmin and MainManager through this endpoint
        if (!userRoles.Contains(RoleNames.SuperAdmin) && !userRoles.Contains(RoleNames.MainManager))
            throw new UnauthorizedAccessException("هذا الحساب لا يملك صلاحية الوصول إلى لوحة المدير العام.");

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        var permissions = await GetPermissionsForUserAsync(user.Id);

        // No school context for Main Manager/SuperAdmin
        return await BuildAuthResponseAsync(
            user, userRoles, permissions,
            null, null,
            ipAddress, userAgent);
    }

    // ─── Refresh Token ────────────────────────────────────────────────────────

    public async Task<AuthResponseDto> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent)
    {
        var storedToken = await _context.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Token == refreshToken);

        if (storedToken == null || !storedToken.IsActive)
            throw new UnauthorizedAccessException("رمز التجديد غير صالح أو منتهي الصلاحية.");

        var user = storedToken.User;
        if (!user.IsActive)
            throw new UnauthorizedAccessException("الحساب معطل.");

        // Revoke old token (rotation: record which token replaced it once known below).
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTimeOffset.UtcNow;

        var userRoles = await _userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForUserAsync(user.Id);

        // Try to restore school context from old token's associated role
        var activeSchoolRole = await _context.UserSchoolRoles
            .Include(x => x.School)
            .Include(x => x.Role)
            .Where(x => x.UserId == user.Id && x.IsActive)
            .FirstOrDefaultAsync();

        // Validate school/role activeness on refresh too — a user that
        // logged in before a deactivation should not silently keep access.
        if (activeSchoolRole != null)
        {
            if (activeSchoolRole.School != null && !activeSchoolRole.School.IsActive)
                throw new UnauthorizedAccessException("هذه المدرسة غير نشطة حالياً.");

            if (activeSchoolRole.Role != null && !activeSchoolRole.Role.IsActive)
                throw new UnauthorizedAccessException("دورك في هذه المدرسة غير نشط.");
        }

        int? schoolId = activeSchoolRole?.SchoolId;
        string? schoolName = activeSchoolRole?.School?.Name;

        // MainManager/SuperAdmin have no school context
        if (userRoles.Contains(RoleNames.SuperAdmin) || userRoles.Contains(RoleNames.MainManager))
        {
            schoolId = null;
            schoolName = null;
        }

        // Issue new tokens and link old → new for rotation tracking.
        var newRawRefreshToken = _jwtService.GenerateRefreshToken();
        var newRefreshExpiry = DateTimeOffset.UtcNow.AddDays(30);

        storedToken.ReplacedByToken = newRawRefreshToken;

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = newRawRefreshToken,
            ExpiresAt = newRefreshExpiry,
            IpAddress = ipAddress,
            UserAgent = userAgent
        });
        await _context.SaveChangesAsync();

        var accessToken = _jwtService.GenerateAccessToken(
            user.Id, user.UserName!, userRoles, permissions,
            schoolId, user.PreferredLanguage);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRawRefreshToken,
            AccessTokenExpiry = DateTimeOffset.UtcNow.AddMinutes(60),
            RefreshTokenExpiry = newRefreshExpiry,
            User = new UserTokenInfoDto
            {
                UserId = user.Id,
                Username = user.UserName!,
                FullName = user.FullName,
                ActiveSchoolId = schoolId,
                ActiveSchoolName = schoolName,
                PreferredLanguage = user.PreferredLanguage,
                Roles = userRoles.ToList(),
                Permissions = permissions
            }
        };
    }

    // ─── Logout ───────────────────────────────────────────────────────────────

    public async Task LogoutAsync(string refreshToken)
    {
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(x => x.Token == refreshToken);

        if (storedToken == null || storedToken.IsRevoked) return;

        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
    }

    // ─── Get Current User ─────────────────────────────────────────────────────

    public async Task<CurrentUserDto> GetCurrentUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("المستخدم غير موجود.");

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForUserAsync(userId);

        var activeSchoolRole = await _context.UserSchoolRoles
            .Include(x => x.School)
            .Where(x => x.UserId == userId && x.IsActive)
            .FirstOrDefaultAsync();

        return new CurrentUserDto
        {
            UserId = user.Id,
            Username = user.UserName ?? "",
            FullName = user.FullName,
            Email = user.Email,
            PreferredLanguage = user.PreferredLanguage,
            ActiveSchoolId = activeSchoolRole?.SchoolId,
            ActiveSchoolName = activeSchoolRole?.School?.Name,
            Roles = roles.ToList(),
            Permissions = permissions
        };
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private async Task<List<string>> GetPermissionsForUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return new List<string>();

        var userRoles = await _userManager.GetRolesAsync(user);

        var permissions = await _context.RolePermissions
            .Include(rp => rp.Role)
            .Include(rp => rp.Permission)
            .Where(rp => userRoles.Contains(rp.Role.Name!))
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync();

        return permissions;
    }

    private async Task<AuthResponseDto> BuildAuthResponseAsync(
        ApplicationUser user,
        IList<string> roles,
        List<string> permissions,
        int? activeSchoolId,
        string? activeSchoolName,
        string? ipAddress,
        string? userAgent)
    {
        var accessToken = _jwtService.GenerateAccessToken(
            user.Id, user.UserName!, roles, permissions,
            activeSchoolId, user.PreferredLanguage);

        var rawRefreshToken = _jwtService.GenerateRefreshToken();
        var refreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(30);

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = rawRefreshToken,
            ExpiresAt = refreshTokenExpiry,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            AccessTokenExpiry = DateTimeOffset.UtcNow.AddMinutes(60),
            RefreshTokenExpiry = refreshTokenExpiry,
            User = new UserTokenInfoDto
            {
                UserId = user.Id,
                Username = user.UserName!,
                FullName = user.FullName,
                ActiveSchoolId = activeSchoolId,
                ActiveSchoolName = activeSchoolName,
                PreferredLanguage = user.PreferredLanguage,
                Roles = roles.ToList(),
                Permissions = permissions
            }
        };
    }

    // ─── Forgot / Reset Password ──────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<string?> ForgotPasswordAsync(string username)
    {
        // Always return a token string (in dev) regardless of whether the user
        // exists — but for unknown/inactive users we still return null so the
        // caller treats it as "no-op success" without leaking a token.
        var user = await _userManager.FindByNameAsync(username);
        if (user == null || !user.IsActive)
        {
            _logger.LogInformation("Forgot-password requested for unknown/inactive user {Username}", username);
            return null;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // In a real deployment this would be emailed/SMS'd. For dev we log it
        // AND return it so the client (and tests) can complete the flow.
        _logger.LogInformation(
            "Password reset token for user {Username}: {Token}",
            user.UserName, token);

        // Only return the token when ASPNETCORE_ENVIRONMENT == Development
        // so we don't leak it in production by mistake.
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase)
            ? token
            : null;
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(string username, string token, string newPassword)
    {
        var user = await _userManager.FindByNameAsync(username)
            ?? throw new KeyNotFoundException("المستخدم غير موجود.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("الحساب معطل.");

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Password reset failed for {Username}: {Errors}", user.UserName, errors);
            throw new ArgumentException(errors);
        }

        _logger.LogInformation("Password reset succeeded for {Username}", user.UserName);
    }
}
