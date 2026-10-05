using AlFalah.Application.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class SchoolDriveSetupRepository(AlFalahDbContext db) : ISchoolDriveSetupRepository
{
    public async Task<bool> CanConfigureAsync(string userId, int schoolId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == userId && x.IsActive, ct) ||
            !await db.Schools.AnyAsync(x => x.Id == schoolId && x.IsActive, ct)) return false;
        var global = await (from assignment in db.UserRoles
            join role in db.Roles on assignment.RoleId equals role.Id
            where assignment.UserId == userId && role.IsActive && (role.Name == RoleNames.SuperAdmin || role.Name == RoleNames.MainManager)
            select assignment.UserId).AnyAsync(ct);
        return global || await db.UserSchoolRoles.AnyAsync(x => x.SchoolId == schoolId && x.UserId == userId && x.IsActive &&
            x.Role.IsActive && x.Role.Name == RoleNames.SchoolManager && x.School.ManagerUserId == userId, ct);
    }
    public Task<SchoolDriveSetupConnection?> ConnectionAsync(int schoolId, CancellationToken ct) =>
        db.SchoolGoogleDrives.AsNoTracking().Where(x => x.SchoolId == schoolId)
            .Select(x => new SchoolDriveSetupConnection(x.SharedDriveId, x.ProtectedCredential != "", x.UpdatedAtUtc)).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyList<SchoolDriveFolderBoundary>> BoundariesAsync(int schoolId, CancellationToken ct)
    {
        var other = await db.SchoolGoogleDrives.AsNoTracking().Where(x => x.SchoolId != schoolId && x.RootFolderId != "")
            .Select(x => new SchoolDriveFolderBoundary(x.RootFolderId, true, false)).ToListAsync(ct);
        var grants = await db.TeacherDriveFolders.AsNoTracking().Where(x => x.SchoolId == schoolId && x.IsActive)
            .Select(x => new SchoolDriveFolderBoundary(x.RootItemId, false, true)).ToListAsync(ct);
        // Retained library/archive roots must remain inside a replacement school root.
        var hasStorageSchema = !db.Database.IsRelational() || (await db.Database.GetAppliedMigrationsAsync(ct))
            .Any(x => x.EndsWith("_SchoolFileStorageFoundation", StringComparison.Ordinal));
        var roots = hasStorageSchema
            ? await db.StorageFolders.AsNoTracking().Where(x => x.SchoolId == schoolId && x.ParentFolderId == null && x.IsActive)
                .Select(x => new SchoolDriveFolderBoundary(x.DriveItemId, false, x.OwnerTeacherId != null)).ToListAsync(ct)
            : [];
        return other.Concat(grants).Concat(roots).Distinct().ToArray();
    }
}
