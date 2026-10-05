using System.Linq.Expressions;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class StorageLibraryRepository(AlFalahDbContext db) : IStorageLibraryRepository
{
    private IQueryable<StoredFile> Files(int school) => db.StoredFiles.AsNoTracking().Where(x => x.SchoolId == school);
    private Expression<Func<StoredFile, bool>> Protected => f =>
        f.SourceKind == StoredFileSourceKind.HistoricalImport || f.SourceKind == StoredFileSourceKind.VisitArchive ||
        db.EvidenceReviewDecisions.Any(d => d.SchoolId == f.SchoolId && d.StoredFileId == f.Id && d.Decision == EvidenceReviewStatus.Approved) ||
        db.EvidenceLinks.Any(l => l.SchoolId == f.SchoolId && l.StoredFileId == f.Id && l.Status == EvidenceLinkStatus.Approved) ||
        db.TeacherEvidenceSubmissions.IgnoreQueryFilters().Any(s => s.Id == f.LegacySubmissionId && s.ReviewStatus == EvidenceReviewStatus.Approved) ||
        db.StoredFileVersions.Any(v => v.SchoolId == f.SchoolId && v.StoredFileId == f.Id && v.VersionNumber > 1);

    public Task<StorageTeacher?> FindTeacherAsync(int schoolId, string userId, CancellationToken ct) =>
        (from t in db.InstructorProfiles.AsNoTracking()
         join m in db.TeacherDriveFolders.AsNoTracking() on t.Id equals m.TeacherId
         where t.SchoolId == schoolId && t.UserId == userId && t.IsActive && t.User.IsActive && m.SchoolId == schoolId && m.IsActive
         select new StorageTeacher(t.Id, t.UserId, new(m.DriveId, m.RootItemId), m.FolderDisplayName)).SingleOrDefaultAsync(ct);

    public async Task<StorageContextDto> ContextAsync(int schoolId, CancellationToken ct)
    {
        var name = await db.Schools.Where(x => x.Id == schoolId).Select(x => x.Name).SingleAsync(ct);
        var year = await db.AcademicYears.Where(x => x.IsActive).OrderByDescending(x => x.Id)
            .Select(x => new { x.Id, x.NameAr }).FirstOrDefaultAsync(ct);
        return new(schoolId, name, year?.Id, year?.NameAr, false, false, "Unavailable", null);
    }

    public async Task<IReadOnlyList<StorageDriveRoot>> TeacherRootsAsync(int schoolId, CancellationToken ct) =>
        await db.TeacherDriveFolders.AsNoTracking().Where(x => x.SchoolId == schoolId && x.IsActive)
            .Select(x => new StorageDriveRoot(x.DriveId, x.RootItemId)).ToListAsync(ct);
    public Task<StorageFolder?> FindFolderAsync(int schoolId, int id, CancellationToken ct) =>
        db.StorageFolders.AsTracking().SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.Id == id && x.IsActive, ct);
    public Task<StorageFolder?> FindProviderFolderAsync(int schoolId, string itemId, CancellationToken ct) =>
        db.StorageFolders.AsTracking().SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.DriveItemId == itemId && x.IsActive, ct);
    public Task<StorageFolder?> FindRootAsync(int schoolId, int? teacherId, CancellationToken ct) =>
        db.StorageFolders.AsTracking().SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.OwnerTeacherId == teacherId &&
            x.ParentFolderId == null && x.IsActive && x.Kind == (teacherId == null ? StorageFolderKind.SchoolLibrary : StorageFolderKind.Teacher), ct);
    public async Task<StorageFolder> AddFolderAsync(StorageFolder folder, CancellationToken ct)
    {
        db.StorageFolders.Add(folder);
        db.AuditLogs.Add(new() { SchoolId = folder.SchoolId, UserId = folder.CreatedByUserId,
            Action = "Storage.FolderCreated", EntityName = "StorageFolder", EntityId = folder.DriveItemId });
        await SaveAsync(ct);
        return folder;
    }
    public async Task SaveFolderAsync(StorageFolder folder, byte[] expectedVersion, CancellationToken ct)
    {
        db.Entry(folder).Property(x => x.RowVersion).OriginalValue = expectedVersion;
        db.AuditLogs.Add(new() { SchoolId = folder.SchoolId, UserId = folder.UpdatedByUserId,
            Action = "Storage.FolderMoved", EntityName = "StorageFolder", EntityId = folder.Id.ToString() });
        await SaveAsync(ct);
    }
    public async Task<StoragePage<StorageFolderDto>> FoldersAsync(int schoolId, int? owner, int? parent, int page, int pageSize, CancellationToken ct)
    {
        var q = db.StorageFolders.AsNoTracking().Where(x => x.SchoolId == schoolId && x.IsActive &&
            x.OwnerTeacherId == owner && x.ParentFolderId == parent && x.Kind != StorageFolderKind.VisitArchive);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.ParentFolderId, x.DisplayName, x.Kind, x.RowVersion }).ToListAsync(ct);
        return new(rows.Select(x => new StorageFolderDto(x.Id, x.ParentFolderId, x.DisplayName, x.Kind.ToString(), Convert.ToBase64String(x.RowVersion))).ToList(), total, page, pageSize);
    }

    public async Task<StoragePage<StorageReadRow>> FilesAsync(int schoolId, int? owner, StorageListRequest request, CancellationToken ct)
    {
        var q = Files(schoolId).Where(x => x.SourceKind != StoredFileSourceKind.VisitArchive &&
            (owner == null ? x.OwnerTeacherId == null : x.OwnerTeacherId == owner) &&
            (request.Global || x.FolderId == request.FolderId) &&
            (request.Search == null || x.DisplayName.Contains(request.Search)));
        var total = await q.CountAsync(ct);
        var protectedIds = Files(schoolId).Where(Protected).Select(x => x.Id);
        var joined = from f in q
                     join v in db.StoredFileVersions.AsNoTracking() on f.CurrentVersionId equals v.Id
                     select new { File = f, Version = v, Protected = protectedIds.Contains(f.Id) };
        var sorted = request.Sort switch
        {
            "size" => request.Descending ? joined.OrderByDescending(x => x.Version.SizeInBytes) : joined.OrderBy(x => x.Version.SizeInBytes),
            "date" => request.Descending ? joined.OrderByDescending(x => x.Version.UploadedAtUtc) : joined.OrderBy(x => x.Version.UploadedAtUtc),
            _ => request.Descending ? joined.OrderByDescending(x => x.File.DisplayName) : joined.OrderBy(x => x.File.DisplayName)
        };
        var rows = await sorted.ThenBy(x => x.File.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new { x.File.Id, x.File.FolderId, x.File.DisplayName, x.Version.SizeInBytes, x.Version.MimeType,
                x.Version.UploadedAtUtc, x.Version.Availability, x.File.RowVersion, x.Protected, x.Version.DriveItemId, x.File.SourceKind }).ToListAsync(ct);
        return new(rows.Select(x => new StorageReadRow(new(x.Id, x.FolderId, x.DisplayName, x.SizeInBytes, x.MimeType,
            x.UploadedAtUtc, x.Availability == StoredFileAvailability.Missing ? "MissingFromDrive" : "Managed", x.Protected,
            Convert.ToBase64String(x.RowVersion)), x.DriveItemId, x.SourceKind)).ToList(), total, request.Page, request.PageSize);
    }
    public async Task<StorageFileDetailsDto?> DetailsAsync(int schoolId, int id, CancellationToken ct)
    {
        var f = await Files(schoolId).Where(x => x.Id == id).Select(x => new { x.Id, x.FolderId, x.DisplayName, x.RowVersion, x.CurrentVersionId }).SingleOrDefaultAsync(ct);
        if (f is null) return null;
        var versions = await db.StoredFileVersions.AsNoTracking().Where(x => x.SchoolId == schoolId && x.StoredFileId == id)
            .OrderByDescending(x => x.VersionNumber).Select(x => new StorageVersionDto(x.Id, x.VersionNumber, x.SizeInBytes, x.MimeType, x.UploadedAtUtc, x.Availability.ToString())).ToListAsync(ct);
        var v = versions.Single(x => x.VersionId == f.CurrentVersionId);
        return new(new(f.Id, f.FolderId, f.DisplayName, v.Size, v.MimeType, v.UploadedAt,
            v.Availability == "Missing" ? "MissingFromDrive" : "Managed", await Files(schoolId).Where(x => x.Id == id).AnyAsync(Protected, ct), Convert.ToBase64String(f.RowVersion)), versions);
    }
    public async Task<StorageMutationTarget?> MutationTargetAsync(int schoolId, int id, CancellationToken ct)
    {
        var file = await db.StoredFiles.AsTracking().SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.Id == id, ct);
        if (file is null) return null;
        var version = await db.StoredFileVersions.AsTracking().SingleAsync(x => x.SchoolId == schoolId && x.Id == file.CurrentVersionId, ct);
        return new(file, version, await Files(schoolId).Where(x => x.Id == id).AnyAsync(Protected, ct));
    }
    public async Task<bool> HasProtectedDescendantsAsync(int schoolId, int folderId, CancellationToken ct)
    {
        var folders = await db.StorageFolders.AsNoTracking().Where(x => x.SchoolId == schoolId && x.IsActive)
            .Select(x => new { x.Id, x.ParentFolderId }).ToListAsync(ct);
        var ids = new HashSet<int> { folderId };
        while (true)
        {
            var next = folders.Where(x => x.ParentFolderId != null && ids.Contains(x.ParentFolderId.Value) && !ids.Contains(x.Id)).Select(x => x.Id).ToList();
            if (next.Count == 0) break;
            ids.UnionWith(next);
        }
        return await Files(schoolId).Where(x => ids.Contains(x.FolderId)).AnyAsync(Protected, ct);
    }
    public async Task SetAvailabilityAsync(int schoolId, int id, bool missing, CancellationToken ct)
    {
        var target = await MutationTargetAsync(schoolId, id, ct);
        if (target is null) return;
        target.Version.Availability = missing ? StoredFileAvailability.Missing : StoredFileAvailability.Available;
        target.Version.MissingFromDriveAtUtc = missing ? DateTimeOffset.UtcNow : null;
        if (target.File.LegacyProvenanceJson != null && target.File.SharedWriterFingerprint == null)
        {
            target.File.SharedWriterProvenanceJson = target.File.LegacyProvenanceJson;
            target.File.SharedWriterFingerprint = target.File.LegacyFingerprint;
        }
        await SaveAsync(ct);
    }
    public Task<StorageOperation?> FindOperationAsync(int schoolId, string actor, string key, CancellationToken ct) =>
        db.Set<StorageOperation>().AsTracking().SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.ActorUserId == actor && x.RequestKey == key, ct);
    public Task<StorageOperation?> GetOperationAsync(int schoolId, int id, CancellationToken ct) =>
        db.Set<StorageOperation>().AsTracking().SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.Id == id, ct);
    public async Task AddOperationAsync(StorageOperation operation, CancellationToken ct)
    {
        db.Set<StorageOperation>().Add(operation);
        await SaveAsync(ct);
    }
    public Task SaveOperationAsync(StorageOperation operation, CancellationToken ct) => SaveAsync(ct);
    public async Task CompleteUploadAsync(StorageOperation operation, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            if (operation.ChangeRequestId != null)
            {
                var change = await db.Set<FileChangeRequest>().AsTracking().SingleAsync(x => x.SchoolId == operation.SchoolId && x.Id == operation.ChangeRequestId, ct);
                var original = await db.StoredFiles.AsTracking().SingleAsync(x => x.SchoolId == operation.SchoolId && x.Id == change.StoredFileId, ct);
                if (change.Status != "Pending" || change.CandidateVersionId != null || original.CurrentVersionId != change.OriginalVersionId) throw new StorageConflictException();
                var number = await db.StoredFileVersions.Where(x => x.SchoolId == operation.SchoolId && x.StoredFileId == original.Id).MaxAsync(x => x.VersionNumber, ct) + 1;
                var candidate = new StoredFileVersion { SchoolId = operation.SchoolId, StoredFileId = original.Id, VersionNumber = number,
                    DriveId = operation.DriveId, DriveItemId = operation.ProviderItemId, DriveFileName = operation.DisplayName,
                    FileExtension = Path.GetExtension(operation.DisplayName), SizeInBytes = operation.Size, MimeType = operation.MimeType,
                    SHA256 = operation.SHA256, UploadedByUserId = operation.ActorUserId, UploadedAtUtc = operation.CreatedAtUtc, Availability = StoredFileAvailability.Available };
                db.StoredFileVersions.Add(candidate); await SaveAsync(ct);
                change.CandidateVersionId = candidate.Id;
                operation.StoredFileId = original.Id; operation.VersionId = candidate.Id; operation.Status = "Completed"; operation.ErrorCode = null;
                db.AuditLogs.Add(new() { SchoolId = original.SchoolId, UserId = operation.ActorUserId,
                    Action = "Storage.CandidateUploaded", EntityName = "FileChangeRequest", EntityId = change.Id.ToString() });
                await SaveAsync(ct);
                if (tx is not null) await tx.CommitAsync(ct);
                return;
            }
            // Unique provider/version indexes and the operation rowversion resolve concurrent reconciliation.
            var file = new StoredFile { SchoolId = operation.SchoolId, FolderId = operation.FolderId!.Value,
                OwnerTeacherId = operation.OwnerTeacherId, DisplayName = operation.DisplayName, NeedsLink = true,
                SourceKind = operation.OwnerTeacherId == null ? StoredFileSourceKind.SchoolUpload : StoredFileSourceKind.TeacherUpload,
                LegacySubmissionId = operation.LegacySubmissionId };
            if (operation.LegacySubmissionId != null)
            {
                var source = await StorageProvenance.ReadAsync(db, operation.LegacySubmissionId.Value, ct);
                file.LegacyProvenanceJson = file.SharedWriterProvenanceJson = StorageProvenance.Serialize(source);
                file.LegacyFingerprint = file.SharedWriterFingerprint = StorageProvenance.Hash(file.LegacyProvenanceJson);
            }
            db.StoredFiles.Add(file);
            await SaveAsync(ct);
            var version = new StoredFileVersion { SchoolId = file.SchoolId, StoredFileId = file.Id, VersionNumber = 1,
                DriveId = operation.DriveId, DriveItemId = operation.ProviderItemId, DriveFileName = operation.DisplayName,
                FileExtension = Path.GetExtension(operation.DisplayName), SizeInBytes = operation.Size, MimeType = operation.MimeType,
                SHA256 = operation.SHA256, UploadedByUserId = operation.ActorUserId, UploadedAtUtc = operation.CreatedAtUtc,
                Availability = StoredFileAvailability.Available };
            db.StoredFileVersions.Add(version);
            await SaveAsync(ct);
            file.CurrentVersionId = version.Id;
            if (operation.LegacyTaskId != null)
            {
                var source = await db.TeacherEvidenceSubmissions.SingleAsync(x => x.Id == operation.LegacySubmissionId, ct);
                var task = await db.EvidenceTasks.SingleAsync(x => x.Id == operation.LegacyTaskId, ct);
                var requirement = await db.EvidenceRequirements.SingleOrDefaultAsync(x => x.SchoolId == file.SchoolId && x.AcademicYearId == source.AcademicYearId && x.OriginalTaskId == task.Id, ct);
                if (requirement == null)
                {
                    requirement = new() { SchoolId = file.SchoolId, AcademicYearId = source.AcademicYearId, Code = task.Code, DisplayName = task.NameAr, OriginalTaskId = task.Id, SortOrder = task.SortOrder };
                    db.EvidenceRequirements.Add(requirement); await SaveAsync(ct);
                }
                db.EvidenceLinks.Add(new() { SchoolId = file.SchoolId, AcademicYearId = source.AcademicYearId!.Value,
                    StoredFileId = file.Id, VersionId = version.Id, RequirementId = requirement.Id, TeacherId = file.OwnerTeacherId,
                    Status = EvidenceLinkStatus.PendingReview, SubmittedAtUtc = operation.CreatedAtUtc });
                file.NeedsLink = false;
            }
            operation.StoredFileId = file.Id; operation.VersionId = version.Id; operation.Status = "Completed"; operation.ErrorCode = null;
            db.AuditLogs.Add(new() { SchoolId = file.SchoolId, UserId = operation.ActorUserId,
                Action = "Storage.UploadCompleted", EntityName = "StoredFile", EntityId = file.Id.ToString() });
            await SaveAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch { db.ChangeTracker.Clear(); throw; }
    }
    public async Task SaveMutationAsync(StorageMutationTarget target, byte[] expectedVersion, string actor, bool delete, CancellationToken ct)
    {
        if (target.File.LegacyProvenanceJson != null && target.File.SharedWriterFingerprint == null)
        {
            target.File.SharedWriterProvenanceJson = target.File.LegacyProvenanceJson;
            target.File.SharedWriterFingerprint = target.File.LegacyFingerprint;
        }
        db.Entry(target.File).Property(x => x.RowVersion).OriginalValue = expectedVersion;
        db.AuditLogs.Add(new() { SchoolId = target.File.SchoolId, UserId = actor, Action = delete ? "Storage.FileDeleted" : "Storage.FileRenamed",
            EntityName = "StoredFile", EntityId = target.File.Id.ToString() });
        await SaveAsync(ct);
    }
    public Task<int?> FindLegacyFileAsync(int schoolId, long submissionId, CancellationToken ct) =>
        db.StoredFiles.IgnoreQueryFilters().AsNoTracking().Where(x => x.SchoolId == schoolId && x.LegacySubmissionId == submissionId)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
    public async Task<StorageDiscoveryPageDto> IndexDiscoveredFoldersAsync(StorageFolder parent, AlFalah.Application.DTOs.TeacherDrive.GoogleDriveFileList page, string actor, CancellationToken ct)
    {
        var children = page.Files.Where(x => !x.Trashed && x.Parents.Contains(parent.DriveItemId)).ToList();
        var ids = children.Select(x => x.Id).ToList();
        var archiveIds = (await VisitArchiveRepository.ProtectedProviderIdsQuery(db, ids).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        children.RemoveAll(x => archiveIds.Contains(x.Id) ||
            x.AppProperties?.ContainsKey("visitId") == true && x.AppProperties.ContainsKey("approvalRevision"));
        ids = children.Select(x => x.Id).ToList();
        var folders = await db.StorageFolders.AsTracking().Where(x => x.SchoolId == parent.SchoolId && ids.Contains(x.DriveItemId)).ToListAsync(ct);
        foreach (var child in children.Where(x => x.IsFolder && folders.All(f => f.DriveItemId != x.Id)))
        {
            var folder = new StorageFolder { SchoolId = parent.SchoolId, ParentFolderId = parent.Id, OwnerTeacherId = parent.OwnerTeacherId,
                Kind = parent.Kind, DisplayName = child.Name, DriveId = parent.DriveId, DriveItemId = child.Id, CreatedByUserId = actor };
            db.StorageFolders.Add(folder); folders.Add(folder);
        }
        await SaveAsync(ct);
        var versions = await db.StoredFileVersions.AsNoTracking().Where(x => x.SchoolId == parent.SchoolId && ids.Contains(x.DriveItemId) &&
            x.StoredFile.OwnerTeacherId == parent.OwnerTeacherId && x.StoredFile.CurrentVersionId == x.Id)
            .Select(x => new { x.DriveItemId, x.StoredFileId }).ToListAsync(ct);
        var fileMap = versions.ToDictionary(x => x.DriveItemId, x => x.StoredFileId);
        var folderMap = folders.Where(x => x.IsActive && x.OwnerTeacherId == parent.OwnerTeacherId).ToDictionary(x => x.DriveItemId, x => x.Id);
        return new(children.Select(x => new StorageDiscoveryItemDto(
            folderMap.TryGetValue(x.Id, out var folder) ? folder : null,
            fileMap.TryGetValue(x.Id, out var file) ? file : null,
            x.Name, x.IsFolder, x.Size, x.MimeType, x.IsFolder || fileMap.ContainsKey(x.Id) ? "Managed" : "Unindexed")).ToList(), page.NextPageToken);
    }
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new StorageConflictException(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException s && s.Number is 2601 or 2627)
        { throw new StorageConflictException(); }
    }
}
