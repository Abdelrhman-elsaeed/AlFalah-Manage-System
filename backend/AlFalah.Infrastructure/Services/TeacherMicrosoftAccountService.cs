using System.Security.Claims;
using AlFalah.Application.Common.Exceptions;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AlFalah.Infrastructure.Services;

public sealed class TeacherMicrosoftAccountService : ITeacherMicrosoftAccountService
{
    private readonly AlFalahDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly AuditLogWriter _audit;
    private readonly SchoolScopeGuard _scopeGuard;

    public TeacherMicrosoftAccountService(AlFalahDbContext context, IConfiguration configuration, AuditLogWriter audit, SchoolScopeGuard scopeGuard)
    {
        _context = context;
        _configuration = configuration;
        _audit = audit;
        _scopeGuard = scopeGuard;
    }

    public async Task<TeacherDriveStatusDto> GetStatusAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var identity = ReadIdentity(principal);
        var account = await _context.TeacherMicrosoftAccounts.AsNoTracking()
            .Where(x => x.TenantId == identity.TenantId && x.ObjectId == identity.ObjectId && x.IsLinked)
            .Select(x => new { x.TeacherId, TeacherName = x.Teacher.User.FirstName + " " + x.Teacher.User.LastName })
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null)
            return new(false, false, null, "NotLinked", string.Empty);

        var folder = await _context.TeacherDriveFolders.AsNoTracking()
            .Where(x => x.TeacherId == account.TeacherId && x.IsActive)
            .Select(x => new { x.FolderDisplayName })
            .SingleOrDefaultAsync(cancellationToken);

        return new(true, folder is not null, folder?.FolderDisplayName,
            folder is null ? "DriveNotConfigured" : "Connected", account.TeacherName.Trim());
    }

    public async Task<LinkMicrosoftAccountResultDto> LinkAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var identity = ReadIdentity(principal);
        var email = Normalize(identity.Email);
        var account = await _context.TeacherMicrosoftAccounts
            .Include(x => x.Teacher).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.NormalizedMicrosoftEmail == email, cancellationToken);

        if (account is null)
            throw new TeacherDriveAccessDeniedException("حساب Microsoft المستخدم غير مرتبط ببياناتك. يرجى التواصل مع إدارة المدرسة.");

        if (account.IsLinked && (account.TenantId != identity.TenantId || account.ObjectId != identity.ObjectId))
            throw new TeacherDriveAccessDeniedException("حساب Microsoft المستخدم غير مرتبط ببياناتك. يرجى التواصل مع إدارة المدرسة.");

        var duplicate = await _context.TeacherMicrosoftAccounts.AnyAsync(
            x => x.TeacherId != account.TeacherId && x.TenantId == identity.TenantId && x.ObjectId == identity.ObjectId,
            cancellationToken);
        if (duplicate)
            throw new InvalidOperationException("حساب Microsoft مرتبط بالفعل بمعلم آخر.");

        account.TenantId = identity.TenantId;
        account.ObjectId = identity.ObjectId;
        account.MicrosoftEmail = identity.Email;
        account.NormalizedMicrosoftEmail = email;
        account.IsLinked = true;
        account.LinkedAtUtc = DateTimeOffset.UtcNow;
        account.LastLoginAtUtc = DateTimeOffset.UtcNow;
        _audit.Write(account.Teacher.SchoolId, account.Teacher.UserId, "TeacherMicrosoftAccount.Linked", "TeacherMicrosoftAccount", account.Id.ToString(), null,
            new { account.TeacherId, account.TenantId, account.ObjectId });
        await _context.SaveChangesAsync(cancellationToken);
        return new(true, "تم ربط حساب Microsoft بنجاح.");
    }

    public async Task<(int TeacherId, int SchoolId, string TeacherDisplayName)> ResolveLinkedTeacherAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var identity = ReadIdentity(principal);
        var account = await _context.TeacherMicrosoftAccounts.AsNoTracking()
            .Where(x => x.TenantId == identity.TenantId && x.ObjectId == identity.ObjectId && x.IsLinked && x.Teacher.IsActive && !x.Teacher.IsDeleted)
            .Select(x => new { x.TeacherId, x.Teacher.SchoolId, Name = x.Teacher.User.FirstName + " " + x.Teacher.User.LastName })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null)
            throw new TeacherDriveAccessDeniedException("حساب Microsoft المستخدم غير مرتبط ببياناتك. يرجى التواصل مع إدارة المدرسة.");
        return (account.TeacherId, account.SchoolId, account.Name.Trim());
    }

    public async Task<TeacherMicrosoftAccountAdminDto> ConfigureExpectedEmailAsync(int teacherId, string microsoftEmail, CancellationToken cancellationToken = default)
    {
        var teacher = await _context.InstructorProfiles.SingleOrDefaultAsync(x => x.Id == teacherId && x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("المعلم غير موجود.");
        await _scopeGuard.EnsureCanMutateSchoolAsync(teacher.SchoolId, cancellationToken);
        var normalized = Normalize(microsoftEmail);
        var account = await _context.TeacherMicrosoftAccounts.SingleOrDefaultAsync(x => x.TeacherId == teacherId, cancellationToken);
        if (account is null)
        {
            account = new TeacherMicrosoftAccount { TeacherId = teacherId, MicrosoftEmail = microsoftEmail.Trim(), NormalizedMicrosoftEmail = normalized };
            _context.TeacherMicrosoftAccounts.Add(account);
        }
        else if (account.IsLinked && account.NormalizedMicrosoftEmail != normalized)
        {
            throw new InvalidOperationException("لا يمكن تغيير بريد حساب Microsoft بعد الربط. أعد ضبط الربط أولاً.");
        }
        else
        {
            account.MicrosoftEmail = microsoftEmail.Trim();
            account.NormalizedMicrosoftEmail = normalized;
        }
        await _context.SaveChangesAsync(cancellationToken);
        return new(teacherId, account.MicrosoftEmail, account.IsLinked, account.ObjectId, account.LinkedAtUtc == default ? null : account.LinkedAtUtc);
    }

    private EntraIdentity ReadIdentity(ClaimsPrincipal principal)
    {
        var tenantId = principal.FindFirst("tid")?.Value;
        var objectId = principal.FindFirst("oid")?.Value;
        var email = principal.FindFirst("preferred_username")?.Value ?? principal.FindFirst("email")?.Value ?? principal.FindFirst("upn")?.Value;
        var allowedTenant = _configuration["AzureAd:TenantId"];
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(email) ||
            (!string.IsNullOrWhiteSpace(allowedTenant) && !string.Equals(tenantId, allowedTenant, StringComparison.OrdinalIgnoreCase)))
            throw new TeacherDriveAccessDeniedException("تعذر التحقق من حساب Microsoft. يرجى تسجيل الدخول بالحساب المدرسي الصحيح.");
        return new(tenantId, objectId, email, principal);
    }

    private static string Normalize(string email) => email.Trim().ToUpperInvariant();
}
