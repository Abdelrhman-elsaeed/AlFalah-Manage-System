using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

public sealed class EvidenceReviewService(IEvidenceRepository repository, IStorageRepository transactions, EvidenceWorkflowContext context) : IEvidenceReviewService
{
    public async Task<EvidenceLinkDto> ReviewAsync(int id, ReviewEvidenceLinkRequest request, CancellationToken ct = default)
    {
        await context.ReviewAsync(ct);
        if (request.Decision is not (EvidenceReviewStatus.Approved or EvidenceReviewStatus.Rejected)) throw new ArgumentException("القرار غير صالح.");
        var note = request.Decision == EvidenceReviewStatus.Rejected ? EvidenceWorkflowContext.Reason(request.Note) : request.Note?.Trim();
        if (note?.Length > 1000) throw new ArgumentException("الملاحظة طويلة.");
        var link = await repository.LinkAsync(context.School, id, ct) ?? throw new KeyNotFoundException();
        var file = await context.FileAsync(link.StoredFileId, false, request.Decision == EvidenceReviewStatus.Approved, ct);
        var reviewer = await repository.ActorNameAsync(context.Actor, ct);
        return await transactions.InSerializableTransactionAsync(async () =>
        {
            var expected = EvidenceWorkflowContext.Expected(request.RowVersion, link.RowVersion);
            if (!link.IsActive || link.Status is not (EvidenceLinkStatus.PendingReview or EvidenceLinkStatus.Resubmitted) || link.VersionId != file.CurrentVersionId)
                throw new StorageConflictException();
            link.Status = request.Decision == EvidenceReviewStatus.Approved ? EvidenceLinkStatus.Approved : EvidenceLinkStatus.Rejected;
            file.UpdatedAtUtc = DateTimeOffset.UtcNow;
            repository.AddDecision(new EvidenceReviewDecision { SchoolId = context.School, EvidenceLinkId = id,
                StoredFileId = link.StoredFileId, VersionId = link.VersionId, Decision = request.Decision,
                ReviewedByUserId = context.Actor, ReviewerName = reviewer, ReviewedAtUtc = DateTimeOffset.UtcNow, Note = note });
            await repository.SaveAsync(context.Actor, context.School, "Storage.LinkReviewed", id.ToString(), link, expected, ct);
            return await repository.LinkDtoAsync(context.School, id, ct);
        }, ct);
    }
}
