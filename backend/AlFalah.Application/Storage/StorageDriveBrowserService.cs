using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.Common;
using AlFalah.Application.Duty;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Storage;

public sealed class StorageDriveBrowserService(IStorageDriveBrowserRepository repository,IStorageRepository scopes,
    IStorageAuthorizationService auth,ICurrentUserService user,IStorageProvider provider,IStorageDriveHandles handles,
    ISchoolDriveSetupRepository setup,IDutyRepository duty,IOptions<StorageOptions> options) : IStorageDriveBrowserService
{
    private int School=>user.ActiveSchoolId??throw Denied();
    private string Actor=>user.UserId??throw Denied();
    private static UnauthorizedSchoolAccessException Denied()=>new("لا تملك صلاحية الوصول إلى ملفات هذه المدرسة.");
    private async Task<StorageDriveRoot> RootAsync(bool mutation,CancellationToken ct)
    {
        if(!options.Value.ReadModelEnabled||mutation&&!options.Value.AdministrationEnabled)throw new StorageUnavailableException();
        await auth.RequireSchoolPermissionAsync(School,mutation?PermissionNames.StorageManageSchool:PermissionNames.StorageViewSchool,ct);
        if(mutation&&(await auth.RequireScopeAsync(School,ct)).ManagerUserId!=Actor)throw Denied();
        var root=await scopes.GetSchoolDriveRootAsync(School,ct)??throw new StorageUnavailableException();
        provider.ResetRequestCache();
        if(await provider.MetadataAsync(School,root.RootItemId,ct) is not {IsFolder:true,Trashed:false} meta||
            (meta.SharedDriveId??"")!=root.DriveId)throw new StorageUnavailableException();
        return root;
    }
    private DriveBrowserHandle Handle(StorageDriveRoot root,string id)=>new(School,root.RootItemId,id,Actor,DateTimeOffset.UtcNow.AddHours(12));
    private void Validate(DriveBrowserHandle handle,StorageDriveRoot root,bool expiry=true,bool sameActor=true)
    {
        if(handle.School!=School||sameActor&&handle.Actor!=Actor||handle.Root!=root.RootItemId||expiry&&handle.Expires<DateTimeOffset.UtcNow)throw Denied();
    }
    private async Task<GoogleDriveFile> ItemAsync(string id,StorageDriveRoot root,CancellationToken ct,bool allowTrashed=false)
    {
        var item=await provider.MetadataAsync(School,id,ct)??throw new KeyNotFoundException("الملف غير متاح؛ حدّث المجلد.");
        if((item.SharedDriveId??"")!=root.DriveId||item.Trashed&&!allowTrashed)throw new KeyNotFoundException("الملف محذوف أو غير متاح.");
        // Even a replay of a confirmed trash operation verifies its current live parent boundary.
        var boundary=item.Trashed&&allowTrashed?item.Parents.SingleOrDefault():id;
        if(boundary==null||!await provider.IsWithinAsync(School,root.RootItemId,boundary,ct))throw Denied();
        foreach(var foreign in (await setup.BoundariesAsync(School,ct)).Where(x=>x.OtherSchool))
            if(await provider.IsWithinAsync(School,foreign.ItemId,boundary,ct))throw Denied();
        return item;
    }
    private async Task<string?> BranchAsync(GoogleDriveFile item,StorageDriveRoot root,CancellationToken ct)
    {
        var current=item;var seen=new HashSet<string>();var dutyBranch=false;var visitBranch=false;
        while(current.Id!=root.RootItemId&&seen.Add(current.Id)&&seen.Count<=64)
        {
            dutyBranch|=current.Name=="المناوبات"&&current.Parents.Contains(root.RootItemId);visitBranch|=current.Name=="أرشيف الزيارات"&&current.Parents.Contains(root.RootItemId);
            if(current.Parents.Count!=1)throw Denied();
            current=await provider.MetadataAsync(School,current.Parents[0],ct)??throw Denied();
        }
        if(current.Id!=root.RootItemId)throw Denied();
        var protection=await repository.ProtectionAsync(School,seen.ToArray(),ct);
        if(protection.Contains("Foreign"))throw Denied();
        // The actual school manager browses their storage independently of feature enablement.
        // A storage delegate still needs each protected document policy; storage rights alone are insufficient.
        if((await auth.RequireScopeAsync(School,ct)).ManagerUserId!=Actor){
            if((dutyBranch||protection.Contains("Duty"))&&(await duty.AccessAsync(School,Actor,ct))?.Manager!=true)throw Denied();
            if(visitBranch||protection.Contains("Visit")){
                await auth.RequireSchoolPermissionAsync(School,PermissionNames.StorageViewArchive,ct);
                if(!await scopes.HasPermissionAsync(Actor,School,PermissionNames.VisitView,ct))throw Denied();
            }
        }
        return dutyBranch||visitBranch||protection.Contains("Duty")||protection.Contains("Visit")?"ProtectedArchive":null;
    }
    public async Task<DriveBrowserPage> BrowseAsync(string? handle,string? pageToken,CancellationToken ct)
    {
        if(pageToken?.Length>2048)throw new ArgumentException("طلب الصفحة غير صالح.");
        var root=await RootAsync(false,ct);var access=await auth.AccessAsync(School,false,false,ct);
        var actualManager=(await auth.RequireScopeAsync(School,ct)).ManagerUserId==Actor;
        if(handle==null)return new("ملفات المدرسة",null,[],[new(handles.ProtectHandle(Handle(root,root.RootItemId)),"ملفات المدرسة",true,null,GoogleDriveFile.FolderMimeType,null,false,"SchoolRoot")],null,access.CanManage&&actualManager);
        var decoded=handles.ReadHandle(handle);Validate(decoded,root);
        var folder=await ItemAsync(decoded.Item,root,ct);if(!folder.IsFolder)throw new ArgumentException("اختر مجلدًا.");
        await BranchAsync(folder,root,ct);
        var page=await provider.ChildrenAsync(School,folder.Id,root.DriveId,pageToken,ct);
        var items=new List<DriveBrowserItem>();
        foreach(var item in page.Files.Where(x=>!x.Trashed&&x.Parents.Count==1&&x.Parents[0]==folder.Id&&(x.SharedDriveId??"")==root.DriveId))
        {
            string? protection;
            try{await ItemAsync(item.Id,root,ct);protection=await BranchAsync(item,root,ct);}catch(UnauthorizedSchoolAccessException){continue;}
            if(folder.Id==root.RootItemId&&item.Name is "مكتبة المدرسة" or "ملفات المعلمين" or "المناوبات" or "أرشيف الزيارات")protection="SystemFolder";
            // Mutation eligibility is re-evaluated for the exact action and linked descendants at confirmation.
            items.Add(new(handles.ProtectHandle(Handle(root,item.Id)),item.Name,item.IsFolder,item.Size,item.MimeType,item.ModifiedTime,
                access.CanManage&&actualManager,protection));
        }
        var crumbs=new List<DriveBrowserCrumb>();var node=folder;var seen=new HashSet<string>();
        while(seen.Add(node.Id)&&seen.Count<=64){crumbs.Insert(0,new(handles.ProtectHandle(Handle(root,node.Id)),node.Id==root.RootItemId?"ملفات المدرسة":node.Name));
            if(node.Id==root.RootItemId)break;node=await provider.MetadataAsync(School,node.Parents.Single(),ct)??throw Denied();}
        await auth.RequireSchoolPermissionAsync(School,PermissionNames.StorageViewSchool,ct);
        return new(folder.Id==root.RootItemId?"ملفات المدرسة":folder.Name,handle,crumbs,items,page.NextPageToken,access.CanManage&&actualManager);
    }
    public async Task<DriveFileContentDto> ContentAsync(string handle,CancellationToken ct)
    {
        var root=await RootAsync(false,ct);var decoded=handles.ReadHandle(handle);Validate(decoded,root);
        var item=await ItemAsync(decoded.Item,root,ct);await BranchAsync(item,root,ct);
        if(item.IsFolder)throw new ArgumentException("اختر ملفًا للتنزيل.");
        await auth.RequireSchoolPermissionAsync(School,PermissionNames.StorageViewSchool,ct);
        var proof=(await repository.ProofsAsync(School,[item.Id],ct)).SingleOrDefault(x=>x.Signed);
        var content=await provider.ContentAsync(School,item.Id,ct);
        if(proof==null)return content;
        // Verify managed signed bytes before sending any response. A temporary stream keeps
        // memory bounded and deletes its private staging file when the HTTP response ends.
        var verified=new FileStream(Path.Combine(Path.GetTempPath(),"alfalah-signed-"+Guid.NewGuid().ToString("N")),FileMode.CreateNew,
            FileAccess.ReadWrite,FileShare.None,65536,FileOptions.Asynchronous|FileOptions.SequentialScan|FileOptions.DeleteOnClose);
        try{
            await using var source=content.Content;using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer=new byte[65536];long length=0;int read;
            while((read=await source.ReadAsync(buffer,ct))>0){length+=read;if(length>proof.Length)throw new StorageUnavailableException("نسخة Drive لا تطابق المستند المحفوظ؛ راجع مدير المدرسة.");hash.AppendData(buffer,0,read);await verified.WriteAsync(buffer.AsMemory(0,read),ct);}
            if(length!=proof.Length||!string.Equals(Convert.ToHexString(hash.GetHashAndReset()),proof.Hash,StringComparison.OrdinalIgnoreCase))throw new StorageUnavailableException("نسخة Drive لا تطابق المستند المحفوظ؛ راجع مدير المدرسة.");
            verified.Position=0;return new(verified,content.FileName,content.ContentType,proof.Length);
        }catch{await verified.DisposeAsync();throw;}
    }
    private async Task<IReadOnlyList<GoogleDriveFile>> TreeAsync(GoogleDriveFile item,StorageDriveRoot root,CancellationToken ct)
    {
        var result=new List<GoogleDriveFile>{item};var pending=new Queue<GoogleDriveFile>();if(item.IsFolder)pending.Enqueue(item);
        var seen=new HashSet<string>{item.Id};
        while(pending.Count>0){var folder=pending.Dequeue();string? token=null;
            do{var page=await provider.ChildrenAsync(School,folder.Id,root.DriveId,token,ct);token=page.NextPageToken;
                foreach(var child in page.Files.Where(x=>!x.Trashed&&x.Parents.Count==1&&x.Parents[0]==folder.Id)){
                    await ItemAsync(child.Id,root,ct);await BranchAsync(child,root,ct);
                    if(!seen.Add(child.Id))throw new StorageConflictException();result.Add(child);if(child.IsFolder)pending.Enqueue(child);
                    if(result.Count>1000)throw new BusinessRuleException("المجلد كبير؛ احذف محتوياته على دفعات من المجلدات الفرعية.");
                }
            }while(token!=null);
        }
        return result;
    }
    public async Task<DriveImpactDto> ImpactAsync(DriveImpactRequest request,CancellationToken ct)
    {
        if(request.Action is not ("Delete" or "Rename" or "Move"))throw new ArgumentException("الإجراء غير صالح.");
        var root=await RootAsync(true,ct);var handle=handles.ReadHandle(request.Handle);Validate(handle,root);
        if(handle.Item==root.RootItemId)throw new BusinessRuleException("جذر المدرسة محمي؛ افتح المجلد لإدارة محتوياته.");
        var item=await ItemAsync(handle.Item,root,ct);var branch=await BranchAsync(item,root,ct);
        var tree=await TreeAsync(item,root,ct);var ids=tree.Select(x=>x.Id).Order(StringComparer.Ordinal).ToArray();
        var impact=await repository.ImpactAsync(School,ids,ct);
        if(impact.Busy)throw new BusinessRuleException("هناك رفع جارٍ لهذه الملفات؛ انتظر اكتماله ثم أعد المحاولة.");
        var systemFolder=item.IsFolder&&item.Parents.Contains(root.RootItemId)&&item.Name is "مكتبة المدرسة" or "ملفات المعلمين" or "المناوبات" or "أرشيف الزيارات";
        if(request.Action!="Delete"&&(impact.FixedIdentity||branch!=null||systemFolder))throw new BusinessRuleException("اسم ومكان هذا الأصل مرتبطان بسجل محفوظ؛ يمكنك تصفحه أو حذفه بعد مراجعة الأثر.");
        string? name=null,destination=null;
        if(request.Action=="Rename"){
            name=ValidatedStorageUpload.SafeName(request.Name??"");
            if(!item.IsFolder&&!string.Equals(Path.GetExtension(name),Path.GetExtension(item.Name),StringComparison.OrdinalIgnoreCase))throw new ArgumentException("احتفظ بامتداد الملف عند تعديل الاسم.");
        }
        if(request.Action=="Move"){
            var dest=handles.ReadHandle(request.Destination??"");Validate(dest,root);var meta=await ItemAsync(dest.Item,root,ct);
            if(!meta.IsFolder||ids.Contains(meta.Id))throw new ArgumentException("اختر مجلدًا آخر خارج المجلد المنقول.");
            if(await BranchAsync(meta,root,ct)!=null)throw new BusinessRuleException("مجلدات الأرشيف مخصصة للمستندات الآلية.");destination=meta.Id;
            if(meta.Name is "المناوبات" or "أرشيف الزيارات")throw new BusinessRuleException("مجلدات الأرشيف مخصصة للمستندات الآلية.");
            await MoveBoundaryAsync(ids,meta.Id,ct);
        }
        var confirmedHandle=handle with{Expires=DateTimeOffset.UtcNow.AddMinutes(20)};
        var plan=new DriveMutationPlan(confirmedHandle,request.Action,name,destination,item.Parents.Single(),ids,impact.Fingerprint,item.Version??"",item.Name);
        return new(handles.ProtectPlan(plan),item.Name,request.Action,tree.Count(x=>!x.IsFolder),tree.Count(x=>x.IsFolder),impact.LinkedTo,impact.LinkedCount,impact.SignedDocuments,
            impact.SignedDocuments>0?"ستُحذف النسخة من Drive، وستبقى التوقيعات والنسخ الأصلية محفوظة في النظام. يلزم إجراء استعادة لإعادة الأرشفة.":
            impact.LinkedCount>0?"سيُزال الملف من الشواهد النشطة المرتبطة به، مع بقاء سجل المراجعات.":"سينتقل العنصر ومحتوياته إلى مهملات Google Drive.");
    }
    public async Task<DriveMutationDto> MutateAsync(DriveMutationRequest request,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(request.RequestKey)||request.RequestKey.Length>100)throw new ArgumentException("معرّف الطلب غير صالح.");
        var root=await RootAsync(true,ct);var plan=handles.ReadPlan(request.Ticket);Validate(plan.Handle,root);
        DriveMutationOperation? work=null;
        await repository.ExclusiveAsync(School,async()=>{
            work=await repository.OperationAsync(School,Actor,request.RequestKey,ct);
            var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(plan))));
            if(work!=null&&work.Operation.Fingerprint!=fingerprint)throw new StorageConflictException();
            if(work==null){if((await repository.ImpactAsync(School,plan.Items,ct)).Fingerprint!=plan.LedgerFingerprint)throw new StorageConflictException();
                work=await repository.ReserveAsync(School,Actor,request.RequestKey,plan,fingerprint,root.DriveId,ct);}
            await ExecuteAsync(work,root,ct);
        },ct);
        return Dto(work!.Operation);
    }
    public async Task<DriveMutationDto> ReconcileAsync(int id,CancellationToken ct)
    {
        var root=await RootAsync(true,ct);DriveMutationOperation? work=null;
        await repository.ExclusiveAsync(School,async()=>{work=await repository.OperationAsync(School,id,ct)??throw new KeyNotFoundException();Validate(work.Plan.Handle,root,false,false);await ExecuteAsync(work,root,ct);},ct);
        return Dto(work!.Operation);
    }
    private async Task ExecuteAsync(DriveMutationOperation work,StorageDriveRoot root,CancellationToken ct)
    {
        if(work.Operation.Status=="Completed")return;
        var plan=work.Plan;if(plan.Handle.Item==root.RootItemId)throw Denied();
        try{
            var impact=await repository.ImpactAsync(School,plan.Items,ct);if(impact.Fingerprint!=plan.LedgerFingerprint||impact.Busy)throw new StorageConflictException();
            var item=await ItemAsync(plan.Handle.Item,root,ct,plan.Action is "Delete" or "Restore");await BranchAsync(item,root,ct);
            if(item.Parents.Count!=1)throw new StorageConflictException();
            if(plan.Action=="Restore"){
                if(item.Parents[0]!=plan.Parent)throw new StorageConflictException();
                var saved=new Dictionary<string,GoogleDriveFile>();
                foreach(var batch in plan.Items.Chunk(4))foreach(var value in await Task.WhenAll(batch.Select(id=>provider.MetadataAsync(School,id,ct)))){
                    if(value==null||(value.SharedDriveId??"")!=root.DriveId)throw new StorageConflictException();saved[value.Id]=value;
                }
                foreach(var value in saved.Values){var cursor=value;var seen=new HashSet<string>();
                    while(cursor.Id!=item.Id&&seen.Add(cursor.Id)&&seen.Count<64){if(cursor.Parents.Count!=1||!saved.TryGetValue(cursor.Parents[0],out var parent))throw new StorageConflictException();cursor=parent;}
                    if(cursor.Id!=item.Id)throw new StorageConflictException();
                }
                foreach(var expected in await repository.LocationsAsync(School,plan.Items,root.RootItemId,ct)){
                    var actual=saved[expected.Item];if(actual.Name!=expected.Name||expected.Parent!=null&&!actual.Parents.Contains(expected.Parent))throw new StorageConflictException();
                }
                // Drive may keep descendants explicitly trashed after restoring their parent.
                // Use only the persisted confirmed subtree, parent first; never adopt new items.
                var ordered=new Queue<string>();ordered.Enqueue(item.Id);var restored=new HashSet<string>();
                while(ordered.Count>0){var id=ordered.Dequeue();if(!restored.Add(id))throw new StorageConflictException();
                    if(saved[id].Trashed)await provider.RestoreAsync(School,id,ct);
                    foreach(var child in saved.Values.Where(x=>x.Id!=id&&x.Parents.Contains(id)))ordered.Enqueue(child.Id);
                }
                if(restored.Count!=plan.Items.Count)throw new StorageConflictException();
                provider.ResetRequestCache();
                foreach(var id in plan.Items)await ItemAsync(id,root,ct);
                foreach(var proof in await repository.ProofsAsync(School,plan.Items,ct)){
                    var content=await provider.ContentAsync(School,proof.Item,ct);await using var stream=content.Content;
                    using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var buffer=new byte[65536];long length=0;int read;
                    while((read=await stream.ReadAsync(buffer,ct))>0){length+=read;if(length>proof.Length)throw new StorageUnavailableException();hash.AppendData(buffer,0,read);}
                    if(length!=proof.Length||Convert.ToHexString(hash.GetHashAndReset())!=proof.Hash)throw new StorageUnavailableException();
                }
            }else if(plan.Action=="Delete"){
                if(item.Parents[0]!=plan.Parent)throw new StorageConflictException();
                if(!item.Trashed){
                    var actual=(await TreeAsync(item,root,ct)).Select(x=>x.Id).Order(StringComparer.Ordinal);
                    if(!actual.SequenceEqual(plan.Items)||item.Version!=plan.ProviderVersion&&plan.ProviderVersion!="")throw new StorageConflictException();
                    await provider.TrashAsync(School,item.Id,root.DriveId,ct);
                }
                provider.ResetRequestCache();if(await provider.MetadataAsync(School,item.Id,ct) is not {Trashed:true})throw new StorageUnavailableException();
                var retained=new Dictionary<string,GoogleDriveFile>();
                foreach(var batch in plan.Items.Chunk(4))foreach(var value in await Task.WhenAll(batch.Select(id=>provider.MetadataAsync(School,id,ct)))){
                    if(value==null)throw new StorageConflictException();retained[value.Id]=value;
                }
                foreach(var value in retained.Values){var cursor=value;var seen=new HashSet<string>();
                    while(cursor.Id!=item.Id&&seen.Add(cursor.Id)&&seen.Count<64){
                        if(cursor.Parents.Count!=1||!retained.TryGetValue(cursor.Parents[0],out var parent))throw new StorageConflictException();cursor=parent;
                    }
                    if(cursor.Id!=item.Id)throw new StorageConflictException();
                }
            }else if(plan.Action=="Rename"){
                if(item.Parents[0]!=plan.Parent)throw new StorageConflictException();
                if(item.Name!=plan.Name){if(plan.ProviderVersion!=""&&item.Version!=plan.ProviderVersion)throw new StorageConflictException();await provider.RenameAsync(School,item.Id,plan.Name!,ct);}
                provider.ResetRequestCache();if((await ItemAsync(item.Id,root,ct)).Name!=plan.Name)throw new StorageUnavailableException();
            }else{
                var dest=await ItemAsync(plan.Destination!,root,ct);if(await BranchAsync(dest,root,ct)!=null)throw new StorageConflictException();
                await MoveBoundaryAsync(plan.Items,dest.Id,ct);
                if(!dest.IsFolder||await provider.IsWithinAsync(School,item.Id,dest.Id,ct))throw new StorageConflictException();
                if(item.Parents[0]!=plan.Destination){if(item.Parents[0]!=plan.Parent||plan.ProviderVersion!=""&&item.Version!=plan.ProviderVersion)throw new StorageConflictException();await provider.MoveAsync(School,item.Id,plan.Parent,dest.Id,ct);}
                provider.ResetRequestCache();if(!(await ItemAsync(item.Id,root,ct)).Parents.Contains(dest.Id))throw new StorageUnavailableException();
            }
            var latestRoot=await RootAsync(true,ct);
            if(latestRoot!=root)throw new StorageConflictException();
            await repository.CompleteAsync(work,Actor,ct);
        }catch(Exception e) when(e is HttpRequestException or OperationCanceledException or StorageUnavailableException or StorageConflictException or KeyNotFoundException or System.Data.Common.DbException){
            await repository.UncertainAsync(work.Operation,e is StorageConflictException?"ChangedSinceConfirmation":"VerificationRequired",CancellationToken.None);
        }
    }
    private async Task MoveBoundaryAsync(IReadOnlyList<string> ids,string destination,CancellationToken ct)
    {
        var dest=await repository.DestinationAsync(School,destination,ct);
        var placements=await repository.PlacementsAsync(School,ids,ct);
        if(dest?.Kind is StorageFolderKind.DutyArchive or StorageFolderKind.VisitArchive||placements.Count>0&&
            (dest==null||placements.Any(x=>x.Kind!=dest.Kind||x.Owner!=dest.Owner)))
            throw new BusinessRuleException("اختر مجلدًا من مساحة المالك نفسها؛ لا يمكن نقل الملفات إلى أرشيف محمي أو تغيير صاحبها.");
    }
    public async Task<IReadOnlyList<DriveMutationDto>> PendingAsync(CancellationToken ct){
        await auth.RequireSchoolPermissionAsync(School,PermissionNames.StorageManageSchool,ct);
        if((await auth.RequireScopeAsync(School,ct)).ManagerUserId!=Actor)throw Denied();
        return await repository.PendingAsync(School,ct);
    }
    public async Task<IReadOnlyList<DriveMutationDto>> DeletedAsync(CancellationToken ct){await RootAsync(true,ct);return await repository.DeletedAsync(School,ct);}
    public async Task<DriveMutationDto> RestoreAsync(int deleteOperation,CancellationToken ct)
    {
        var root=await RootAsync(true,ct);DriveMutationOperation? work=null;
        await repository.ExclusiveAsync(School,async()=>{
            var original=await repository.OperationAsync(School,deleteOperation,ct)??throw new KeyNotFoundException();
            if(original.Operation.Action!="DriveDelete"||original.Operation.Status!="Completed")throw new StorageConflictException();
            Validate(original.Plan.Handle,root,false,false);
            var key="restore:"+deleteOperation;
            work=await repository.OperationAsync(School,Actor,key,ct);
            if(work==null){var current=await repository.ImpactAsync(School,original.Plan.Items,ct);
                var plan=original.Plan with{Action="Restore",LedgerFingerprint=current.Fingerprint,Handle=original.Plan.Handle with{Expires=DateTimeOffset.UtcNow.AddMinutes(20)}};
                work=await repository.ReserveAsync(School,Actor,key,plan,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(plan)))),root.DriveId,ct);
            }
            await ExecuteAsync(work,root,ct);
        },ct);
        return Dto(work!.Operation);
    }
    private static DriveMutationDto Dto(Domain.Entities.Storage.StorageOperation op)=>new(op.Id,op.Status,op.Action[4..],op.DisplayName,op.ErrorCode);
}
