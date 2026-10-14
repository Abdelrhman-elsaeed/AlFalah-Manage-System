using System.Security.Cryptography;
using System.Text.Json;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Duty;

public sealed class DutyArchiveDriveService(IDutyDocumentRepository repo,IStorageRepository storage,IStorageLibraryRepository library,IStorageProvider provider)
{
    public static DutyException Identity() => new("DutyArchiveIdentity","تعذّر التحقق من النسخة المؤرشفة. النسخة المحفوظة متاحة. / Archive verification failed. The retained copy is available.",409);
    private static DutyException Moved() => new("DutyArchiveMoved","تغيّر موضع المستند أو اسمه في الأرشيف. راجع إعداد الأرشيف. / The archive document location or name changed. Review archive setup.",409);
    public async Task<StorageDriveRoot> RootAsync(int school,string? target,CancellationToken ct)
    {
        provider.ResetRequestCache();
        var root=await storage.GetSchoolDriveRootAsync(school,ct)??throw new StorageUnavailableException();
        var meta=await provider.MetadataAsync(school,root.RootItemId,ct);
        if(meta is not {IsFolder:true,Trashed:false} || (meta.SharedDriveId??"")!=root.DriveId)throw new StorageUnavailableException();
        if(target is not null&&!await provider.IsWithinAsync(school,root.RootItemId,target,ct))throw Moved();
        foreach(var grant in await library.TeacherRootsAsync(school,ct))
        {
            if(grant.DriveId!=root.DriveId||grant.RootItemId==root.RootItemId||!await provider.IsWithinAsync(school,root.RootItemId,grant.RootItemId,ct)
                ||target is not null&&(await provider.IsWithinAsync(school,grant.RootItemId,target,ct)||await provider.IsWithinAsync(school,target,grant.RootItemId,ct))) throw Identity();
        }
        return root;
    }
    public async Task<StorageFolder> DestinationAsync(DutySignedDocument doc,CancellationToken ct)
    {
        var s=DutyDocumentIntegrity.Validate(doc);var h=s.GetProperty("Header").Deserialize<DutyExecutionHeader>()!;
        var scope=await repo.ScopeAsync(doc.SchoolId,doc.AssignmentId,ct);
        var date=DateOnly.Parse(s.GetProperty("Date").GetString()!,System.Globalization.CultureInfo.InvariantCulture);
        StorageFolder? result=null;
        if(!await repo.ExclusiveAsync($"folders:{doc.SchoolId}",async token=>
        {
            var root=await RootAsync(doc.SchoolId,null,token);
            var chain=new List<(string Key,string Name)> {("duty","المناوبات"),($"year:{scope.YearId}",h.Year),($"term:{scope.TermId}",$"الفصل الدراسي {h.Semester}"),($"teacher:{doc.AuthorUserId}",await repo.TeacherFolderNameAsync(doc.SchoolId,doc.AuthorUserId,h.ActualTeacher,token)),($"date:{date:yyyy-MM-dd}",date.ToString("yyyy-MM-dd"))};
            StorageFolder? parent=null;var identity="";
            foreach(var (key,name) in chain)
            {
                identity=identity.Length==0?key:identity+"/"+key;
                var parentItem=parent?.DriveItemId??root.RootItemId;
                var folder=await repo.FolderAsync(doc.SchoolId,identity,token);
                if(folder is null)
                {
                    // A native folder with the same name is ambiguous; never adopt a browser-selected folder.
                    string? page=null;
                    do {var children=await provider.ChildrenAsync(doc.SchoolId,parentItem,root.DriveId,page,token);
                        if(children.Files.Any(x=>!x.Trashed&&x.Name==DutyDocumentIntegrity.SafeName(name)))throw Identity();page=children.NextPageToken;
                    } while(page is not null);
                    folder=new(){SchoolId=doc.SchoolId,Kind=StorageFolderKind.DutyArchive,DutyIdentity=identity,DisplayName=DutyDocumentIntegrity.SafeName(name),ParentFolderId=parent?.Id,
                        DriveId=root.DriveId,DriveItemId=await provider.AllocateIdAsync(doc.SchoolId,token)};
                    await repo.SaveFolderAsync(folder,token); // Persist reserved ID BEFORE create.
                }
                if(folder.DriveId!=root.DriveId||folder.ParentFolderId!=parent?.Id||folder.Kind!=StorageFolderKind.DutyArchive||!folder.IsActive)throw Identity();
                provider.ResetRequestCache();var meta=await provider.MetadataAsync(doc.SchoolId,folder.DriveItemId,token);
                if(meta is null)
                {
                    if(folder.DutyProvisioned)throw new DutyException("DutyArchiveMissing","مجلد محفوظ مفقود من الأرشيف. يلزم مراجعته. / A retained archive folder is missing. Review is required.",409);
                    await provider.CreateFolderAsync(doc.SchoolId,folder.DriveItemId,parentItem,folder.DisplayName,token);
                    provider.ResetRequestCache();meta=await provider.MetadataAsync(doc.SchoolId,folder.DriveItemId,token);
                }
                if(meta is not {IsFolder:true,Trashed:false}||meta.Name!=folder.DisplayName||!meta.Parents.Contains(parentItem))throw Moved();
                await RootAsync(doc.SchoolId,folder.DriveItemId,token);
                if(!folder.DutyProvisioned){folder.DutyProvisioned=true;await repo.SaveFolderAsync(folder,token);}
                parent=folder;
            }
            result=parent;
        },ct))throw new StorageUnavailableException();
        return result!;
    }
    public static IReadOnlyDictionary<string,string> Properties(DutyArchiveIntent i,DutyPdfArtifact a) => new Dictionary<string,string>
    { ["schoolId"]=i.SchoolId.ToString(System.Globalization.CultureInfo.InvariantCulture),["dutyDocumentId"]=i.DocumentId.ToString(System.Globalization.CultureInfo.InvariantCulture),["dutyOperation"]=i.OperationIdentity!,["sha256"]=a.Hash };
    public async Task<bool> ExistsAndVerifiedAsync(DutyArchiveIntent i,DutyPdfArtifact a,CancellationToken ct)
    {
        DutyDocumentIntegrity.Validate(a);var root=await RootAsync(i.SchoolId,null,ct);
        if(root.DriveId!=i.DriveId||root.RootItemId!=i.SchoolRootItemId)throw Identity();
        var folder=await repo.FolderByIdAsync(i.SchoolId,i.FolderId!.Value,ct)??throw Identity();
        var visited=new HashSet<int>();var node=folder;
        while(true)
        {
            if(!visited.Add(node.Id)||visited.Count>8)throw Identity();
            var parent=node.ParentFolderId is { } id ? await repo.FolderByIdAsync(i.SchoolId,id,ct)??throw Identity() : null;
            var meta=await provider.MetadataAsync(i.SchoolId,node.DriveItemId,ct);
            if(meta is null)throw new DutyException("DutyArchiveMissing","المجلد المؤرشف غير موجود. / Archive folder missing.",409);
            if(meta is not {IsFolder:true,Trashed:false}||meta.Name!=node.DisplayName||!meta.Parents.Contains(parent?.DriveItemId??root.RootItemId))throw Moved();
            if(parent is null)break;node=parent;
        }
        if(folder.DriveItemId!=i.FolderItemId)throw Identity();
        await RootAsync(i.SchoolId,folder.DriveItemId,ct);
        var item=await provider.MetadataAsync(i.SchoolId,i.ProviderItemId!,ct);
        if(item is null)return false; // Only a definitive provider 404 allows write to the SAME reserved ID.
        if(item.Trashed||!item.Parents.Contains(folder.DriveItemId)||item.Name!=a.FileName||!await provider.IsWithinAsync(i.SchoolId,root.RootItemId,item.Id,ct))throw Moved();
        if(item.MimeType!="application/pdf"||item.Size!=a.Bytes.LongLength||item.AppProperties is null||Properties(i,a).Any(x=>!item.AppProperties.TryGetValue(x.Key,out var value)||value!=x.Value))throw Identity();
        var content=await provider.ContentAsync(i.SchoolId,item.Id,ct);await using var stream=content.Content;
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var buffer=new byte[65536];long length=0;int read;
        while((read=await stream.ReadAsync(buffer,ct))!=0){length+=read;if(length>a.Bytes.LongLength)throw Identity();hash.AppendData(buffer,0,read);}
        if(length!=a.Bytes.LongLength||Convert.ToHexString(hash.GetHashAndReset())!=a.Hash)throw Identity();
        return true;
    }
    public async Task VerifyAsync(DutyArchiveIntent i,DutyPdfArtifact a,CancellationToken ct)
    {if(!await ExistsAndVerifiedAsync(i,a,ct))throw new DutyException("DutyArchiveMissing","المستند غير موجود في الأرشيف؛ النسخة المحفوظة متاحة. / Archive document missing; the retained copy is available.",409);}
}
