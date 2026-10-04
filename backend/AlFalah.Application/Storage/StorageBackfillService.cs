using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

/// <summary>Offline SQL metadata import. No provider client, token service, HTTP or file-byte dependency.</summary>
public sealed class StorageBackfillService(IStorageBackfillRepository repository)
{
    public Task<StorageBackfillReport> RunAsync(bool dryRun = true, CancellationToken ct = default) => dryRun
        ? BuildAsync(true, ct)
        : repository.InSerializableTransactionAsync(() => BuildAsync(false, ct), ct);

    private async Task<StorageBackfillReport> BuildAsync(bool dryRun, CancellationToken ct)
    {
        var input = await repository.LoadAsync(ct);
        var tasks = input.Tasks.ToDictionary(x => x.Id);
        var existing = input.ExistingFiles.ToDictionary(x => x.LegacySubmissionId);
        var folders = input.Folders.ToDictionary(x => (x.SchoolId, x.DriveId, x.DriveItemId));
        var requirements = input.Requirements.ToDictionary(x => (x.SchoolId, x.AcademicYearId, x.TemplateVersion, x.Code));
        var newFolders = new List<StorageFolder>();
        var newRequirements = new List<EvidenceRequirement>();
        var files = new List<StoredFile>();
        var versions = new List<StoredFileVersion>();
        var links = new List<EvidenceLink>();
        var decisions = new List<EvidenceReviewDecision>();
        var issues = new List<StorageBackfillIssue>();
        var results = new List<(LegacyStorageSubmission Row, bool Existing, bool File, bool Link, bool Decision, bool Exception)>();
        foreach (var row in input.Submissions)
        {
            ct.ThrowIfCancellationRequested();
            var snapshot = JsonSerializer.Serialize(row);
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
            var canLink = row.TaskId.HasValue && row.AcademicYearId.HasValue;
            var hasDecision = canLink && row.ReviewStatus is EvidenceReviewStatus.Approved or EvidenceReviewStatus.Rejected;
            if (existing.TryGetValue(row.Id, out var previous))
            {
                var drift = previous.SharedWriterFingerprint != null
                    ? ManagedDrift(previous, row, canLink)
                    : previous.Fingerprint != fingerprint || !previous.HasCurrentVersion || canLink != previous.HasLink || hasDecision != previous.HasDecision;
                if (drift) issues.Add(new(row.Id, "LegacyOrTargetDrift"));
                results.Add((row, true, false, false, false, drift));
                continue;
            }
            var error = Validate(row, input, tasks);
            if (error is not null)
            {
                issues.Add(new(row.Id, error));
                results.Add((row, false, false, false, false, true));
                continue;
            }
            if (canLink)
            {
                var sourceTask = tasks[row.TaskId!.Value];
                var requirementKey = ((int?)row.SchoolId, row.AcademicYearId, 1, sourceTask.Code);
                if (requirements.TryGetValue(requirementKey, out var targetRequirement) && targetRequirement.OriginalTaskId != sourceTask.Id)
                {
                    issues.Add(new(row.Id, "ConflictingRequirementIdentity"));
                    results.Add((row, false, false, false, false, true));
                    continue;
                }
            }
            var folderKey = (row.SchoolId, row.DriveId, row.ParentItemId);
            if (!folders.TryGetValue(folderKey, out var folder))
            {
                // The actual recorded parent only; never invent a Drive ancestor relationship.
                folder = new StorageFolder
                {
                    SchoolId = row.SchoolId, OwnerTeacherId = row.TeacherId, Kind = StorageFolderKind.Teacher,
                    DisplayName = "مجلد ملفات المعلم (مرجع موروث)", DriveId = row.DriveId, DriveItemId = row.ParentItemId,
                    CreatedAtUtc = row.CreatedAtUtc, UpdatedAtUtc = row.UpdatedAtUtc
                };
                folders.Add(folderKey, folder);
                newFolders.Add(folder);
            }
            else if (folder.Kind != StorageFolderKind.Teacher || folder.OwnerTeacherId != row.TeacherId)
            {
                issues.Add(new(row.Id, "ConflictingFolderOwnership"));
                results.Add((row, false, false, false, false, true));
                continue;
            }
            var file = new StoredFile
            {
                SchoolId = row.SchoolId, OwnerTeacherId = row.TeacherId, Folder = folder,
                DisplayName = row.FileName, SourceKind = StoredFileSourceKind.TeacherUpload, NeedsLink = !canLink,
                IsDeleted = row.IsDeleted, DeletedAtUtc = row.DeletedAtUtc, DeletedByUserId = row.DeletedByUserId,
                LegacySubmissionId = row.Id, LegacyProvenanceJson = snapshot, LegacyFingerprint = fingerprint,
                CreatedAtUtc = row.CreatedAtUtc, UpdatedAtUtc = row.UpdatedAtUtc
            };
            var version = new StoredFileVersion
            {
                SchoolId = row.SchoolId, StoredFile = file, VersionNumber = 1,
                DriveId = row.DriveId, DriveItemId = row.DriveItemId, DriveFileName = row.FileName,
                FileExtension = row.FileExtension, MimeType = row.MimeType, SizeInBytes = row.SizeInBytes,
                // Legacy ledger cannot prove bytes, hashes, or the actual uploading user.
                Availability = row.IsDeleted ? StoredFileAvailability.Deleted : row.IsMissingFromDrive ? StoredFileAvailability.Missing :
                    row.UploadStatus != EvidenceUploadStatus.Completed ? StoredFileAvailability.UploadIncomplete : StoredFileAvailability.Unverified,
                MissingFromDriveAtUtc = row.MissingFromDriveAtUtc, UploadedAtUtc = row.UploadedAtUtc,
                CreatedAtUtc = row.CreatedAtUtc, UpdatedAtUtc = row.UpdatedAtUtc
            };
            files.Add(file);
            versions.Add(version);
            if (canLink)
            {
                var task = tasks[row.TaskId!.Value];
                var key = ((int?)row.SchoolId, row.AcademicYearId, 1, task.Code);
                if (!requirements.TryGetValue(key, out var requirement))
                {
                    requirement = new EvidenceRequirement
                    {
                        SchoolId = row.SchoolId, AcademicYearId = row.AcademicYearId, TemplateVersion = 1,
                        Code = task.Code, DisplayName = task.NameAr, OriginalTaskId = task.Id, SortOrder = task.SortOrder
                    };
                    requirements.Add(key, requirement);
                    newRequirements.Add(requirement);
                }
                var link = new EvidenceLink
                {
                    SchoolId = row.SchoolId, AcademicYearId = row.AcademicYearId!.Value, StoredFile = file,
                    Version = version, Requirement = requirement, TeacherId = row.TeacherId,
                    IsActive = !row.IsDeleted, SubmittedAtUtc = row.UploadedAtUtc,
                    Status = row.ReviewStatus switch
                    {
                        EvidenceReviewStatus.Approved => EvidenceLinkStatus.Approved,
                        EvidenceReviewStatus.Rejected => EvidenceLinkStatus.Rejected,
                        EvidenceReviewStatus.PendingReview => EvidenceLinkStatus.PendingReview,
                        _ => EvidenceLinkStatus.Draft
                    },
                    CreatedAtUtc = row.CreatedAtUtc, UpdatedAtUtc = row.UpdatedAtUtc
                };
                links.Add(link);
                if (hasDecision)
                    decisions.Add(new EvidenceReviewDecision
                    {
                        SchoolId = row.SchoolId, EvidenceLink = link, Version = version, Decision = row.ReviewStatus,
                        ReviewedByUserId = row.ReviewedByUserId, Note = row.ReviewNote, ReviewedAtUtc = row.ReviewedAtUtc,
                        IsLegacyImported = true, CreatedAtUtc = row.ReviewedAtUtc ?? row.CreatedAtUtc,
                        UpdatedAtUtc = row.ReviewedAtUtc ?? row.UpdatedAtUtc
                    });
            }
            results.Add((row, false, true, canLink, hasDecision, false));
        }
        var groups = results.GroupBy(x => new { x.Row.SchoolId, x.Row.TeacherId, x.Row.TaskId, x.Row.AcademicYearId,
            x.Row.ReviewStatus, x.Row.UploadStatus, x.Row.IsDeleted, x.Row.IsMissingFromDrive })
            .Select(g => new StorageBackfillGroup(g.Key.SchoolId, g.Key.TeacherId, g.Key.TaskId, g.Key.AcademicYearId,
                g.Key.ReviewStatus, g.Key.UploadStatus, g.Key.IsDeleted, g.Key.IsMissingFromDrive, g.Count(),
                g.Count(x => x.Existing), g.Count(x => x.File), g.Count(x => x.Link), g.Count(x => x.Decision), g.Count(x => x.Exception))).ToArray();
        // Exceptions do not silently turn into success; safe rows are still importable and every skipped row is explained.
        if (!dryRun)
            await repository.SaveGraphAsync(newFolders, newRequirements, files, versions, links, decisions, ct);
        return new(dryRun, input.Submissions.Count, results.Count(x => x.Existing), files.Count, versions.Count,
            links.Count, decisions.Count, results.Count(x => x.File && !x.Link), groups, issues);
    }

