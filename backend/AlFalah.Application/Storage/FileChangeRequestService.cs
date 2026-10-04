using AlFalah.Application.Common;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

public sealed class FileChangeRequestService(IEvidenceRepository repository, IStorageRepository transactions,
    EvidenceWorkflowContext context, IStorageProvider provider) : IFileChangeRequestService
{
    public async Task<StorageFileDetailsDto> HistoryAsync(int file, CancellationToken ct = default)
    {
        await context.FileAsync(file, false, false, ct, history: true);
        return await repository.HistoryAsync(context.School, file, ct) ?? throw new KeyNotFoundException();
    }
    public async Task<StoragePage<ChangeQueueItemDto>> QueueAsync(int year, int page = 1, string? status = "Pending", CancellationToken ct = default)
    {
        await context.ReviewAsync(ct); await context.YearAsync(year, ct);
        if (page is < 1 or > 100000 || status != null && status is not ("Pending" or "Approved" or "Rejected")) throw new ArgumentException("الفلاتر غير صالحة.");
        return await repository.ChangeQueueAsync(context.School, year, page, status, ct);
    }
    public async Task<FileChangeDto> CreateAsync(int fileId, CreateFileChangeRequest request, CancellationToken ct = default)
    {
        var reason = EvidenceWorkflowContext.Reason(request.Reason);
        if (request.Kind is not ("Replace" or "Delete")) throw new ArgumentException("نوع التغيير غير صالح.");
        var file = await context.FileAsync(fileId, true, false, ct);
        return await transactions.InSerializableTransactionAsync(async () =>
        {
            var expected = EvidenceWorkflowContext.Expected(request.RowVersion, file.RowVersion);
            if (request.ReplaceBeforeReview && (request.Kind != "Replace" || await repository.HasApprovalHistoryAsync(context.School, fileId, ct)))
                throw new BusinessRuleException("يلزم قرار المراجع لتغيير ملف سبق اعتماده.");
            if ((await repository.ChangesAsync(context.School, fileId, ct)).Any(x => x.Status == "Pending")) throw new StorageConflictException();
            var change = new FileChangeRequest { SchoolId = context.School, StoredFileId = fileId,
                OriginalVersionId = file.CurrentVersionId!.Value, Kind = request.Kind, Reason = reason, RequestedByUserId = context.Actor, ReplaceBeforeReview = request.ReplaceBeforeReview };
            repository.AddChange(change);
            // Touch the asset so concurrent replacement/request/mutation races conflict on the same row.
            file.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await repository.SaveAsync(context.Actor, context.School, "Storage.ChangeRequested", fileId.ToString(), file, expected, ct);
            return (await repository.ChangesAsync(context.School, fileId, ct)).Single(x => x.Id == change.Id);
        }, ct);
    }
    public async Task CompleteCandidateAsync(int id, CancellationToken ct = default)
    {
        var change = await repository.ChangeAsync(context.School, id, ct) ?? throw new KeyNotFoundException();
        if (!change.ReplaceBeforeReview || change.Status != "Pending") return;
        await context.FileAsync(change.StoredFileId, true, false, ct);
        if (change.RequestedByUserId != context.Actor) throw new UnauthorizedSchoolAccessException("الطلب ليس ملكك.");
        await DecideAsync(change, new(true, "استبدال قبل أول اعتماد؛ الروابط تتطلب مراجعة مستقلة.", Convert.ToBase64String(change.RowVersion)), ct, true);
    }
    public async Task<IReadOnlyList<FileChangeDto>> ListAsync(int file, CancellationToken ct = default)
    {
        await context.FileAsync(file, false, false, ct, history: true);
        return await repository.ChangesAsync(context.School, file, ct);
    }
    public async Task<ReplacementUploadTarget> RequireUploadAsync(int id, CancellationToken ct = default)
    {
        context.Enabled();
        var change = await repository.ChangeAsync(context.School, id, ct) ?? throw new KeyNotFoundException();
        var file = await context.FileAsync(change.StoredFileId, true, false, ct);
        if (change.RequestedByUserId != context.Actor) throw new UnauthorizedSchoolAccessException("الطلب ليس ملكك.");
        if (change.Kind != "Replace" || change.Status != "Pending" || change.OriginalVersionId != file.CurrentVersionId)
            throw new StorageConflictException();
        return new(file.Id, file.FolderId, file.OwnerTeacherId);
    }
    public async Task<FileChangeDto> ReviewAsync(int id, ReviewFileChangeRequest request, CancellationToken ct = default)
    {
        await context.ReviewAsync(ct);
        var change = await repository.ChangeAsync(context.School, id, ct) ?? throw new KeyNotFoundException();
        return await DecideAsync(change, request, ct);
    }
    private async Task<FileChangeDto> DecideAsync(FileChangeRequest change, ReviewFileChangeRequest request, CancellationToken ct, bool direct = false)
    {
        var id = change.Id;
        var note = request.Approve ? request.Note?.Trim() : EvidenceWorkflowContext.Reason(request.Note);
        if (note?.Length > 1000) throw new ArgumentException("الملاحظة طويلة.");
        var file = await context.FileAsync(change.StoredFileId, false, false, ct);
        var reviewer = await repository.ActorNameAsync(context.Actor, ct);
        if (request.Approve && change.Kind == "Replace")
        {
            var v = await repository.VersionAsync(context.School, file.Id, change.CandidateVersionId ?? 0, ct) ?? throw new ArgumentException("ارفع النسخة الجديدة قبل القرار.");
            var root = await transactions.GetSchoolDriveRootAsync(context.School, ct) ?? throw new StorageUnavailableException();
            if (file.OwnerTeacherId != null)
            {
                root = await transactions.GetTeacherDriveRootAsync(context.School, file.OwnerTeacherId.Value, ct) ?? throw new UnauthorizedSchoolAccessException("سُحبت منحة المعلم.");
                var school = await transactions.GetSchoolDriveRootAsync(context.School, ct) ?? throw new StorageUnavailableException();
                if (!await provider.IsWithinAsync(context.School, school.RootItemId, root.RootItemId, ct)) throw new UnauthorizedSchoolAccessException("منحة المعلم خارج المدرسة.");
            }
            await context.RequireAvailableAsync(file, v, root, ct);
        }
        return await transactions.InSerializableTransactionAsync(async () =>
        {
            var expected = EvidenceWorkflowContext.Expected(request.RowVersion, change.RowVersion);
            if (direct && await repository.HasApprovalHistoryAsync(context.School, file.Id, ct)) throw new StorageConflictException();
            if (change.Status != "Pending" || file.CurrentVersionId != change.OriginalVersionId) throw new StorageConflictException();
            change.Status = request.Approve ? "Approved" : "Rejected";
            repository.AddChangeDecision(new() { SchoolId = context.School, FileChangeRequestId = id,
                Decision = change.Status, ReviewedByUserId = context.Actor, ReviewerName = reviewer, Note = note });
            if (request.Approve)
            {
                if (change.Kind == "Replace")
                {
                    file.CurrentVersionId = change.CandidateVersionId;
                    foreach (var link in await repository.FileLinksAsync(context.School, file.Id, ct))
                    {
                        if (!link.IsActive) continue;
                        // Explicitly migrate every active link, including links to older versions; audit lists the request.
                        link.VersionId = change.CandidateVersionId!.Value;
                        link.Status = EvidenceLinkStatus.PendingReview;
                        link.SubmittedAtUtc = DateTimeOffset.UtcNow;
                    }
                }
                else
                {
                    // Logical withdrawal retains Drive bytes for the authorized history endpoint.
                    file.IsDeleted = true; file.DeletedAtUtc = DateTimeOffset.UtcNow; file.DeletedByUserId = context.Actor;
                }
            }
            await repository.SaveAsync(context.Actor, context.School, "Storage.ChangeDecided", id.ToString(), change, expected, ct);
            return (await repository.ChangesAsync(context.School, file.Id, ct)).Single(x => x.Id == id);
        }, ct);
    }
    public async Task<DriveFileContentDto> VersionContentAsync(int fileId, int version, CancellationToken ct = default)
    {
        var file = await context.FileAsync(fileId, false, false, ct, history: true);
        var v = await repository.VersionAsync(context.School, fileId, version, ct) ?? throw new KeyNotFoundException();
        var root = file.OwnerTeacherId != null
            ? await transactions.GetTeacherDriveRootAsync(context.School, file.OwnerTeacherId.Value, ct)
            : await transactions.GetSchoolDriveRootAsync(context.School, ct);
        if (root is null) throw new UnauthorizedSchoolAccessException("لا توجد منحة للملف.");
        await context.RequireAvailableAsync(file, v, root, ct);
        var content = await provider.ContentAsync(context.School, v.DriveItemId, ct);
        return content with { FileName = v.DriveFileName, ContentType = v.MimeType ?? "application/octet-stream" };
    }
}
