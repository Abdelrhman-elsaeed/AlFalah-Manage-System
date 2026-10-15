using AlFalah.Application.Common;
using AlFalah.Application.Duty;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.Duty;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.Storage;

[Collection("Duty SQL")]
public sealed class StorageDriveBrowserSqlTests(DutySqlFixture fixture)
{
    private readonly IStorageDriveHandles handles=new StorageDriveHandles(new EphemeralDataProtectionProvider());
    private sealed record World(DutyExecutionSqlTests.Scene Scene,FakeGoogleDrive Drive,int Library,int File);
    private async Task<World> Seed()
    {
        var s=await new DutyExecutionSqlTests(fixture).Seed(guardian:false);await using var db=fixture.Db();
        string Id(string value)=>value+"-"+s.School;
        var role="drive-"+Guid.NewGuid().ToString("N");db.Roles.Add(new(){Id=role,Name=role,NormalizedName=role.ToUpperInvariant()});db.UserSchoolRoles.Add(new(){UserId=s.Manager,SchoolId=s.School,RoleId=role});
        foreach(var name in new[]{PermissionNames.StorageViewSchool,PermissionNames.StorageManageSchool,PermissionNames.StorageDelegate,PermissionNames.StorageViewArchive,PermissionNames.VisitView}){
            var permission=await db.Permissions.SingleOrDefaultAsync(x=>x.Name==name);
            if(permission==null){permission=new(){Name=name,Group="Storage browser test"};db.Permissions.Add(permission);await db.SaveChangesAsync();}
            db.RolePermissions.Add(new(){RoleId=role,PermissionId=permission.Id});
        }
        db.SchoolGoogleDrives.Add(new(){SchoolId=s.School,RootFolderId=Id("root"),ProtectedCredential="synthetic",IsEnabled=true});
        var library=new StorageFolder{SchoolId=s.School,DisplayName="مكتبة المدرسة",Kind=StorageFolderKind.SchoolLibrary,DriveItemId=Id("library")};db.Add(library);await db.SaveChangesAsync();
        var drive=new FakeGoogleDrive{IdPrefix=s.School+"-"}.AddFolder(Id("root"),"Synthetic root").AddFolder(Id("library"),"مكتبة المدرسة",Id("root"))
            .AddFolder(Id("teachers"),"ملفات المعلمين",Id("root")).AddFolder(Id("duty"),"المناوبات",Id("root")).AddFolder(Id("visits"),"أرشيف الزيارات",Id("root"))
            .AddFolder(Id("native"),"مجلد إضافي",Id("root")).AddFolder(Id("outside"),"Outside");
        var user=new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,s.Manager,s.School,true);var scope=new StorageRepository(db);
        var api=new StorageLibraryService(new StorageLibraryRepository(db),scope,new StorageAuthorizationService(scope,user,new StorageDriveBoundary(new(drive)),TimeProvider.System),user,new GoogleStorageProvider(drive),null!,Options.Create(new StorageOptions{ReadModelEnabled=true}));
        using var bytes=new MemoryStream("%PDF-1.7\nSynthetic delete proof"u8.ToArray());
        var file=await api.UploadAsync(new(bytes,"دليل اصطناعي.pdf",bytes.Length,library.Id,"native-proof",false));return new(s,drive,library.Id,file.StoredFileId!.Value);
    }
    private StorageDriveBrowserRepository Repo(AlFalahDbContext db)=>new(db,new EvidenceSubmissionService(db,new AuditLogWriter(db,new HttpContextAccessor(),NullLogger<AuditLogWriter>.Instance)));
    private StorageDriveBrowserService Service(AlFalahDbContext db,World w,string? actor=null,int? school=null)
    {
        var user=new TeacherDriveHarness.TestCurrentUser(actor==null?RoleNames.SchoolManager:RoleNames.Instructor,actor??w.Scene.Manager,school??w.Scene.School,true);
        var scope=new StorageRepository(db);var provider=new GoogleStorageProvider(w.Drive);var auth=new StorageAuthorizationService(scope,user,new StorageDriveBoundary(new(w.Drive)),TimeProvider.System);
        var options=Options.Create(new StorageOptions{ReadModelEnabled=true,AdministrationEnabled=true});
        var visits=new VisitArchiveService(new VisitArchiveRepository(db),scope,auth,user,new EnabledVisits(),null!,provider,options,TimeProvider.System);
        return new(Repo(db),scope,auth,user,provider,handles,new SchoolDriveSetupRepository(db),new DutyRepository(db),options);
    }
    private string Handle(World w,string item,string? actor=null,int? school=null)=>handles.ProtectHandle(new(school??w.Scene.School,"root-"+w.Scene.School,Alias(w,item),actor??w.Scene.Manager,DateTimeOffset.UtcNow.AddHours(1)));
    [DutySqlFact]public async Task Virtual_school_folder_opens_actual_native_children_and_files_without_feature_routes_or_adoption()
    {
        var w=await Seed();await using var db=fixture.Db();var api=Service(db,w);
        var home=await api.BrowseAsync(null,null,default);home.Items.Should().ContainSingle().Which.Name.Should().Be("ملفات المدرسة");
        // Only visit policy is replaced; other roots and untracked native folders use actual provider metadata.
        await w.Drive.RenameAsync(w.Scene.School,Alias(w,"visits"),"أرشيف آخر");
        var root=await api.BrowseAsync(home.Items.Single().Handle,null,default);root.Items.Should().HaveCount(5);
        root.Items.Should().Contain(x=>x.Name=="المناوبات");root.Items.Should().Contain(x=>x.Name=="مجلد إضافي");
        var library=await api.BrowseAsync(root.Items.Single(x=>x.Name=="مكتبة المدرسة").Handle,null,default);library.Items.Should().ContainSingle();
        var content=await api.ContentAsync(library.Items[0].Handle,default);await using var stream=content.Content;stream.Length.Should().BeGreaterThan(0);
        (await db.StorageFolders.CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(1);
    }
    [DutySqlFact]public async Task Manager_delete_withdraws_approved_active_links_keeps_decisions_and_soft_deletes_ledger()
    {
        var w=await Seed();await using var db=fixture.Db();var file=await db.StoredFiles.SingleAsync(x=>x.Id==w.File);var version=await db.StoredFileVersions.SingleAsync(x=>x.Id==file.CurrentVersionId);
        var year=await db.AcademicTerms.Where(x=>x.Id==w.Scene.Term).Select(x=>x.AcademicYearId).SingleAsync();
        var req=new EvidenceRequirement{SchoolId=w.Scene.School,AcademicYearId=year,Code="delete-proof-"+w.Scene.School,DisplayName="شاهد معتمد اصطناعي"};db.Add(req);await db.SaveChangesAsync();
        db.EvidenceLinks.Add(new(){SchoolId=w.Scene.School,AcademicYearId=year,StoredFileId=file.Id,VersionId=version.Id,RequirementId=req.Id,Status=EvidenceLinkStatus.Approved});await db.SaveChangesAsync();
        var api=Service(db,w);var impact=await api.ImpactAsync(new(Handle(w,version.DriveItemId),"Delete"),default);impact.LinkedTo.Should().Contain(req.DisplayName);
        var request=new DriveMutationRequest(impact.Ticket,"delete-proof");(await api.MutateAsync(request,default)).Status.Should().Be("Completed");
        (await api.MutateAsync(request,default)).Status.Should().Be("Completed");w.Drive.Trashed.Should().ContainSingle();db.ChangeTracker.Clear();
        (await db.StoredFiles.IgnoreQueryFilters().SingleAsync(x=>x.Id==file.Id)).IsDeleted.Should().BeTrue();
        var link=await db.EvidenceLinks.SingleAsync(x=>x.StoredFileId==file.Id);link.IsActive.Should().BeFalse();link.Status.Should().Be(EvidenceLinkStatus.Approved);
        (await db.StoredFileVersions.SingleAsync(x=>x.Id==version.Id)).Availability.Should().Be(StoredFileAvailability.Deleted);
        (await db.AuditLogs.CountAsync(x=>x.SchoolId==w.Scene.School&&x.Action=="Storage.DriveDeleteCompleted")).Should().Be(1);
    }
    [DutySqlFact]public async Task Lost_trash_response_survives_new_context_and_reconciles_without_another_provider_mutation()
    {
        var w=await Seed();int operation;await using(var db=fixture.Db()){
            var item=await db.StoredFileVersions.SingleAsync(x=>x.StoredFileId==w.File);var api=Service(db,w);
            var impact=await api.ImpactAsync(new(Handle(w,item.DriveItemId),"Delete"),default);w.Drive.LoseNextTrashResponse=true;
            var result=await api.MutateAsync(new(impact.Ticket,"lost-reply"),default);result.Status.Should().Be("NeedsAttention");operation=result.Id;
        }
        await using(var db=fixture.Db()){
            (await Service(db,w).ReconcileAsync(operation,default)).Status.Should().Be("Completed");
            (await db.StoredFiles.IgnoreQueryFilters().SingleAsync(x=>x.Id==w.File)).IsDeleted.Should().BeTrue();
        }
        w.Drive.Trashed.Should().ContainSingle();
    }
    [DutySqlFact]public async Task Actual_SQL_rollback_after_trash_keeps_operation_recoverable_and_links_atomic()
    {
        var w=await Seed();int operation;
        await using(var db=fixture.Db()){
            var item=await db.StoredFileVersions.SingleAsync(x=>x.StoredFileId==w.File);var api=Service(db,w);var impact=await api.ImpactAsync(new(Handle(w,item.DriveItemId),"Delete"),default);
            await db.Database.ExecuteSqlRawAsync($"CREATE TRIGGER TR_DriveDeleteRollback_{w.Scene.School} ON StoredFiles AFTER UPDATE AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Id={w.File} AND IsDeleted=1) THROW 51600,'Synthetic storage deletion SQL rollback',1; END");
            try{var result=await api.MutateAsync(new(impact.Ticket,"rollback-proof"),default);result.Status.Should().Be("NeedsAttention");operation=result.Id;
                db.ChangeTracker.Clear();(await db.StoredFiles.IgnoreQueryFilters().SingleAsync(x=>x.Id==w.File)).IsDeleted.Should().BeFalse();}
            finally{await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER TR_DriveDeleteRollback_{w.Scene.School}");}
        }
        await using(var db=fixture.Db())(await Service(db,w).ReconcileAsync(operation,default)).Status.Should().Be("Completed");w.Drive.Trashed.Should().ContainSingle();
    }
    [DutySqlFact]public async Task Handles_are_school_actor_root_and_expiry_bound_and_root_itself_cannot_be_deleted()
    {
        var w=await Seed();await using var db=fixture.Db();var api=Service(db,w);
        await FluentActions.Invoking(()=>api.ImpactAsync(new(Handle(w,"root"),"Delete"),default)).Should().ThrowAsync<BusinessRuleException>();
        await FluentActions.Invoking(()=>api.BrowseAsync(Handle(w,"outside"),null,default)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(()=>api.BrowseAsync(Handle(w,"root",school:w.Scene.School+1),null,default)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(()=>api.BrowseAsync(Handle(w,"root",actor:w.Scene.Teacher),null,default)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        var expired=handles.ProtectHandle(new(w.Scene.School,"root-"+w.Scene.School,Alias(w,"library"),w.Scene.Manager,DateTimeOffset.UtcNow.AddMinutes(-1)));
        await FluentActions.Invoking(()=>api.BrowseAsync(expired,null,default)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(()=>Service(db,w,w.Scene.Teacher).BrowseAsync(Handle(w,"root"),null,default)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(()=>api.BrowseAsync("forged-provider-id",null,default)).Should().ThrowAsync<ArgumentException>();w.Drive.Trashed.Should().BeEmpty();
    }
    [DutySqlFact]public async Task Confirmation_rejects_new_evidence_links_and_changed_provider_tree_before_any_write()
    {
        var w=await Seed();await using var db=fixture.Db();var api=Service(db,w);var impact=await api.ImpactAsync(new(Handle(w,"library"),"Delete"),default);
        w.Drive.AddFile("new-arrival","New file.pdf",Alias(w,"library"));
        var result=await api.MutateAsync(new(impact.Ticket,"changed-tree"),default);result.Status.Should().Be("NeedsAttention");w.Drive.Trashed.Should().BeEmpty();
        await FluentActions.Invoking(()=>api.ImpactAsync(new(Handle(w,"library"),"Rename","Other library"),default)).Should().ThrowAsync<BusinessRuleException>();
    }
    [DutySqlFact]public async Task Explicit_restore_checks_bytes_preserves_identity_and_does_not_reactivate_withdrawn_evidence()
    {
        var w=await Seed();await using var db=fixture.Db();var api=Service(db,w);var version=await db.StoredFileVersions.SingleAsync(x=>x.StoredFileId==w.File);
        w.Drive.TrashDescendants=true;
        var originalHash=version.SHA256;var impact=await api.ImpactAsync(new(Handle(w,"library"),"Delete"),default);
        var deletion=await api.MutateAsync(new(impact.Ticket,"restore-source"),default);deletion.Status.Should().Be("Completed");
        (await api.DeletedAsync(default)).Should().Contain(x=>x.Id==deletion.Id);
        (await api.RestoreAsync(deletion.Id,default)).Status.Should().Be("Completed");
        (await api.RestoreAsync(deletion.Id,default)).Status.Should().Be("Completed");
        (await api.DeletedAsync(default)).Should().NotContain(x=>x.Id==deletion.Id);db.ChangeTracker.Clear();
        var file=await db.StoredFiles.SingleAsync(x=>x.Id==w.File);file.NeedsLink.Should().BeTrue();file.CurrentVersionId.Should().Be(version.Id);
        (await db.StoredFileVersions.SingleAsync(x=>x.Id==version.Id)).SHA256.Should().Be(originalHash);w.Drive.Trashed.Should().ContainSingle();
    }
    [DutySqlFact]public async Task Ordinary_rename_and_move_update_SQL_and_provider_and_reject_protected_destination()
    {
        var w=await Seed();await using var db=fixture.Db();var api=Service(db,w);var version=await db.StoredFileVersions.SingleAsync(x=>x.StoredFileId==w.File);
        var rename=await api.ImpactAsync(new(Handle(w,version.DriveItemId),"Rename","اسم جديد.pdf"),default);
        (await api.MutateAsync(new(rename.Ticket,"rename-proof"),default)).Status.Should().Be("Completed");
        var destination=new StorageFolder{SchoolId=w.Scene.School,ParentFolderId=w.Library,Kind=StorageFolderKind.SchoolLibrary,DisplayName="وجهة",DriveItemId="destination-"+w.Scene.School};db.Add(destination);await db.SaveChangesAsync();w.Drive.AddFolder(destination.DriveItemId,destination.DisplayName,Alias(w,"library"));
        var move=await api.ImpactAsync(new(Handle(w,version.DriveItemId),"Move",Destination:Handle(w,destination.DriveItemId)),default);
        (await api.MutateAsync(new(move.Ticket,"move-proof"),default)).Status.Should().Be("Completed");db.ChangeTracker.Clear();
        var file=await db.StoredFiles.SingleAsync(x=>x.Id==w.File);file.DisplayName.Should().Be("اسم جديد.pdf");file.FolderId.Should().Be(destination.Id);
        (await w.Drive.GetFileAsync(w.Scene.School,version.DriveItemId))!.Parents.Should().Contain(destination.DriveItemId);
        await FluentActions.Invoking(()=>api.ImpactAsync(new(Handle(w,version.DriveItemId),"Move",Destination:Handle(w,"duty")),default)).Should().ThrowAsync<BusinessRuleException>();
        w.Drive.AddFolder("unindexed-protected-"+w.Scene.School,"Unindexed archive child",Alias(w,"duty"));
        await FluentActions.Invoking(()=>api.ImpactAsync(new(Handle(w,version.DriveItemId),"Move",Destination:Handle(w,"unindexed-protected-"+w.Scene.School)),default)).Should().ThrowAsync<BusinessRuleException>();
        w.Drive.AddFile("unindexed-file-"+w.Scene.School,"Native file.pdf",Alias(w,"native"));
        await FluentActions.Invoking(()=>api.ImpactAsync(new(Handle(w,"unindexed-file-"+w.Scene.School),"Move",Destination:Handle(w,"unindexed-protected-"+w.Scene.School)),default)).Should().ThrowAsync<BusinessRuleException>();
    }
    [DutySqlFact]public async Task Signed_letter_deletion_keeps_completed_identity_frozen_bytes_local_access_and_explicit_restore()
    {
        var w=await Seed();await using var db=fixture.Db();QuestPDF.Settings.License=QuestPDF.Infrastructure.LicenseType.Community;
        var signature=await db.UserSignatures.SingleAsync(x=>x.UserId==w.Scene.Teacher);signature.SignatureDrawnData="data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"TestAssets/Duty/signature-0.png")));
        (await db.SchoolGoogleDrives.SingleAsync(x=>x.SchoolId==w.Scene.School)).DutyArchiveEnabled=true;await db.SaveChangesAsync();w.Drive.RemoveExternally(Alias(w,"duty"));
        var execution=new DutyExecutionService(new DutyRepository(db),new DutyExecutionRepository(db),new TeacherDriveHarness.TestCurrentUser(RoleNames.Instructor,w.Scene.Teacher,w.Scene.School,true));
        var detail=await execution.GetAsync(w.Scene.Assignment,default);await execution.AcknowledgeAsync(w.Scene.Assignment,new(detail.AssignmentRevision,Guid.NewGuid()),default);
        var doc=await db.Set<DutySignedDocument>().SingleAsync(x=>x.SchoolId==w.Scene.School);var snapshot=doc.SnapshotHash;
        var provider=new GoogleStorageProvider(w.Drive);var repo=new DutyDocumentRepository(db);var drive=new DutyArchiveDriveService(repo,new StorageRepository(db),new StorageLibraryRepository(db),provider);var options=Options.Create(new DutyArchiveOptions{ProcessingEnabled=true});
        await new DutyArchiveProcessor(repo,new DutyExecutionRepository(db),new DutyPdfRenderer(),drive,provider,options,NullLogger<DutyArchiveProcessor>.Instance).ProcessBatchAsync(default);
        var intent=await db.Set<DutyArchiveIntent>().SingleAsync(x=>x.DocumentId==doc.Id);intent.State.Should().Be("TestVerified");var providerId=intent.ProviderItemId!;var artifact=await db.Set<DutyPdfArtifact>().SingleAsync(x=>x.DocumentId==doc.Id);var hash=artifact.Hash;
        var api=Service(db,w);var impact=await api.ImpactAsync(new(Handle(w,providerId),"Delete"),default);impact.SignedDocuments.Should().Be(1);
        var exact=await api.ContentAsync(Handle(w,providerId),default);await using(var stream=exact.Content){using var bytes=new MemoryStream();await stream.CopyToAsync(bytes);DutyDocumentIntegrity.Hash(bytes.ToArray()).Should().Be(hash);}
        w.Drive.SetContentExternally(providerId,"%PDF-1.7 tampered signed copy"u8.ToArray());
        await FluentActions.Invoking(()=>api.ContentAsync(Handle(w,providerId),default)).Should().ThrowAsync<StorageUnavailableException>();
        w.Drive.SetContentExternally(providerId,artifact.Bytes);
        var deletion=await api.MutateAsync(new(impact.Ticket,"signed-delete"),default);deletion.Status.Should().Be("Completed");
        db.ChangeTracker.Clear();intent=await db.Set<DutyArchiveIntent>().SingleAsync(x=>x.DocumentId==doc.Id);intent.State.Should().Be("TestVerified");intent.ErrorCode.Should().Be("DutyArchiveDeleted");intent.ProviderItemId.Should().Be(providerId);
        var reader=new DutyDocumentService(new DutyRepository(db),new DutyExecutionRepository(db),repo,new DutyPdfRenderer(),new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,w.Scene.Manager,w.Scene.School,true),drive,options);
        (await reader.ContentAsync(doc.Id,false,default)).Hash.Should().Be(hash);(await db.Set<DutySignedDocument>().SingleAsync(x=>x.Id==doc.Id)).SnapshotHash.Should().Be(snapshot);
        (await api.RestoreAsync(deletion.Id,default)).Status.Should().Be("Completed");(await db.Set<DutyArchiveIntent>().SingleAsync(x=>x.DocumentId==doc.Id)).ProviderItemId.Should().Be(providerId);
        (await db.Set<DutyPdfArtifact>().SingleAsync(x=>x.DocumentId==doc.Id)).Hash.Should().Be(hash);
    }
    [DutySqlFact]public async Task Two_contexts_with_same_confirmed_request_commit_one_delete_and_one_audit()
    {
        var w=await Seed();DriveImpactDto impact;await using(var db=fixture.Db()){
            var item=await db.StoredFileVersions.SingleAsync(x=>x.StoredFileId==w.File);impact=await Service(db,w).ImpactAsync(new(Handle(w,item.DriveItemId),"Delete"),default);
        }
        await using var first=fixture.Db();await using var second=fixture.Db();var request=new DriveMutationRequest(impact.Ticket,"concurrent-delete");
        var results=await Task.WhenAll(Service(first,w).MutateAsync(request,default),Service(second,w).MutateAsync(request,default));
        results.Select(x=>x.Id).Distinct().Should().ContainSingle();results.Should().OnlyContain(x=>x.Status=="Completed");w.Drive.Trashed.Should().ContainSingle();
        (await first.AuditLogs.CountAsync(x=>x.SchoolId==w.Scene.School&&x.Action=="Storage.DriveDeleteCompleted")).Should().Be(1);
    }
    [DutySqlFact]public async Task A_new_link_or_live_management_revocation_invalidates_confirmation_before_any_provider_write()
    {
        var w=await Seed();await using var db=fixture.Db();var api=Service(db,w);var file=await db.StoredFiles.SingleAsync(x=>x.Id==w.File);var version=await db.StoredFileVersions.SingleAsync(x=>x.Id==file.CurrentVersionId);
        var impact=await api.ImpactAsync(new(Handle(w,version.DriveItemId),"Delete"),default);var year=await db.AcademicTerms.Where(x=>x.Id==w.Scene.Term).Select(x=>x.AcademicYearId).SingleAsync();
        var req=new EvidenceRequirement{SchoolId=w.Scene.School,AcademicYearId=year,Code="new-link-"+w.Scene.School,DisplayName="شاهد جديد"};db.Add(req);await db.SaveChangesAsync();
        db.EvidenceLinks.Add(new(){SchoolId=w.Scene.School,AcademicYearId=year,StoredFileId=file.Id,VersionId=version.Id,RequirementId=req.Id,Status=EvidenceLinkStatus.Draft});await db.SaveChangesAsync();
        await FluentActions.Invoking(()=>api.MutateAsync(new(impact.Ticket,"stale-link"),default)).Should().ThrowAsync<StorageConflictException>();
        var current=await api.ImpactAsync(new(Handle(w,version.DriveItemId),"Delete"),default);
        var permission=await db.Permissions.Where(x=>x.Name==PermissionNames.StorageManageSchool).Select(x=>x.Id).SingleAsync();
        var roleIds=await db.UserSchoolRoles.Where(x=>x.SchoolId==w.Scene.School&&x.UserId==w.Scene.Manager).Select(x=>x.RoleId).ToArrayAsync();
        await db.RolePermissions.Where(x=>roleIds.Contains(x.RoleId)&&x.PermissionId==permission).ExecuteDeleteAsync();
        await FluentActions.Invoking(()=>api.MutateAsync(new(current.Ticket,"revoked"),default)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();w.Drive.Trashed.Should().BeEmpty();
    }
    [DutySqlFact]public async Task Folder_deletion_withdraws_registered_missing_evidence_and_restore_never_invents_its_bytes()
    {
        var w=await Seed();await using var db=fixture.Db();var file=await db.StoredFiles.SingleAsync(x=>x.Id==w.File);var version=await db.StoredFileVersions.SingleAsync(x=>x.Id==file.CurrentVersionId);
        var year=await db.AcademicTerms.Where(x=>x.Id==w.Scene.Term).Select(x=>x.AcademicYearId).SingleAsync();
        var req=new EvidenceRequirement{SchoolId=w.Scene.School,AcademicYearId=year,Code="missing-link-"+w.Scene.School,DisplayName="شاهد اختفى من Drive"};db.Add(req);await db.SaveChangesAsync();
        db.EvidenceLinks.Add(new(){SchoolId=w.Scene.School,AcademicYearId=year,StoredFileId=file.Id,VersionId=version.Id,RequirementId=req.Id,Status=EvidenceLinkStatus.Approved});await db.SaveChangesAsync();
        w.Drive.RemoveExternally(version.DriveItemId);var api=Service(db,w);var impact=await api.ImpactAsync(new(Handle(w,"library"),"Delete"),default);
        impact.Files.Should().Be(0);impact.LinkedTo.Should().Contain(req.DisplayName);var deletion=await api.MutateAsync(new(impact.Ticket,"missing-evidence"),default);deletion.Status.Should().Be("Completed");
        db.ChangeTracker.Clear();(await db.StoredFiles.IgnoreQueryFilters().SingleAsync(x=>x.Id==file.Id)).IsDeleted.Should().BeTrue();(await db.EvidenceLinks.SingleAsync(x=>x.StoredFileId==file.Id)).IsActive.Should().BeFalse();
        (await api.RestoreAsync(deletion.Id,default)).Status.Should().Be("Completed");db.ChangeTracker.Clear();
        (await db.StoredFiles.IgnoreQueryFilters().SingleAsync(x=>x.Id==file.Id)).IsDeleted.Should().BeTrue();(await db.StoredFileVersions.SingleAsync(x=>x.Id==version.Id)).Availability.Should().Be(StoredFileAvailability.Deleted);
        (await w.Drive.GetFileAsync(w.Scene.School,version.DriveItemId)).Should().BeNull();w.Drive.Trashed.Should().ContainSingle().Which.Should().Be(Alias(w,"library"));
    }
    private static string Alias(World w,string id)=>id is "root" or "library" or "teachers" or "duty" or "visits" or "native" or "outside"?id+"-"+w.Scene.School:id;
    private sealed class EnabledVisits:IFeatureFlagService{public bool IsVisitsV2Enabled(int? schoolId=null)=>true;}
}