    private static bool ManagedDrift(BackfillExistingFile previous, LegacyStorageSubmission source, bool canLink)
    {
        if (previous.SharedWriterProvenanceJson == null || !previous.HasCurrentVersion || canLink && !previous.HasLink) return true;
        if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previous.SharedWriterProvenanceJson))) != previous.SharedWriterFingerprint) return true;
        try
        {
            var baseline = JsonSerializer.Deserialize<LegacyStorageSubmission>(previous.SharedWriterProvenanceJson);
            // Review columns remain the immutable compatibility baseline. Runtime decisions belong to links.
            return baseline == null || baseline.Id != source.Id || baseline.SchoolId != source.SchoolId || baseline.TeacherId != source.TeacherId ||
                baseline.TaskId != source.TaskId || baseline.AcademicYearId != source.AcademicYearId || baseline.DriveId != source.DriveId || baseline.DriveItemId != source.DriveItemId ||
                baseline.SizeInBytes != source.SizeInBytes || baseline.MimeType != source.MimeType || baseline.FileExtension != source.FileExtension ||
                baseline.UploadedAtUtc != source.UploadedAtUtc || baseline.CreatedAtUtc != source.CreatedAtUtc || baseline.UploadStatus != source.UploadStatus ||
                canLink && source.ReviewStatus is (EvidenceReviewStatus.Approved or EvidenceReviewStatus.Rejected) && !previous.HasDecision ||
                baseline.ReviewStatus != source.ReviewStatus || baseline.ReviewNote != source.ReviewNote || baseline.ReviewedByUserId != source.ReviewedByUserId || baseline.ReviewedAtUtc != source.ReviewedAtUtc;
        }
        catch (JsonException) { return true; }
    }

    private static string? Validate(LegacyStorageSubmission r, StorageBackfillInput input, IReadOnlyDictionary<int, LegacyStorageTask> tasks)
    {
        if (!input.SchoolIds.Contains(r.SchoolId) || !input.TeacherIds.Contains(r.TeacherId)) return "MissingSchoolOrTeacher";
        if (string.IsNullOrWhiteSpace(r.DriveItemId) || string.IsNullOrWhiteSpace(r.ParentItemId) || r.SizeInBytes < 0) return "InvalidDriveMetadata";
        if (r.TaskId.HasValue && !tasks.ContainsKey(r.TaskId.Value)) return "MissingTask";
        if (r.AcademicYearId.HasValue && !input.AcademicYearIds.Contains(r.AcademicYearId.Value)) return "MissingAcademicYear";
        if (r.ReviewedByUserId is not null && !input.UserIds.Contains(r.ReviewedByUserId) ||
            r.DeletedByUserId is not null && !input.UserIds.Contains(r.DeletedByUserId)) return "MissingHistoricalActor";
        return null;
    }
}
