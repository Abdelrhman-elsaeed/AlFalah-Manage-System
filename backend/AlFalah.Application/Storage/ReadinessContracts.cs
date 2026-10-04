using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

public sealed record ReadinessFilter(int AcademicYearId, int TemplateVersion = 1, string? DomainCode = null,
    string? StandardCode = null, string? ResponsibleUserId = null, EvidenceImportance? Importance = null,
    string? Status = null, string? Search = null, bool CriticalOnly = false, bool HideCompleted = false,
    bool TrackerOnly = false, int Page = 1, int PageSize = 25, bool GapsOnly = false);
public sealed record TemplateSection(string Code, string Name, int SortOrder);
public sealed record TemplateItem(string SourceKey, int SortOrder, string StandardCode, string DisplayName,
    string ResponsibleRole, string Importance, string ImportanceReason, string ReferencePath,
    string CompletionAction, string SourceSha256, string[] CandidateTaskCodes, string MappingStatus);
public sealed record MatrixReference(int Ordinal, string SourceSha256, string SourceResourceSha256, string State);
public sealed record EvaluationTemplateSnapshot(int Version, string Name, string Rounding, string SourceName,
    string SourceSha256, TemplateSection[] Domains, TemplateSection[] Standards, TemplateItem[] Items, MatrixReference[] MatrixReferences);
public sealed record InitializeEvaluationRequest(int AcademicYearId, int TemplateVersion = 1);
public sealed record RequirementFacts
{
    public int Id { get; init; }
    public int AcademicYearId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string? DomainCode { get; init; }
    public string? StandardCode { get; init; }
    public int? OriginalTaskId { get; init; }
    public string? ResponsibleUserId { get; init; }
    public string ResponsibleName { get; init; } = "";
    public string? ResponsibleRole { get; init; }
    public EvidenceImportance Importance { get; init; }
    public bool IsMandatory { get; init; }
    public EvidenceFulfillmentPolicy Policy { get; init; }
    public int MinimumApprovedLinks { get; init; }
    public string? SourceKey { get; init; }
    public string? SourceSHA256 { get; init; }
    public string? ReferencePath { get; init; }
    public string? CompletionAction { get; init; }
    public string? ImportanceReason { get; init; }
    public string? CandidateTaskCodesJson { get; init; }
    public DateOnly? DueDate { get; init; }
    public string FollowUpStatus { get; init; } = "";
    public string? FollowUpNote { get; init; }
    public byte[] RowVersion { get; init; } = [];
    public int Links { get; init; }
    public int AvailableLinks { get; init; }
    public int ApprovedLinks { get; init; }
    public int PendingLinks { get; init; }
    public int RejectedLinks { get; init; }
    public int? AvailableFileId { get; init; }
    public int SortOrder { get; init; }
}
public sealed record ReadinessRequirementDto(int Id, string Code, string Name, string? DomainCode, string? StandardCode,
    int? OriginalTaskId, string? ResponsibleUserId, string ResponsibleName, string? ResponsibleRole, string Importance,
    bool IsMandatory, string Policy, int RequiredLinks, int Links, int AvailableLinks, int ApprovedLinks,
    bool Fulfilled, string Status, IReadOnlyList<string> GapReasons, string CompletionAction, string ActionUrl,
    int? AvailableFileId, string? ReferencePath, string? SourceKey, string? SourceSHA256, IReadOnlyList<string> CandidateTaskCodes,
    string MappingStatus, DateOnly? DueDate, string FollowUpStatus, string? FollowUpNote, string RowVersion);
public sealed record ReadinessAggregate(string? DomainCode, string? StandardCode, int Requirements, int Denominator,
    int Numerator, int Links, int ApprovedLinks, int CriticalGaps);
public sealed record ReadinessMetric(string Code, string Name, int Requirements, int Numerator, int Denominator,
    decimal? Percentage, string CalculationState, int UniqueFiles, int Links, int ApprovedLinks, int Gaps, int CriticalGaps);
public sealed record ReadinessDto(int SchoolId, string SchoolName, int AcademicYearId, string AcademicYearName,
    int TemplateVersion, string TemplateName, string TemplateSHA256, string Rounding,
    DateTimeOffset CalculatedAtUtc, ReadinessFilter Filters, ReadinessMetric Overall,
    IReadOnlyList<ReadinessMetric> Domains, IReadOnlyList<ReadinessMetric> Standards);
public sealed record ReadinessFileCount(string? DomainCode, string? StandardCode, int Count);
public sealed record EvaluationMemberDto(string UserId, string Name);
public sealed record EvaluationVersionDto(int Version, string Name, string SHA256);
public sealed record DigitalIndexFileDto(int FileId, string Name, string? MimeType, long Size, int Links, int ApprovedLinks);
public sealed record EvaluationIdentity(string SchoolName, string YearName, string? HeaderText);
public sealed record ReadinessLiveFile(int FileId, int VersionId, string DriveItemId, string DriveId,
    string SchoolRoot, string AuthorizedRoot, string AuthorizedDriveId);
public sealed record AvailabilityObservation(int VersionId, bool Missing);
public sealed record ConfigureFollowUpRequest(string? ResponsibleUserId, string? ResponsibleRole,
    EvidenceImportance Importance, bool IsMandatory, EvidenceFulfillmentPolicy Policy, int MinimumApprovedLinks,
    DateOnly? DueDate, string FollowUpStatus, string? Note, string Reason, string RowVersion);
