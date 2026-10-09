using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Storage;

public sealed class VisitArchiveProcessor(IVisitArchiveRepository repository, VisitArchiveDriveService drive,
    IStorageProvider provider, IVisitV2DocumentService documents, IOptions<StorageOptions> options,
    TimeProvider time, ILogger<VisitArchiveProcessor> logger) : IVisitArchiveProcessor
{
    private static readonly Meter Meter = new("AlFalah.Storage.Archive", "1.0");
    private static readonly Counter<long> Claims = Meter.CreateCounter<long>("archive.claims");
    private static readonly Counter<long> Failures = Meter.CreateCounter<long>("archive.failures");
    private static readonly Counter<long> Expired = Meter.CreateCounter<long>("archive.expired_leases");
    private static readonly Histogram<double> Generation = Meter.CreateHistogram<double>("archive.pdf.ms");
    private static readonly Histogram<double> Upload = Meter.CreateHistogram<double>("archive.upload.ms");
    private static readonly Histogram<double> Lag = Meter.CreateHistogram<double>("archive.pending.seconds");
    private bool Enabled => options.Value.AdministrationEnabled && options.Value.ReadModelEnabled;

    public async Task<int> ProcessBatchAsync(CancellationToken ct = default)
    {
        if (!Enabled) return 0;
        var processed = 0;
        foreach (var id in await repository.DueAsync(time.GetUtcNow(), 25, ct))
            await repository.ExclusiveAsync("operation:" + id, async token =>
            {
                if (!Enabled) return;
                drive.Reset();
                var lease = Guid.NewGuid();
                var operation = await repository.ClaimAsync(id, time.GetUtcNow(), lease, token);
                if (operation == null) return;
                if (!await repository.SchoolEnabledAsync(operation.SchoolId, token)) return;
                Claims.Add(1); Lag.Record((time.GetUtcNow() - operation.CreatedAtUtc).TotalSeconds);
                if (operation.LastErrorCode == "ExpiredLease") Expired.Add(1);
                using var workCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
                using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
                var heartbeat = Heartbeat(id, lease, heartbeatCancellation.Token, workCancellation);
                try
                {
                    if (operation.Attempts > 5) throw new InvalidOperationException();
                    await ProcessAsync(operation, workCancellation.Token); processed++;
                }
                catch (Exception error) when (!token.IsCancellationRequested)
                {
                    var code = error is ArchiveIdentityException ? "IdentityMismatch" : error is StorageUnavailableException ? "StorageUnavailable" : "ArchiveUnavailable";
                    Failures.Add(1, new KeyValuePair<string, object?>("code", code));
                    // No exception object, credentials, asset contents or provider responses in logs.
                    logger.LogWarning("Visit archive operation {OperationId}, revision {Revision} failed: {Code}", id, operation.ApprovalRevision, code);
                    await repository.FailAsync(id, lease, code, time.GetUtcNow(), token);
                }
                finally
                {
                    heartbeatCancellation.Cancel();
                    try { await heartbeat; } catch (OperationCanceledException) { }
                }
            }, ct);
        return processed;
    }
    private async Task Heartbeat(int id, Guid lease, CancellationToken ct, CancellationTokenSource work)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(20), ct);
                await repository.RenewAsync(id, lease, time.GetUtcNow(), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch { work.Cancel(); }
    }
    private async Task ProcessAsync(VisitArchiveOperation operation, CancellationToken ct)
    {
        if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operation.SnapshotJson!))) != operation.SnapshotSHA256)
            throw new ArchiveIdentityException();
        if (operation.PdfBytes == null)
        {
            var snapshot = JsonSerializer.Deserialize<VisitApprovalSnapshot>(operation.SnapshotJson!) ?? throw new ArchiveIdentityException();
            if (snapshot.SchemaVersion != 1 || snapshot.Report.Id != operation.VisitId || snapshot.Report.SchoolId != operation.SchoolId || !snapshot.Assets.Frozen)
                throw new ArchiveIdentityException();
            var started = Stopwatch.GetTimestamp();
            var pdf = await documents.BuildPdfAsync(snapshot.Report, snapshot.Assets, ct);
            Generation.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (pdf.Content.Length > 50 * 1024 * 1024) throw new ArchiveIdentityException();
            operation.PdfBytes = pdf.Content;
            operation.PdfSHA256 = Convert.ToHexString(SHA256.HashData(pdf.Content));
            await repository.SaveAsync(operation, "PdfPrepared", ct);
        }
        if (operation.ProviderItemId == null)
        {
            var archive = await drive.FolderAsync(operation.SchoolId, ct);
            var visit = await repository.VisitAsync(operation.SchoolId, operation.VisitId, ct)
                ?? throw new StorageUnavailableException("الزيارة المرتبطة بالتقرير غير متاحة.");
            var folder = await drive.TeacherFolderAsync(operation.SchoolId, archive, visit.InstructorId,
                visit.InstructorName, ct);
            var root = await drive.RootAsync(operation.SchoolId, folder.DriveItemId, ct);
            operation.ProviderItemId = await provider.AllocateIdAsync(operation.SchoolId, ct);
            operation.UploadIdentity = $"{operation.SchoolId}:{operation.VisitId}:{operation.ApprovalRevision}:{operation.RecoveryGeneration}";
            operation.DriveId = root.DriveId; operation.SchoolRootItemId = root.RootItemId;
            operation.ArchiveFolderItemId = folder.DriveItemId; operation.ArchiveFolderId = folder.Id;
            await repository.SaveAsync(operation, "UploadReserved", ct);
        }
        if (await drive.VerifyAsync(operation, operation.ProviderItemId, operation.PdfSHA256!, operation.PdfBytes.LongLength, ct))
        { await repository.CompleteAsync(operation, ct); return; }
        // Reservations made before teacher folders were introduced may still point at
        // the archive root. Preserve the provider ID, but place an unuploaded PDF in
        // its teacher's folder. A file already uploaded in the old root completed above.
        var archiveRoot = await repository.ArchiveFolderAsync(operation.SchoolId, ct);
        if (archiveRoot != null && operation.ArchiveFolderId == archiveRoot.Id)
        {
            var visit = await repository.VisitAsync(operation.SchoolId, operation.VisitId, ct)
                ?? throw new StorageUnavailableException("الزيارة المرتبطة بالتقرير غير متاحة.");
            var teacherFolder = await drive.TeacherFolderAsync(operation.SchoolId, archiveRoot,
                visit.InstructorId, visit.InstructorName, ct);
            operation.ArchiveFolderId = teacherFolder.Id;
            operation.ArchiveFolderItemId = teacherFolder.DriveItemId;
            await repository.SaveAsync(operation, "TeacherFolderSelected", ct);
        }
        // Inspect the saved ID before each attempt. A retry uses exactly the same ID:
        // even a delayed remote create can only conflict, never create a second file.
        drive.Reset();
        await drive.RootAsync(operation.SchoolId, operation.ArchiveFolderItemId, ct);
        operation.UploadStarted = true;
        await repository.SaveAsync(operation, "UploadStarted", ct);
        if (!Enabled || !await repository.SchoolEnabledAsync(operation.SchoolId, ct)) throw new StorageUnavailableException();
        var startedUpload = Stopwatch.GetTimestamp();
        using var bytes = new MemoryStream(operation.PdfBytes, writable: false);
        await provider.UploadArchiveAsync(operation.SchoolId, operation.ProviderItemId, operation.ArchiveFolderItemId!,
            $"تقرير-زيارة-{operation.VisitId}-اعتماد-{operation.ApprovalRevision}.pdf", bytes, VisitArchiveDriveService.Identity(operation), ct);
        Upload.Record(Stopwatch.GetElapsedTime(startedUpload).TotalMilliseconds);
        drive.Reset();
        if (!await drive.VerifyAsync(operation, operation.ProviderItemId, operation.PdfSHA256!, operation.PdfBytes.LongLength, ct)) throw new ArchiveIdentityException();
        await repository.CompleteAsync(operation, ct);
    }
    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        if (!Enabled) return 0;
        var after = 0; var count = 0;
        while (true)
        {
            var batch = await repository.ReconcileBatchAsync(after, 100, ct);
            if (batch.Count == 0) break;
            foreach (var row in batch)
            {
                after = row.Artifact.Id;
                await repository.ExclusiveAsync("operation:" + row.Artifact.OperationId, async token =>
                {
                    drive.Reset();
                    var operation = (await repository.OperationsAsync(row.Artifact.SchoolId, row.Artifact.VisitId, token)).Single(o => o.Id == row.Artifact.OperationId);
                    if (operation.Status != VisitArchiveStatus.Completed) return;
                    try
                    {
                        var exists = await drive.VerifyAsync(operation, row.Version.DriveItemId, row.Version.SHA256!, row.Version.SizeInBytes, token);
                        await repository.SetAvailabilityAsync(row, !exists, time.GetUtcNow(), token); count++;
                    }
                    catch (ArchiveIdentityException) { await repository.SetAvailabilityAsync(row, true, time.GetUtcNow(), token); }
                    catch (Exception) when (!token.IsCancellationRequested)
                    { logger.LogWarning("Archive reconciliation unavailable for operation {OperationId}", operation.Id); }
                }, ct);
            }
        }
        return count;
    }
}
