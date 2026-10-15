using System.Data;
using System.Linq.Expressions;
using System.Text.Json;
using AlFalah.Application.Duty;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class DutyDocumentRepository(AlFalahDbContext db) : IDutyDocumentRepository
{
    public async Task<DutyArchiveSlice> ArchiveAsync(int school,DutyArchiveQuery query,DateOnly today,CancellationToken ct)
    {
        var current=db.Set<DutyAssignment>().AsNoTracking().Where(x=>x.SchoolId==school&&!x.IsCancelled)
            .Select(x=>new{AssignmentId=x.Id,AuthorId=x.EffectiveTeacherId,Revision=x.Revision});
        var historical=db.Set<DutySignedDocument>().AsNoTracking().Where(x=>x.SchoolId==school)
            .Select(x=>new{AssignmentId=x.AssignmentId,AuthorId=x.AuthorUserId,Revision=x.AssignmentRevision});
        var all=from g in current.Union(historical)
                join a in db.Set<DutyAssignment>().AsNoTracking() on g.AssignmentId equals a.Id
                join p in db.Set<DutyPlan>().AsNoTracking() on a.DutyPlanId equals p.Id
                join u in db.Users.IgnoreQueryFilters().AsNoTracking() on g.AuthorId equals u.Id
                where a.SchoolId==school&&p.SchoolId==school&&(!query.TermId.HasValue||p.AcademicTermId==query.TermId)
                select new{g.AssignmentId,g.AuthorId,g.Revision,a.Date,Name=u.FirstName+" "+u.LastName,IsCurrent=a.EffectiveTeacherId==g.AuthorId&&a.Revision==g.Revision&&!a.IsCancelled,
                    DocumentCount=db.Set<DutySignedDocument>().Count(d=>d.SchoolId==school&&d.AssignmentId==a.Id)};
        if(query.TeacherId is not null)all=all.Where(x=>x.AuthorId==query.TeacherId);
        if(query.From is not null)all=all.Where(x=>x.Date>=query.From);
        if(query.To is not null)all=all.Where(x=>x.Date<=query.To);
        else all=all.Where(x=>x.Date<=today||x.DocumentCount>0);
        var total=await all.CountAsync(ct);
        var page=await all.OrderByDescending(x=>x.Date).ThenBy(x=>x.AssignmentId).ThenBy(x=>x.AuthorId).ThenByDescending(x=>x.Revision)
            .Skip((query.Page-1)*10).Take(10).ToListAsync(ct);
        var parameter=Expression.Parameter(typeof(DutySignedDocument),"document");Expression matches=Expression.Constant(false);
        foreach(var row in page)
        {
            var key=Expression.AndAlso(Expression.Equal(Expression.Property(parameter,nameof(DutySignedDocument.AssignmentId)),Expression.Constant(row.AssignmentId)),
                Expression.AndAlso(Expression.Equal(Expression.Property(parameter,nameof(DutySignedDocument.AuthorUserId)),Expression.Constant(row.AuthorId)),
                    Expression.Equal(Expression.Property(parameter,nameof(DutySignedDocument.AssignmentRevision)),Expression.Constant(row.Revision))));
            matches=Expression.OrElse(matches,key);
        }
        var docs=await db.Set<DutySignedDocument>().AsNoTracking().Where(x=>x.SchoolId==school)
            .Where(Expression.Lambda<Func<DutySignedDocument,bool>>(matches,parameter))
            .Where(x=>!db.Set<DutySignedDocument>().Any(later=>later.SchoolId==school&&later.AssignmentId==x.AssignmentId&&later.AuthorUserId==x.AuthorUserId&&later.AssignmentRevision==x.AssignmentRevision&&later.Kind==x.Kind&&later.Id>x.Id))
            .OrderByDescending(x=>x.Id).ToListAsync(ct);
        var ids=docs.Select(x=>x.Id).ToArray();
        return new(total,page.Select(x=>new DutyArchiveGroup(x.AssignmentId,x.AuthorId,x.Revision,x.Date,x.Name,x.IsCurrent,x.DocumentCount)).ToArray(),docs,
            await Intents.AsNoTracking().Where(x=>x.SchoolId==school&&ids.Contains(x.DocumentId)).ToListAsync(ct),
            await db.Set<DutyPdfArtifact>().AsNoTracking().Where(x=>x.SchoolId==school&&ids.Contains(x.DocumentId??0)).ToListAsync(ct));
    }
    private IQueryable<DutyArchiveIntent> Intents => db.Set<DutyArchiveIntent>();
    public async Task<DutyArchiveReadiness> ReadinessAsync(int school,CancellationToken ct)
    {
        var d=await db.SchoolGoogleDrives.AsNoTracking().SingleOrDefaultAsync(x=>x.SchoolId==school,ct);
        return new(d?.DutyArchiveEnabled??false,false,d is null ? "NotConfigured" : !d.IsEnabled || string.IsNullOrWhiteSpace(d.RootFolderId) || string.IsNullOrWhiteSpace(d.ProtectedCredential) ? "Disconnected" : "Connected");
    }
    public async Task SetEnabledAsync(int school,bool enabled,string actor,CancellationToken ct)
    {
        var d=await db.SchoolGoogleDrives.SingleOrDefaultAsync(x=>x.SchoolId==school,ct) ?? throw new DutyException("DutyArchiveSetup","جهّز اتصال المدرسة أولًا. / Set up school connectivity first.",409);
        if(d.DutyArchiveEnabled==enabled)return;
        d.DutyArchiveEnabled=enabled; Audit(school,"Activation",actor,enabled.ToString()); await db.SaveChangesAsync(ct);
    }
    public Task<DutyPdfArtifact?> ArtifactAsync(int school,int document,CancellationToken ct) => db.Set<DutyPdfArtifact>().AsNoTracking().SingleOrDefaultAsync(x=>x.SchoolId==school&&x.DocumentId==document,ct);
    public Task<DutyPdfArtifact?> RosterArtifactAsync(int school,int plan,int revision,CancellationToken ct) => db.Set<DutyPdfArtifact>().AsNoTracking().SingleOrDefaultAsync(x=>x.SchoolId==school&&x.PlanId==plan&&x.PlanRevision==revision,ct);
    public async Task AddArtifactAsync(DutyPdfArtifact artifact,CancellationToken ct)
    {
        DutyDocumentIntegrity.Validate(artifact); db.Add(artifact); await db.SaveChangesAsync(ct);
        if (artifact.DocumentId is { } document)
        {
            var i=await Intents.SingleAsync(x=>x.SchoolId==artifact.SchoolId&&x.DocumentId==document,ct);
            i.ArtifactId=artifact.Id; await db.SaveChangesAsync(ct);
        }
    }
    public Task<DutyArchiveIntent?> IntentAsync(int school,int document,CancellationToken ct) => Intents.AsTracking().SingleOrDefaultAsync(x=>x.SchoolId==school&&x.DocumentId==document,ct);
    public Task<List<DutyArchiveIntent>> IntentsAsync(int school,int assignment,CancellationToken ct) => Intents.AsTracking().Where(x=>x.SchoolId==school&&db.Set<DutySignedDocument>().Any(d=>d.Id==x.DocumentId&&d.AssignmentId==assignment)).ToListAsync(ct);
    public async Task<List<(int School,int Document)>> DueAsync(DateTimeOffset now,CancellationToken ct)
    {
        var rows=await Intents.AsNoTracking().Where(x=>x.ErrorCode!="DutyArchiveDeleted"&&
            ((x.State=="PendingD3"||x.State=="Pending"||x.State=="RetryScheduled")&&(x.NextAttemptAt==null||x.NextAttemptAt<=now)||x.State=="Delivering"&&x.LeaseExpiresAt<=now)
            && (x.ArtifactId==null || db.SchoolGoogleDrives.Any(d=>d.SchoolId==x.SchoolId&&d.IsEnabled&&d.DutyArchiveEnabled)))
            .OrderBy(x=>x.Id).Take(25).Select(x=>new{x.SchoolId,x.DocumentId}).ToListAsync(ct);
        return rows.Select(x=>(x.SchoolId,x.DocumentId)).ToList();
    }
    public async Task<List<(int School,int Document)>> ReconcileDueAsync(CancellationToken ct)
    {
        var rows=await Intents.AsNoTracking().Where(x=>x.State=="Archived"&&x.ErrorCode!="DutyArchiveDeleted"&&(x.VerifiedAt==null||x.VerifiedAt<DateTimeOffset.UtcNow.AddMinutes(-15)))
            .OrderBy(x=>x.VerifiedAt).Take(25).Select(x=>new{x.SchoolId,x.DocumentId}).ToListAsync(ct);
        return rows.Select(x=>(x.SchoolId,x.DocumentId)).ToList();
    }
    // Session lock remains held throughout external I/O; a reserved provider ID remains the fence after connection loss.
    public async Task<bool> ExclusiveAsync(string resource,Func<CancellationToken,Task> action,CancellationToken ct)
    {
        if(!db.Database.IsSqlServer()) { await action(ct); return true; }
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd=db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText="DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r;";
            var parameter=cmd.CreateParameter();parameter.ParameterName="@resource";parameter.Value="AlFalah:DutyArchive:"+resource;cmd.Parameters.Add(parameter);
            if(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))<0)return false;
            try { await action(ct);return true; }
            finally {cmd.CommandText="EXEC sys.sp_releaseapplock @Resource=@resource,@LockOwner='Session';";if(cmd.Connection?.State==ConnectionState.Open)await cmd.ExecuteNonQueryAsync(CancellationToken.None);}
        }
        finally {await db.Database.CloseConnectionAsync();}
    }
    public async Task<DutyArchiveIntent?> ClaimAsync(int school,int document,Guid token,DateTimeOffset now,CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var changed=await Intents.Where(x=>x.SchoolId==school&&x.DocumentId==document&&x.ErrorCode!="DutyArchiveDeleted"&&
            ((x.State=="PendingD3"||x.State=="Pending"||x.State=="RetryScheduled")&&(x.NextAttemptAt==null||x.NextAttemptAt<=now)||x.State=="Delivering"&&x.LeaseExpiresAt<=now))
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Delivering").SetProperty(x=>x.LeaseToken,token).SetProperty(x=>x.LeaseExpiresAt,now.AddMinutes(2))
                .SetProperty(x=>x.LastAttemptAt,now).SetProperty(x=>x.Attempts,x=>x.Attempts+1),ct);
        return changed==1 ? await IntentAsync(school,document,ct) : null;
    }
    public async Task RenewAsync(int id,Guid token,DateTimeOffset now,CancellationToken ct)
    {
        await using var connection=new SqlConnection(db.Database.GetConnectionString());await connection.OpenAsync(ct);
        await using var cmd=connection.CreateCommand();
        cmd.CommandText="UPDATE DutyArchiveIntents SET LeaseExpiresAt=@expiry WHERE Id=@id AND LeaseToken=@token AND State='Delivering'";
        cmd.Parameters.AddWithValue("@expiry",now.AddMinutes(2));cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@token",token);
        if(await cmd.ExecuteNonQueryAsync(ct)!=1)throw new StorageConflictException();
    }
    public async Task SaveAsync(DutyArchiveIntent i,CancellationToken ct)
    {
        var current=await Intents.AsNoTracking().Where(x=>x.Id==i.Id).Select(x=>new{x.RowVersion,x.LeaseToken}).SingleAsync(ct);
        if(current.LeaseToken!=i.LeaseToken)throw new StorageConflictException();
        db.Entry(i).Property(x=>x.RowVersion).OriginalValue=current.RowVersion;await db.SaveChangesAsync(ct);
    }
    public async Task FailAsync(int school,int document,Guid token,string code,DateTimeOffset now,CancellationToken ct)
    {
        db.ChangeTracker.Clear();var i=await IntentAsync(school,document,ct);if(i is null||i.LeaseToken!=token)return;
        i.ErrorCode=code;i.State=i.Attempts>=5||code is "DutyArtifactUnavailable" or "DutyArchiveIdentity" or "DutyArchiveMoved" ? "NeedsAttention" : "RetryScheduled";
        i.NextAttemptAt=i.State=="RetryScheduled" ? now.AddSeconds(Math.Min(3600,30*Math.Pow(2,i.Attempts-1))) : null;i.LeaseExpiresAt=null;
        Audit(school,"Failed",null,$"{document}:{code}");await db.SaveChangesAsync(ct);
    }
    public async Task CompleteAsync(DutyArchiveIntent i,DutyPdfArtifact artifact,bool real,CancellationToken ct)
    {
        // Shared file/version pointer is serialized per school, including delayed older corrections.
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"AlFalah:DutyLedger:"+i.SchoolId},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Duty ledger lock timeout',1;",ct);
        var current=await Intents.AsNoTracking().SingleAsync(x=>x.Id==i.Id,ct);if(current.LeaseToken!=i.LeaseToken)throw new StorageConflictException();
        var doc=await db.Set<DutySignedDocument>().AsNoTracking().SingleAsync(x=>x.Id==i.DocumentId&&x.SchoolId==i.SchoolId,ct);
        var identity=$"{i.FolderId}:{doc.Kind}";
        var file=await db.StoredFiles.IgnoreQueryFilters().AsTracking().SingleOrDefaultAsync(x=>x.SchoolId==i.SchoolId&&x.DutyIdentity==identity,ct);
        if(file is null){file=new(){SchoolId=i.SchoolId,FolderId=i.FolderId!.Value,SourceKind=StoredFileSourceKind.DutyArchive,DutyIdentity=identity,DisplayName=artifact.FileName,NeedsLink=false};db.Add(file);await db.SaveChangesAsync(ct);}
        var version=await db.StoredFileVersions.SingleOrDefaultAsync(x=>x.SchoolId==i.SchoolId&&x.DriveItemId==i.ProviderItemId,ct);
        if(version is null)
        {
            var n=(await db.StoredFileVersions.Where(x=>x.StoredFileId==file.Id).Select(x=>(int?)x.VersionNumber).MaxAsync(ct)??0)+1;
            version=new(){SchoolId=i.SchoolId,StoredFileId=file.Id,VersionNumber=n,DriveId=i.DriveId!,DriveItemId=i.ProviderItemId!,DriveFileName=artifact.FileName,
                FileExtension=".pdf",SHA256=artifact.Hash,SizeInBytes=artifact.Bytes.LongLength,MimeType="application/pdf",UploadedByUserId=doc.AuthorUserId,UploadedAtUtc=DateTimeOffset.UtcNow,Availability=StoredFileAvailability.Available};
            db.Add(version);await db.SaveChangesAsync(ct);
        }
        var latest=await Intents.Where(x=>x.SchoolId==i.SchoolId&&x.FolderId==i.FolderId&&x.StoredFileVersionId!=null)
            .Join(db.Set<DutySignedDocument>(),x=>x.DocumentId,d=>d.Id,(x,d)=>new{x.StoredFileVersionId,d.Id,d.Kind}).Where(x=>x.Kind==doc.Kind).OrderByDescending(x=>x.Id).FirstOrDefaultAsync(ct);
        if(latest is null||doc.Id>=latest.Id)file.CurrentVersionId=version.Id;
        i.StoredFileVersionId=version.Id;i.State=real?"Archived":"TestVerified";i.VerifiedAt=DateTimeOffset.UtcNow;i.ErrorCode=null;i.NextAttemptAt=null;i.LeaseExpiresAt=null;
        Audit(i.SchoolId,real?"Archived":"TestVerified",null,$"{doc.Id}:{artifact.Hash}");await SaveAsync(i,ct);await tx.CommitAsync(ct);
    }
    public async Task RetryAsync(int school,int document,string actor,CancellationToken ct)
    {
        if(!await ExclusiveAsync($"operation:{school}:{document}",async token=>
        {
            db.ChangeTracker.Clear();var i=await IntentAsync(school,document,token)??throw DutyDocumentIntegrity.Error();
            if(i.State is not ("RetryScheduled" or "NeedsAttention"))throw new DutyException("DutyArchiveConflict","تغيّرت حالة الأرشيف. حدّث البيانات. / Archive state changed. Refresh.",409);
            i.State="Pending";i.NextAttemptAt=null;i.Attempts=0;i.LeaseToken=null;i.LeaseExpiresAt=null;i.ErrorCode=null;
            Audit(school,"Retry",actor,document.ToString());await db.SaveChangesAsync(token);
        },ct))throw new DutyException("DutyArchiveConflict","الأرشفة جارية بالفعل. / Archive delivery is already running.",409);
    }
    public async Task<DutyArchiveScope> ScopeAsync(int school,int assignment,CancellationToken ct) => await (
        from a in db.Set<DutyAssignment>().AsNoTracking() join p in db.Set<DutyPlan>() on a.DutyPlanId equals p.Id
        join term in db.AcademicTerms on p.AcademicTermId equals term.Id where a.SchoolId==school&&a.Id==assignment
        select new DutyArchiveScope(term.AcademicYearId,term.Id)).SingleAsync(ct);
    public Task<StorageFolder?> FolderAsync(int school,string identity,CancellationToken ct) => db.StorageFolders.AsTracking().SingleOrDefaultAsync(x=>x.SchoolId==school&&x.DutyIdentity==identity,ct);
    public Task<StorageFolder?> FolderByIdAsync(int school,int id,CancellationToken ct) => db.StorageFolders.AsNoTracking().SingleOrDefaultAsync(x=>x.SchoolId==school&&x.Id==id&&x.Kind==StorageFolderKind.DutyArchive,ct);
    public async Task SaveFolderAsync(StorageFolder folder,CancellationToken ct){if(folder.Id==0)db.Add(folder);await db.SaveChangesAsync(ct);}
    public async Task<string> TeacherFolderNameAsync(int school,string user,string name,CancellationToken ct)
    {
        var duplicates=await db.InstructorProfiles.AsNoTracking().CountAsync(x=>x.SchoolId==school&&(x.User.FirstName+" "+x.User.LastName)==name,ct);
        var used=await db.StorageFolders.AnyAsync(x=>x.SchoolId==school&&x.Kind==StorageFolderKind.DutyArchive&&x.DisplayName==DutyDocumentIntegrity.SafeName(name)&&!x.DutyIdentity!.EndsWith("/teacher:"+user),ct);
        var safe=DutyDocumentIntegrity.SafeName(name);if(duplicates<=1&&!used)return safe;
        var code=await db.InstructorProfiles.IgnoreQueryFilters().Where(x=>x.UserId==user).Select(x=>(int?)x.Id).FirstOrDefaultAsync(ct);
        return $"{safe} (معلم {code?.ToString()??DutyDocumentIntegrity.Hash(user)[..6]})";
    }
    public async Task MarkAvailabilityAsync(DutyArchiveIntent i,string? error,DateTimeOffset at,CancellationToken ct)
    {
        if(i.ErrorCode=="DutyArchiveDeleted")return; // Explicit removal is not unexplained drift or an automatic upload retry.
        i.ErrorCode=error;i.VerifiedAt=at;
        if(i.StoredFileVersionId is { } id&&error!="ConnectionUnavailable")
        {
            var v=await db.StoredFileVersions.SingleAsync(x=>x.SchoolId==i.SchoolId&&x.Id==id,ct);v.Availability=error is null?StoredFileAvailability.Available:StoredFileAvailability.Missing;
            v.MissingFromDriveAtUtc=error is null?null:at;
        }
        await db.SaveChangesAsync(ct);
    }
    private void Audit(int school,string action,string? actor,string reason)=>db.AuditLogs.Add(new(){SchoolId=school,UserId=actor,Action="DutyArchive."+action,EntityName="DutyArchiveIntent",Reason=reason});
}