public sealed record SaveManualEvaluationRequest(int AcademicYearId, int TemplateVersion, string ScopeCode,
    string Judgment, decimal? Value, string Reason, string? RowVersion);
public sealed record ManualEvaluationDto(int Id, string ScopeCode, string Judgment, decimal? Value, string Reason,
    string EvaluatorName, DateTimeOffset EvaluatedAtUtc, int Revision, string RowVersion);
public sealed record ManualEvaluationHistoryDto(int Revision, string SnapshotJson, DateTimeOffset CreatedAtUtc);
public sealed record FollowUpHistoryDto(string ActorName, string Reason, string OldValuesJson, string NewValuesJson, DateTimeOffset CreatedAtUtc);
public sealed record ReadinessExportData(ReadinessDto Summary, IReadOnlyList<ReadinessRequirementDto> Rows,
    IReadOnlyList<ManualEvaluationDto> ManualEvaluations, string? HeaderText, IReadOnlyList<DigitalIndexFileDto>? Files = null);
public sealed record ReadinessExportResult(byte[] Content, string ContentType, string FileName);
public interface IReadinessRepository
{
    Task<SelfEvaluationTemplate?> TemplateAsync(int version, CancellationToken ct);
    Task<IReadOnlyList<EvaluationMemberDto>> MembersAsync(int school, CancellationToken ct);
    Task<IReadOnlyList<EvaluationVersionDto>> VersionsAsync(CancellationToken ct);
    Task<StoragePage<DigitalIndexFileDto>> IndexAsync(int school, ReadinessFilter filter, IReadOnlyList<int> denied, CancellationToken ct);
    Task AddTemplateAsync(SelfEvaluationTemplate template, CancellationToken ct);
    Task<bool> ScopeExistsAsync(int school, int year, int version, CancellationToken ct);
    Task InitializeAsync(SchoolEvaluationScope scope, IReadOnlyList<EvidenceRequirement> requirements, string actor, CancellationToken ct);
    Task<EvaluationIdentity> IdentityAsync(int school, int year, CancellationToken ct);
    Task<StoragePage<RequirementFacts>> PageAsync(int school, ReadinessFilter filter, IReadOnlyList<int> deniedVersions, bool gaps, CancellationToken ct);
    Task<IReadOnlyList<ReadinessAggregate>> AggregatesAsync(int school, ReadinessFilter filter, IReadOnlyList<int> deniedVersions, CancellationToken ct);
    Task<IReadOnlyList<ReadinessFileCount>> FileCountsAsync(int school, ReadinessFilter filter, IReadOnlyList<int> deniedVersions, CancellationToken ct);
    Task<IReadOnlyList<ReadinessLiveFile>> LiveFilesAsync(int school, ReadinessFilter filter, int afterFileId, int limit, CancellationToken ct);
    Task ObserveAsync(int school, IReadOnlyList<AvailabilityObservation> observations, string actor, CancellationToken ct);
    Task<EvidenceRequirement?> RequirementAsync(int school, int id, CancellationToken ct);
    Task SaveFollowUpAsync(EvidenceRequirement requirement, byte[] expected, RequirementFollowUpRevision revision, CancellationToken ct);
    Task<IReadOnlyList<FollowUpHistoryDto>> FollowUpHistoryAsync(int school, int id, CancellationToken ct);
    Task<ManualEvaluation?> ManualAsync(int school, int year, int version, string scopeCode, CancellationToken ct);
    Task SaveManualAsync(ManualEvaluation evaluation, byte[]? expected, string actor, CancellationToken ct);
    Task<IReadOnlyList<ManualEvaluationDto>> ManualsAsync(int school, int year, int version, CancellationToken ct);
    Task<IReadOnlyList<ManualEvaluationHistoryDto>> ManualHistoryAsync(int school, int id, CancellationToken ct);
}
public interface IReadinessService
{
    Task<EvaluationTemplateSnapshot> TemplateAsync(int version, CancellationToken ct = default);
    Task<IReadOnlyList<EvaluationMemberDto>> MembersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EvaluationVersionDto>> VersionsAsync(CancellationToken ct = default);
    Task<StoragePage<DigitalIndexFileDto>> IndexAsync(ReadinessFilter filter, CancellationToken ct = default);
    Task InitializeAsync(InitializeEvaluationRequest request, CancellationToken ct = default);
    Task<StoragePage<ReadinessRequirementDto>> RequirementsAsync(ReadinessFilter filter, bool gaps = false, CancellationToken ct = default);
    Task<ReadinessDto> ReadinessAsync(ReadinessFilter filter, CancellationToken ct = default);
    Task<ReadinessRequirementDto> ConfigureAsync(int id, ConfigureFollowUpRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<FollowUpHistoryDto>> FollowUpHistoryAsync(int id, CancellationToken ct = default);
    Task<ManualEvaluationDto> SaveManualAsync(SaveManualEvaluationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ManualEvaluationDto>> ManualsAsync(int year, int version, CancellationToken ct = default);
    Task<IReadOnlyList<ManualEvaluationHistoryDto>> ManualHistoryAsync(int id, CancellationToken ct = default);
    Task<ReadinessExportData> ExportDataAsync(ReadinessFilter filter, CancellationToken ct = default);
}
public interface IReadinessExportRenderer { ReadinessExportResult Render(string format, ReadinessExportData data); }
public interface IReadinessExportService { Task<ReadinessExportResult> ExportAsync(string format, ReadinessFilter filter, CancellationToken ct = default); }
