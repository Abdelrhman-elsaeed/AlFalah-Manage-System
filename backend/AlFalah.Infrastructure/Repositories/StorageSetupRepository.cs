using System.Data;
using System.Text.Json;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class StorageSetupRepository(AlFalahDbContext db) : IStorageSetupRepository
{
    public async Task<IReadOnlyList<SetupTeacher>> TeachersAsync(int schoolId, CancellationToken ct) =>
        await db.InstructorProfiles.AsNoTracking().Where(t => t.SchoolId == schoolId && t.IsActive && !t.IsDeleted && t.User.IsActive)
            .OrderBy(t => t.Id).Select(t => new SetupTeacher(t.Id,
                (t.User.FirstName + " " + t.User.LastName).Trim(),
                db.TeacherDriveFolders.Where(m => m.TeacherId == t.Id && m.SchoolId == schoolId && m.IsActive)
                    .Select(m => m.RootItemId).FirstOrDefault())).ToListAsync(ct);

    public Task<StorageFolder?> TrackedFolderAsync(int schoolId, string itemId, CancellationToken ct) =>
        db.StorageFolders.AsTracking().SingleOrDefaultAsync(f => f.SchoolId == schoolId && f.DriveItemId == itemId && f.IsActive, ct);

    public Task<StorageFolder?> TrackedRootAsync(int schoolId, StorageFolderKind kind, CancellationToken ct) =>
        db.StorageFolders.AsTracking().SingleOrDefaultAsync(f => f.SchoolId == schoolId && f.Kind == kind &&
            f.OwnerTeacherId == null && f.ParentFolderId == null && f.IsActive, ct);

    public Task<StorageFolder?> TrackedChildAsync(int schoolId, int parentId, string name, CancellationToken ct) =>
        db.StorageFolders.AsTracking().SingleOrDefaultAsync(f => f.SchoolId == schoolId && f.ParentFolderId == parentId &&
            f.DisplayName == name && f.IsActive, ct);

    public Task<StorageFolder?> TrackedArchiveTeacherAsync(int schoolId, int parentId, int teacherId, CancellationToken ct) =>
        db.StorageFolders.AsTracking().SingleOrDefaultAsync(f => f.SchoolId == schoolId && f.ParentFolderId == parentId &&
            f.OwnerTeacherId == teacherId && f.Kind == StorageFolderKind.VisitArchive && f.IsActive, ct);

    public Task<StorageFolder?> TrackedTeacherRootAsync(int schoolId, int teacherId, CancellationToken ct) =>
        db.StorageFolders.AsTracking().SingleOrDefaultAsync(f => f.SchoolId == schoolId && f.OwnerTeacherId == teacherId &&
            f.ParentFolderId == null && f.IsActive, ct);

    public Task<bool> HasTeacherEvidenceAsync(int schoolId, int teacherId, CancellationToken ct) =>
        db.TeacherEvidenceSubmissions.AsNoTracking().AnyAsync(s => s.SchoolId == schoolId && s.TeacherId == teacherId && !s.IsDeleted, ct);

    public async Task<bool> HasProtectedDataAsync(int schoolId, int folderId, CancellationToken ct)
    {
        var ids = await DescendantIdsAsync(schoolId, folderId, ct);
        return await db.StoredFiles.AsNoTracking().AnyAsync(f => f.SchoolId == schoolId && ids.Contains(f.FolderId), ct)
            || await db.Set<StorageOperation>().AsNoTracking().AnyAsync(o => o.SchoolId == schoolId && o.FolderId.HasValue &&
                ids.Contains(o.FolderId.Value) && o.Action != "CreateFolder", ct)
            || await db.VisitArchiveOperations.AsNoTracking().AnyAsync(o => o.SchoolId == schoolId && o.ArchiveFolderId.HasValue &&
                ids.Contains(o.ArchiveFolderId.Value), ct);
    }

    private async Task<HashSet<int>> DescendantIdsAsync(int schoolId, int folderId, CancellationToken ct)
    {
        var parents = await db.StorageFolders.AsNoTracking().Where(f => f.SchoolId == schoolId)
            .Select(f => new { f.Id, f.ParentFolderId }).ToListAsync(ct);
        var ids = new HashSet<int> { folderId };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var row in parents)
                if (row.ParentFolderId is int parent && ids.Contains(parent)) changed |= ids.Add(row.Id);
        }
        return ids;
    }

    public async Task<SetupRecoveryImpact> RecoveryImpactAsync(int schoolId, int folderId, CancellationToken ct)
    {
        var ids = await DescendantIdsAsync(schoolId, folderId, ct);
        var files = await db.StoredFiles.AsNoTracking().CountAsync(f => f.SchoolId == schoolId && ids.Contains(f.FolderId), ct);
        var operations = await db.Set<StorageOperation>().AsNoTracking().CountAsync(o => o.SchoolId == schoolId &&
            o.FolderId.HasValue && ids.Contains(o.FolderId.Value) && o.Action != "CreateFolder", ct);
        operations += await db.VisitArchiveOperations.AsNoTracking().CountAsync(o => o.SchoolId == schoolId &&
            o.ArchiveFolderId.HasValue && ids.Contains(o.ArchiveFolderId.Value), ct);
        return new(ids.Count, files, operations);
    }

    public async Task DeactivateSubtreeAsync(int schoolId, int folderId, string actor, CancellationToken ct)
    {
        var ids = await DescendantIdsAsync(schoolId, folderId, ct);
        var rows = await db.StorageFolders.Where(f => f.SchoolId == schoolId && ids.Contains(f.Id) && f.IsActive).ToListAsync(ct);
        foreach (var row in rows)
        {
            row.IsActive = false;
            row.UpdatedAtUtc = DateTimeOffset.UtcNow;
            row.UpdatedByUserId = actor;
        }
        db.AuditLogs.Add(new() { SchoolId = schoolId, UserId = actor,
            Action = "Storage.SetupLegacyLinksDetached", EntityName = nameof(StorageFolder), EntityId = folderId.ToString(),
            OldValues = JsonSerializer.Serialize(new { FolderIds = ids.Order().ToArray() }),
            Reason = "Explicit manager confirmation to detach missing Drive subtree; historical files remain recorded." });
        await db.SaveChangesAsync(ct);
    }

    public async Task RebindFolderAsync(StorageFolder folder, string driveId, string itemId, string name, string actor, CancellationToken ct)
    {
        var oldItemId = folder.DriveItemId;
        folder.DriveId = driveId;
        folder.DriveItemId = itemId;
        folder.DisplayName = name;
        folder.UpdatedByUserId = actor;
        folder.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new() { SchoolId = folder.SchoolId, UserId = actor, Action = "Storage.SetupFolderRebound",
            EntityName = nameof(StorageFolder), EntityId = folder.Id.ToString(),
            OldValues = JsonSerializer.Serialize(new { DriveItemId = oldItemId }),
            NewValues = JsonSerializer.Serialize(new { DriveItemId = itemId }) });
        await db.SaveChangesAsync(ct);
    }

    public async Task TrackAsync(StorageFolder folder, CancellationToken ct)
    {
        db.StorageFolders.Add(folder);
        db.AuditLogs.Add(new() { SchoolId = folder.SchoolId, UserId = folder.CreatedByUserId,
            Action = "Storage.SetupFolderTracked", EntityName = "StorageFolder", EntityId = folder.DriveItemId });
        await db.SaveChangesAsync(ct);
    }

    public Task<StorageOperation?> ReservationAsync(int schoolId, string key, CancellationToken ct) =>
        db.Set<StorageOperation>().AsTracking().SingleOrDefaultAsync(o => o.SchoolId == schoolId && o.Action == "CreateFolder" && o.RequestKey == key, ct);

    public async Task<IReadOnlyList<StorageOperation>> ReservationsForFolderAsync(int schoolId, string parentItemId, string name, CancellationToken ct) =>
        await db.Set<StorageOperation>().AsTracking().Where(o => o.SchoolId == schoolId && o.Action == "CreateFolder" &&
            o.ParentItemId == parentItemId && o.DisplayName == name).OrderByDescending(o => o.Id).Take(1).ToListAsync(ct);

    public async Task ReserveAsync(StorageOperation operation, CancellationToken ct)
    { db.Set<StorageOperation>().Add(operation); await db.SaveChangesAsync(ct); }

    public Task SaveReservationAsync(StorageOperation operation, CancellationToken ct) => db.SaveChangesAsync(ct);

    public Task<bool> ArchiveEnabledAsync(int schoolId, CancellationToken ct) =>
        db.SchoolGoogleDrives.AsNoTracking().AnyAsync(d => d.SchoolId == schoolId && d.IsEnabled && d.VisitArchiveEnabled, ct);

    public async Task SetArchiveEnabledAsync(int schoolId, bool enabled, string actor, CancellationToken ct)
    {
        var connection = await db.SchoolGoogleDrives.SingleAsync(d => d.SchoolId == schoolId && d.IsEnabled, ct);
        if (connection.VisitArchiveEnabled == enabled) return;
        connection.VisitArchiveEnabled = enabled;
        connection.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new() { SchoolId = schoolId, UserId = actor, Action = enabled ? "Storage.VisitArchiveEnabled" : "Storage.VisitArchiveDisabled",
            EntityName = nameof(SchoolGoogleDrive), EntityId = connection.Id.ToString() });
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExclusiveAsync(int schoolId, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) { await action(ct); return true; }
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r;";
            var parameter = cmd.CreateParameter(); parameter.ParameterName = "@resource";
            parameter.Value = "SFS5:folder:" + schoolId; cmd.Parameters.Add(parameter);
            if (Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) < 0) return false;
            try { await action(ct); return true; }
            finally
            {
                cmd.CommandText = "EXEC sys.sp_releaseapplock @Resource=@resource,@LockOwner='Session';";
                if (cmd.Connection?.State == ConnectionState.Open) await cmd.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
