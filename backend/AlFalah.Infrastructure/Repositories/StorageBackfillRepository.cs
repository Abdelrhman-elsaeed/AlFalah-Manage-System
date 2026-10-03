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
                db.EvidenceReviewDecisions.Any(d => d.StoredFileId == x.Id && d.SchoolId == x.SchoolId))).ToListAsync(ct);
        return new(submissions, tasks, schools, teachers, years, users, existing,
            await db.StorageFolders.AsNoTracking().ToListAsync(ct), await db.EvidenceRequirements.AsNoTracking().ToListAsync(ct));
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
        foreach (var folder in files.Select(x => x.Folder).Where(x => x.Id != 0).Distinct()) db.Attach(folder);
        foreach (var requirement in links.Select(x => x.Requirement).Where(x => x.Id != 0).Distinct()) db.Attach(requirement);
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
