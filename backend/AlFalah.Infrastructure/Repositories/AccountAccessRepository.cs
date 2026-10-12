using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class AccountAccessRepository(AlFalahDbContext context) : IAccountAccessRepository
{
    public Task<bool> HasSignatureAsync(string userId, CancellationToken ct) =>
        context.Set<UserSignature>().AsNoTracking().AnyAsync(x => x.UserId == userId
            && x.SignatureDrawnData != null && x.SignatureDrawnData != "", ct);

    public Task<bool> IsActiveManagerAsync(string userId, int schoolId, CancellationToken ct) =>
        context.UserSchoolRoles.AsNoTracking().AnyAsync(x => x.UserId == userId && x.SchoolId == schoolId
            && x.IsActive && !x.IsDeleted && x.User.IsActive && x.School.IsActive
            && !x.School.IsDeleted && x.Role.Name == RoleNames.SchoolManager, ct);

    public Task<UserCredentialSnapshot?> ReadCredentialAsync(string userId, int? schoolId, CancellationToken ct) =>
        context.Users.AsNoTracking().Where(x => x.Id == userId && !x.IsDeleted
            && (schoolId == null || x.UserSchoolRoles.Any(r => r.SchoolId == schoolId
                && r.IsActive && !r.IsDeleted)))
            .Select(x => new UserCredentialSnapshot(x.Id, x.PasswordHash, x.ProtectedPassword,
                context.UserRoles.Any(r => r.UserId == x.Id && context.Roles.Any(role => role.Id == r.RoleId
                    && (role.Name == RoleNames.SuperAdmin || role.Name == RoleNames.MainManager)))))
            .SingleOrDefaultAsync(ct);

    public async Task RecordRevealAsync(string actorId, string targetId, int? schoolId, bool available, CancellationToken ct)
    {
        // Never put the password, ciphertext, or password hash in an audit payload.
        context.AuditLogs.Add(new AuditLog { UserId = actorId, SchoolId = schoolId,
            Action = available ? "User.PasswordRevealed" : "User.PasswordUnavailable",
            EntityName = "User", EntityId = targetId });
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
