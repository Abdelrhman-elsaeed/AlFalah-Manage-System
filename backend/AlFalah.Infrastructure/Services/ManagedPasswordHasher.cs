using AlFalah.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AlFalah.Infrastructure.Services;

/// <summary>Keep Identity's one-way hash, alongside a separately protected manager-visible copy.</summary>
public sealed class ManagedPasswordHasher(UserPasswordProtector protector, IOptions<PasswordHasherOptions> options)
    : PasswordHasher<ApplicationUser>(options)
{
    public override string HashPassword(ApplicationUser user, string password)
    {
        var hash = base.HashPassword(user, password);
        user.ProtectedPassword = protector.Protect(user.Id, password, hash);
        return hash;
    }
}
