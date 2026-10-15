using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class StorageDriveBrowserRepository(AlFalahDbContext db, EvidenceSubmissionService legacyWriter) : IStorageDriveBrowserRepository
{
    private async Task<int[]> FolderScopeAsync(int school,IReadOnlyList<string> ids,CancellationToken ct)
    {
        var roots=await db.StorageFolders.AsNoTracking().Where(x=>x.SchoolId==school&&ids.Contains(x.DriveItemId)).Select(x=>x.Id).ToArrayAsync(ct);
        if(roots.Length==0)return [];
        var rows=await db.StorageFolders.AsNoTracking().Where(x=>x.SchoolId==school).Select(x=>new{x.Id,x.ParentFolderId,x.DriveItemId}).ToListAsync(ct);
        var scope=roots.ToHashSet();
        bool changed;do{changed=false;foreach(var row in rows)if(row.ParentFolderId.HasValue&&scope.Contains(row.ParentFolderId.Value))changed|=scope.Add(row.Id);}while(changed);
        if(scope.Count>5000)throw new StorageUnavailableException("السجل كبير؛ راجع المجلدات الفرعية ونفّذ الحذف على دفعات.");return scope.ToArray();
    }
    public async Task<IReadOnlyList<string>> ProtectionAsync(int school,IReadOnlyList<string> ids,CancellationToken ct)
    {
        var kinds=await db.StorageFolders.AsNoTracking().Where(x=>x.SchoolId==school&&ids.Contains(x.DriveItemId)).Select(x=>x.Kind).Distinct().ToListAsync(ct);
        var result=new List<string>();
        var currentRoot=await db.SchoolGoogleDrives.Where(x=>x.SchoolId==school).Select(x=>x.RootFolderId).SingleOrDefaultAsync(ct);
        if(await db.StoredFileVersions.AnyAsync(x=>x.SchoolId!=school&&ids.Contains(x.DriveItemId)&&
            db.SchoolGoogleDrives.Any(d=>d.SchoolId==x.SchoolId&&d.RootFolderId!=currentRoot),ct))result.Add("Foreign");
        if(kinds.Contains(StorageFolderKind.DutyArchive)||await db.Set<DutyArchiveIntent>().AnyAsync(x=>x.SchoolId==school&&ids.Contains(x.ProviderItemId!),ct))result.Add("Duty");
        if(kinds.Contains(StorageFolderKind.VisitArchive)||await db.Set<VisitArchiveOperation>().AnyAsync(x=>x.SchoolId==school&&ids.Contains(x.ProviderItemId!),ct)||
            await db.Set<VisitArchiveArtifact>().AnyAsync(x=>x.SchoolId==school&&db.StoredFileVersions.Any(v=>v.SchoolId==school&&v.Id==x.StoredFileVersionId&&ids.Contains(v.DriveItemId)),ct))result.Add("Visit");
        return result;
    }
    public Task<DriveFolderPlacement?> DestinationAsync(int school,string item,CancellationToken ct)=>
        db.StorageFolders.AsNoTracking().Where(x=>x.SchoolId==school&&x.IsActive&&x.DriveItemId==item).Select(x=>new DriveFolderPlacement(x.Id,x.OwnerTeacherId,x.Kind)).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyList<DriveFolderPlacement>> PlacementsAsync(int school,IReadOnlyList<string> items,CancellationToken ct)
    {
        var files=db.StoredFileVersions.Where(x=>x.SchoolId==school&&items.Contains(x.DriveItemId)).Select(x=>x.StoredFile.FolderId);
        return await db.StorageFolders.AsNoTracking().Where(x=>x.SchoolId==school&&(items.Contains(x.DriveItemId)||files.Contains(x.Id)))
            .Select(x=>new DriveFolderPlacement(x.Id,x.OwnerTeacherId,x.Kind)).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<DriveContentProof>> ProofsAsync(int school,IReadOnlyList<string> items,CancellationToken ct)=>
        await db.StoredFileVersions.IgnoreQueryFilters().AsNoTracking().Where(x=>x.SchoolId==school&&items.Contains(x.DriveItemId)&&x.SHA256!=null)
            .Select(x=>new DriveContentProof(x.DriveItemId,x.SHA256!,x.SizeInBytes,
                x.StoredFile.SourceKind==StoredFileSourceKind.DutyArchive||x.StoredFile.SourceKind==StoredFileSourceKind.VisitArchive)).ToListAsync(ct);
    public async Task<IReadOnlyList<DriveExpectedLocation>> LocationsAsync(int school,IReadOnlyList<string> items,string root,CancellationToken ct)
    {
        var folders=await db.StorageFolders.AsNoTracking().Where(x=>x.SchoolId==school&&items.Contains(x.DriveItemId)).ToListAsync(ct);
        var parentIds=folders.Where(x=>x.ParentFolderId!=null).Select(x=>x.ParentFolderId!.Value).Distinct().ToArray();
        var parents=await db.StorageFolders.AsNoTracking().Where(x=>x.SchoolId==school&&parentIds.Contains(x.Id)).Select(x=>new{x.Id,x.DriveItemId}).ToDictionaryAsync(x=>x.Id,x=>x.DriveItemId,ct);
        var result=folders.Select(x=>new DriveExpectedLocation(x.DriveItemId,x.DisplayName,x.ParentFolderId.HasValue?parents.GetValueOrDefault(x.ParentFolderId.Value):
            x.Kind is StorageFolderKind.SchoolLibrary or StorageFolderKind.DutyArchive or StorageFolderKind.VisitArchive?root:null)).ToList();
        result.AddRange(await db.StoredFileVersions.IgnoreQueryFilters().AsNoTracking().Where(x=>x.SchoolId==school&&items.Contains(x.DriveItemId)&&
            (x.StoredFile.SourceKind==StoredFileSourceKind.DutyArchive||x.StoredFile.SourceKind==StoredFileSourceKind.VisitArchive))
            .Select(x=>new DriveExpectedLocation(x.DriveItemId,x.DriveFileName,x.StoredFile.Folder.DriveItemId)).ToListAsync(ct));
        return result;
    }
    public async Task<IReadOnlyList<DriveMutationDto>> DeletedAsync(int school,CancellationToken ct)=>
        await db.Set<StorageOperation>().AsNoTracking().Where(x=>x.SchoolId==school&&x.Action=="DriveDelete"&&x.Status=="Completed"&&
            !db.Set<StorageOperation>().Any(r=>r.SchoolId==school&&r.RequestKey=="restore:"+x.Id&&r.Action=="DriveRestore"&&r.Status=="Completed"))
            .OrderByDescending(x=>x.Id).Take(100).Select(x=>new DriveMutationDto(x.Id,x.Status,"Delete",x.DisplayName,x.ErrorCode)).ToListAsync(ct);
    public async Task<DriveLedgerImpact> ImpactAsync(int school,IReadOnlyList<string> ids,CancellationToken ct)
    {
        var scope=await FolderScopeAsync(school,ids,ct);
        var folders=await db.StorageFolders.AsNoTracking().Where(x=>x.SchoolId==school&&scope.Contains(x.Id)).OrderBy(x=>x.Id).ToListAsync(ct);
        var versions=await db.StoredFileVersions.IgnoreQueryFilters().AsNoTracking().Where(x=>x.SchoolId==school&&(ids.Contains(x.DriveItemId)||scope.Contains(x.StoredFile.FolderId))).OrderBy(x=>x.Id).ToListAsync(ct);
        var fileIds=versions.Select(x=>x.StoredFileId).Distinct().ToArray();
        var files=await db.StoredFiles.IgnoreQueryFilters().AsNoTracking().Where(x=>x.SchoolId==school&&fileIds.Contains(x.Id)).OrderBy(x=>x.Id).ToListAsync(ct);
        var links=await db.EvidenceLinks.AsNoTracking().Where(x=>x.SchoolId==school&&fileIds.Contains(x.StoredFileId)&&x.IsActive).OrderBy(x=>x.Id)
            .Select(x=>new{x.Id,x.RowVersion,Name=x.Requirement.DisplayName}).ToListAsync(ct);
        var duty=await db.Set<DutyArchiveIntent>().AsNoTracking().Where(x=>x.SchoolId==school&&(ids.Contains(x.ProviderItemId!)||ids.Contains(x.FolderItemId!)||x.FolderId!=null&&scope.Contains(x.FolderId.Value))).OrderBy(x=>x.Id).ToListAsync(ct);
        var visits=await db.Set<VisitArchiveOperation>().AsNoTracking().Where(x=>x.SchoolId==school&&(ids.Contains(x.ProviderItemId!)||ids.Contains(x.ArchiveFolderItemId!)||x.ArchiveFolderId!=null&&scope.Contains(x.ArchiveFolderId.Value))).OrderBy(x=>x.Id).ToListAsync(ct);
        var related=versions.Select(x=>x.DriveItemId).Concat(folders.Select(x=>x.DriveItemId)).Concat(ids).Distinct().ToArray();
        var legacy=await db.TeacherEvidenceSubmissions.AsNoTracking().Where(x=>x.SchoolId==school&&(related.Contains(x.DriveItemId)||related.Contains(x.ParentItemId))&&!x.IsDeleted).OrderBy(x=>x.Id)
            .Select(x=>new{x.Id,x.UpdatedAtUtc,Name=x.Task==null?"ملف المعلم":x.Task.NameAr}).ToListAsync(ct);
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{
            Folders=folders.Select(x=>new{x.Id,x.RowVersion}),Versions=versions.Select(x=>new{x.Id,x.RowVersion}),
            Files=files.Select(x=>new{x.Id,x.RowVersion}),Links=links,Legacy=legacy,
            Duty=duty.Select(x=>new{x.Id,x.RowVersion}),Visits=visits.Select(x=>new{x.Id,x.RowVersion})}))));
        var fixedIdentity=folders.Any(x=>x.ParentFolderId==null||x.Kind is StorageFolderKind.DutyArchive or StorageFolderKind.VisitArchive)
            ||files.Any(x=>x.SourceKind is StoredFileSourceKind.DutyArchive or StoredFileSourceKind.VisitArchive or StoredFileSourceKind.HistoricalImport)
            ||links.Count>0||duty.Count>0||visits.Count>0;
        return new(fingerprint,links.Select(x=>x.Name).Concat(legacy.Select(x=>x.Name)).Distinct().Take(30).ToArray(),links.Count+legacy.Count,
            duty.Count+visits.Count,fixedIdentity,duty.Any(x=>x.State=="Delivering")||visits.Any(x=>x.Status==VisitArchiveStatus.Processing));
    }
    public async Task<DriveMutationOperation?> OperationAsync(int school,string actor,string key,CancellationToken ct) =>
        await ReadAsync(await db.Set<StorageOperation>().SingleOrDefaultAsync(x=>x.SchoolId==school&&x.ActorUserId==actor&&x.RequestKey==key&&x.Action.StartsWith("Drive"),ct),ct);
    public async Task<DriveMutationOperation?> OperationAsync(int school,int id,CancellationToken ct) =>
        await ReadAsync(await db.Set<StorageOperation>().SingleOrDefaultAsync(x=>x.SchoolId==school&&x.Id==id&&x.Action.StartsWith("Drive"),ct),ct);
    private async Task<DriveMutationOperation?> ReadAsync(StorageOperation? op,CancellationToken ct)
    {
        if(op==null)return null;
        var payload=await db.AuditLogs.AsNoTracking().Where(x=>x.SchoolId==op.SchoolId&&x.Action=="Storage.DriveMutationReserved"&&x.EntityId==op.Id.ToString()).Select(x=>x.NewValues).SingleAsync(ct);
        return new(op,JsonSerializer.Deserialize<DriveMutationPlan>(payload!)!);
    }
    public async Task<IReadOnlyList<DriveMutationDto>> PendingAsync(int school,CancellationToken ct) =>
        await db.Set<StorageOperation>().AsNoTracking().Where(x=>x.SchoolId==school&&x.Action.StartsWith("Drive")&&x.Status!="Completed")
        .OrderByDescending(x=>x.Id).Take(25).Select(x=>new DriveMutationDto(x.Id,x.Status,x.Action.Substring(4),x.DisplayName,x.ErrorCode)).ToListAsync(ct);
    public async Task<DriveMutationOperation> ReserveAsync(int school,string actor,string key,DriveMutationPlan plan,string fingerprint,string drive,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var op=new StorageOperation{SchoolId=school,ActorUserId=actor,RequestKey=key,Action="Drive"+plan.Action,Fingerprint=fingerprint,
            ProviderItemId=plan.Handle.Item,ParentItemId=plan.Parent,DriveId=drive,DisplayName=plan.Name??plan.OriginalName,Status="Pending"};
        db.Add(op);await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(new(){SchoolId=school,UserId=actor,Action="Storage.DriveMutationReserved",EntityName="StorageOperation",EntityId=op.Id.ToString(),NewValues=JsonSerializer.Serialize(plan)});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(op,plan);
    }
    public async Task CompleteAsync(DriveMutationOperation work,string actor,CancellationToken ct)
    {
        try{await CompleteCoreAsync(work,actor,ct);}catch(DbUpdateException){throw new StorageConflictException();}
    }
    private async Task CompleteCoreAsync(DriveMutationOperation work,string actor,CancellationToken ct)
    {
        var op=work.Operation;var plan=work.Plan;var school=op.SchoolId;var ids=plan.Items;
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        if((await ImpactAsync(school,ids,ct)).Fingerprint!=plan.LedgerFingerprint)throw new StorageConflictException();
        var scope=plan.Action=="Delete"?await FolderScopeAsync(school,ids,ct):[];
        var versions=await db.StoredFileVersions.IgnoreQueryFilters().Where(x=>x.SchoolId==school&&(ids.Contains(x.DriveItemId)||scope.Contains(x.StoredFile.FolderId))).ToListAsync(ct);
        var fileIds=versions.Select(x=>x.StoredFileId).ToArray();
        var files=await db.StoredFiles.IgnoreQueryFilters().Where(x=>x.SchoolId==school&&fileIds.Contains(x.Id)).ToListAsync(ct);
        var folders=await db.StorageFolders.Where(x=>x.SchoolId==school&&ids.Contains(x.DriveItemId)).ToListAsync(ct);
        if(plan.Action=="Delete")
        {
            foreach(var file in files){
                // Signed snapshots and ledger identities survive removal of their external copy.
                if(file.SourceKind is not (StoredFileSourceKind.DutyArchive or StoredFileSourceKind.VisitArchive)){
                    file.IsDeleted=true;file.DeletedAtUtc=DateTimeOffset.UtcNow;file.DeletedByUserId=actor;
                }
            }
            foreach(var version in versions){version.Availability=files.Any(x=>x.Id==version.StoredFileId&&x.SourceKind is StoredFileSourceKind.DutyArchive or StoredFileSourceKind.VisitArchive)?StoredFileAvailability.Missing:StoredFileAvailability.Deleted;version.MissingFromDriveAtUtc=DateTimeOffset.UtcNow;}
            var links=await db.EvidenceLinks.Where(x=>x.SchoolId==school&&fileIds.Contains(x.StoredFileId)&&x.IsActive).ToListAsync(ct);
            foreach(var link in links)link.IsActive=false;
            var related=versions.Select(x=>x.DriveItemId).Concat(ids).ToArray();
            var folderItems=await db.StorageFolders.Where(x=>x.SchoolId==school&&scope.Contains(x.Id)).Select(x=>x.DriveItemId).ToArrayAsync(ct);
            var legacy=await db.TeacherEvidenceSubmissions.Where(x=>x.SchoolId==school&&(related.Contains(x.DriveItemId)||folderItems.Contains(x.ParentItemId))&&!x.IsDeleted).ToListAsync(ct);
            foreach(var file in legacy)await legacyWriter.MarkDeletedAsync(file.TeacherId,file.Id,actor,ct);
            foreach(var intent in await db.Set<DutyArchiveIntent>().Where(x=>x.SchoolId==school&&(ids.Contains(x.ProviderItemId!)||ids.Contains(x.FolderItemId!)||x.FolderId!=null&&scope.Contains(x.FolderId.Value))).ToListAsync(ct))
            {intent.ErrorCode="DutyArchiveDeleted";intent.NextAttemptAt=null;intent.LeaseExpiresAt=null;}
            foreach(var intent in await db.Set<VisitArchiveOperation>().Where(x=>x.SchoolId==school&&(ids.Contains(x.ProviderItemId!)||ids.Contains(x.ArchiveFolderItemId!)||x.ArchiveFolderId!=null&&scope.Contains(x.ArchiveFolderId.Value))).ToListAsync(ct))
            {intent.LastErrorCode="DeletedByManager";intent.NextAttemptAtUtc=null;intent.LeaseExpiresAtUtc=null;}
        }
        else if(plan.Action=="Restore")
        {
            foreach(var file in files.Where(x=>x.IsDeleted)){file.IsDeleted=false;file.DeletedAtUtc=null;file.DeletedByUserId=null;file.NeedsLink=true;}
            foreach(var version in versions){version.Availability=StoredFileAvailability.Available;version.MissingFromDriveAtUtc=null;}
            // Restoring bytes does not reinstate previously withdrawn evidence approvals.
            foreach(var intent in await db.Set<DutyArchiveIntent>().Where(x=>x.SchoolId==school&&x.ErrorCode=="DutyArchiveDeleted"&&(ids.Contains(x.ProviderItemId!)||ids.Contains(x.FolderItemId!))).ToListAsync(ct))
            {intent.ErrorCode=null;intent.VerifiedAt=DateTimeOffset.UtcNow;}
            foreach(var intent in await db.Set<VisitArchiveOperation>().Where(x=>x.SchoolId==school&&x.LastErrorCode=="DeletedByManager"&&(ids.Contains(x.ProviderItemId!)||ids.Contains(x.ArchiveFolderItemId!))).ToListAsync(ct))
            {intent.LastErrorCode=null;}
        }
        else if(plan.Action=="Rename")
        {
            foreach(var file in files)file.DisplayName=plan.Name!;
            foreach(var folder in folders)folder.DisplayName=plan.Name!;
            foreach(var legacy in await db.TeacherEvidenceSubmissions.Where(x=>x.SchoolId==school&&x.DriveItemId==op.ProviderItemId).ToListAsync(ct))legacy.FileName=plan.Name!;
        }
        else if(plan.Action=="Move")
        {
            var dest=await db.StorageFolders.SingleOrDefaultAsync(x=>x.SchoolId==school&&x.IsActive&&x.DriveItemId==plan.Destination,ct);
            if((files.Count>0||folders.Count>0)&&dest==null)throw new StorageConflictException();
            foreach(var file in files)file.FolderId=dest!.Id;
            foreach(var folder in folders)folder.ParentFolderId=dest!.Id;
            foreach(var legacy in await db.TeacherEvidenceSubmissions.Where(x=>x.SchoolId==school&&x.DriveItemId==op.ProviderItemId).ToListAsync(ct))legacy.ParentItemId=plan.Destination!;
        }
        op.Status="Completed";op.ErrorCode=null;op.UpdatedAtUtc=DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new(){SchoolId=school,UserId=actor,Action="Storage.Drive"+plan.Action+"Completed",EntityName="StorageOperation",EntityId=op.Id.ToString(),
            NewValues=JsonSerializer.Serialize(new{Files=fileIds,Items=ids,LinkedEvidenceWithdrawn=plan.Action=="Delete"}),Reason="Confirmed in storage; retained history and signatures preserved."});
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}
        catch(DbUpdateException){throw new StorageConflictException();}
    }
    public async Task UncertainAsync(StorageOperation op,string code,CancellationToken ct)
    {
        // A SQL rollback must not leave tracked mutated entities for a subsequent save.
        db.ChangeTracker.Clear();var current=await db.Set<StorageOperation>().SingleAsync(x=>x.SchoolId==op.SchoolId&&x.Id==op.Id,ct);
        current.Status="NeedsAttention";current.ErrorCode=code;current.UpdatedAtUtc=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);
        op.Status=current.Status;op.ErrorCode=code;
    }
    public async Task ExclusiveAsync(int school,Func<Task> action,CancellationToken ct)
    {
        await using var connection=new SqlConnection(db.Database.GetConnectionString());await connection.OpenAsync(ct);
        var resource="AlFalah:DriveBrowser:"+school;
        await using var cmd=connection.CreateCommand();cmd.CommandText="DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=15000; SELECT @r;";
        cmd.Parameters.AddWithValue("@key",resource);if(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))<0)throw new StorageConflictException();
        try{await action();}finally{cmd.CommandText="EXEC sys.sp_releaseapplock @Resource=@key,@LockOwner='Session';";await cmd.ExecuteNonQueryAsync(CancellationToken.None);}
    }
}
