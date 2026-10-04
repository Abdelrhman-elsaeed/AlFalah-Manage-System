using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Application.DTOs.TeacherDrive;

namespace AlFalah.Application.Storage;

public sealed record RequirementDto(int Id, int AcademicYearId, string Code, string DisplayName, string? DomainCode,
    string? StandardCode, int? OriginalTaskId, string Importance, string? ResponsibleUserId, string? ResponsibleRole,
    string FulfillmentPolicy, int MinimumApprovedLinks, string RowVersion);
public sealed record ConfigureRequirementRequest(string? DomainCode, string? StandardCode, EvidenceImportance Importance,
    string? ResponsibleUserId, string? ResponsibleRole, EvidenceFulfillmentPolicy FulfillmentPolicy, int MinimumApprovedLinks, string RowVersion);
public sealed record CreateEvidenceLinkRequest(int RequirementId, int AcademicYearId, int? TeacherId = null);
public sealed record SubmitEvidenceLinkRequest(string RowVersion);
public sealed record ReviewEvidenceLinkRequest(EvidenceReviewStatus Decision, string? Note, string RowVersion);
public sealed record EvidenceQueueRequest(int AcademicYearId, int? RequirementId = null, int? TeacherId = null,
    string? StandardCode = null, EvidenceLinkStatus? Status = null, int Page = 1, int PageSize = 25);
public sealed record EvidenceDecisionDto(int Id, int VersionId, string Decision, string? ReviewedByUserId, string? Note, DateTimeOffset? ReviewedAtUtc, string ReviewerName);
public sealed record EvidenceLinkDto(int Id, int StoredFileId, int RequirementId, int AcademicYearId, int? TeacherId,
    int VersionId, string FileName, string RequirementName, string TeacherName, string Status, string Availability,
    string RowVersion, IReadOnlyList<EvidenceDecisionDto> Decisions);
public sealed record EvidenceCountsDto(int Files, int Links, int ApprovedLinks, int FulfilledRequirements, int Requirements);
public sealed record CreateFileChangeRequest(string Kind, string Reason, string RowVersion, bool ReplaceBeforeReview = false);
public sealed record ReviewFileChangeRequest(bool Approve, string? Note, string RowVersion);
public sealed record FileChangeDecisionDto(string Decision, string ReviewedByUserId, string? Note, DateTimeOffset CreatedAtUtc, string ReviewerName);
public sealed record FileChangeDto(int Id, int StoredFileId, int OriginalVersionId, int? CandidateVersionId, string Kind,
    string Status, string Reason, string RequestedByUserId, string RowVersion, IReadOnlyList<FileChangeDecisionDto> Decisions, bool ReplaceBeforeReview);
public sealed record ReplacementUploadTarget(int FileId, int FolderId, int? OwnerTeacherId);
public sealed record EvidenceTeacherDto(int Id, string DisplayName);
public sealed record ChangeQueueItemDto(int Id, int StoredFileId, string FileName, string Kind, string Status, string Reason);

