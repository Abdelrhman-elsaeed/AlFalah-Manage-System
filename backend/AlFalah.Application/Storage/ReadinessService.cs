using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

public sealed class ReadinessService(IReadinessRepository repository, IEvidenceRepository evidence,
    IStorageRepository transactions, EvidenceWorkflowContext context, IStorageProvider provider) : IReadinessService
{
    public const int ExportLimit = 5000;
    public const int LiveVerificationLimit = 10000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static EvaluationTemplateSnapshot BuiltInTemplate()
    {
        using var stream = typeof(ReadinessService).Assembly.GetManifestResourceStream("AlFalah.Application.Storage.Templates.self-evaluation-v1.json")!;
        return JsonSerializer.Deserialize<EvaluationTemplateSnapshot>(stream, Json)!;
    }
    public static string TemplateHash(EvaluationTemplateSnapshot snapshot) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot, Json))));
    public async Task<IReadOnlyList<EvaluationMemberDto>> MembersAsync(CancellationToken ct = default)
    { await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct); return await repository.MembersAsync(context.School, ct); }
    public async Task<IReadOnlyList<EvaluationVersionDto>> VersionsAsync(CancellationToken ct = default)
    { await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct); return await repository.VersionsAsync(ct); }
    public async Task<StoragePage<DigitalIndexFileDto>> IndexAsync(ReadinessFilter filter, CancellationToken ct = default)
    { var (_, denied) = await PrepareAsync(filter, ct); return await repository.IndexAsync(context.School, filter, denied, ct); }
    public async Task<EvaluationTemplateSnapshot> TemplateAsync(int version, CancellationToken ct = default)
    {
        await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct);
        return await LoadTemplateAsync(version, ct);
    }
    private async Task<EvaluationTemplateSnapshot> LoadTemplateAsync(int version, CancellationToken ct)
    {
        var stored = await repository.TemplateAsync(version, ct);
        if (stored != null) return JsonSerializer.Deserialize<EvaluationTemplateSnapshot>(stored.SnapshotJson, Json)!;
        if (version == 1) return BuiltInTemplate();
        throw new KeyNotFoundException("إصدار القالب غير موجود.");
    }
    public async Task InitializeAsync(InitializeEvaluationRequest request, CancellationToken ct = default)
    {
        await context.ManageAsync(ct); await context.YearAsync(request.AcademicYearId, ct);
        var template = await LoadTemplateAsync(request.TemplateVersion, ct);
        await transactions.InSerializableTransactionAsync(async () =>
        {
            if (await repository.ScopeExistsAsync(context.School, request.AcademicYearId, template.Version, ct)) return true;
            if (await repository.TemplateAsync(template.Version, ct) == null)
                await repository.AddTemplateAsync(new() { Version = template.Version, Name = template.Name, SHA256 = TemplateHash(template), SnapshotJson = JsonSerializer.Serialize(template, Json) }, ct);
            var existing = await evidence.RequirementsAsync(context.School, request.AcademicYearId, ct);
            var rows = template.Items.Where(item => !existing.Any(x => x.TemplateVersion == template.Version && x.Code == "S4-" + item.SourceKey)).Select(item => new EvidenceRequirement {
                SchoolId = context.School, AcademicYearId = request.AcademicYearId, TemplateVersion = template.Version,
                Code = "S4-" + item.SourceKey, DisplayName = item.DisplayName, DomainCode = item.StandardCode[..1], StandardCode = item.StandardCode,
                SourceKey = item.SourceKey, SourceSHA256 = item.SourceSha256, ReferencePath = item.ReferencePath,
                CompletionAction = item.CompletionAction, ImportanceReason = item.ImportanceReason,
                CandidateTaskCodesJson = JsonSerializer.Serialize(item.CandidateTaskCodes), ResponsibleRole = item.ResponsibleRole,
                Importance = Enum.Parse<EvidenceImportance>(item.Importance), SortOrder = item.SortOrder, IsMandatory = true }).ToArray();
            await repository.InitializeAsync(new() { SchoolId = context.School, AcademicYearId = request.AcademicYearId,
                TemplateVersion = template.Version, CreatedByUserId = context.Actor }, rows, context.Actor, ct);
            return true;
        }, ct);
    }
    private async Task<(EvaluationTemplateSnapshot Template, IReadOnlyList<int> Denied)> PrepareAsync(ReadinessFilter filter, CancellationToken ct)
    {
        await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct); await context.YearAsync(filter.AcademicYearId, ct);
        Validate(filter);
        var template = await LoadTemplateAsync(filter.TemplateVersion, ct);
        // A version never implicitly reinterprets another school/year's rows. Uninitialized scopes are 0/0.
        var denied = new List<int>(); var last = 0; var checkedFiles = 0;
        while (true)
        {
            var files = await repository.LiveFilesAsync(context.School, filter, last, 200, ct);
            if (files.Count == 0) break;
            checkedFiles += files.Count;
            if (checkedFiles > LiveVerificationLimit) throw new StorageUnavailableException("تجاوز التقرير حد التحقق المباشر للملفات؛ ضيّق نطاق المرشحات.");
            var observations = new List<AvailabilityObservation>();
            foreach (var file in files)
            {
                if (file.DriveId != file.AuthorizedDriveId || !await provider.IsWithinAsync(context.School, file.SchoolRoot, file.AuthorizedRoot, ct))
                { denied.Add(file.VersionId); continue; }
                var meta = await provider.MetadataAsync(context.School, file.DriveItemId, ct);
                var available = meta is { Trashed: false, IsFolder: false } && await provider.IsWithinAsync(context.School, file.AuthorizedRoot, file.DriveItemId, ct);
                observations.Add(new(file.VersionId, !available));
            }
            await repository.ObserveAsync(context.School, observations, context.Actor, ct);
            last = files[^1].FileId;
        }
        // Delegations can be revoked during provider I/O; recheck before returning or exporting any data.
        await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct);
        return (template, denied);
    }
    public static void Validate(ReadinessFilter f)
    {
        if (f.AcademicYearId < 1 || f.TemplateVersion < 1 || f.Page is < 1 or > 100000 || f.PageSize is < 1 or > 100 ||
            f.Search?.Length > 200 || f.ResponsibleUserId?.Length > 450 || f.Importance != null && !Enum.IsDefined(f.Importance.Value) ||
            f.DomainCode != null && f.DomainCode is not ("1" or "2" or "3" or "4") ||
            f.StandardCode != null && !RequirementCatalogService.Standards.Any(x => x.Code == f.StandardCode) ||
            f.StandardCode != null && f.DomainCode != null && f.StandardCode[..1] != f.DomainCode ||
            f.Status != null && f.Status is not ("Fulfilled" or "Unfulfilled" or "NoFile" or "Unavailable" or "AwaitingReview" or "Rejected" or "InsufficientApprovedLinks"))
            throw new ArgumentException("مرشحات التقويم غير صالحة.");
    }
    public async Task<StoragePage<ReadinessRequirementDto>> RequirementsAsync(ReadinessFilter filter, bool gaps = false, CancellationToken ct = default)
    {
        var (_, denied) = await PrepareAsync(filter, ct);
        var rows = await repository.PageAsync(context.School, filter, denied, gaps, ct);
        return new(rows.Items.Select(Row).ToArray(), rows.Total, rows.Page, rows.PageSize);
    }
    public async Task<ReadinessDto> ReadinessAsync(ReadinessFilter filter, CancellationToken ct = default)
    {
        var (template, denied) = await PrepareAsync(filter, ct);
        return await SummaryAsync(filter, template, denied, ct);
    }
    private async Task<ReadinessDto> SummaryAsync(ReadinessFilter filter, EvaluationTemplateSnapshot template, IReadOnlyList<int> denied, CancellationToken ct)
    {
        var aggregates = await repository.AggregatesAsync(context.School, filter, denied, ct);
        var files = await repository.FileCountsAsync(context.School, filter, denied, ct);
        var identity = await repository.IdentityAsync(context.School, filter.AcademicYearId, ct);
        ReadinessMetric Metric(string code, string name, IEnumerable<ReadinessAggregate> selected, int count)
        {
            var rows = selected.ToArray(); var n = rows.Sum(x => x.Numerator); var d = rows.Sum(x => x.Denominator);
            return new(code, name, rows.Sum(x => x.Requirements), n, d, Percentage(n, d), d == 0 ? "NoRequirements" : "Calculated", count,
                rows.Sum(x => x.Links), rows.Sum(x => x.ApprovedLinks), d - n, rows.Sum(x => x.CriticalGaps));
        }
        return new(context.School, identity.SchoolName, filter.AcademicYearId, identity.YearName, template.Version, template.Name, TemplateHash(template), template.Rounding,
            DateTimeOffset.UtcNow, filter,
            Metric("school", identity.SchoolName, aggregates, files[0].Count),
            template.Domains.OrderBy(x => x.SortOrder).Select(d => Metric(d.Code, d.Name, aggregates.Where(x => x.DomainCode == d.Code), files.FirstOrDefault(x => x.DomainCode == d.Code && x.StandardCode == null)?.Count ?? 0)).ToArray(),
            template.Standards.OrderBy(x => x.SortOrder).Select(s => Metric(s.Code, s.Name, aggregates.Where(x => x.StandardCode == s.Code), files.FirstOrDefault(x => x.StandardCode == s.Code)?.Count ?? 0)).ToArray());
    }
    public static decimal? Percentage(int n, int d) => d == 0 ? null : Math.Round(100m * n / d, 2, MidpointRounding.AwayFromZero);
    public static ReadinessRequirementDto Row(RequirementFacts r)
    {
        var required = r.Policy == EvidenceFulfillmentPolicy.AnyApprovedLink ? 1 : r.MinimumApprovedLinks;
        var fulfilled = r.ApprovedLinks >= required;
        var reasons = new List<string>();
        if (!fulfilled)
        {
            if (r.Links == 0) reasons.Add("NoFile");
            if (r.Links > r.AvailableLinks) reasons.Add("Unavailable");
            if (r.PendingLinks > 0) reasons.Add("AwaitingReview");
            if (r.RejectedLinks > 0) reasons.Add("Rejected");
            if (r.ApprovedLinks > 0 || reasons.Count == 0) reasons.Add("InsufficientApprovedLinks");
        }
        return new(r.Id, r.Code, r.Name, r.DomainCode, r.StandardCode, r.OriginalTaskId, r.ResponsibleUserId, r.ResponsibleName,
            r.ResponsibleRole, r.Importance.ToString(), r.IsMandatory, r.Policy.ToString(), required, r.Links, r.AvailableLinks, r.ApprovedLinks,
            fulfilled, fulfilled ? "Fulfilled" : reasons[0], reasons,
            r.CompletionAction ?? "اربط ملفًا متاحًا بهذا المتطلب وأرسله للمراجعة حتى يكتمل عدد الروابط المعتمدة.",
            $"/school-manager/storage?requirement={r.Id}&academicYearId={r.AcademicYearId}", r.AvailableFileId, r.ReferencePath, r.SourceKey, r.SourceSHA256,
            r.CandidateTaskCodesJson == null ? [] : JsonSerializer.Deserialize<string[]>(r.CandidateTaskCodesJson)!,
            r.SourceKey == null ? "ExactExistingRequirement" : "CandidateTasksNeedContentReview", r.DueDate, r.FollowUpStatus, r.FollowUpNote, Convert.ToBase64String(r.RowVersion));
    }
    public async Task<ReadinessRequirementDto> ConfigureAsync(int id, ConfigureFollowUpRequest request, CancellationToken ct = default)
    {
        await context.ManageAsync(ct);
        var r = await repository.RequirementAsync(context.School, id, ct) ?? throw new KeyNotFoundException();
        if (r.AcademicYearId == null || !r.IsActive) throw new KeyNotFoundException();
        if (!Enum.IsDefined(request.Importance) || !Enum.IsDefined(request.Policy) || request.MinimumApprovedLinks is < 1 or > 100 ||
            request.ResponsibleRole?.Length > 100 || request.Note?.Length > 1000 || request.FollowUpStatus is not ("NotStarted" or "InProgress" or "ReadyForReview")) throw new ArgumentException("إعداد المتابعة غير صالح.");
        if (request.ResponsibleUserId != null && await transactions.GetActorScopeAsync(request.ResponsibleUserId, context.School, ct) is not { IsMember: true }) throw new ArgumentException("المسؤول ليس عضوًا نشطًا في المدرسة.");
        var expected = EvidenceWorkflowContext.Expected(request.RowVersion, r.RowVersion);
        var before = JsonSerializer.Serialize(r, Json); var reason = EvidenceWorkflowContext.Reason(request.Reason);
        r.ResponsibleUserId = request.ResponsibleUserId; r.ResponsibleRole = request.ResponsibleRole; r.Importance = request.Importance;
        r.IsMandatory = request.IsMandatory; r.FulfillmentPolicy = request.Policy; r.MinimumApprovedLinks = request.MinimumApprovedLinks;
        r.DueDate = request.DueDate; r.FollowUpStatus = request.FollowUpStatus; r.FollowUpNote = request.Note; r.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await repository.SaveFollowUpAsync(r, expected, new() { SchoolId = context.School, RequirementId = id, ActorUserId = context.Actor,
            Reason = reason, OldValuesJson = before, NewValuesJson = JsonSerializer.Serialize(r, Json) }, ct);
        var filter = new ReadinessFilter(r.AcademicYearId.Value, r.TemplateVersion, Search: r.Code, PageSize: 100);
        return (await RequirementsAsync(filter, ct: ct)).Items.Single(x => x.Id == id);
    }
    public async Task<IReadOnlyList<FollowUpHistoryDto>> FollowUpHistoryAsync(int id, CancellationToken ct = default)
    {
        await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct);
        _ = await repository.RequirementAsync(context.School, id, ct) ?? throw new KeyNotFoundException();
        return await repository.FollowUpHistoryAsync(context.School, id, ct);
    }
    public async Task<ManualEvaluationDto> SaveManualAsync(SaveManualEvaluationRequest request, CancellationToken ct = default)
    {
        await context.ManageAsync(ct); await context.YearAsync(request.AcademicYearId, ct);
        var template = await LoadTemplateAsync(request.TemplateVersion, ct);
        if (!await repository.ScopeExistsAsync(context.School, request.AcademicYearId, request.TemplateVersion, ct)) throw new ArgumentException("هيّئ قالب السنة أولًا.");
        if (request.ScopeCode != "school" && !template.Domains.Any(x => x.Code == request.ScopeCode) && !template.Standards.Any(x => x.Code == request.ScopeCode) ||
            string.IsNullOrWhiteSpace(request.Judgment) && request.Value == null || request.Judgment.Length > 300 || request.Value is < -999999 or > 999999 ||
            request.Value != null && Math.Round(request.Value.Value, 2, MidpointRounding.AwayFromZero) != request.Value.Value) throw new ArgumentException("قيمة أو نطاق التقويم اليدوي غير صالح.");
        var reason = EvidenceWorkflowContext.Reason(request.Reason);
        return await transactions.InSerializableTransactionAsync(async () =>
        {
            var e = await repository.ManualAsync(context.School, request.AcademicYearId, request.TemplateVersion, request.ScopeCode, ct);
            byte[]? expected = null;
            if (e != null) expected = EvidenceWorkflowContext.Expected(request.RowVersion ?? "", e.RowVersion);
            else if (request.RowVersion != null) throw new StorageConflictException();
            e ??= new() { SchoolId = context.School, AcademicYearId = request.AcademicYearId, TemplateVersion = request.TemplateVersion, ScopeCode = request.ScopeCode };
            e.Judgment = request.Judgment.Trim(); e.Value = request.Value; e.Reason = reason; e.EvaluatorUserId = context.Actor;
            e.EvaluatorName = await evidence.ActorNameAsync(context.Actor, ct); e.EvaluatedAtUtc = DateTimeOffset.UtcNow; e.UpdatedAtUtc = e.EvaluatedAtUtc; e.Revision++;
            await repository.SaveManualAsync(e, expected, context.Actor, ct);
            return (await repository.ManualsAsync(context.School, request.AcademicYearId, request.TemplateVersion, ct)).Single(x => x.Id == e.Id);
        }, ct);
    }
    public async Task<IReadOnlyList<ManualEvaluationDto>> ManualsAsync(int year, int version, CancellationToken ct = default)
    {
        await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct); await context.YearAsync(year, ct);
        return await repository.ManualsAsync(context.School, year, version, ct);
    }
    public async Task<IReadOnlyList<ManualEvaluationHistoryDto>> ManualHistoryAsync(int id, CancellationToken ct = default)
    {
        await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct);
        return await repository.ManualHistoryAsync(context.School, id, ct);
    }
    public async Task<ReadinessExportData> ExportDataAsync(ReadinessFilter filter, CancellationToken ct = default)
    {
        var (template, denied) = await PrepareAsync(filter, ct);
        // Repeatable-read serializable transaction keeps summaries/rows/manual judgments in one snapshot.
        return await transactions.InSerializableTransactionAsync(async () =>
        {
            var summary = await SummaryAsync(filter, template, denied, ct);
            var rows = new List<ReadinessRequirementDto>();
            for (var page = 1; ; page++)
            {
                var result = await repository.PageAsync(context.School, filter with { Page = page, PageSize = 100 }, denied, false, ct);
                if (result.Total > ExportLimit) throw new ArgumentException($"حد التصدير {ExportLimit} متطلب؛ ضيّق المرشحات.");
                rows.AddRange(result.Items.Select(Row)); if (rows.Count >= result.Total) break;
            }
            var identity = await repository.IdentityAsync(context.School, filter.AcademicYearId, ct);
            var manuals = await repository.ManualsAsync(context.School, filter.AcademicYearId, filter.TemplateVersion, ct);
            var selectedManuals = manuals.Where(x => filter.StandardCode != null ? x.ScopeCode == filter.StandardCode : filter.DomainCode != null
                ? x.ScopeCode == filter.DomainCode || x.ScopeCode.StartsWith(filter.DomainCode + ".", StringComparison.Ordinal) : true).ToArray();
            var files = new List<DigitalIndexFileDto>();
            for (var page = 1; ; page++)
            {
                var index = await repository.IndexAsync(context.School, filter with { Page = page, PageSize = 100 }, denied, ct);
                if (index.Total > ExportLimit) throw new ArgumentException($"حد التصدير {ExportLimit} ملف؛ ضيّق المرشحات.");
                files.AddRange(index.Items); if (files.Count >= index.Total) break;
            }
            await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct);
            return new ReadinessExportData(summary, rows, selectedManuals, identity.HeaderText, files);
        }, ct);
    }
}

public sealed class ReadinessExportService(IReadinessService readiness, IReadinessExportRenderer renderer) : IReadinessExportService
{
    public async Task<ReadinessExportResult> ExportAsync(string format, ReadinessFilter filter, CancellationToken ct = default)
    {
        if (format is not ("csv" or "excel" or "pdf")) throw new ArgumentException("صيغة التصدير غير مدعومة.");
        return renderer.Render(format, await readiness.ExportDataAsync(filter, ct));
    }
}
