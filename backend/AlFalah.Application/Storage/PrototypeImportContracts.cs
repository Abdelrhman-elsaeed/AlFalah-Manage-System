using AlFalah.Domain.Entities.Storage;

namespace AlFalah.Application.Storage;

public sealed record ImportSourceRow(string Key, string Name, string? DomainCode = null, string? StandardCode = null,
    string? RequirementCode = null, string? ResponsibleName = null, string? ReferencePath = null,
    long? Size = null, string? Extension = null, string? SourceStatus = null);
public sealed record ImportPreviewRequest(int AcademicYearId, int TemplateVersion, string SourceVersion,
    string FileName, Stream Content);
public sealed record ImportReviewRequest(string RowVersion, string Digest, string Reason);
public sealed record ImportResolveRequest(string RowVersion, int? RequirementId, string? ResponsibleUserId, string Reason);
public sealed record ImportBatchDto(int Id, int SchoolId, int AcademicYearId, int TemplateVersion, string SourceName,
    string SourceVersion, string SourceSHA256, string Status, DateTimeOffset CreatedAtUtc, string? ReviewedByUserId,
    DateTimeOffset? ReviewedAtUtc, DateTimeOffset? CommittedAtUtc, int Rows, int Matched, int Missing, int Conflicts,
    int ReferenceOnly, int ImportedFiles, string Digest, string? ReviewedDigest, string RowVersion);
public sealed record ImportRowDto(int Id, ImportSourceRow Source, string Classification, string Status,
    string? Reason, int? RequirementId, string? ResponsibleUserId, IReadOnlyList<EvaluationMemberDto> Suggestions,
    int? StoredFileId, int? UploadOperationId, string RowVersion);
public sealed record ImportContext(SelfEvaluationTemplate Template, IReadOnlyList<EvidenceRequirement> Requirements,
    IReadOnlyList<EvaluationMemberDto> Members);
public sealed record ImportBytesRequest(Stream Content, string FileName, long Length, string RowVersion, string Reason);
public interface IPrototypeImportRepository
{
    Task<T> ExclusiveAsync<T>(int school, string key, Func<Task<T>> action, CancellationToken ct);
    Task<T> BytesExclusiveAsync<T>(int school, string hash, Func<Task<T>> action, CancellationToken ct);
    Task<StorageOperation?> HashOperationAsync(int school, string hash, CancellationToken ct);
    Task<ImportContext> ContextAsync(int school, int year, int template, CancellationToken ct);
    Task<PrototypeImportBatch?> FindAsync(int school, int year, int template, string hash, CancellationToken ct);
    Task<PrototypeImportBatch?> BatchAsync(int school, int id, CancellationToken ct);
    Task<IReadOnlyList<PrototypeImportBatch>> BatchesAsync(int school, CancellationToken ct);
    Task<IReadOnlyList<PrototypeImportRow>> RowsAsync(int school, int batch, CancellationToken ct);
    Task<PrototypeImportRow?> RowAsync(int school, int batch, int id, CancellationToken ct);
    Task AddAsync(PrototypeImportBatch batch, IReadOnlyList<PrototypeImportRow> rows, string actor, CancellationToken ct);
    Task SaveAsync(int school, string actor, string action, int id, CancellationToken ct);
    Task<int?> DuplicateFileAsync(int school, string hash, CancellationToken ct);
    Task<int?> LibraryRootAsync(int school, CancellationToken ct);
}
public interface IPrototypeImportService
{
    Task<ImportBatchDto> PreviewAsync(ImportPreviewRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ImportBatchDto>> ListAsync(CancellationToken ct = default);
    Task<ImportBatchDto> GetAsync(int id, CancellationToken ct = default);
    Task<StoragePage<ImportRowDto>> RowsAsync(int id, int page, string? classification, CancellationToken ct = default);
    Task<ImportBatchDto> ResolveAsync(int id, int row, ImportResolveRequest request, CancellationToken ct = default);
    Task<ImportBatchDto> ReviewAsync(int id, ImportReviewRequest request, CancellationToken ct = default);
    Task<ImportBatchDto> CommitAsync(int id, ImportReviewRequest request, CancellationToken ct = default);
    Task<byte[]> ExportAsync(int id, CancellationToken ct = default);
    Task<ImportRowDto> BytesAsync(int id, int row, ImportBytesRequest request, CancellationToken ct = default);
    Task<ImportRowDto> ReconcileAsync(int id, int row, CancellationToken ct = default);
}
