using System.Data;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class StorageBackfillRepository(AlFalahDbContext db) : IStorageBackfillRepository
{
    public async Task<StorageBackfillInput> LoadAsync(CancellationToken ct)
    {
        var submissions = await db.TeacherEvidenceSubmissions.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new LegacyStorageSubmission(x.Id, x.SchoolId, x.TeacherId, x.TaskId, x.AcademicYearId,
                x.DriveId, x.DriveItemId, x.ParentItemId, x.FileName, x.FileExtension, x.MimeType, x.SizeInBytes, x.ETag,
                x.UploadStatus, x.ReviewStatus, x.ReviewedAtUtc, x.ReviewedByUserId, x.ReviewNote,
                x.IsDeleted, x.DeletedAtUtc, x.DeletedByUserId, x.IsMissingFromDrive, x.MissingFromDriveAtUtc,
                x.UploadedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc)).ToListAsync(ct);
        var tasks = await db.EvidenceTasks.AsNoTracking().Select(x => new LegacyStorageTask(x.Id, x.Code, x.NameAr, x.SortOrder)).ToListAsync(ct);
        var schools = (await db.Schools.IgnoreQueryFilters().AsNoTracking().Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var teachers = (await db.InstructorProfiles.IgnoreQueryFilters().AsNoTracking().Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var years = (await db.AcademicYears.AsNoTracking().Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var users = (await db.Users.IgnoreQueryFilters().AsNoTracking().Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var existing = await db.StoredFiles.IgnoreQueryFilters().AsNoTracking().Where(x => x.LegacySubmissionId != null)
            .Select(x => new BackfillExistingFile(x.LegacySubmissionId!.Value, x.LegacyFingerprint,
                x.CurrentVersionId != null && db.StoredFileVersions.Any(v => v.Id == x.CurrentVersionId && v.StoredFileId == x.Id && v.SchoolId == x.SchoolId),
                db.EvidenceLinks.Any(l => l.StoredFileId == x.Id && l.SchoolId == x.SchoolId),
                db.EvidenceReviewDecisions.Any(d => d.StoredFileId == x.Id && d.SchoolId == x.SchoolId), x.SharedWriterProvenanceJson, x.SharedWriterFingerprint)).ToListAsync(ct);
        return new(submissions, tasks, schools, teachers, years, users, existing,
            await db.StorageFolders.AsNoTracking().ToListAsync(ct), await db.EvidenceRequirements.AsNoTracking().ToListAsync(ct));
    }
    public async Task<IReadOnlyList<SharedWriterRepairTarget>> LoadSharedWriterRepairsAsync(CancellationToken ct)
    {
        var files = await db.StoredFiles.IgnoreQueryFilters().AsTracking().Where(x => x.LegacySubmissionId != null && x.LegacyFingerprint == null && x.SharedWriterFingerprint == null &&
            db.Set<StorageOperation>().Any(o => o.StoredFileId == x.Id && o.Status == "Completed" && o.Action == "Upload" && o.LegacySubmissionId == x.LegacySubmissionId)).ToListAsync(ct);
        var result = new List<SharedWriterRepairTarget>();
        foreach (var file in files)
        {
            var source = await StorageProvenance.ReadAsync(db, file.LegacySubmissionId!.Value, ct);
            var task = await db.EvidenceTasks.Where(x => x.Id == source.TaskId).Select(x => new LegacyStorageTask(x.Id, x.Code, x.NameAr, x.SortOrder)).SingleOrDefaultAsync(ct);
            var version = await db.StoredFileVersions.AsTracking().SingleAsync(x => x.SchoolId == file.SchoolId && x.Id == file.CurrentVersionId, ct);
            result.Add(new(file, version, source, task));
        }
        return result;
    }

    public async Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var result = await operation();
        if (tx is not null) await tx.CommitAsync(ct);
        return result;
    }

    public async Task SaveGraphAsync(IReadOnlyList<StorageFolder> folders, IReadOnlyList<EvidenceRequirement> requirements,
        IReadOnlyList<StoredFile> files, IReadOnlyList<StoredFileVersion> versions, IReadOnlyList<EvidenceLink> links,
        IReadOnlyList<EvidenceReviewDecision> decisions, CancellationToken ct)
    {
        // Detached existing principals must not be inserted again.
        foreach (var file in files.Where(x => x.Folder.Id != 0))
        {
            var tracked = db.StorageFolders.Local.SingleOrDefault(x => x.Id == file.Folder.Id);
            if (tracked != null) file.Folder = tracked;
            else db.Attach(file.Folder);
        }
        foreach (var link in links.Where(x => x.Requirement.Id != 0))
        {
            var tracked = db.EvidenceRequirements.Local.SingleOrDefault(x => x.Id == link.Requirement.Id);
            if (tracked != null) link.Requirement = tracked;
            else db.Attach(link.Requirement);
        }
        db.StorageFolders.AddRange(folders);
        db.EvidenceRequirements.AddRange(requirements);
        db.StoredFiles.AddRange(files);
        await db.SaveChangesAsync(ct);
        db.StoredFileVersions.AddRange(versions);
        await db.SaveChangesAsync(ct);
        foreach (var file in files) file.CurrentVersionId = versions.Single(x => ReferenceEquals(x.StoredFile, file)).Id;
        foreach (var decision in decisions) decision.StoredFileId = decision.Version.StoredFileId;
        db.EvidenceLinks.AddRange(links);
        db.EvidenceReviewDecisions.AddRange(decisions);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }
}
