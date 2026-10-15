using AlFalah.Application.Common;
using AlFalah.Application.Duty;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.Duty;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.Storage;

[Collection("Duty SQL")]
public sealed class StorageMapSqlTests(DutySqlFixture fixture)
{
    [DutySqlFact]
    public async Task Library_setup_provisions_protected_duty_root_in_SQL_and_reapplication_keeps_its_identity_without_activation()
    {
        var scene=await new DutyExecutionSqlTests(fixture).Seed();await using var db=fixture.Db();
        var role="setup-"+Guid.NewGuid().ToString("N");db.Roles.Add(new(){Id=role,Name=role,NormalizedName=role.ToUpperInvariant()});
        db.UserSchoolRoles.Add(new(){UserId=scene.Manager,SchoolId=scene.School,RoleId=role});
        foreach(var name in new[]{PermissionNames.StorageManageSchool,PermissionNames.StorageDelegate})
            db.RolePermissions.Add(new(){RoleId=role,PermissionId=await db.Permissions.Where(x=>x.Name==name).Select(x=>x.Id).SingleAsync()});
        db.SchoolGoogleDrives.Add(new(){SchoolId=scene.School,RootFolderId="setup-root",ProtectedCredential="synthetic-only",IsEnabled=true});await db.SaveChangesAsync();
        var drive=new FakeGoogleDrive().AddFolder("setup-root","Synthetic setup");var provider=new GoogleStorageProvider(drive);
        var scopes=new StorageRepository(db);var library=new StorageLibraryRepository(db);var user=new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,scene.Manager,scene.School,true);
        var service=new StorageSetupService(new StorageSetupRepository(db),scopes,library,new StorageAuthorizationService(scopes,user,new StorageDriveBoundary(new(drive)),TimeProvider.System),
            user,provider,null!,Options.Create(new StorageOptions{AdministrationEnabled=true,ReadModelEnabled=true}),new DutyArchiveDriveService(new DutyDocumentRepository(db),scopes,library,provider));
        (await service.PlanAsync()).Folders.Single(x=>x.Key=="duty").State.Should().Be("Missing");
        var first=await service.ApplyAsync(true,false);first.Warnings.Should().BeEmpty();
        var duty=await db.StorageFolders.AsNoTracking().SingleAsync(x=>x.SchoolId==scene.School&&x.Kind==StorageFolderKind.DutyArchive);
        duty.DutyIdentity.Should().Be("duty");duty.DutyProvisioned.Should().BeTrue();
        var second=await service.ApplyAsync(true,false);second.Created.Should().Be(0);second.Warnings.Should().BeEmpty();
        (await db.StorageFolders.AsNoTracking().SingleAsync(x=>x.SchoolId==scene.School&&x.Kind==StorageFolderKind.DutyArchive)).DriveItemId.Should().Be(duty.DriveItemId);
        drive.ChildIdsOf("setup-root").Should().HaveCount(4);
        var flags=await db.SchoolGoogleDrives.AsNoTracking().SingleAsync(x=>x.SchoolId==scene.School);flags.DutyArchiveEnabled.Should().BeFalse();flags.VisitArchiveEnabled.Should().BeFalse();
    }
    [DutySqlFact]
    public async Task School_root_map_verifies_branches_and_preserves_separate_archive_teacher_and_school_permissions()
    {
        var scene=await new DutyExecutionSqlTests(fixture).Seed();await using var db=fixture.Db();
        var role="map-"+Guid.NewGuid().ToString("N");db.Roles.Add(new(){Id=role,Name=role,NormalizedName=role.ToUpperInvariant()});
        db.UserSchoolRoles.Add(new(){UserId=scene.Manager,SchoolId=scene.School,RoleId=role});
        var permission=await db.Permissions.SingleAsync(x=>x.Name==PermissionNames.StorageViewSchool);
        db.RolePermissions.Add(new(){RoleId=role,PermissionId=permission.Id});
        db.RolePermissions.Add(new(){RoleId=role,PermissionId=await db.Permissions.Where(x=>x.Name==PermissionNames.StorageManageSchool).Select(x=>x.Id).SingleAsync()});
        db.SchoolGoogleDrives.Add(new(){SchoolId=scene.School,RootFolderId="root",ProtectedCredential="synthetic-only",IsEnabled=true});
        var library=new StorageFolder{SchoolId=scene.School,Kind=StorageFolderKind.SchoolLibrary,DisplayName="مكتبة المدرسة",DriveItemId="library"};db.Add(library);await db.SaveChangesAsync();
        var domain=new StorageFolder{SchoolId=scene.School,Kind=StorageFolderKind.SchoolLibrary,ParentFolderId=library.Id,DisplayName="1 - الإدارة المدرسية",DriveItemId="domain"};db.Add(domain);await db.SaveChangesAsync();
        var standard=new StorageFolder{SchoolId=scene.School,Kind=StorageFolderKind.SchoolLibrary,ParentFolderId=domain.Id,DisplayName="1.1 - التخطيط",DriveItemId="standard"};db.Add(standard);await db.SaveChangesAsync();
        var drive=new FakeGoogleDrive().AddFolder("root","Synthetic school").AddFolder("library",library.DisplayName,"root")
            .AddFolder("domain",domain.DisplayName,"library").AddFolder("standard",standard.DisplayName,"domain")
            .AddFolder("teachers","ملفات المعلمين","root").AddFolder("outside","Outside");
        var nested=new StorageFolder{SchoolId=scene.School,Kind=StorageFolderKind.SchoolLibrary,ParentFolderId=standard.Id,DisplayName="Nested evidence",DriveItemId="nested"};db.Add(nested);await db.SaveChangesAsync();drive.AddFolder("nested",nested.DisplayName,"standard");
        var uploadUser=new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,scene.Manager,scene.School,true);var uploadScopes=new StorageRepository(db);
        var uploader=new StorageLibraryService(new StorageLibraryRepository(db),uploadScopes,new StorageAuthorizationService(uploadScopes,uploadUser,new StorageDriveBoundary(new(drive)),TimeProvider.System),
            uploadUser,new GoogleStorageProvider(drive),null!,Options.Create(new StorageOptions{ReadModelEnabled=true}));
        using(var content=new MemoryStream("%PDF-1.7\nSynthetic nested file"u8.ToArray()))
            await uploader.UploadAsync(new(content,"synthetic.pdf",content.Length,nested.Id,"nested-file",false));
        StorageMapService Service(string actor,int school) {
            var user=new TeacherDriveHarness.TestCurrentUser(actor==scene.Manager?RoleNames.SchoolManager:RoleNames.Instructor,actor,school,true);
            var scopes=new StorageRepository(db);var provider=new GoogleStorageProvider(drive);var auth=new StorageAuthorizationService(scopes,user,new StorageDriveBoundary(new(drive)),TimeProvider.System);
            var visits=new VisitArchiveService(new VisitArchiveRepository(db),scopes,auth,user,new EnabledVisits(),null!,provider,Options.Create(new StorageOptions{AdministrationEnabled=true,ReadModelEnabled=true}),TimeProvider.System);
            return new(new StorageMapRepository(db),scopes,new StorageLibraryRepository(db),auth,user,provider,new DutyRepository(db),visits,Options.Create(new StorageOptions{ReadModelEnabled=true}));
        }
        var map=await Service(scene.Manager,scene.School).ReadAsync(default);
        map.Spaces.Should().HaveCount(4);map.Nodes.Should().HaveCount(2);map.RegisteredFiles.Should().Be(1);
        map.Nodes.Single(x=>x.Id==standard.Id).Files.Should().Be(1);map.Nodes.Single(x=>x.Id==domain.Id).Files.Should().Be(0);
        map.Spaces.Single(x=>x.Key=="library").CanOpen.Should().BeTrue();map.Spaces.Single(x=>x.Key=="teachers").State.Should().Be("Available");
        map.Spaces.Single(x=>x.Key=="duty").CanOpen.Should().BeFalse(); // No document identity exposed before provisioning.
        map.Spaces.Single(x=>x.Key=="visits").CanOpen.Should().BeFalse();
        await FluentActions.Invoking(()=>Service(scene.Teacher,scene.School).ReadAsync(default)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(()=>Service(scene.Manager,scene.School+10000).ReadAsync(default)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        drive.MoveExternally("domain","outside");map=await Service(scene.Manager,scene.School).ReadAsync(default);
        map.Nodes.Should().ContainSingle().Which.State.Should().Be("Unavailable");
        drive.MoveExternally("library","outside");map=await Service(scene.Manager,scene.School).ReadAsync(default);
        map.Nodes.Should().BeEmpty();map.Spaces.Single(x=>x.Key=="library").CanOpen.Should().BeFalse();map.Spaces.Single(x=>x.Key=="library").State.Should().Be("Unavailable");
    }
    private sealed class EnabledVisits:IFeatureFlagService {public bool IsVisitsV2Enabled(int? schoolId=null)=>true;}
}
