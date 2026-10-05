using System.Text;
using System.Text.Json;
using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Storage;

public sealed class PrototypeImportService(IPrototypeImportRepository repository, IStorageAuthorizationService authorization,
    ICurrentUserService user, IOptions<StorageOptions> options, IStorageLibraryService library, IEvidenceLinkService links,
    IStorageLibraryRepository uploads) : IPrototypeImportService
{
    private int School => user.ActiveSchoolId ?? throw new UnauthorizedSchoolAccessException("اختر المدرسة أولًا.");
    private string Actor => user.UserId ?? throw new UnauthorizedAccessException();
    private async Task Authorize(CancellationToken ct)
    {
        if (!options.Value.AdministrationEnabled || !options.Value.ReadModelEnabled) throw new KeyNotFoundException("الاستيراد غير مفعّل.");
        await authorization.RequireSchoolPermissionAsync(School, PermissionNames.StorageManageSchool, ct);
    }
    private async Task<PrototypeImportBatch> Batch(int id, CancellationToken ct) => await repository.BatchAsync(School, id, ct) ?? throw new KeyNotFoundException("دفعة الاستيراد غير موجودة.");
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static ImportSourceRow Source(PrototypeImportRow row) => string.IsNullOrWhiteSpace(row.SourceJson) || row.SourceJson == "{}"
        ? new("legacy-row-" + row.SourceOrdinal, "مرجع تاريخي — يلزم مصدر كامل للمطابقة", ReferencePath: row.ReferencePath)
        : JsonSerializer.Deserialize<ImportSourceRow>(row.SourceJson, Json)!;
    private readonly Dictionary<string, EvaluationTemplateSnapshot> templates = new();
    private void Classify(PrototypeImportRow row, ImportContext context)
    {
        var source = Source(row);
        if (!templates.TryGetValue(context.Template.SHA256, out var template))
            templates[context.Template.SHA256] = template = JsonSerializer.Deserialize<EvaluationTemplateSnapshot>(context.Template.SnapshotJson, Json)!;
        var requirement = row.RequirementId == null ? null : context.Requirements.SingleOrDefault(x => x.Id == row.RequirementId);
        if (row.RequirementId != null && requirement == null) { row.Classification = "Conflict"; row.ExceptionNote = "المتطلب لم يعد نشطًا في المدرسة والسنة والقالب."; return; }
        if (source.StandardCode != null && !template.Standards.Any(x => x.Code == source.StandardCode) ||
            source.DomainCode != null && !template.Domains.Any(x => x.Code == source.DomainCode) ||
            source.StandardCode != null && source.DomainCode != null && !source.StandardCode.StartsWith(source.DomainCode + ".", StringComparison.Ordinal))
        { row.Classification = "Conflict"; row.ExceptionNote = "كود المجال أو المعيار يتعارض مع قالب S4؛ صحح المصدر أو استخدم نسخة قالب معتمدة."; return; }
        if (requirement != null && (source.StandardCode != null && source.StandardCode != requirement.StandardCode || source.DomainCode != null && source.DomainCode != requirement.DomainCode))
        { row.Classification = "Conflict"; row.ExceptionNote = "المتطلب المختار لا يطابق مجال/معيار المصدر."; return; }
        if (row.ResponsibleUserId != null && !context.Members.Any(x => x.UserId == row.ResponsibleUserId))
        { row.Classification = "Conflict"; row.ExceptionNote = "المسؤول لم يعد عضوًا نشطًا في المدرسة."; return; }
        row.Classification = requirement == null ? source.StandardCode != null ? "New" : "Missing" : "Matched";
        row.ExceptionNote = requirement == null ? "مرجع بلا متطلب مطابق؛ اختر متطلبًا من S4 أو احتفظ به ضمن الاستثناءات." :
            row.ResponsibleUserId == null && source.ResponsibleName != null ? "اسم المصدر ليس هوية؛ لم يُعتمد مسؤول. المرجع بلا أصل حتى رفع البايتات." : "مرجع بلا أصل؛ لا يرفع الجاهزية ولا ينقل حالة المصدر إلى اعتماد.";
    }
    private async Task<(IReadOnlyList<PrototypeImportRow> Rows, ImportContext Context, string Digest)> Inspect(PrototypeImportBatch batch, CancellationToken ct)
    {
        var rows = await repository.RowsAsync(School, batch.Id, ct);
        var context = await repository.ContextAsync(School, batch.AcademicYearId, batch.TemplateVersion, ct);
        var digest = PrototypeImportParser.Hash(JsonSerializer.Serialize(new { batch.SourceSHA256, batch.SourceVersion, batch.TemplateVersion,
            context.Template.SHA256, Requirements = context.Requirements.Select(x => new { x.Id, x.Code, x.StandardCode, x.DomainCode, x.ResponsibleUserId, Version = Convert.ToBase64String(x.RowVersion) }),
            Members = context.Members.OrderBy(x => x.UserId), Rows = rows.Select(x => new { x.SourceRowKey, x.SourceRowSHA256, x.RequirementId, x.ResponsibleUserId, x.ResolutionReason }) }));
        return (rows, context, digest);
    }
    private static ImportBatchDto Dto(PrototypeImportBatch batch, IReadOnlyList<PrototypeImportRow> rows, string digest) => new(batch.Id, batch.SchoolId,
        batch.AcademicYearId, batch.TemplateVersion, batch.SourceName, batch.SourceVersion, batch.SourceSHA256, batch.Status.ToString(), batch.CreatedAtUtc,
        batch.ReviewedByUserId, batch.ReviewedAtUtc, batch.CommittedAtUtc, rows.Count, rows.Count(x => x.Classification == "Matched"),
        rows.Count(x => x.Classification is "Missing" or "New"), rows.Count(x => x.Classification == "Conflict"), rows.Count(x => x.StoredFileId == null),
        rows.Count(x => x.StoredFileId != null), digest, batch.ReviewedDigest, Convert.ToBase64String(batch.RowVersion));
    private static ImportRowDto RowDto(PrototypeImportRow row, ImportContext context) => new(row.Id, Source(row), row.Classification,
        row.Status == PrototypeImportStatus.Exception ? "NeedsReview" : row.StoredFileId != null ? "Imported" : "ReferenceOnly", row.ExceptionNote,
        row.RequirementId, row.ResponsibleUserId, context.Members.Where(x => Source(row).ResponsibleName != null && x.Name.Contains(Source(row).ResponsibleName!, StringComparison.OrdinalIgnoreCase)).Take(10).ToArray(),
        row.StoredFileId, row.UploadOperationId, Convert.ToBase64String(row.RowVersion));
    public async Task<ImportBatchDto> PreviewAsync(ImportPreviewRequest request, CancellationToken ct = default)
    {
        await Authorize(ct);
        if (string.IsNullOrWhiteSpace(request.SourceVersion) || request.SourceVersion.Length > 64) throw new ArgumentException("نسخة المصدر مطلوبة وحدها 64 حرفًا.");
        var name = ValidatedStorageUpload.SafeName(request.FileName);
        var source = await PrototypeImportParser.ReadAsync(request.Content, name, ct);
        return await repository.ExclusiveAsync(School, source.Hash, async () =>
        {
            await Authorize(ct);
            var existing = await repository.FindAsync(School, request.AcademicYearId, request.TemplateVersion, source.Hash, ct);
            if (existing != null) { if (existing.SourceVersion != request.SourceVersion) throw new StorageConflictException(); return await GetAsync(existing.Id, ct); }
            var context = await repository.ContextAsync(School, request.AcademicYearId, request.TemplateVersion, ct);
            var batch = new PrototypeImportBatch { SchoolId = School, AcademicYearId = request.AcademicYearId, TemplateVersion = request.TemplateVersion,
                SourceVersion = request.SourceVersion, SourceName = name, SourceSHA256 = source.Hash, Status = PrototypeImportStatus.Preview, CreatedByUserId = Actor };
            var rows = source.Rows.Select((s, i) =>
            {
                var row = new PrototypeImportRow { SchoolId = School, SourceOrdinal = i + 1, SourceRowKey = s.Key, SourceJson = JsonSerializer.Serialize(s),
                    SourceRowSHA256 = PrototypeImportParser.Hash(JsonSerializer.Serialize(s)), ReferencePath = s.ReferencePath, Status = PrototypeImportStatus.Preview };
                var candidates = context.Requirements.Where(x => x.Code == s.RequirementCode || x.SourceKey == s.Key).ToArray();
                if (candidates.Length == 1) row.RequirementId = candidates[0].Id;
                Classify(row, context); return row;
            }).ToArray();
            await repository.AddAsync(batch, rows, Actor, ct);
            return await GetAsync(batch.Id, ct);
        }, ct);
    }
    public async Task<IReadOnlyList<ImportBatchDto>> ListAsync(CancellationToken ct = default)
    {
        await Authorize(ct); var result = new List<ImportBatchDto>();
        foreach (var batch in await repository.BatchesAsync(School, ct)) result.Add(await GetAsync(batch.Id, ct));
        return result;
    }
    public async Task<ImportBatchDto> GetAsync(int id, CancellationToken ct = default)
    {
        await Authorize(ct); var batch = await Batch(id, ct); var inspection = await Inspect(batch, ct);
        return Dto(batch, inspection.Rows, inspection.Digest);
    }
    public async Task<StoragePage<ImportRowDto>> RowsAsync(int id, int page, string? classification, CancellationToken ct = default)
    {
        await Authorize(ct); if (page is < 1 or > 100000 || classification is not (null or "" or "Matched" or "New" or "Missing" or "Conflict")) throw new ArgumentException("مرشح الصفوف غير صالح.");
        var data = await Inspect(await Batch(id, ct), ct);
        var rows = data.Rows.Where(x => string.IsNullOrEmpty(classification) || x.Classification == classification).ToArray();
        return new(rows.Skip((page - 1) * 50).Take(50).Select(x => RowDto(x, data.Context)).ToArray(), rows.Length, page, 50);
    }
    public async Task<ImportBatchDto> ResolveAsync(int id, int rowId, ImportResolveRequest request, CancellationToken ct = default)
    {
        await Authorize(ct);
        return await repository.ExclusiveAsync(School, id.ToString(), async () =>
        {
            await Authorize(ct); var batch = await Batch(id, ct); if (batch.CommittedAtUtc != null) throw new StorageConflictException();
            var row = await repository.RowAsync(School, id, rowId, ct) ?? throw new KeyNotFoundException();
            EvidenceWorkflowContext.Expected(request.RowVersion, row.RowVersion);
            var context = await repository.ContextAsync(School, batch.AcademicYearId, batch.TemplateVersion, ct);
            if (request.RequirementId != null && !context.Requirements.Any(x => x.Id == request.RequirementId) || request.ResponsibleUserId != null && !context.Members.Any(x => x.UserId == request.ResponsibleUserId)) throw new ArgumentException("المتطلب أو المسؤول خارج نطاق المدرسة والسنة والقالب.");
            row.RequirementId = request.RequirementId; row.ResponsibleUserId = request.ResponsibleUserId; row.ResolutionReason = EvidenceWorkflowContext.Reason(request.Reason); Classify(row, context);
            batch.ReviewedDigest = null; batch.ReviewedAtUtc = null; batch.ReviewedByUserId = null; batch.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await repository.SaveAsync(School, Actor, "Storage.ImportResolved", id, ct); return await GetAsync(id, ct);
        }, ct);
    }
    public Task<ImportBatchDto> ReviewAsync(int id, ImportReviewRequest request, CancellationToken ct = default) => Decision(id, request, false, ct);
    public Task<ImportBatchDto> CommitAsync(int id, ImportReviewRequest request, CancellationToken ct = default) => Decision(id, request, true, ct);
    private async Task<ImportBatchDto> Decision(int id, ImportReviewRequest request, bool commit, CancellationToken ct)
    {
        await Authorize(ct);
        return await repository.ExclusiveAsync(School, id.ToString(), async () =>
        {
            await Authorize(ct); var batch = await Batch(id, ct); var data = await Inspect(batch, ct);
            if (commit && batch.CommittedAtUtc != null)
            {
                if (request.Digest != batch.ReviewedDigest) throw new StorageConflictException();
                return Dto(batch, data.Rows, data.Digest);
            }
            EvidenceWorkflowContext.Expected(request.RowVersion, batch.RowVersion);
            if (request.Digest != data.Digest) throw new StorageConflictException();
            var reason = EvidenceWorkflowContext.Reason(request.Reason);
            if (batch.CommittedAtUtc != null) throw new StorageConflictException();
            foreach (var row in data.Rows) Classify(row, data.Context);
            if (commit)
            {
                if (batch.ReviewedDigest != data.Digest || batch.ReviewedAtUtc == null) throw new StorageConflictException();
                foreach (var row in data.Rows) row.Status = row.Classification == "Conflict" ? PrototypeImportStatus.Exception : PrototypeImportStatus.ReferenceOnly;
                batch.Status = PrototypeImportStatus.Committed; batch.CommittedAtUtc = DateTimeOffset.UtcNow; batch.CommittedByUserId = Actor;
            }
            else { batch.ReviewedDigest = data.Digest; batch.ReviewedAtUtc = DateTimeOffset.UtcNow; batch.ReviewedByUserId = Actor; batch.ReviewReason = reason; }
            batch.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await repository.SaveAsync(School, Actor, commit ? "Storage.ImportCommitted" : "Storage.ImportReviewed", id, ct);
            return await GetAsync(id, ct);
        }, ct);
    }
    public async Task<byte[]> ExportAsync(int id, CancellationToken ct = default)
    {
        await Authorize(ct); var data = await Inspect(await Batch(id, ct), ct);
        static string Cell(string? text) { text ??= ""; if (text.TrimStart().StartsWith('=') || text.TrimStart().StartsWith('+') || text.TrimStart().StartsWith('-') || text.TrimStart().StartsWith('@')) text = "'" + text; return "\"" + text.Replace("\"", "\"\"") + "\""; }
        var csv = new StringBuilder("key,name,referencePath,classification,state,reason,requirementId,responsibleUserId,fileId\r\n");
        foreach (var row in data.Rows)
        {
            var dto = RowDto(row, data.Context); csv.AppendLine(string.Join(",", new[] { dto.Source.Key, dto.Source.Name, dto.Source.ReferencePath, dto.Classification, dto.Status, dto.Reason, dto.RequirementId?.ToString(), dto.ResponsibleUserId, dto.StoredFileId?.ToString() }.Select(Cell)));
        }
        await Authorize(ct); return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }
    public async Task<ImportRowDto> BytesAsync(int id, int rowId, ImportBytesRequest request, CancellationToken ct = default)
    {
        await Authorize(ct); var batch = await Batch(id, ct);
        if (batch.CommittedAtUtc == null) throw new StorageConflictException();
        var row = await repository.RowAsync(School, id, rowId, ct) ?? throw new KeyNotFoundException();
        EvidenceWorkflowContext.Expected(request.RowVersion, row.RowVersion); EvidenceWorkflowContext.Reason(request.Reason);
        var context = await repository.ContextAsync(School, batch.AcademicYearId, batch.TemplateVersion, ct);
        Classify(row, context); if (row.Classification != "Matched" || row.RequirementId == null) throw new ArgumentException("راجع مطابقة المتطلب قبل استيراد الأصل.");
        await using var file = await ValidatedStorageUpload.ReadAsync(request.Content, request.FileName, request.Length, ct);
        var source = Source(row);
        if (source.Size != null && source.Size != file.Size || source.Extension != null && !string.Equals(source.Extension.TrimStart('.'), Path.GetExtension(file.FileName).TrimStart('.'), StringComparison.OrdinalIgnoreCase) || source.Name != file.FileName)
            throw new ArgumentException("اسم الأصل أو حجمه أو امتداده لا يطابق المرجع. صحح المصدر ثم راجعه.");
        if (row.BytesSHA256 != null && row.BytesSHA256 != file.SHA256) throw new StorageConflictException();
        // Persist the actor and hash before external I/O. Same-row races serialize, cross-row identical bytes share a reservation.
        return await repository.BytesExclusiveAsync(School, file.SHA256, async () =>
        {
            await Authorize(ct);
            row.BytesSHA256 = file.SHA256;
            var duplicate = await repository.DuplicateFileAsync(School, file.SHA256, ct);
            if (row.StoredFileId != null) duplicate = row.StoredFileId;
            if (duplicate != null) await library.DetailsAsync(duplicate.Value, ct);
            else
            {
                var key = "import:" + file.SHA256;
                var pending = await repository.HashOperationAsync(School, file.SHA256, ct);
                if (pending != null && pending.ActorUserId != Actor) throw new StorageConflictException();
                if (pending != null) row.UploadOperationId = pending.Id;
                var root = await repository.LibraryRootAsync(School, ct) ?? throw new ArgumentException("هيّئ مكتبة المدرسة أولًا.");
                await repository.SaveAsync(School, Actor, "Storage.ImportBytesReserved", id, ct);
                StorageUploadDto result;
                try { result = pending != null ? await library.ReconcileAsync(pending.Id, ct) : await library.UploadAsync(new(file.Content, file.FileName, file.Size, root, key, false), ct); }
                catch
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var reservation = await uploads.FindOperationAsync(School, Actor, key, timeout.Token);
                    if (reservation != null) { row.UploadOperationId = reservation.Id; await repository.SaveAsync(School, Actor, "Storage.ImportBytesUncertain", id, timeout.Token); }
                    throw;
                }
                row.UploadOperationId = result.OperationId; duplicate = result.StoredFileId;
                if (result.Status != "Completed") { await repository.SaveAsync(School, Actor, "Storage.ImportBytesPending", id, ct); return RowDto(row, context); }
            }
            var downloaded = await library.ContentAsync(duplicate!.Value, ct);
            await using (downloaded.Content)
            {
                var actual = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(downloaded.Content, ct));
                if (actual != file.SHA256) throw new StorageConflictException();
            }
            await Authorize(ct); await Attach(row, batch, duplicate.Value, ct);
            await repository.SaveAsync(School, Actor, "Storage.ImportBytesAttached", id, ct); return RowDto(row, context);
        }, ct);
    }
    private async Task Attach(PrototypeImportRow row, PrototypeImportBatch batch, int file, CancellationToken ct)
    {
        var context = await repository.ContextAsync(School, batch.AcademicYearId, batch.TemplateVersion, ct); Classify(row, context);
        if (row.Classification != "Matched" || row.RequirementId == null) throw new StorageConflictException();
        if (!(await links.FileLinksAsync(file, ct: ct)).Any(x => x.RequirementId == row.RequirementId && x.AcademicYearId == batch.AcademicYearId))
            await links.CreateAsync(file, new(row.RequirementId.Value, batch.AcademicYearId), ct);
        row.StoredFileId = file; row.Status = PrototypeImportStatus.Committed; row.ExceptionNote = "تم رفع الأصل وربطه كمسودة؛ ينتظر إرسالًا ومراجعة مستقلة.";
    }
    public async Task<ImportRowDto> ReconcileAsync(int id, int rowId, CancellationToken ct = default)
    {
        await Authorize(ct); var batch = await Batch(id, ct); var row = await repository.RowAsync(School, id, rowId, ct) ?? throw new KeyNotFoundException();
        if (batch.CommittedAtUtc == null || row.UploadOperationId == null) throw new ArgumentException("لا توجد عملية رفع محفوظة؛ أعد إرسال نفس الأصل لاستئناف الحجز.");
        var result = await library.ReconcileAsync(row.UploadOperationId.Value, ct);
        await Authorize(ct);
        if (result.Status == "Completed")
        {
            var content = await library.ContentAsync(result.StoredFileId!.Value, ct);
            await using (content.Content)
                if (Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(content.Content, ct)) != row.BytesSHA256) throw new StorageConflictException();
            await Authorize(ct); await Attach(row, batch, result.StoredFileId.Value, ct);
        }
        await repository.SaveAsync(School, Actor, "Storage.ImportBytesReconciled", id, ct);
        return RowDto(row, await repository.ContextAsync(School, batch.AcademicYearId, batch.TemplateVersion, ct));
    }
}
