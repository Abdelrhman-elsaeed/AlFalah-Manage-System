using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

public sealed class EvidenceLinkService(IEvidenceRepository repository, IStorageRepository transactions, EvidenceWorkflowContext context) : IEvidenceLinkService
{
    public async Task<EvidenceLinkDto> CreateAsync(int fileId, CreateEvidenceLinkRequest request, CancellationToken ct = default)
    {
        var file = await context.FileAsync(fileId, true, true, ct);
        var requirement = await repository.RequirementAsync(context.School, request.RequirementId, ct) ?? throw new KeyNotFoundException();
        if (!requirement.IsActive || requirement.AcademicYearId != request.AcademicYearId) throw new ArgumentException("المتطلب خارج السنة المحددة.");
        var teacher = request.TeacherId ?? file.OwnerTeacherId;
        if (file.OwnerTeacherId != null && teacher != file.OwnerTeacherId) throw new ArgumentException("ملكية الرابط لا تطابق الملف.");
        if (teacher != null && !await repository.ActiveTeacherAsync(context.School, teacher.Value, ct)) throw new ArgumentException("المعلم خارج المدرسة.");
        return await transactions.InSerializableTransactionAsync(async () =>
        {
            if ((await repository.FileLinksAsync(context.School, fileId, ct)).Any(x => x.IsActive && x.RequirementId == requirement.Id && x.TeacherId == teacher && x.AcademicYearId == request.AcademicYearId))
                throw new StorageConflictException();
            var link = new EvidenceLink { SchoolId = context.School, AcademicYearId = request.AcademicYearId,
                StoredFileId = fileId, VersionId = file.CurrentVersionId!.Value, RequirementId = requirement.Id, TeacherId = teacher, Status = EvidenceLinkStatus.Draft };
            repository.AddLink(link); file.NeedsLink = false;
            file.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await repository.SaveAsync(context.Actor, context.School, "Storage.LinkCreated", fileId.ToString(), null, null, ct);
            return await repository.LinkDtoAsync(context.School, link.Id, ct);
        }, ct);
    }
    public async Task<EvidenceLinkDto> SubmitAsync(int id, SubmitEvidenceLinkRequest request, CancellationToken ct = default)
    {
        context.Enabled();
        var link = await repository.LinkAsync(context.School, id, ct) ?? throw new KeyNotFoundException();
        var file = await context.FileAsync(link.StoredFileId, true, true, ct);
        var expected = EvidenceWorkflowContext.Expected(request.RowVersion, link.RowVersion);
        if (!link.IsActive || link.VersionId != file.CurrentVersionId || link.Status is not (EvidenceLinkStatus.Draft or EvidenceLinkStatus.Rejected)) throw new StorageConflictException();
        link.Status = link.Status == EvidenceLinkStatus.Rejected ? EvidenceLinkStatus.Resubmitted : EvidenceLinkStatus.PendingReview;
        link.SubmittedAtUtc = DateTimeOffset.UtcNow;
        await repository.SaveAsync(context.Actor, context.School, "Storage.LinkSubmitted", id.ToString(), link, expected, ct);
        return await repository.LinkDtoAsync(context.School, id, ct);
    }
    public async Task<IReadOnlyList<EvidenceLinkDto>> FileLinksAsync(int file, bool own = false, CancellationToken ct = default)
    {
        await context.FileAsync(file, false, false, ct, history: true, ownOnly: own);
        return await repository.LinkDtosAsync(context.School, (await repository.FileLinksAsync(context.School, file, ct)).Select(x => x.Id).ToArray(), ct);
    }
    public async Task<StoragePage<EvidenceLinkDto>> QueueAsync(EvidenceQueueRequest request, CancellationToken ct = default)
    {
        await context.ReviewAsync(ct); await context.YearAsync(request.AcademicYearId, ct);
        if (request.Page is < 1 or > 100000 || request.PageSize is < 1 or > 100 ||
            request.Status != null && !Enum.IsDefined(request.Status.Value) || request.Decided && request.Status != null)
            throw new ArgumentException("الفلاتر غير صالحة.");
        var page = await repository.LinkIdsAsync(context.School, request, ct);
        var rows = await repository.LinkDtosAsync(context.School, page.Items, ct);
        return new(rows, page.Total, page.Page, page.PageSize);
    }
    public async Task<EvidenceCountsDto> CountsAsync(int year, bool own, CancellationToken ct = default)
    {
        await context.ReadAsync(ct); await context.YearAsync(year, ct);
        int? owner = null;
        if (own)
        {
            // Require a teacher with a current grant, rather than a user-supplied teacher ID.
            owner = await repository.OwnTeacherIdAsync(context.School, context.Actor, ct) ?? throw new KeyNotFoundException("لا توجد منحة معلم نشطة.");
        }
        else await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct);
        foreach (var id in await repository.ApprovedFileIdsAsync(context.School, year, owner, ct, ownOnly: own))
        {
            try { await context.FileAsync(id, false, true, ct); }
            catch (KeyNotFoundException) { /* Missing state is persisted; no longer counts. */ }
        }
        return await repository.CountsAsync(context.School, year, owner, ct);
    }
}
