using System.Data;
using System.Text.Json;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class StorageRepository(AlFalahDbContext db) : IStorageRepository
{
    public async Task<StorageActorScope?> GetActorScopeAsync(string userId, int schoolId, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive, ct)) return null;
        var school = await db.Schools.AsNoTracking().Where(x => x.Id == schoolId && x.IsActive)
            .Select(x => new { x.ManagerUserId }).SingleOrDefaultAsync(ct);
        if (school is null) return null;
        var member = await db.UserSchoolRoles.AsNoTracking().AnyAsync(x => x.SchoolId == schoolId && x.UserId == userId && x.IsActive, ct);
        return new(userId, schoolId, school.ManagerUserId, member);
    }

    public Task<bool> HasPermissionAsync(string userId, int schoolId, string permission, CancellationToken ct) =>
        (from assignment in db.UserSchoolRoles.AsNoTracking()
         join rolePermission in db.RolePermissions.AsNoTracking() on assignment.RoleId equals rolePermission.RoleId
         where assignment.UserId == userId && assignment.SchoolId == schoolId && assignment.IsActive && rolePermission.Permission.Name == permission
         select rolePermission.Id).AnyAsync(ct);

    public Task<bool> HasDelegationAsync(string userId, int schoolId, DateTimeOffset now, CancellationToken ct) =>
        db.StorageDelegations.AsNoTracking().AnyAsync(x => x.SchoolId == schoolId && x.GranteeUserId == userId &&
            x.RevokedAt == null && x.StartsAt <= now && (x.ExpiresAt == null || x.ExpiresAt > now), ct);

    public Task<bool> HasOverlappingDelegationAsync(int schoolId, string userId, DateTimeOffset start, DateTimeOffset? end, CancellationToken ct) =>
        db.StorageDelegations.AsNoTracking().AnyAsync(x => x.SchoolId == schoolId && x.GranteeUserId == userId && x.RevokedAt == null &&
            (x.ExpiresAt == null || x.ExpiresAt > start) && (end == null || x.StartsAt < end), ct);

    public async Task<IReadOnlyList<StorageDelegation>> GetDelegationsAsync(int schoolId, CancellationToken ct) =>
        await db.StorageDelegations.AsNoTracking().Where(x => x.SchoolId == schoolId).OrderByDescending(x => x.StartsAt).ToListAsync(ct);

    public Task<StorageDelegation?> GetDelegationAsync(int schoolId, int id, CancellationToken ct) =>
        db.StorageDelegations.AsTracking().SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.Id == id, ct);

    public async Task AddDelegationAsync(StorageDelegation delegation, CancellationToken ct)
    {
        db.StorageDelegations.Add(delegation);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(Audit(delegation, "Storage.DelegationGranted", delegation.GrantedByManagerUserId, delegation.Reason));
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeDelegationAsync(StorageDelegation delegation, byte[] expectedVersion, CancellationToken ct)
    {
        db.Entry(delegation).Property(x => x.RowVersion).OriginalValue = expectedVersion;
        db.AuditLogs.Add(Audit(delegation, "Storage.DelegationRevoked", delegation.RevokedByManagerUserId!, delegation.RevocationReason!));
        await db.SaveChangesAsync(ct);
    }

    public async Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        try
        {
            var result = await operation();
            if (transaction is not null) await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException) { throw new StorageConflictException(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627 or 1205)
        { throw new StorageConflictException(); }
        catch (SqlException ex) when (ex.Number == 1205) { throw new StorageConflictException(); }
        catch (InvalidOperationException ex) when (IsDeadlock(ex)) { throw new StorageConflictException(); }
    }

    public Task<StorageFileAccess?> GetFileAccessAsync(int schoolId, int id, CancellationToken ct) =>
        (from file in db.StoredFiles.AsNoTracking()
         join version in db.StoredFileVersions.AsNoTracking() on file.CurrentVersionId equals version.Id
         join teacher in db.InstructorProfiles.AsNoTracking() on file.OwnerTeacherId equals teacher.Id into owners
         from teacher in owners.DefaultIfEmpty()
         where file.SchoolId == schoolId && file.Id == id
         select new StorageFileAccess(file.Id, file.SchoolId, file.OwnerTeacherId, teacher == null ? null : teacher.UserId,
             teacher != null && teacher.IsActive && teacher.SchoolId == schoolId && teacher.User.IsActive,
             version.DriveId, version.DriveItemId, file.SourceKind, version.Availability, file.IsDeleted)).SingleOrDefaultAsync(ct);

    public Task<StorageDriveRoot?> GetSchoolDriveRootAsync(int schoolId, CancellationToken ct) =>
        db.SchoolGoogleDrives.AsNoTracking().Where(x => x.SchoolId == schoolId && x.IsEnabled)
            .Select(x => new StorageDriveRoot(x.SharedDriveId ?? "", x.RootFolderId)).SingleOrDefaultAsync(ct);

    public Task<StorageDriveRoot?> GetTeacherDriveRootAsync(int schoolId, int teacherId, CancellationToken ct) =>
        db.TeacherDriveFolders.AsNoTracking().Where(x => x.SchoolId == schoolId && x.TeacherId == teacherId && x.IsActive &&
            db.SchoolGoogleDrives.Any(d => d.SchoolId == schoolId && d.IsEnabled && (d.SharedDriveId ?? "") == x.DriveId))
            .Select(x => new StorageDriveRoot(x.DriveId, x.RootItemId)).SingleOrDefaultAsync(ct);

    private static AuditLog Audit(StorageDelegation d, string action, string actor, string reason) => new()
    {
        SchoolId = d.SchoolId, UserId = actor, Action = action, EntityName = nameof(StorageDelegation),
        EntityId = d.Id.ToString(), Reason = reason,
        NewValues = JsonSerializer.Serialize(new { d.Id, d.GranteeUserId, d.StartsAt, d.ExpiresAt, d.RevokedAt })
    };

    private static bool IsDeadlock(Exception error)
    {
        for (Exception? cause = error; cause is not null; cause = cause.InnerException)
            if (cause is SqlException sql && sql.Number == 1205) return true;
        return false;
    }
}
