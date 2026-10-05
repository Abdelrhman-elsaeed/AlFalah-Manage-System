using System.Data;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class PrototypeImportRepository(AlFalahDbContext db, IReadinessRepository readiness) : IPrototypeImportRepository
{
    public async Task<T> BytesExclusiveAsync<T>(int school, string hash, Func<Task<T>> action, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) return await action();
        await db.Database.OpenConnectionAsync(ct);
        var resource = $"sfs-import-bytes:{school}:{hash}";
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=10000; IF @r < 0 THROW 51000, 'Import bytes lock unavailable', 1;", ct);
            return await action();
        }
        finally
        {
            try { await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sp_releaseapplock @Resource={resource}, @LockOwner='Session';", CancellationToken.None); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
    public Task<StorageOperation?> HashOperationAsync(int school, string hash, CancellationToken ct) => db.Set<StorageOperation>().AsNoTracking()
        .FirstOrDefaultAsync(x => x.SchoolId == school && x.Action == "Upload" && x.RequestKey == "import:" + hash, ct);
    public async Task<T> ExclusiveAsync<T>(int school, string key, Func<Task<T>> action, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (db.Database.IsSqlServer())
        {
            var resource = $"sfs-import:{school}:{key}";
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51000, 'Import lock unavailable', 1;", ct);
        }
        try { var result = await action(); if (tx != null) await tx.CommitAsync(ct); return result; }
        catch (DbUpdateConcurrencyException) { throw new StorageConflictException(); }
    }
    public async Task<ImportContext> ContextAsync(int school, int year, int template, CancellationToken ct)
    {
        if (!await db.AcademicYears.AnyAsync(x => x.Id == year, ct)) throw new KeyNotFoundException("السنة غير موجودة.");
        var t = await readiness.TemplateAsync(template, ct) ?? throw new KeyNotFoundException("هيّئ قالب S4 أولًا.");
        if (!await readiness.ScopeExistsAsync(school, year, template, ct)) throw new ArgumentException("هيّئ تقويم المدرسة والسنة من قالب S4 قبل الاستيراد.");
        return new(t, await db.EvidenceRequirements.AsNoTracking().Where(x => x.SchoolId == school && x.AcademicYearId == year && x.TemplateVersion == template && x.IsActive).OrderBy(x => x.Id).ToListAsync(ct),
            await readiness.MembersAsync(school, ct));
    }
    public Task<PrototypeImportBatch?> FindAsync(int school, int year, int template, string hash, CancellationToken ct) =>
        db.Set<PrototypeImportBatch>().AsTracking().SingleOrDefaultAsync(x => x.SchoolId == school && x.AcademicYearId == year && x.TemplateVersion == template && x.SourceSHA256 == hash, ct);
    public Task<PrototypeImportBatch?> BatchAsync(int school, int id, CancellationToken ct) => db.Set<PrototypeImportBatch>().AsTracking().SingleOrDefaultAsync(x => x.SchoolId == school && x.Id == id, ct);
    public async Task<IReadOnlyList<PrototypeImportBatch>> BatchesAsync(int school, CancellationToken ct) => await db.Set<PrototypeImportBatch>().AsNoTracking().Where(x => x.SchoolId == school).OrderByDescending(x => x.Id).Take(50).ToListAsync(ct);
    public async Task<IReadOnlyList<PrototypeImportRow>> RowsAsync(int school, int batch, CancellationToken ct) => await db.Set<PrototypeImportRow>().AsTracking().Where(x => x.SchoolId == school && x.BatchId == batch).OrderBy(x => x.SourceOrdinal).ToListAsync(ct);
    public Task<PrototypeImportRow?> RowAsync(int school, int batch, int id, CancellationToken ct) => db.Set<PrototypeImportRow>().AsTracking().SingleOrDefaultAsync(x => x.SchoolId == school && x.BatchId == batch && x.Id == id, ct);
    public async Task AddAsync(PrototypeImportBatch batch, IReadOnlyList<PrototypeImportRow> rows, string actor, CancellationToken ct)
    {
        db.Add(batch); await db.SaveChangesAsync(ct);
        foreach (var row in rows) row.BatchId = batch.Id;
        db.AddRange(rows); await SaveAsync(batch.SchoolId, actor, "Storage.ImportPreview", batch.Id, ct);
    }
    public async Task SaveAsync(int school, string actor, string action, int id, CancellationToken ct)
    {
        db.AuditLogs.Add(new() { SchoolId = school, UserId = actor, Action = action, EntityName = "PrototypeImportBatch", EntityId = id.ToString() });
        await db.SaveChangesAsync(ct);
    }
    public Task<int?> DuplicateFileAsync(int school, string hash, CancellationToken ct) => db.StoredFileVersions.AsNoTracking()
        .Where(x => x.SchoolId == school && x.SHA256 == hash && x.Availability == StoredFileAvailability.Available &&
            x.StoredFile.SourceKind == StoredFileSourceKind.SchoolUpload && x.StoredFile.OwnerTeacherId == null && !x.StoredFile.IsDeleted && x.StoredFile.CurrentVersionId == x.Id)
        .Select(x => (int?)x.StoredFileId).FirstOrDefaultAsync(ct);
    public Task<int?> LibraryRootAsync(int school, CancellationToken ct) => db.StorageFolders.Where(x => x.SchoolId == school && x.Kind == StorageFolderKind.SchoolLibrary && x.ParentFolderId == null && x.IsActive).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
}
