using Microsoft.AspNetCore.DataProtection;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Encrypts the school's Google Drive secrets at rest with ASP.NET Core Data Protection,
/// so a database dump alone never yields a usable Drive credential.
///
/// The purpose string is versioned: changing it would invalidate every stored credential,
/// which is why it must stay stable across releases.
/// </summary>
public sealed class GoogleDriveCredentialProtector
{
    private const string Purpose = "AlFalah.SchoolGoogleDrive.Credential.v1";
    private readonly IDataProtector _protector;

    public GoogleDriveCredentialProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector(Purpose);

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    /// <summary>
    /// Unprotects a stored secret. A tampered or key-rotated payload surfaces as a clear
    /// configuration error rather than an opaque cryptographic exception, because the fix
    /// is always the same: a manager must re-enter the credential.
    /// </summary>
    public string Unprotect(string ciphertext)
    {
        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "تعذر فك تشفير بيانات اعتماد Google Drive المحفوظة. يجب على مدير المدرسة إعادة إدخالها.", ex);
        }
    }
}
