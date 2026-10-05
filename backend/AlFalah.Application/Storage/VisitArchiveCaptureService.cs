using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

public sealed class VisitArchiveCaptureService(IVisitArchiveRepository repository, IVisitArchiveAssetFreezer freezer)
    : IVisitArchiveCaptureService
{
    public async Task<VisitV2PdfAssetSources> PrepareAsync(int visitId, string approverId, CancellationToken ct) =>
        await freezer.FreezeAsync(await repository.ApprovalAssetsAsync(visitId, approverId, ct), ct);

    public Task CaptureAsync(VisitV2DetailDto report, VisitV2PdfAssetSources assets, int revision, string source, CancellationToken ct)
    {
        if (report.Status != (int)VisitStatus.Approved || report.ApprovedAt == null || revision < 1)
            throw new InvalidOperationException("Only an approval transition can stage an archive.");
        var json = JsonSerializer.Serialize(new VisitApprovalSnapshot(1, report, assets with { GeneratedAtUtc = report.ApprovedAt }));
        return repository.StageAsync(new VisitArchiveOperation
        {
            SchoolId = report.SchoolId, VisitId = report.Id, ApprovalRevision = revision,
            Status = VisitArchiveStatus.Pending, ApprovedAtUtc = report.ApprovedAt.Value, ApprovalSource = source,
            SnapshotJson = json, SnapshotSHA256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))
        }, ct);
    }
    public Task MarkHistoricalAsync(int visitId, CancellationToken ct) => repository.MarkHistoricalAsync(visitId, ct);
    public async Task<VisitApprovalSnapshot?> SnapshotAsync(int schoolId, int visitId, int revision, CancellationToken ct)
    {
        var json = await repository.SnapshotJsonAsync(schoolId, visitId, revision, ct);
        return json == null ? null : JsonSerializer.Deserialize<VisitApprovalSnapshot>(json);
    }
}
