using System.Text.Json;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Manager-owned setup for the one Google Drive account behind a school's evidence files.
/// Secrets travel in, never out: the audit trail and every response describe the connection
/// without reproducing any part of the credential.
/// </summary>
public sealed class SchoolGoogleDriveService : ISchoolGoogleDriveService
{
    private readonly AlFalahDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly AuditLogWriter _audit;
    private readonly GoogleDriveCredentialProtector _protector;
    private readonly IGoogleDriveTokenService _tokens;

    public SchoolGoogleDriveService(
        AlFalahDbContext context,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard,
        AuditLogWriter audit,
        GoogleDriveCredentialProtector protector,
        IGoogleDriveTokenService tokens)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _audit = audit;
        _protector = protector;
        _tokens = tokens;
    }

    public async Task<SchoolGoogleDriveSettingsDto> GetForCurrentSchoolAsync(CancellationToken cancellationToken = default)
    {
        EnsureManager();
        var schoolId = ResolveSchoolId();
        var drive = await _context.SchoolGoogleDrives.AsNoTracking().SingleOrDefaultAsync(x => x.SchoolId == schoolId, cancellationToken);
        return Map(schoolId, drive);
    }

    public async Task<SchoolGoogleDriveSettingsDto> ConfigureForCurrentSchoolAsync(
        ConfigureSchoolGoogleDriveRequest request, CancellationToken cancellationToken = default)
    {
        EnsureManager();
        var schoolId = ResolveSchoolId();
        var drive = await _context.SchoolGoogleDrives.SingleOrDefaultAsync(x => x.SchoolId == schoolId, cancellationToken);
        var isNew = drive is null;
        Validate(request, drive);

        var before = drive is null ? null : Describe(drive);
        if (drive is null)
        {
            drive = new SchoolGoogleDrive { SchoolId = schoolId };
            _context.SchoolGoogleDrives.Add(drive);
        }

        drive.CredentialType = request.CredentialType;
        drive.SchoolGoogleEmail = request.SchoolGoogleEmail.Trim();
        drive.SharedDriveId = Clean(request.SharedDriveId);
        drive.RootFolderId = request.RootFolderId.Trim();
        drive.RootFolderDisplayName = request.RootFolderDisplayName.Trim();
        drive.IsEnabled = request.IsEnabled;

        if (request.CredentialType == GoogleDriveCredentialType.ServiceAccount)
        {
            drive.ImpersonatedUserEmail = Clean(request.ImpersonatedUserEmail);
            // Switching grant type must not leave the previous grant's fields behind, or a
            // stale client id would be used to interpret a service-account key.
            drive.OAuthClientId = null;
            drive.ProtectedOAuthClientSecret = null;
            if (!string.IsNullOrWhiteSpace(request.ServiceAccountJson))
                drive.ProtectedCredential = _protector.Protect(request.ServiceAccountJson!.Trim());
        }
        else
        {
            drive.ImpersonatedUserEmail = null;
            var requestedClientId = Clean(request.OAuthClientId);
            var oauthClientWasReplaced = drive.CredentialType != GoogleDriveCredentialType.OAuthRefreshToken
                || !string.Equals(drive.OAuthClientId, requestedClientId, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(request.OAuthClientSecret);

            drive.OAuthClientId = requestedClientId;
            if (!string.IsNullOrWhiteSpace(request.OAuthClientSecret))
                drive.ProtectedOAuthClientSecret = _protector.Protect(request.OAuthClientSecret!.Trim());

            // Replacing OAuth client settings starts a fresh consent flow. Retaining the old
            // refresh-token blob would report a connection that cannot actually be used.
            if (oauthClientWasReplaced)
                drive.ProtectedCredential = string.Empty;
            if (!string.IsNullOrWhiteSpace(request.OAuthRefreshToken))
                drive.ProtectedCredential = _protector.Protect(request.OAuthRefreshToken!.Trim());
        }

        if (isNew) drive.ConnectedAtUtc = DateTimeOffset.UtcNow;
        drive.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _audit.Write(schoolId, _currentUser.UserId, "SchoolGoogleDrive.Configured", "SchoolGoogleDrive",
            drive.Id == 0 ? null : drive.Id.ToString(), null, before, Describe(drive));
        await _context.SaveChangesAsync(cancellationToken);

        // The stored credential may have changed under a token that is still cached, so drop
        // it: otherwise the school would keep using the old account until the token expired.
        _tokens.InvalidateCachedToken(schoolId);
        return Map(schoolId, drive);
    }

    private int ResolveSchoolId() =>
        _scopeGuard.ResolveAllowedSchoolId(null) ?? throw new UnauthorizedSchoolAccessException("اختر مدرسة قبل إعداد ملفات الإنجاز.");

    private void EnsureManager()
    {
        if (!_currentUser.IsGlobalAdmin() && !_currentUser.GetRoles().Contains(RoleNames.SchoolManager))
            throw new UnauthorizedSchoolAccessException("إعداد ملفات الإنجاز متاح لمدير المدرسة فقط.");
    }

    private static void Validate(ConfigureSchoolGoogleDriveRequest request, SchoolGoogleDrive? existing)
    {
        if (!Enum.IsDefined(request.CredentialType))
            throw new InvalidOperationException("نوع بيانات اعتماد Google Drive غير مدعوم.");
        if (!System.Net.Mail.MailAddress.TryCreate(request.SchoolGoogleEmail, out _))
            throw new InvalidOperationException("بريد حساب Google الخاص بالمدرسة غير صالح.");
        if (string.IsNullOrWhiteSpace(request.RootFolderId) || string.IsNullOrWhiteSpace(request.RootFolderDisplayName))
            throw new InvalidOperationException("معرّف المجلد الرئيسي واسمه مطلوبان.");

        // A credential is only optional on an update that keeps the SAME grant type; changing
        // type always needs fresh material, since the stored blob means something else.
        var keepsExistingCredential = existing is not null
            && existing.CredentialType == request.CredentialType
            && !string.IsNullOrWhiteSpace(existing.ProtectedCredential);

        if (request.CredentialType == GoogleDriveCredentialType.ServiceAccount)
        {
            if (string.IsNullOrWhiteSpace(request.ServiceAccountJson))
            {
                if (!keepsExistingCredential)
                    throw new InvalidOperationException("يجب إدخال مفتاح حساب الخدمة (Service Account JSON).");
            }
            else
            {
                EnsureServiceAccountJsonIsUsable(request.ServiceAccountJson!);
            }

            // Both SharedDriveId and ImpersonatedUserEmail are OPTIONAL: a service account may
            // also be pointed at an ordinary My Drive folder that has been shared with it, which
            // is enough for browsing and downloading evidence.
            //
            // Uploading is the part that can still fail: a service account owns no storage
            // quota, so a file it CREATES in a plain My Drive folder is rejected by Google with
            // `storageQuotaExceeded`. That is deliberately not blocked here — GoogleDriveClient
            // reports the quota reason verbatim if it happens, instead of this refusing a
            // configuration an administrator may have good reason to choose.
            if (!string.IsNullOrWhiteSpace(request.ImpersonatedUserEmail)
                && !System.Net.Mail.MailAddress.TryCreate(request.ImpersonatedUserEmail, out _))
                throw new InvalidOperationException("بريد المستخدم المُنتحل (Impersonated User) غير صالح.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.OAuthClientId))
                throw new InvalidOperationException("OAuth Client ID مطلوب.");
            if (string.IsNullOrWhiteSpace(request.OAuthClientSecret)
                && string.IsNullOrWhiteSpace(existing?.ProtectedOAuthClientSecret))
                throw new InvalidOperationException("OAuth Client Secret مطلوب.");

            // The refresh token is deliberately NOT required here. Obtaining one by hand is the
            // step the authorization-code flow exists to remove: a manager saves the client id
            // and secret first, which is what GoogleDriveOAuthService needs to build the consent
            // URL, and the callback then fills in ProtectedCredential. Until that happens the
            // settings DTO reports HasStoredCredential = false, which is how the UI knows to
            // show "connect" rather than "connected".
            //
            // keepsExistingCredential still matters for the service-account branch above, where
            // there is no interactive flow and the key must be present one way or another.
        }
    }

    /// <summary>
    /// Rejects an unusable service-account key at configuration time. Catching it here means
    /// the manager sees the problem on the settings screen instead of teachers discovering it
    /// as a failed upload days later.
    /// </summary>
    private static void EnsureServiceAccountJsonIsUsable(string json)
    {
        string? clientEmail;
        string? privateKey;
        try
        {
            using var document = JsonDocument.Parse(json);
            clientEmail = document.RootElement.TryGetProperty("client_email", out var email) ? email.GetString() : null;
            privateKey = document.RootElement.TryGetProperty("private_key", out var key) ? key.GetString() : null;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("مفتاح حساب الخدمة (Service Account JSON) ليس ملف JSON صالحاً.", ex);
        }

        if (string.IsNullOrWhiteSpace(clientEmail) || string.IsNullOrWhiteSpace(privateKey))
            throw new InvalidOperationException("مفتاح حساب الخدمة يجب أن يحتوي على client_email و private_key.");
        if (!privateKey!.Contains("PRIVATE KEY", StringComparison.Ordinal))
            throw new InvalidOperationException("قيمة private_key في مفتاح حساب الخدمة غير صالحة.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Audit projection. Deliberately excludes every protected field.</summary>
    private static object Describe(SchoolGoogleDrive drive) => new
    {
        CredentialType = drive.CredentialType.ToString(),
        drive.SchoolGoogleEmail,
        drive.ImpersonatedUserEmail,
        drive.OAuthClientId,
        drive.SharedDriveId,
        drive.RootFolderId,
        drive.RootFolderDisplayName,
        drive.IsEnabled
    };

    private static SchoolGoogleDriveSettingsDto Map(int schoolId, SchoolGoogleDrive? drive) => drive is null
        ? new(schoolId, false, false, null, null, null, null, null, null, null, false, null)
        : new(schoolId, true, drive.IsEnabled, drive.CredentialType, drive.SchoolGoogleEmail,
            drive.ImpersonatedUserEmail, drive.OAuthClientId, drive.SharedDriveId, drive.RootFolderId,
            drive.RootFolderDisplayName, !string.IsNullOrWhiteSpace(drive.ProtectedCredential), drive.ConnectedAtUtc);
}