public interface IEvidenceRepository
{
    Task<bool> YearExistsAsync(int year, CancellationToken ct);
    Task<IReadOnlyList<AcademicYearDto>> YearsAsync(CancellationToken ct);
    Task<IReadOnlyList<EvidenceTeacherDto>> TeachersAsync(int school, CancellationToken ct);
    Task<string> ActorNameAsync(string actor, CancellationToken ct);
    Task<IReadOnlyList<EvidenceTask>> TasksAsync(CancellationToken ct);
    Task<IReadOnlyList<EvidenceRequirement>> RequirementsAsync(int school, int year, CancellationToken ct);
    Task<EvidenceRequirement?> RequirementAsync(int school, int id, CancellationToken ct);
    Task<bool> ActiveTeacherAsync(int school, int id, CancellationToken ct);
    Task<int?> OwnTeacherIdAsync(int school, string user, CancellationToken ct);
    Task<IReadOnlyList<int>> ApprovedFileIdsAsync(int school, int year, int? owner, CancellationToken ct, bool ownOnly = false);
    Task AddRequirementsAsync(IEnumerable<EvidenceRequirement> rows, CancellationToken ct);
    Task<StoredFile?> FileAsync(int school, int id, CancellationToken ct);
    Task<bool> HasApprovalHistoryAsync(int school, int file, CancellationToken ct);
    Task<StoredFileVersion?> VersionAsync(int school, int file, int version, CancellationToken ct);
    Task<EvidenceLink?> LinkAsync(int school, int id, CancellationToken ct);
    Task<IReadOnlyList<EvidenceLink>> FileLinksAsync(int school, int file, CancellationToken ct);
    Task<StoragePage<int>> LinkIdsAsync(int school, EvidenceQueueRequest request, CancellationToken ct);
    Task<EvidenceLinkDto> LinkDtoAsync(int school, int id, CancellationToken ct);
    Task<IReadOnlyList<EvidenceLinkDto>> LinkDtosAsync(int school, IReadOnlyList<int> ids, CancellationToken ct);
    void AddLink(EvidenceLink link);
    void AddDecision(EvidenceReviewDecision decision);
    void AddChange(FileChangeRequest request);
    void AddChangeDecision(FileChangeDecision decision);
    Task<FileChangeRequest?> ChangeAsync(int school, int id, CancellationToken ct);
    Task<IReadOnlyList<FileChangeDto>> ChangesAsync(int school, int file, CancellationToken ct);
    Task<StoragePage<ChangeQueueItemDto>> ChangeQueueAsync(int school, int year, int page, string? status, CancellationToken ct);
    Task<StorageFileDetailsDto?> HistoryAsync(int school, int file, CancellationToken ct);
    Task SaveAsync(string actor, int school, string action, string entityId, object? expected, byte[]? version, CancellationToken ct);
    Task<EvidenceCountsDto> CountsAsync(int school, int year, int? owner, CancellationToken ct);
    Task<EvidenceMatrixDto> MatrixAsync(int school, EvidenceMatrixFilterDto filter, CancellationToken ct);
    Task<EvidenceCellFilesDto> CellAsync(int school, int teacher, int task, int year, CancellationToken ct);
    Task<int?> LegacyLinkAsync(int school, long submission, CancellationToken ct);
}
public interface IRequirementCatalogService
{
    Task<IReadOnlyList<AcademicYearDto>> YearsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EvidenceTeacherDto>> TeachersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RequirementDto>> ListAsync(int year, string? search = null, CancellationToken ct = default);
    Task<IReadOnlyList<RequirementDto>> InitializeAsync(int year, CancellationToken ct = default);
    Task<RequirementDto> ConfigureAsync(int id, ConfigureRequirementRequest request, CancellationToken ct = default);
}
public interface IEvidenceLinkService
{
    Task<EvidenceLinkDto> CreateAsync(int file, CreateEvidenceLinkRequest request, CancellationToken ct = default);
    Task<EvidenceLinkDto> SubmitAsync(int id, SubmitEvidenceLinkRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<EvidenceLinkDto>> FileLinksAsync(int file, bool own = false, CancellationToken ct = default);
    Task<StoragePage<EvidenceLinkDto>> QueueAsync(EvidenceQueueRequest request, CancellationToken ct = default);
    Task<EvidenceCountsDto> CountsAsync(int year, bool own, CancellationToken ct = default);
}
public interface IEvidenceReviewService
{
    Task<EvidenceLinkDto> ReviewAsync(int id, ReviewEvidenceLinkRequest request, CancellationToken ct = default);
}
public interface IFileChangeRequestService
{
    Task<FileChangeDto> CreateAsync(int file, CreateFileChangeRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<FileChangeDto>> ListAsync(int file, CancellationToken ct = default);
    Task<FileChangeDto> ReviewAsync(int id, ReviewFileChangeRequest request, CancellationToken ct = default);
    Task<ReplacementUploadTarget> RequireUploadAsync(int id, CancellationToken ct = default);
    Task CompleteCandidateAsync(int id, CancellationToken ct = default);
    Task<DriveFileContentDto> VersionContentAsync(int file, int version, CancellationToken ct = default);
    Task<StorageFileDetailsDto> HistoryAsync(int file, CancellationToken ct = default);
    Task<StoragePage<ChangeQueueItemDto>> QueueAsync(int year, int page = 1, string? status = "Pending", CancellationToken ct = default);
}
public interface IStorageEvidenceReadService
{
    Task<IReadOnlyList<AcademicYearDto>> YearsAsync(CancellationToken ct = default);
    Task<EvidenceMatrixDto> MatrixAsync(EvidenceMatrixFilterDto filter, CancellationToken ct = default);
    Task<EvidenceCellFilesDto> CellAsync(int teacher, int task, int year, CancellationToken ct = default);
}
