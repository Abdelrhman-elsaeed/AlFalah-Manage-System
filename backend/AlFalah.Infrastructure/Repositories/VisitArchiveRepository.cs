using System.Data;
using System.Text.Json;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class VisitArchiveRepository(AlFalahDbContext db) : IVisitArchiveRepository
{
    private IQueryable<VisitArchiveOperation> Ops => db.Set<VisitArchiveOperation>();
    private IQueryable<ArchiveVisitScope> Visits => db.Visits.AsNoTracking()
        .Where(v => v.ExperienceVersion == ExperienceVersion.PrototypeV2)
        .Select(v => new ArchiveVisitScope { VisitId=v.Id, SchoolId=v.SchoolId, InstructorId=v.InstructorId,
            InstructorName=v.Instructor.FirstName + " " + v.Instructor.LastName, Subject=v.Subject,
            VisitDate=v.VisitDate, Category=v.VisitCategory, CreatorId=v.CreatedByUserId,
            Status=v.Status, ApprovalRevision=v.ApprovalRevision });

    public async Task<VisitV2PdfAssetSources> ApprovalAssetsAsync(int visitId, string approverId, CancellationToken ct)
    {
        var v = await db.Visits.AsNoTracking().Where(v => v.Id == visitId).Select(v => new
        {
            v.InstructorId, v.CreatedByUserId, v.School.Name, v.School.LogoUrl,
            Settings = v.School.ReportSettings
        }).SingleAsync(ct);
        var ids = new[] { v.InstructorId, v.CreatedByUserId, approverId };
        var signatures = await db.UserSignatures.AsNoTracking().Where(s => ids.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, s => !string.IsNullOrWhiteSpace(s.SignatureImageUrl) ? s.SignatureImageUrl : s.SignatureDrawnData, ct);
        return new(v.Settings?.ReportHeaderText ?? v.Name, v.Settings?.ReportFooterText ?? "", v.Settings?.PrimaryColor ?? "#0F7132",
            v.Settings?.LogoUrl ?? v.LogoUrl, signatures.GetValueOrDefault(v.InstructorId), signatures.GetValueOrDefault(v.CreatedByUserId),
            signatures.GetValueOrDefault(approverId), v.Settings?.ShowModeratorSignature ?? true, v.Settings?.ShowManagerSignature ?? true);
    }
    // Staged on the SAME scoped DbContext, saved by the visit repository's SQL transaction.
    public Task StageAsync(VisitArchiveOperation operation, CancellationToken ct)
    {
        db.Set<VisitArchiveOperation>().Add(operation);
        Audit(operation, "Created");
        return Task.CompletedTask;
    }
    public async Task MarkHistoricalAsync(int visitId, CancellationToken ct)
    {
        var rows = await db.Set<VisitArchiveArtifact>().Where(a => a.VisitId == visitId && a.IsCurrent).ToListAsync(ct);
        foreach (var row in rows) { row.IsCurrent = false; Audit(row.SchoolId, row.VisitId, row.ApprovalRevision, "Historical", row.OperationId); }
    }
    public Task<ArchiveVisitScope?> VisitAsync(int schoolId, int visitId, CancellationToken ct) =>
        Visits.SingleOrDefaultAsync(v => v.SchoolId == schoolId && v.VisitId == visitId, ct);
    private IQueryable<VisitArchiveOperation> ReadOperations => Ops.AsNoTracking().Select(o => new VisitArchiveOperation
    {
        Id=o.Id, SchoolId=o.SchoolId, VisitId=o.VisitId, ApprovalRevision=o.ApprovalRevision, Status=o.Status,
        Attempts=o.Attempts, ApprovedAtUtc=o.ApprovedAtUtc, ApprovalSource=o.ApprovalSource, LastAttemptAtUtc=o.LastAttemptAtUtc,
        CompletedAtUtc=o.CompletedAtUtc, NextAttemptAtUtc=o.NextAttemptAtUtc, LastErrorCode=o.LastErrorCode,
        DriveId=o.DriveId, SchoolRootItemId=o.SchoolRootItemId, ArchiveFolderItemId=o.ArchiveFolderItemId,
        ProviderItemId=o.ProviderItemId, UploadIdentity=o.UploadIdentity, PdfSHA256=o.PdfSHA256
    });
    public async Task<IReadOnlyList<VisitArchiveOperation>> OperationsAsync(int schoolId, int visitId, CancellationToken ct) =>
        await ReadOperations.Where(o => o.SchoolId == schoolId && o.VisitId == visitId).OrderByDescending(o => o.ApprovalRevision).ToListAsync(ct);
    public async Task<string?> SnapshotJsonAsync(int schoolId, int visitId, int revision, CancellationToken ct)
    {
        var row = await Ops.AsNoTracking().Where(o => o.SchoolId == schoolId && o.VisitId == visitId && o.ApprovalRevision == revision)
            .Select(o => new { o.SnapshotJson, o.SnapshotSHA256 }).SingleOrDefaultAsync(ct);
        if (row?.SnapshotJson == null) return null;
        if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(row.SnapshotJson))) != row.SnapshotSHA256)
            throw new ArchiveIdentityException();
        return row.SnapshotJson;
    }
    public async Task<ArchiveReadBatch> ReadBatchAsync(int schoolId, IReadOnlyList<int> visitIds, CancellationToken ct)
    {
        var operations = await ReadOperations.Where(o => o.SchoolId == schoolId && visitIds.Contains(o.VisitId)).OrderByDescending(o => o.ApprovalRevision).ToListAsync(ct);
        var artifacts = await ArtifactQuery.Where(a => a.Artifact.SchoolId == schoolId && visitIds.Contains(a.Artifact.VisitId)).ToListAsync(ct);
        var files = artifacts.Select(a => a.Version.StoredFileId).Distinct().ToArray();
        var versions = await db.StoredFileVersions.AsNoTracking().Where(v => v.SchoolId == schoolId && files.Contains(v.StoredFileId)).ToListAsync(ct);
        return new(operations, artifacts, versions);
    }
    public async Task<StoragePage<ArchiveVisitScope>> ListAsync(int schoolId, VisitArchiveQuery query, CancellationToken ct)
    {
        var q = Visits.Where(v => v.SchoolId == schoolId && Ops.Any(o => o.VisitId == v.VisitId &&
            (query.From == null || o.ApprovedAtUtc >= query.From) && (query.To == null || o.ApprovedAtUtc <= query.To) &&
            (query.Status == null || query.Status == "MissingFromDrive" &&
             db.Set<VisitArchiveArtifact>().Any(a => a.OperationId == o.Id && db.StoredFileVersions.Any(f => f.Id == a.StoredFileVersionId && f.Availability == StoredFileAvailability.Missing)))))
            .Where(v => query.TeacherId == null || v.InstructorId == query.TeacherId);
        if (query.Status != null && query.Status != "MissingFromDrive")
        {
            var status = Enum.Parse<VisitArchiveStatus>(query.Status);
            q = Visits.Where(v => v.SchoolId == schoolId && (query.TeacherId == null || v.InstructorId == query.TeacherId) &&
                Ops.Any(o => o.VisitId == v.VisitId && o.Status == status &&
                    (status != VisitArchiveStatus.Completed || !db.Set<VisitArchiveArtifact>().Any(a => a.OperationId == o.Id &&
                        db.StoredFileVersions.Any(f => f.Id == a.StoredFileVersionId && f.Availability == StoredFileAvailability.Missing))) &&
                    (query.From == null || o.ApprovedAtUtc >= query.From) && (query.To == null || o.ApprovedAtUtc <= query.To)));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(v => Ops.Where(o => o.VisitId == v.VisitId).Max(o => o.ApprovedAtUtc))
            .ThenByDescending(v => v.VisitId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }
    private IQueryable<ArchiveArtifactRow> ArtifactQuery =>
        from a in db.Set<VisitArchiveArtifact>().AsNoTracking()
        join v in db.StoredFileVersions.AsNoTracking() on a.StoredFileVersionId equals v.Id
        select new ArchiveArtifactRow { Artifact=a, Version=v };
    public async Task<IReadOnlyList<ArchiveArtifactRow>> ArtifactsAsync(int schoolId, int visitId, CancellationToken ct) =>
        await ArtifactQuery.Where(x => x.Artifact.SchoolId == schoolId && x.Artifact.VisitId == visitId).ToListAsync(ct);
    public async Task<IReadOnlyList<VisitArchiveVersionDto>> VersionsAsync(int schoolId, int fileId, CancellationToken ct) =>
        await db.StoredFileVersions.AsNoTracking().Where(v => v.SchoolId == schoolId && v.StoredFileId == fileId)
            .OrderByDescending(v => v.VersionNumber).Select(v => new VisitArchiveVersionDto(v.Id, v.VersionNumber, v.UploadedAtUtc,
                v.Availability == StoredFileAvailability.Missing ? "MissingFromDrive" : "Available", v.SizeInBytes)).ToListAsync(ct);
    public Task<StoredFileVersion?> VersionAsync(int schoolId, int fileId, int versionId, CancellationToken ct) =>
        db.StoredFileVersions.AsNoTracking().SingleOrDefaultAsync(v => v.SchoolId == schoolId && v.StoredFileId == fileId && v.Id == versionId, ct);
    public async Task<IReadOnlyList<VisitArchiveTeacherDto>> TeachersAsync(int schoolId, CancellationToken ct)
    {
        var teachers = await Visits.Where(v => v.SchoolId == schoolId && Ops.Any(o => o.VisitId == v.VisitId))
            .Select(v => new { v.InstructorId, v.InstructorName }).Distinct()
            .OrderBy(v => v.InstructorName).ThenBy(v => v.InstructorId).Take(1000).ToListAsync(ct);
        return teachers.Select(v => new VisitArchiveTeacherDto(v.InstructorId, v.InstructorName)).ToArray();
    }
    public async Task<IReadOnlyList<int>> DueAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        await Ops.AsNoTracking().Where(o => o.SnapshotJson != null &&
            (o.Status == VisitArchiveStatus.Pending || o.Status == VisitArchiveStatus.RetryScheduled && o.NextAttemptAtUtc <= now ||
             o.Status == VisitArchiveStatus.Processing && o.LeaseExpiresAtUtc <= now))
            .OrderBy(o => o.CreatedAtUtc).ThenBy(o => o.Id).Take(limit).Select(o => o.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ProtectedProviderIdsAsync(int schoolId, IReadOnlyList<string> itemIds, CancellationToken ct)
        => await ProtectedProviderIdsQuery(db, itemIds).ToListAsync(ct);

    internal static IQueryable<string> ProtectedProviderIdsQuery(AlFalahDbContext db, IReadOnlyList<string> itemIds)
    {
        // Deny-only lookup over already observed provider IDs: an external cross-school
        // move must not turn another school's archive into an ordinary teacher file.
        // No archive record, school identity or content is disclosed by this lookup.
        var versions = db.StoredFileVersions.AsNoTracking().Where(v => itemIds.Contains(v.DriveItemId) &&
            db.StoredFiles.IgnoreQueryFilters().Any(f => f.Id == v.StoredFileId && f.SourceKind == StoredFileSourceKind.VisitArchive)).Select(v => v.DriveItemId);
        var reserved = db.Set<VisitArchiveOperation>().AsNoTracking().Where(o => o.ProviderItemId != null && itemIds.Contains(o.ProviderItemId)).Select(o => o.ProviderItemId!);
        var folders = db.StorageFolders.AsNoTracking().Where(f => f.Kind == StorageFolderKind.VisitArchive && itemIds.Contains(f.DriveItemId)).Select(f => f.DriveItemId);
        return versions.Union(reserved).Union(folders);
    }

    // Session-owned SQL application lock survives lease expiry during an in-flight upload.
    // A crashed/disconnected process releases it. Persisted provider ID is the second fence
    // against an upload finishing remotely after SQL connectivity was lost.
    public async Task<bool> ExclusiveAsync(string resource, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r;";
            var parameter = cmd.CreateParameter(); parameter.ParameterName = "@resource"; parameter.Value = "SFS5:" + resource; cmd.Parameters.Add(parameter);
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
    public async Task<VisitArchiveOperation?> ClaimAsync(int id, DateTimeOffset now, Guid token, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var changed = await db.Set<VisitArchiveOperation>().Where(o => o.Id == id &&
            (o.Status == VisitArchiveStatus.Pending || o.Status == VisitArchiveStatus.RetryScheduled && o.NextAttemptAtUtc <= now ||
             o.Status == VisitArchiveStatus.Processing && o.LeaseExpiresAtUtc <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, VisitArchiveStatus.Processing)
                .SetProperty(o => o.LastErrorCode, o => o.Status == VisitArchiveStatus.Processing ? "ExpiredLease" : o.LastErrorCode)
                .SetProperty(o => o.LeaseToken, token).SetProperty(o => o.LockedAtUtc, now)
                .SetProperty(o => o.LeaseExpiresAtUtc, now.AddMinutes(2)).SetProperty(o => o.LastAttemptAtUtc, now)
                .SetProperty(o => o.Attempts, o => o.Attempts + 1).SetProperty(o => o.UpdatedAtUtc, now), ct);
        if (changed != 1) return null;
        var operation = await db.Set<VisitArchiveOperation>().SingleAsync(o => o.Id == id, ct);
        Audit(operation, "Claimed"); await db.SaveChangesAsync(ct);
        return operation;
    }
    public async Task RenewAsync(int id, Guid token, DateTimeOffset now, CancellationToken ct)
    {
        // Separate connection allows renewal while the scoped repository is doing I/O.
        await using var connection = new SqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE VisitArchiveOperations SET LeaseExpiresAtUtc=@expiry WHERE Id=@id AND LeaseToken=@token AND Status=2";
        cmd.Parameters.AddWithValue("@expiry", now.AddMinutes(2)); cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@token", token);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new StorageConflictException();
    }
    public async Task SaveAsync(VisitArchiveOperation operation, string action, CancellationToken ct)
    {
        var state = await Ops.AsNoTracking().Where(o => o.Id == operation.Id).Select(o => new { o.RowVersion, o.LeaseToken }).SingleAsync(ct);
        if (operation.LeaseToken != state.LeaseToken) throw new StorageConflictException();
        db.Entry(operation).Property(o => o.RowVersion).OriginalValue = state.RowVersion;
        Audit(operation, action); await db.SaveChangesAsync(ct);
    }
    public Task<StorageFolder?> ArchiveFolderAsync(int schoolId, CancellationToken ct) =>
        db.StorageFolders.AsTracking().SingleOrDefaultAsync(f => f.SchoolId == schoolId && f.Kind == StorageFolderKind.VisitArchive && f.ParentFolderId == null && f.IsActive, ct);
    public async Task AddArchiveFolderAsync(StorageFolder folder, CancellationToken ct)
    { db.StorageFolders.Add(folder); await db.SaveChangesAsync(ct); }
    public async Task FailAsync(int id, Guid token, string code, DateTimeOffset now, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var operation = await db.Set<VisitArchiveOperation>().SingleAsync(o => o.Id == id, ct);
        if (operation.LeaseToken != token) return;
        operation.Status = operation.Attempts >= 5 || code == "IdentityMismatch" ? VisitArchiveStatus.NeedsAttention : VisitArchiveStatus.RetryScheduled;
        operation.LastErrorCode = code;
        operation.NextAttemptAtUtc = operation.Status == VisitArchiveStatus.RetryScheduled ? now.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, operation.Attempts - 1))) : null;
        operation.LeaseExpiresAtUtc = null;
        Audit(operation, "Failed"); await db.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(VisitArchiveOperation operation, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize current-pointer decisions with concurrent reopen/approval updates.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Visits WITH (UPDLOCK,HOLDLOCK) WHERE Id={operation.VisitId}", ct);
        var visit = await db.Visits.AsNoTracking().SingleOrDefaultAsync(v => v.Id == operation.VisitId, ct);
        var current = visit is { Status: VisitStatus.Approved } && visit.ApprovalRevision == operation.ApprovalRevision;
        if (current) await MarkHistoricalAsync(operation.VisitId, ct);
        await db.SaveChangesAsync(ct); // Release filtered unique Current index before assigning a replacement.
        var artifact = await db.Set<VisitArchiveArtifact>().SingleOrDefaultAsync(a => a.OperationId == operation.Id, ct);
        StoredFile file;
        if (artifact == null)
        {
            file = new StoredFile { SchoolId = operation.SchoolId, FolderId = operation.ArchiveFolderId!.Value,
                SourceKind = StoredFileSourceKind.VisitArchive, DisplayName = $"تقرير-زيارة-{operation.VisitId}-اعتماد-{operation.ApprovalRevision}.pdf", NeedsLink = true };
            db.StoredFiles.Add(file); await db.SaveChangesAsync(ct);
        }
        else
        {
            var original = await db.StoredFileVersions.SingleAsync(v => v.Id == artifact.StoredFileVersionId, ct);
            file = await db.StoredFiles.AsTracking().SingleAsync(f => f.Id == original.StoredFileId, ct);
        }
        var number = (await db.StoredFileVersions.Where(v => v.StoredFileId == file.Id).Select(v => (int?)v.VersionNumber).MaxAsync(ct) ?? 0) + 1;
        var version = new StoredFileVersion { SchoolId = operation.SchoolId, StoredFileId = file.Id, VersionNumber = number,
            DriveId = operation.DriveId!, DriveItemId = operation.ProviderItemId!, DriveFileName = file.DisplayName, FileExtension = ".pdf",
            SHA256 = operation.PdfSHA256, SizeInBytes = operation.PdfBytes!.LongLength, MimeType = "application/pdf",
            UploadedAtUtc = DateTimeOffset.UtcNow, Availability = StoredFileAvailability.Available };
        db.StoredFileVersions.Add(version); await db.SaveChangesAsync(ct);
        file.CurrentVersionId = version.Id;
        if (artifact == null)
        {
            artifact = new VisitArchiveArtifact { SchoolId = operation.SchoolId, VisitId = operation.VisitId,
                ApprovalRevision = operation.ApprovalRevision, OperationId = operation.Id, OriginalStoredFileVersionId = version.Id };
            db.Set<VisitArchiveArtifact>().Add(artifact);
        }
        artifact.StoredFileVersionId = version.Id; artifact.IsCurrent = current; artifact.LastReconciledAtUtc = DateTimeOffset.UtcNow;
        operation.Status = VisitArchiveStatus.Completed; operation.CompletedAtUtc = DateTimeOffset.UtcNow;
        operation.NextAttemptAtUtc = null; operation.LeaseExpiresAtUtc = null; operation.LastErrorCode = null;
        await SaveAsync(operation, "Completed", ct);
        Audit(operation, current ? "Current" : "Historical"); await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task SetAvailabilityAsync(ArchiveArtifactRow row, bool missing, DateTimeOffset now, CancellationToken ct)
    {
        if (row.Version.Availability == (missing ? StoredFileAvailability.Missing : StoredFileAvailability.Available) &&
            row.Artifact.LastReconciledAtUtc > now.AddMinutes(-15)) return;
        var version = await db.StoredFileVersions.AsTracking().SingleAsync(v => v.Id == row.Version.Id, ct);
        var artifact = await db.Set<VisitArchiveArtifact>().SingleAsync(a => a.Id == row.Artifact.Id, ct);
        if (version.Availability == (missing ? StoredFileAvailability.Missing : StoredFileAvailability.Available) &&
            artifact.LastReconciledAtUtc > now.AddMinutes(-15)) return;
        version.Availability = missing ? StoredFileAvailability.Missing : StoredFileAvailability.Available;
        version.MissingFromDriveAtUtc = missing ? version.MissingFromDriveAtUtc ?? now : null;
        artifact.LastReconciledAtUtc = now;
        Audit(artifact.SchoolId, artifact.VisitId, artifact.ApprovalRevision, missing ? "MissingFromDrive" : "Reconciled", artifact.OperationId);
        await db.SaveChangesAsync(ct);
    }
    public async Task RetryAsync(int schoolId, int visitId, RetryVisitArchiveRequest request, string actor, CancellationToken ct)
    {
        var id = await Ops.Where(o => o.SchoolId == schoolId && o.VisitId == visitId && o.ApprovalRevision == request.ApprovalRevision).Select(o => o.Id).SingleOrDefaultAsync(ct);
        if (id == 0) throw new KeyNotFoundException("لا توجد عملية لهذا الاعتماد.");
        var acquired = await ExclusiveAsync("operation:" + id, async token =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(token);
            var o = await db.Set<VisitArchiveOperation>().SingleAsync(o => o.Id == id, token);
            if (o.SnapshotJson == null || o.Status is VisitArchiveStatus.Pending or VisitArchiveStatus.Processing) throw new StorageConflictException();
            if (o.Status == VisitArchiveStatus.Completed)
            {
                var a = await db.Set<VisitArchiveArtifact>().SingleAsync(a => a.OperationId == id, token);
                var version = await db.StoredFileVersions.SingleAsync(v => v.Id == a.StoredFileVersionId, token);
                if (!request.RecreateMissing || version.Availability != StoredFileAvailability.Missing || string.IsNullOrWhiteSpace(request.Reason)) throw new StorageConflictException();
                o.ProviderItemId = null; o.UploadIdentity = null; o.UploadStarted = false; o.RecoveryGeneration++;
            }
            else if (request.RecreateMissing) throw new StorageConflictException();
            o.Status = VisitArchiveStatus.Pending; o.NextAttemptAtUtc = null; o.Attempts = 0; o.LeaseToken = null; o.LeaseExpiresAtUtc = null;
            o.LastErrorCode = null;
            Audit(schoolId, visitId, o.ApprovalRevision, request.RecreateMissing ? "RecreateAuthorized" : "Retry", id, actor, request.Reason);
            await db.SaveChangesAsync(token); await tx.CommitAsync(token);
        }, ct);
        if (!acquired) throw new StorageConflictException();
    }
    public async Task<IReadOnlyList<ArchiveArtifactRow>> ReconcileBatchAsync(int afterId, int limit, CancellationToken ct) =>
        await ArtifactQuery.Where(x => x.Artifact.Id > afterId).OrderBy(x => x.Artifact.Id).Take(limit).ToListAsync(ct);
    private void Audit(VisitArchiveOperation o, string action) => Audit(o.SchoolId, o.VisitId, o.ApprovalRevision, action, o.Id);
    private void Audit(int school, int visit, int revision, string action, int operation, string? actor = null, string? reason = null) =>
        db.AuditLogs.Add(new AuditLog { SchoolId = school, UserId = actor, Action = "Storage.Archive." + action,
            EntityName = "VisitArchiveOperation", EntityId = operation.ToString(), Reason = reason,
            NewValues = JsonSerializer.Serialize(new { VisitId = visit, SchoolId = school, ApprovalRevision = revision, OperationId = operation }) });
}
