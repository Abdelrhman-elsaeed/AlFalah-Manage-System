using System.Text.Json;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;
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
    private readonly ISchoolDriveFolderService? _folders;
    private readonly ISchoolDriveSetupRepository? _setup;

    public SchoolGoogleDriveService(
        AlFalahDbContext context,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard,
        AuditLogWriter audit,
        GoogleDriveCredentialProtector protector,
        IGoogleDriveTokenService tokens,
        ISchoolDriveFolderService? folders = null,
        ISchoolDriveSetupRepository? setup = null)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _audit = audit;
        _protector = protector;
        _tokens = tokens;
        _folders = folders;
        _setup = setup;
    }

    public async Task<SchoolGoogleDriveSettingsDto> GetForCurrentSchoolAsync(CancellationToken cancellationToken = default)
    {
        EnsureManager();
        var schoolId = ResolveSchoolId();
        await EnsureCurrentManagerAsync(schoolId, cancellationToken);
        var drive = await _context.SchoolGoogleDrives.AsNoTracking().SingleOrDefaultAsync(x => x.SchoolId == schoolId, cancellationToken);
        return Map(schoolId, drive);
    }

    public async Task<SchoolGoogleDriveSettingsDto> ConfigureForCurrentSchoolAsync(
        ConfigureSchoolGoogleDriveRequest request, CancellationToken cancellationToken = default)
    {
        EnsureManager();
        var schoolId = ResolveSchoolId();
        await EnsureCurrentManagerAsync(schoolId, cancellationToken);
        var drive = await _context.SchoolGoogleDrives.SingleOrDefaultAsync(x => x.SchoolId == schoolId, cancellationToken);
        var isNew = drive is null;
        Validate(request, drive);

        SchoolDriveRootSelection? selection = null;
        if (_folders is not null && (request.IsEnabled ||
            !string.IsNullOrWhiteSpace(request.RootFolderId) && request.RootFolderId.Trim() != drive?.RootFolderId))
        {
            if (request.IsEnabled && (request.CredentialType != drive?.CredentialType ||
                !string.IsNullOrWhiteSpace(request.ServiceAccountJson) || !string.IsNullOrWhiteSpace(request.OAuthClientSecret) ||
                request.OAuthClientId?.Trim() != drive?.OAuthClientId))
                throw new BusinessRuleException("احفظ بيانات الاتصال أولًا وأكمل الربط، ثم اختر المجلد وفعّل الملفات.");
            selection = await _folders.ValidateRootAsync(request.RootFolderId.Trim(), cancellationToken);
        }

        var before = drive is null ? null : Describe(drive);
        var previousType = drive?.CredentialType;
        var previousRoot = drive?.RootFolderId;
        var previousSharedDrive = drive?.SharedDriveId;
        var previousSchoolEmail = drive?.SchoolGoogleEmail;
        if (drive is null)
        {
            drive = new SchoolGoogleDrive { SchoolId = schoolId };
            _context.SchoolGoogleDrives.Add(drive);
        }

        drive.CredentialType = request.CredentialType;
        drive.SchoolGoogleEmail = request.SchoolGoogleEmail.Trim();
        drive.SharedDriveId = selection is not null ? selection.SharedDriveId : Clean(request.SharedDriveId);
        drive.RootFolderId = selection?.ItemId ?? request.RootFolderId.Trim();
        drive.RootFolderDisplayName = selection?.Name ?? request.RootFolderDisplayName.Trim();
        drive.IsEnabled = request.IsEnabled;
        if (!request.IsEnabled || previousRoot != null &&
            (previousRoot != drive.RootFolderId || previousSharedDrive != drive.SharedDriveId ||
             previousType != drive.CredentialType || previousSchoolEmail != drive.SchoolGoogleEmail))
            drive.VisitArchiveEnabled = false;

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
            var oauthClientWasReplaced = previousType != GoogleDriveCredentialType.OAuthRefreshToken
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
        await EnsureCurrentManagerAsync(schoolId, cancellationToken);
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
        _scopeGuard.ResolveAllowedSchoolId(_currentUser.ActiveSchoolId) ?? throw new UnauthorizedSchoolAccessException("اختر مدرسة قبل إعداد ملفات الإنجاز.");

    private void EnsureManager()
    {
        if (!_currentUser.IsGlobalAdmin() && !_currentUser.GetRoles().Contains(RoleNames.SchoolManager))
            throw new UnauthorizedSchoolAccessException("إعداد ملفات الإنجاز متاح لمدير المدرسة فقط.");
    }

    private async Task EnsureCurrentManagerAsync(int schoolId, CancellationToken ct)
    {
        if (_setup is not null && (_currentUser.UserId is null || !await _setup.CanConfigureAsync(_currentUser.UserId, schoolId, ct)))
            throw new UnauthorizedSchoolAccessException("لم تعد تملك صلاحية إعداد حساب Google لهذه المدرسة.");
    }

    private static void Validate(ConfigureSchoolGoogleDriveRequest request, SchoolGoogleDrive? existing)
    {
        if (!Enum.IsDefined(request.CredentialType))
            throw new InvalidOperationException("نوع بيانات اعتماد Google Drive غير مدعوم.");
        if (!System.Net.Mail.MailAddress.TryCreate(request.SchoolGoogleEmail, out _))
            throw new InvalidOperationException("بريد حساب Google الخاص بالمدرسة غير صالح.");
        if (request.IsEnabled && (string.IsNullOrWhiteSpace(request.RootFolderId) || string.IsNullOrWhiteSpace(request.RootFolderDisplayName)))
            throw new InvalidOperationException("معرّف المجلد الرئيسي واسمه مطلوبان.");
        if (request.RootFolderId is null || request.RootFolderDisplayName is null || request.RootFolderId.Length > 256 || request.RootFolderDisplayName.Length > 256)
            throw new InvalidOperationException("بيانات المجلد الرئيسي غير صالحة.");

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
            drive.RootFolderDisplayName, !string.IsNullOrWhiteSpace(drive.ProtectedCredential), drive.ConnectedAtUtc,
            !string.IsNullOrWhiteSpace(drive.ProtectedOAuthClientSecret));
}
