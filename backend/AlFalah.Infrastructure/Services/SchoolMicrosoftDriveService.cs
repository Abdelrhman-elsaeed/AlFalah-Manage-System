using AlFalah.Application.Common;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Services;

/// <summary>Manager-owned setup for the single Microsoft drive belonging to a school.</summary>
public sealed class SchoolMicrosoftDriveService : ISchoolMicrosoftDriveService
{
    private readonly AlFalahDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly AuditLogWriter _audit;
    public SchoolMicrosoftDriveService(AlFalahDbContext context, ICurrentUserService currentUser, SchoolScopeGuard scopeGuard, AuditLogWriter audit)
        => (_context, _currentUser, _scopeGuard, _audit) = (context, currentUser, scopeGuard, audit);

    public async Task<SchoolMicrosoftDriveSettingsDto> GetForCurrentSchoolAsync(CancellationToken cancellationToken = default)
    {
        EnsureManager();
        var schoolId = ResolveSchoolId();
        var drive = await _context.SchoolMicrosoftDrives.AsNoTracking().SingleOrDefaultAsync(x => x.SchoolId == schoolId, cancellationToken);
        return Map(schoolId, drive);
    }

    public async Task<SchoolMicrosoftDriveSettingsDto> ConfigureForCurrentSchoolAsync(ConfigureSchoolMicrosoftDriveRequest request, CancellationToken cancellationToken = default)
    {
        EnsureManager(); Validate(request);
        var schoolId = ResolveSchoolId();
        var drive = await _context.SchoolMicrosoftDrives.SingleOrDefaultAsync(x => x.SchoolId == schoolId, cancellationToken);
        var old = drive is null ? null : new { drive.TenantId, drive.SchoolMicrosoftEmail, drive.DriveId, drive.RootItemId, drive.RootFolderDisplayName, drive.IsEnabled };
        if (drive is null) { drive = new SchoolMicrosoftDrive { SchoolId = schoolId }; _context.SchoolMicrosoftDrives.Add(drive); }
        drive.TenantId = request.TenantId.Trim();
        drive.SchoolMicrosoftEmail = request.SchoolMicrosoftEmail.Trim();
        drive.DriveId = request.DriveId.Trim();
        drive.RootItemId = request.RootItemId.Trim();
        drive.RootFolderDisplayName = request.RootFolderDisplayName.Trim();
        drive.IsEnabled = request.IsEnabled;
        drive.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _audit.Write(schoolId, _currentUser.UserId, "SchoolMicrosoftDrive.Configured", "SchoolMicrosoftDrive", drive.Id == 0 ? null : drive.Id.ToString(), null, old,
            new { drive.TenantId, drive.SchoolMicrosoftEmail, drive.DriveId, drive.RootItemId, drive.RootFolderDisplayName, drive.IsEnabled });
        await _context.SaveChangesAsync(cancellationToken);
        return Map(schoolId, drive);
    }

    private int ResolveSchoolId() => _scopeGuard.ResolveAllowedSchoolId(null) ?? throw new UnauthorizedSchoolAccessException("اختر مدرسة قبل إعداد ملفات الإنجاز.");
    private void EnsureManager()
    {
        if (!_currentUser.IsGlobalAdmin() && !_currentUser.GetRoles().Contains(RoleNames.SchoolManager))
            throw new UnauthorizedSchoolAccessException("إعداد ملفات الإنجاز متاح لمدير المدرسة فقط.");
    }
    private static void Validate(ConfigureSchoolMicrosoftDriveRequest request)
    {
        if (!Guid.TryParse(request.TenantId, out _)) throw new InvalidOperationException("Tenant ID غير صالح.");
        if (!System.Net.Mail.MailAddress.TryCreate(request.SchoolMicrosoftEmail, out _)) throw new InvalidOperationException("بريد حساب المدرسة غير صالح.");
        if (new[] { request.DriveId, request.RootItemId, request.RootFolderDisplayName }.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("بيانات مجلد المدرسة غير مكتملة.");
    }
    private static SchoolMicrosoftDriveSettingsDto Map(int schoolId, SchoolMicrosoftDrive? drive) => drive is null
        ? new(schoolId, false, false, null, null, null, null, null, null)
        : new(schoolId, true, drive.IsEnabled, drive.TenantId, drive.SchoolMicrosoftEmail, drive.DriveId, drive.RootItemId, drive.RootFolderDisplayName, drive.ConnectedAtUtc);
}
