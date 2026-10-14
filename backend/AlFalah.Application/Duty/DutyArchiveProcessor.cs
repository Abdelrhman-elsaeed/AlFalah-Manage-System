using AlFalah.Application.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Duty;

public sealed class DutyArchiveProcessor(IDutyDocumentRepository repo,IDutyExecutionRepository execution,IDutyPdfRenderer renderer,
    DutyArchiveDriveService drive,IStorageProvider provider,IOptions<DutyArchiveOptions> options,ILogger<DutyArchiveProcessor> logger)
{
    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        var count=0;
        foreach(var (school,document) in await repo.DueAsync(DateTimeOffset.UtcNow,ct))
        {
            if(!options.Value.ProcessingEnabled && await repo.ArtifactAsync(school,document,ct) is not null)continue;
            await repo.ExclusiveAsync($"operation:{school}:{document}",async token=>
            {
                var lease=Guid.NewGuid();var i=await repo.ClaimAsync(school,document,lease,DateTimeOffset.UtcNow,token);if(i is null)return;
                using var work=CancellationTokenSource.CreateLinkedTokenSource(token);
                using var heartbeatStop=CancellationTokenSource.CreateLinkedTokenSource(token);
                var heartbeat=Heartbeat(i.Id,lease,heartbeatStop.Token,work);
                try
                {
                    var a=await DutyArtifactPreparation.PrepareAsync(repo,execution,renderer,school,document,work.Token);
                    i.ArtifactId=a.Id;
                    var ready=await repo.ReadinessAsync(school,work.Token);
                    if(!options.Value.ProcessingEnabled||!ready.Enabled||ready.ConnectionState!="Connected")
                    {i.State="Pending";i.LeaseExpiresAt=null;i.Attempts=Math.Max(0,i.Attempts-1);await repo.SaveAsync(i,work.Token);return;}
                    var doc=await execution.DocumentAsync(school,document,work.Token)??throw DutyDocumentIntegrity.Error();
                    i.OperationIdentity??=$"{school}:{doc.AssignmentId}:{doc.AssignmentRevision}:{doc.Kind}:{doc.Id}";
                    await repo.SaveAsync(i,work.Token); // Operation and artifact BEFORE any provider writes.
                    if(i.ProviderItemId is null)
                    {
                        var folder=await drive.DestinationAsync(doc,work.Token);var root=await drive.RootAsync(school,folder.DriveItemId,work.Token);
                        i.ProviderItemId=await provider.AllocateIdAsync(school,work.Token);i.DriveId=root.DriveId;i.SchoolRootItemId=root.RootItemId;i.FolderId=folder.Id;i.FolderItemId=folder.DriveItemId;
                        await repo.SaveAsync(i,work.Token);
                    }
                    if(!await drive.ExistsAndVerifiedAsync(i,a,work.Token))
                    {
                        ready=await repo.ReadinessAsync(school,work.Token);
                        if(!options.Value.ProcessingEnabled||!ready.Enabled||ready.ConnectionState!="Connected")throw new StorageUnavailableException();
                        using var bytes=new MemoryStream(a.Bytes,false);
                        await provider.UploadArchiveAsync(school,i.ProviderItemId!,i.FolderItemId!,a.FileName,bytes,DutyArchiveDriveService.Properties(i,a),work.Token);
                        await drive.VerifyAsync(i,a,work.Token);
                    }
                    await repo.CompleteAsync(i,a,provider.IsRealDelivery,work.Token);count++;
                }
                catch(Exception e) when(!token.IsCancellationRequested)
                {
                    var code=e is DutyException d?d.Code:e is StorageUnavailableException?"ConnectionUnavailable":"DeliveryUnavailable";
                    logger.LogWarning("Duty document {Document} archive deferred: {Code}",document,code);
                    await repo.FailAsync(school,document,lease,code,DateTimeOffset.UtcNow,token);
                }
                finally {heartbeatStop.Cancel();try{await heartbeat;}catch(OperationCanceledException){} }
            },ct);
        }
        return count;
    }
    private async Task Heartbeat(int id,Guid lease,CancellationToken ct,CancellationTokenSource work)
    {
        try {while(true){await Task.Delay(TimeSpan.FromSeconds(20),ct);await repo.RenewAsync(id,lease,DateTimeOffset.UtcNow,ct);}}
        catch(OperationCanceledException)when(ct.IsCancellationRequested){}
        catch {work.Cancel();}
    }
    public async Task ReconcileAsync(CancellationToken ct)
    {
        if(!options.Value.ProcessingEnabled)return;
        foreach(var(school,document)in await repo.ReconcileDueAsync(ct))
            await repo.ExclusiveAsync($"operation:{school}:{document}",async token=>
            {
                var i=await repo.IntentAsync(school,document,token);if(i is not {State:"Archived"})return;
                try {await drive.VerifyAsync(i,(await repo.ArtifactAsync(school,document,token))!,token);await repo.MarkAvailabilityAsync(i,null,DateTimeOffset.UtcNow,token);}
                catch(DutyException e){await repo.MarkAvailabilityAsync(i,e.Code,DateTimeOffset.UtcNow,token);}
                catch(Exception)when(!token.IsCancellationRequested){await repo.MarkAvailabilityAsync(i,"ConnectionUnavailable",DateTimeOffset.UtcNow,token);}
            },ct);
    }
}
