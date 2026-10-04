using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

public sealed record LegacyStorageSubmission(long Id, int SchoolId, int TeacherId, int? TaskId, int? AcademicYearId,
    string DriveId, string DriveItemId, string ParentItemId, string FileName, string? FileExtension, string? MimeType,
    long SizeInBytes, string? ETag, EvidenceUploadStatus UploadStatus, EvidenceReviewStatus ReviewStatus,
    DateTimeOffset? ReviewedAtUtc, string? ReviewedByUserId, string? ReviewNote,
    bool IsDeleted, DateTimeOffset? DeletedAtUtc, string? DeletedByUserId,
    bool IsMissingFromDrive, DateTimeOffset? MissingFromDriveAtUtc, DateTimeOffset UploadedAtUtc,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record LegacyStorageTask(int Id, string Code, string NameAr, int SortOrder);
public sealed record BackfillExistingFile(long LegacySubmissionId, string? Fingerprint, bool HasCurrentVersion, bool HasLink, bool HasDecision,
    string? SharedWriterProvenanceJson = null, string? SharedWriterFingerprint = null);
public sealed record SharedWriterRepairTarget(StoredFile File, StoredFileVersion Version, LegacyStorageSubmission Source, LegacyStorageTask? Task);
public sealed record StorageBackfillInput(IReadOnlyList<LegacyStorageSubmission> Submissions,
    IReadOnlyList<LegacyStorageTask> Tasks, IReadOnlySet<int> SchoolIds, IReadOnlySet<int> TeacherIds,
    IReadOnlySet<int> AcademicYearIds, IReadOnlySet<string> UserIds,
    IReadOnlyList<BackfillExistingFile> ExistingFiles, IReadOnlyList<StorageFolder> Folders,
    IReadOnlyList<EvidenceRequirement> Requirements);
public sealed record StorageBackfillIssue(long SubmissionId, string Code);
public sealed record StorageBackfillGroup(int SchoolId, int TeacherId, int? TaskId, int? AcademicYearId,
    EvidenceReviewStatus ReviewStatus, EvidenceUploadStatus UploadStatus, bool IsDeleted, bool IsMissingFromDrive,
    int Source, int Existing, int PlannedFiles, int PlannedLinks, int PlannedDecisions, int Exceptions);
public sealed record StorageBackfillReport(bool DryRun, int SourceCount, int ExistingCount, int CreatedFiles,
    int CreatedVersions, int CreatedLinks, int CreatedDecisions, int NeedsLinkCount,
    IReadOnlyList<StorageBackfillGroup> Groups, IReadOnlyList<StorageBackfillIssue> Issues);
public interface IStorageBackfillRepository
{
    Task<IReadOnlyList<SharedWriterRepairTarget>> LoadSharedWriterRepairsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SharedWriterRepairTarget>>([]);
    Task<StorageBackfillInput> LoadAsync(CancellationToken ct);
    Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct);
    Task SaveGraphAsync(IReadOnlyList<StorageFolder> folders, IReadOnlyList<EvidenceRequirement> requirements,
        IReadOnlyList<StoredFile> files, IReadOnlyList<StoredFileVersion> versions,
        IReadOnlyList<EvidenceLink> links, IReadOnlyList<EvidenceReviewDecision> decisions, CancellationToken ct);
}
