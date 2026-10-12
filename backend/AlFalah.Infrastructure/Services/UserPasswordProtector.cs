using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace AlFalah.Infrastructure.Services;

public sealed class UserPasswordProtector(IDataProtectionProvider provider)
{
    private sealed record Credential(string Password, string Hash);
    private IDataProtector ForUser(string userId) => provider.CreateProtector("AlFalah.UserPassword.v1", userId);

    public string Protect(string userId, string password, string hash) =>
        ForUser(userId).Protect(JsonSerializer.Serialize(new Credential(password, hash)));

    public string? Read(string userId, string? ciphertext, string? currentHash)
    {
        if (string.IsNullOrEmpty(ciphertext) || string.IsNullOrEmpty(currentHash)) return null;
        try
        {
            var credential = JsonSerializer.Deserialize<Credential>(ForUser(userId).Unprotect(ciphertext));
            // Fail closed if a password was changed outside the normal Identity workflow.
            return credential?.Hash == currentHash ? credential.Password : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }
}
