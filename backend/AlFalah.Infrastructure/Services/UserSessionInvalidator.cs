using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Services;

internal static class UserSessionInvalidator
{
    public static async Task InvalidateAsync(
        AlFalahDbContext context,
        IEnumerable<string?> userIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0) return;

        var users = await context.Users
            .Where(user => ids.Contains(user.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var user in users)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.UpdatedAt = now;
        }

        var refreshTokens = await context.RefreshTokens
            .Where(token => ids.Contains(token.UserId)
                && !token.IsRevoked
                && token.ExpiresAt > now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var token in refreshTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
        }
    }
}
