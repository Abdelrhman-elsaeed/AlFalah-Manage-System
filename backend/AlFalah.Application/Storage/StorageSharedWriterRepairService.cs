using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

// Explicit offline preparation step; no provider calls, migration, or cutover.
public sealed class StorageSharedWriterRepairService(IStorageBackfillRepository backfill, IEvidenceRepository repository)
{
    public Task<int> RunAsync(bool dryRun = true, CancellationToken ct = default) => backfill.InSerializableTransactionAsync(async () =>
    {
        var targets = await backfill.LoadSharedWriterRepairsAsync(ct);
        foreach (var target in targets)
        {
            var source = target.Source; var file = target.File; var version = target.Version;
            if (source.SchoolId != file.SchoolId || source.TeacherId != file.OwnerTeacherId || source.DriveId != version.DriveId || source.DriveItemId != version.DriveItemId ||
                source.TaskId == null || source.AcademicYearId == null || target.Task == null || version.VersionNumber != 1)
                throw new StorageConflictException();
            if (dryRun) continue;
            file.SharedWriterProvenanceJson = JsonSerializer.Serialize(source);
            file.SharedWriterFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(file.SharedWriterProvenanceJson)));
            var requirements = await repository.RequirementsAsync(file.SchoolId, source.AcademicYearId.Value, ct);
            var requirement = requirements.SingleOrDefault(x => x.OriginalTaskId == target.Task.Id);
            if (requirement == null)
            {
                requirement = new() { SchoolId = file.SchoolId, AcademicYearId = source.AcademicYearId, Code = target.Task.Code, DisplayName = target.Task.NameAr, OriginalTaskId = target.Task.Id, SortOrder = target.Task.SortOrder };
                await repository.AddRequirementsAsync([requirement], ct);
            }
            if (!(await repository.FileLinksAsync(file.SchoolId, file.Id, ct)).Any(x => x.RequirementId == requirement.Id && x.IsActive))
            {
                var link = new EvidenceLink { SchoolId = file.SchoolId, StoredFileId = file.Id, VersionId = version.Id,
                    AcademicYearId = source.AcademicYearId.Value, RequirementId = requirement.Id, TeacherId = source.TeacherId,
                    Status = source.ReviewStatus switch { EvidenceReviewStatus.Approved => EvidenceLinkStatus.Approved, EvidenceReviewStatus.Rejected => EvidenceLinkStatus.Rejected, _ => EvidenceLinkStatus.PendingReview } };
                repository.AddLink(link);
                await repository.SaveAsync(source.ReviewedByUserId ?? version.UploadedByUserId!, file.SchoolId, "Storage.SharedWriterRepaired", file.Id.ToString(), null, null, ct);
                if (source.ReviewStatus is EvidenceReviewStatus.Approved or EvidenceReviewStatus.Rejected)
                    repository.AddDecision(new() { SchoolId = file.SchoolId, StoredFileId = file.Id, EvidenceLinkId = link.Id, VersionId = version.Id,
                        Decision = source.ReviewStatus, Note = source.ReviewNote, ReviewedByUserId = source.ReviewedByUserId, ReviewedAtUtc = source.ReviewedAtUtc, IsLegacyImported = true });
            }
            file.NeedsLink = false;
            await repository.SaveAsync(source.ReviewedByUserId ?? version.UploadedByUserId!, file.SchoolId, "Storage.SharedWriterRepaired", file.Id.ToString(), null, null, ct);
        }
        return targets.Count;
    }, ct);
}
