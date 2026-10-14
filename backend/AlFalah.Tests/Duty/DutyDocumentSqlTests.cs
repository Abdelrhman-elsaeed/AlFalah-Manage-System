using System.Text.Json;
using AlFalah.Application.Duty;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace AlFalah.Tests.Duty;

[Collection("Duty SQL")]
public sealed class DutyDocumentSqlTests(DutySqlFixture fixture)
{
    private sealed record World(DutyExecutionSqlTests.Scene Scene, FakeGoogleDrive Drive, string Root);
    private static readonly IOptions<DutyArchiveOptions> Enabled = Options.Create(new DutyArchiveOptions { ProcessingEnabled=true });
    private static byte[] Signature(int variant=0)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"TestAssets","Duty",$"signature-{(variant==0?0:1)}.png"));
    }
    private async Task<World> Setup(bool enabled=true)
    {
        QuestPDF.Settings.License=QuestPDF.Infrastructure.LicenseType.Community;
        var s=await new DutyExecutionSqlTests(fixture).Seed(students:12);
        await using var db=fixture.Db();var root="d3-root-"+s.School;
        foreach(var user in new[]{s.Teacher,s.Replacement})
        {
            (await db.UserSignatures.SingleAsync(x=>x.UserId==user)).SignatureDrawnData="data:image/png;base64,"+Convert.ToBase64String(Signature(user==s.Teacher?0:9));
            var account=await db.Users.SingleAsync(x=>x.Id==user);account.FirstName="عبدالله عبدالرحمن محمد";account.LastName="معلم التحقق الاصطناعي";
        }
        var school=await db.Schools.SingleAsync(x=>x.Id==s.School);school.Name="مدرسة التحقق الاصطناعية D3";
        db.SchoolReportSettings.Add(new(){SchoolId=s.School,ReportHeaderText="المملكة العربية السعودية\nوزارة التعليم\nإدارة التعليم — بيانات تحقق اصطناعية",ReportFooterText="مدرسة التحقق الاصطناعية",LogoUrl="https://invalid.example/frozen-unavailable.png"});
        db.SchoolGoogleDrives.Add(new(){SchoolId=s.School,RootFolderId=root,ProtectedCredential="synthetic-test-only",IsEnabled=true,DutyArchiveEnabled=enabled});
        await db.SaveChangesAsync();return new(s,new FakeGoogleDrive().AddFolder(root,"Synthetic D3 root").AddFolder("outside","Outside"),root);
    }
    private DutyExecutionService Execution(AlFalahDbContext db,World w,bool manager=false,string? user=null)=>new(new DutyRepository(db),new DutyExecutionRepository(db),
        new TeacherDriveHarness.TestCurrentUser(manager?RoleNames.SchoolManager:RoleNames.Instructor,user??(manager?w.Scene.Manager:w.Scene.Teacher),w.Scene.School,true));
    private static DutyArchiveDriveService Drive(AlFalahDbContext db,World w)=>new(new DutyDocumentRepository(db),new StorageRepository(db),new StorageLibraryRepository(db),new GoogleStorageProvider(w.Drive));
    private DutyDocumentService Reader(AlFalahDbContext db,World w,bool manager=false,string? user=null)=>new(new DutyRepository(db),new DutyExecutionRepository(db),new DutyDocumentRepository(db),new DutyPdfRenderer(),
        new TeacherDriveHarness.TestCurrentUser(manager?RoleNames.SchoolManager:RoleNames.Instructor,user??(manager?w.Scene.Manager:w.Scene.Teacher),w.Scene.School,true),Drive(db,w),Enabled);
    private static DutyArchiveProcessor Processor(AlFalahDbContext db,World w)=>new(new DutyDocumentRepository(db),new DutyExecutionRepository(db),new DutyPdfRenderer(),Drive(db,w),new GoogleStorageProvider(w.Drive),Enabled,NullLogger<DutyArchiveProcessor>.Instance);
    private async Task<int> SignLetter(AlFalahDbContext db,World w,string? user=null)
    {
        var e=Execution(db,w,user:user);var d=await e.GetAsync(w.Scene.Assignment,default);
        await e.AcknowledgeAsync(w.Scene.Assignment,new(d.AssignmentRevision,Guid.NewGuid()),default);
        return (await db.Set<DutySignedDocument>().AsNoTracking().Where(x=>x.SchoolId==w.Scene.School&&x.Kind=="Letter").OrderByDescending(x=>x.Id).FirstAsync()).Id;
    }
    [DutySqlFact] public async Task Other_teacher_cannot_take_duty_by_acknowledgement_and_retained_replay_has_current_permissions()
    {
        var w=await Setup(false);await using var db=fixture.Db();
        var initial=await db.Set<DutyAssignment>().AsNoTracking().SingleAsync(x=>x.Id==w.Scene.Assignment);
        await FluentActions.Invoking(()=>Execution(db,w,user:w.Scene.Replacement).AcknowledgeAsync(initial.Id,new(initial.Revision,Guid.NewGuid()),default))
            .Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
        (await db.Set<DutySignedDocument>().CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(0);
        (await db.Set<DutyArchiveIntent>().CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(0);
        var sign=new DutySignRequest(initial.Revision,Guid.NewGuid());
        await Execution(db,w).AcknowledgeAsync(initial.Id,sign,default);
        (await db.Set<DutyAssignment>().SingleAsync(x=>x.Id==initial.Id)).EffectiveTeacherId.Should().Be(initial.EffectiveTeacherId);
        var planner=new DutyService(new DutyRepository(db),new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,w.Scene.Manager,w.Scene.School,true));
        var context=await planner.WorkspaceAsync(w.Scene.Term,default);
        await planner.ChangeAsync(new(initial.Id,context.PlanRevision,w.Scene.Replacement,null,"Synthetic ownership review","Emergency",true,null,Guid.NewGuid()),default);
        var replay=await Execution(db,w).AcknowledgeAsync(initial.Id,sign,default);
        replay.CanExecute.Should().BeFalse();replay.Header.ActualTeacher.Should().NotBeEmpty();
        await FluentActions.Invoking(()=>Execution(db,w).AcknowledgeAsync(initial.Id,new(initial.Revision,Guid.NewGuid()),default))
            .Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
        (await db.Set<DutySignedDocument>().CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(1);
        (await db.Set<DutyAssignment>().SingleAsync(x=>x.Id==initial.Id)).EffectiveTeacherId.Should().Be(w.Scene.Replacement);
    }
    [DutySqlFact] public async Task Manager_archive_readiness_detects_disconnection_and_activation_preserves_unrelated_state()
    {
        var w=await Setup(false);await using var db=fixture.Db();var manager=Reader(db,w,true);
        var connected=await manager.ReadinessAsync(default);connected.Enabled.Should().BeFalse();connected.ConnectionState.Should().Be("Connected");
        await FluentActions.Invoking(()=>Reader(db,w).ReadinessAsync(default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
        await FluentActions.Invoking(()=>Reader(db,w).EnableAsync(true,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
        w.Drive.UnreachableSchools.Add(w.Scene.School);
        (await manager.ReadinessAsync(default)).ConnectionState.Should().Be("Disconnected");
        await FluentActions.Invoking(()=>manager.EnableAsync(true,default)).Should().ThrowAsync<Exception>();
        db.ChangeTracker.Clear();var before=await db.SchoolGoogleDrives.SingleAsync(x=>x.SchoolId==w.Scene.School);
        before.DutyArchiveEnabled.Should().BeFalse();var credential=before.ProtectedCredential;var visit=before.VisitArchiveEnabled;
        w.Drive.UnreachableSchools.Clear();(await manager.EnableAsync(true,default)).Enabled.Should().BeTrue();
        (await manager.EnableAsync(false,default)).Enabled.Should().BeFalse();
        db.ChangeTracker.Clear();var after=await db.SchoolGoogleDrives.SingleAsync(x=>x.SchoolId==w.Scene.School);
        after.ProtectedCredential.Should().Be(credential);after.VisitArchiveEnabled.Should().Be(visit);after.RootFolderId.Should().Be(w.Root);
        w.Drive.Uploads.Should().BeEmpty();
    }
    [DutySqlFact] public async Task Future_duty_cannot_be_started_using_an_owned_assignment_or_host_calendar_date()
    {
        var w=await Setup(false);await using var db=fixture.Db();
        var plan=await db.Set<DutyPlan>().SingleAsync(x=>x.SchoolId==w.Scene.School);
        plan.TimeZoneId="Pacific/Honolulu";
        var schoolToday=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById(plan.TimeZoneId)).DateTime);
        var day=await db.Set<DutyAssignment>().SingleAsync(x=>x.Id==w.Scene.Assignment);day.Date=schoolToday.AddDays(1);await db.SaveChangesAsync();
        var service=Execution(db,w);var view=await service.GetAsync(day.Id,default);view.CanExecute.Should().BeFalse();view.SignatureData.Should().BeNull();
        await FluentActions.Invoking(()=>service.AcknowledgeAsync(day.Id,new(day.Revision,Guid.NewGuid()),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyNotToday"&&x.Status==409);
        await FluentActions.Invoking(()=>service.SaveDraftAsync(day.Id,new(day.Revision,0,new(true,true,[],"Future forged edit",null)),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyNotToday");
        (await db.Set<DutySignedDocument>().CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(0);
        (await db.Set<DutyArchiveIntent>().CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(0);day.EffectiveTeacherId.Should().Be(w.Scene.Teacher);
    }
    private async Task<int> SignReport(AlFalahDbContext db,World w,string? user=null,bool populated=false,string? observations=null)
    {
        var e=Execution(db,w,user:user);var d=await e.GetAsync(w.Scene.Assignment,default);
        if(populated)await e.ConfirmLateAsync(w.Scene.Assignment,new(d.AssignmentRevision,w.Scene.Student,w.Scene.Enrollment,new(0,1),new(0,2),"التواصل مع ولي الأمر لترتيب المغادرة","ملاحظة اصطناعية",Guid.NewGuid()),default);
        var report=new DutyReportContent(!populated,true,[],observations??"تم الإشراف على مغادرة الطلاب ومتابعة انصراف آخر طالب.",new(0,3),"ملاحظة المناوب الأصلي",user is null?"":"ملاحظة المعلم البديل");
        var saved=await e.SaveDraftAsync(w.Scene.Assignment,new(d.AssignmentRevision,d.Draft?.Version??0,report),default);
        await e.SubmitAsync(w.Scene.Assignment,new(d.AssignmentRevision,Guid.NewGuid(),saved.Draft!.Version),default);
        return (await db.Set<DutySignedDocument>().AsNoTracking().Where(x=>x.SchoolId==w.Scene.School&&x.Kind=="Report").OrderByDescending(x=>x.Id).FirstAsync()).Id;
    }
    private async Task RetryDue(World w)
    {
        await using var db=fixture.Db();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE DutyArchiveIntents SET NextAttemptAt=NULL WHERE SchoolId={w.Scene.School} AND State='RetryScheduled'");
    }
    [DutySqlFact] public async Task Frozen_local_pdf_survives_profile_branding_shared_fact_changes_and_disconnection()
    {
        var w=await Setup(false);await using var db=fixture.Db();var id=await SignLetter(db,w);var report=await SignReport(db,w,populated:true);
        var first=await Reader(db,w).ContentAsync(report,false,default);first.Bytes[..5].Should().Equal("%PDF-"u8.ToArray());
        db.ChangeTracker.Clear();(await db.Users.SingleAsync(x=>x.Id==w.Scene.Teacher)).FirstName="Changed";
        (await db.UserSignatures.SingleAsync(x=>x.UserId==w.Scene.Teacher)).SignatureDrawnData="data:image/png;base64,"+Convert.ToBase64String(Signature(19));
        (await db.SchoolReportSettings.SingleAsync(x=>x.SchoolId==w.Scene.School)).ReportHeaderText="Changed";await db.SaveChangesAsync();
        var fact=await db.Set<StudentDismissalLateness>().SingleAsync(x=>x.SchoolId==w.Scene.School);
        await Execution(db,w,true).CorrectLateAsync(fact.Id,new(fact.Revision,new(0,2),new(0,3),"Corrected","","Verified correction",false,Guid.NewGuid()),default);
        w.Drive.UnreachableSchools.Add(w.Scene.School);
        var second=await Reader(db,w).ContentAsync(report,false,default);second.Bytes.Should().Equal(first.Bytes);second.Hash.Should().Be(first.Hash);
        (await db.Set<DutyPdfArtifact>().CountAsync(x=>x.DocumentId==report)).Should().Be(1);
        var docs=await Reader(db,w).ListAsync(w.Scene.Assignment,default);docs.Documents.Should().HaveCount(2);docs.Documents.Should().OnlyContain(x=>x.ArchiveState=="SetupUnavailable");
        await Export(db,w,"original-letter",id);await Export(db,w,"original-report",report);
    }
    [DutySqlFact] public async Task Lost_upload_response_reconciles_reserved_id_and_never_claims_fake_Drive_delivery()
    {
        var w=await Setup();await using(var db=fixture.Db()){await SignLetter(db,w);w.Drive.LoseNextUploadResponse=true;await Processor(db,w).ProcessBatchAsync(default);}
        w.Drive.Uploads.Should().HaveCount(1);await RetryDue(w);
        await using(var db=fixture.Db()) {await Processor(db,w).ProcessBatchAsync(default);var i=await db.Set<DutyArchiveIntent>().SingleAsync(x=>x.SchoolId==w.Scene.School);
            i.State.Should().Be("TestVerified");i.StoredFileVersionId.Should().NotBeNull();i.VerifiedAt.Should().NotBeNull();
            var a=await db.Set<DutyPdfArtifact>().SingleAsync(x=>x.Id==i.ArtifactId);var v=await db.StoredFileVersions.SingleAsync(x=>x.Id==i.StoredFileVersionId);
            v.SHA256.Should().Be(a.Hash);v.SizeInBytes.Should().Be(a.Bytes.Length);
            var downloaded=await w.Drive.DownloadAsync(w.Scene.School,i.ProviderItemId!);using var buffer=new MemoryStream();await downloaded.Content.CopyToAsync(buffer);DutyDocumentIntegrity.Hash(buffer.ToArray()).Should().Be(a.Hash);
            (await db.StorageFolders.CountAsync(x=>x.SchoolId==w.Scene.School&&x.Kind==StorageFolderKind.DutyArchive)).Should().Be(5);
            (await db.StoredFileVersions.CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(1);}
        w.Drive.Uploads.Should().HaveCount(1);
    }
    [DutySqlFact] public async Task Successful_upload_then_SQL_rollback_is_recovered_without_second_upload_or_ledger_version()
    {
        var w=await Setup();await using(var db=fixture.Db())
        {
            await SignLetter(db,w);
            // SQL identifiers below contain only an integer generated by the isolated fixture.
            var createFault=$"CREATE TRIGGER TR_D3_TestRollback_{w.Scene.School} ON StoredFileVersions AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE SchoolId={w.Scene.School}) THROW 51901,'Synthetic rollback after successful upload',1; END";
            var removeFault=$"DROP TRIGGER TR_D3_TestRollback_{w.Scene.School}";
            w.Drive.AfterUpload=async()=> {await using var fault=fixture.Db();await fault.Database.ExecuteSqlRawAsync(createFault);};
            await Processor(db,w).ProcessBatchAsync(default);db.ChangeTracker.Clear();
            (await db.StoredFileVersions.CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(0);(await db.StoredFiles.CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(0);
            await db.Database.ExecuteSqlRawAsync(removeFault);w.Drive.AfterUpload=null;
        }
        await RetryDue(w);await using(var db=fixture.Db()){await Processor(db,w).ProcessBatchAsync(default);(await db.StoredFileVersions.CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(1);}
        w.Drive.Uploads.Should().HaveCount(1);
    }
    [DutySqlFact] public async Task Worker_race_expired_lease_restart_and_repeated_batches_keep_one_folder_chain_and_file()
    {
        var w=await Setup();int id;await using(var db=fixture.Db()){id=await SignLetter(db,w);var i=await new DutyDocumentRepository(db).ClaimAsync(w.Scene.School,id,Guid.NewGuid(),DateTimeOffset.UtcNow.AddMinutes(-3),default);i.Should().NotBeNull();}
        await using var one=fixture.Db();await using var two=fixture.Db();await Task.WhenAll(Processor(one,w).ProcessBatchAsync(default),Processor(two,w).ProcessBatchAsync(default));
        await using var restart=fixture.Db();await Processor(restart,w).ProcessBatchAsync(default);
        w.Drive.Uploads.Should().HaveCount(1);(await restart.StorageFolders.CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(5);
        (await restart.StoredFileVersions.CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(1);
    }
    [DutySqlFact] public async Task Author_school_manager_and_revision_scope_is_rechecked_after_membership_changes()
    {
        var w=await Setup(false);await using var db=fixture.Db();var id=await SignLetter(db,w);
        await FluentActions.Invoking(()=>Reader(db,w,user:w.Scene.Replacement).ContentAsync(id,false,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
        (await Reader(db,w,true).ContentAsync(id,false,default)).Hash.Should().NotBeEmpty();
        var other=await Setup(false);await FluentActions.Invoking(()=>Reader(db,other,true).ContentAsync(id,false,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==404);
        db.ChangeTracker.Clear();(await db.UserSchoolRoles.SingleAsync(x=>x.SchoolId==w.Scene.School&&x.UserId==w.Scene.Teacher)).IsActive=false;await db.SaveChangesAsync();
        await FluentActions.Invoking(()=>Reader(db,w).ContentAsync(id,false,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
        var plan=await db.Set<DutyPlan>().SingleAsync(x=>x.SchoolId==w.Scene.School);
        await FluentActions.Invoking(()=>Reader(db,w,true).RosterAsync(w.Scene.Term,plan.Revision+1,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==409);
    }
    [DutySqlFact] public async Task Correction_resigns_new_snapshot_preserves_prior_pdf_and_exact_two_file_ledger()
    {
        var w=await Setup();await using var db=fixture.Db();await SignLetter(db,w);var old=await SignReport(db,w);var first=await Reader(db,w).ContentAsync(old,false,default);
        var e=Execution(db,w,true);var d=await e.GetAsync(w.Scene.Assignment,default);
        await e.CorrectReportAsync(w.Scene.Assignment,new(d.AssignmentRevision,d.Draft!.Version,"Verified manager correction reason",Guid.NewGuid()),default);
        var next=await SignReport(db,w,observations:"تمت مراجعة الملاحظات وإضافة متابعة موثقة بعد تصحيح التقرير.");next.Should().NotBe(old);
        var second=await Reader(db,w).ContentAsync(next,false,default);(await Reader(db,w).ContentAsync(old,false,default)).Bytes.Should().Equal(first.Bytes);
        second.Hash.Should().NotBe(first.Hash);(await db.Set<DutySignedDocument>().SingleAsync(x=>x.Id==next)).PreviousDocumentId.Should().Be(old);
        await Processor(db,w).ProcessBatchAsync(default);db.ChangeTracker.Clear();
        (await db.StoredFiles.CountAsync(x=>x.SchoolId==w.Scene.School&&x.SourceKind==StoredFileSourceKind.DutyArchive)).Should().Be(2);
        (await db.StoredFileVersions.CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(3);
        await Export(db,w,"corrected-report",next);await Export(db,w,"retained-report",old);
    }
    [DutySqlFact] public async Task Duty_SQL_evidence_scope_bytes_reserved_identity_and_storage_mutations_are_protected()
    {
        var w=await Setup();await using var db=fixture.Db();var id=await SignLetter(db,w);await Processor(db,w).ProcessBatchAsync(default);db.ChangeTracker.Clear();
        var i=await db.Set<DutyArchiveIntent>().SingleAsync(x=>x.DocumentId==id);var f=await db.StoredFiles.SingleAsync(x=>x.SchoolId==w.Scene.School);
        foreach(var sql in new[]{ $"UPDATE DutyPdfArtifacts SET Bytes=0x01 WHERE Id={i.ArtifactId}",$"DELETE FROM DutyPdfArtifacts WHERE Id={i.ArtifactId}",
            $"UPDATE DutyArchiveIntents SET ProviderItemId='different' WHERE Id={i.Id}",$"UPDATE DutyArchiveIntents SET State='Pending' WHERE Id={i.Id}",
            $"UPDATE StorageFolders SET DisplayName='different' WHERE Id={i.FolderId}",$"UPDATE StorageFolders SET Kind=2,DutyIdentity=NULL WHERE Id={i.FolderId}",
            $"DELETE FROM StorageFolders WHERE Id={i.FolderId}",$"UPDATE StoredFiles SET SourceKind=2,DutyIdentity=NULL WHERE Id={f.Id}",
            $"UPDATE StoredFiles SET IsDeleted=1 WHERE Id={f.Id}",$"UPDATE StoredFileVersions SET SHA256='wrong' WHERE Id={i.StoredFileVersionId}" })
            await FluentActions.Invoking(()=>db.Database.ExecuteSqlRawAsync(sql)).Should().ThrowAsync<Exception>();
        var deny=await new VisitArchiveRepository(db).ProtectedProviderIdsAsync(w.Scene.School,new[]{i.ProviderItemId!,i.FolderItemId!},default);
        deny.Should().Contain(i.ProviderItemId!);deny.Should().Contain(i.FolderItemId!);
        (await new StorageLibraryRepository(db).FoldersAsync(w.Scene.School,null,null,1,25,default)).Items.Should().NotContain(x=>x.Dto.Id==i.FolderId);
    }
    [DutySqlFact] public async Task Disconnected_setup_and_disabled_readiness_freeze_local_evidence_without_provider_IO()
    {
        var w=await Setup(false);await using var db=fixture.Db();var id=await SignLetter(db,w);await Processor(db,w).ProcessBatchAsync(default);
        w.Drive.Uploads.Should().BeEmpty();w.Drive.GetFileCalls.Should().Be(0);
        (await db.Set<DutyArchiveIntent>().SingleAsync(x=>x.DocumentId==id)).State.Should().Be("Pending");
        (await Reader(db,w).ContentAsync(id,false,default)).Hash.Should().NotBeEmpty();
        await FluentActions.Invoking(()=>Reader(db,w,true).RetryAsync(id,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyArchiveSetup");
    }
    [DutySqlFact] public async Task Moved_renamed_missing_and_uncertain_provider_outputs_never_trigger_blind_rewrites()
    {
        var w=await Setup();await using var db=fixture.Db();var id=await SignLetter(db,w);w.Drive.LoseNextUploadResponse=true;await Processor(db,w).ProcessBatchAsync(default);
        db.ChangeTracker.Clear();var i=await db.Set<DutyArchiveIntent>().SingleAsync(x=>x.DocumentId==id);
        w.Drive.UnreachableSchools.Add(w.Scene.School);await RetryDue(w);await Processor(db,w).ProcessBatchAsync(default);w.Drive.Uploads.Should().HaveCount(1);
        w.Drive.UnreachableSchools.Clear();w.Drive.MoveExternally(i.ProviderItemId!,"outside");await RetryDue(w);await Processor(db,w).ProcessBatchAsync(default);db.ChangeTracker.Clear();
        (await db.Set<DutyArchiveIntent>().SingleAsync(x=>x.DocumentId==id)).State.Should().Be("NeedsAttention");
        w.Drive.Uploads.Should().HaveCount(1);(await Reader(db,w).ContentAsync(id,false,default)).Hash.Should().NotBeEmpty();
    }
    [DutySqlFact] public async Task Root_grant_overlap_and_ambiguous_native_folder_are_rejected_before_file_upload()
    {
        var w=await Setup();await using var db=fixture.Db();await SignLetter(db,w);
        w.Drive.AddFolder("untracked-duty","المناوبات",w.Root);await Processor(db,w).ProcessBatchAsync(default);w.Drive.Uploads.Should().BeEmpty();
        db.ChangeTracker.Clear();(await db.Set<DutyArchiveIntent>().SingleAsync(x=>x.SchoolId==w.Scene.School)).ErrorCode.Should().Be("DutyArchiveIdentity");
        var profile=await db.InstructorProfiles.SingleAsync(x=>x.UserId==w.Scene.Teacher);
        db.TeacherDriveFolders.Add(new(){SchoolId=w.Scene.School,TeacherId=profile.Id,DriveId="",RootItemId=w.Root,FolderDisplayName="Denied root overlap"});await db.SaveChangesAsync();
        await FluentActions.Invoking(()=>Drive(db,w).RootAsync(w.Scene.School,null,default)).Should().ThrowAsync<DutyException>();
    }
    [DutySqlFact] public async Task Corrupt_signature_or_snapshot_is_recoverable_and_never_rendered_as_signed_evidence()
    {
        var w=await Setup(false);await using var db=fixture.Db();var id=await SignLetter(db,w);
        var d=await db.Set<DutySignedDocument>().AsNoTracking().SingleAsync(x=>x.Id==id);d.SignatureBytes[20]^=1;
        FluentActions.Invoking(()=>new DutyPdfRenderer().Signed(d)).Should().Throw<DutyException>().Which.Code.Should().Be("DutyArtifactUnavailable");
        d.SnapshotJson="{}";FluentActions.Invoking(()=>DutyDocumentIntegrity.Validate(d)).Should().Throw<DutyException>();
    }
    [DutySqlFact] public async Task Roster_export_is_frozen_to_plan_revision_and_has_no_archive_obligation()
    {
        var w=await Setup(false);await using var db=fixture.Db();var plan=await db.Set<DutyPlan>().SingleAsync(x=>x.SchoolId==w.Scene.School);
        var first=await Reader(db,w).RosterAsync(w.Scene.Term,plan.Revision,default);db.ChangeTracker.Clear();(await db.Users.SingleAsync(x=>x.Id==w.Scene.Teacher)).FirstName="Changed";await db.SaveChangesAsync();
        (await Reader(db,w).RosterAsync(w.Scene.Term,plan.Revision,default)).Bytes.Should().Equal(first.Bytes);
        (await db.Set<DutyArchiveIntent>().CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(0);
        ExportBytes("official-roster",first);
        var original=JsonSerializer.Deserialize<DutyRosterSnapshot>((await db.Set<DutyPdfArtifact>().SingleAsync(x=>x.SchoolId==w.Scene.School)).ProvenanceJson)!;
        var stress=original with {From=new(2026,9,6),To=new(2027,1,7),Rows=Enumerable.Range(0,124).Select(n=>new DutyRosterRow(new DateOnly(2026,9,6).AddDays(n),n is >30 and <36?"إجازة رسمية":n>115?"الاختبارات — وفق الخطة المعتمدة":"عبدالله عبدالرحمن محمد معلم التحقق الاصطناعي", "", n is >30 and <36)).Where(x=>(int)x.Date.DayOfWeek<5).ToArray()};
        ExportBytes("roster-long-names-closures-exams",new DutyPdfRenderer().Roster(stress));
    }
    [DutySqlFact] public async Task Substitute_signature_has_own_position_and_long_tables_have_continuation_pages()
    {
        var w=await Setup(false);await using var db=fixture.Db();var manager=new DutyService(new DutyRepository(db),new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,w.Scene.Manager,w.Scene.School,true));
        var day=await db.Set<DutyAssignment>().SingleAsync(x=>x.Id==w.Scene.Assignment);var request=new DutyChangeRequest(day.Id,day.Revision,w.Scene.Replacement,null,"Synthetic substitute verification","Replace",true,null,Guid.NewGuid());
        await manager.ChangePreviewAsync(request,default);await manager.ChangeAsync(request,default);
        var current=await Execution(db,w,true).GetAsync(w.Scene.Assignment,default);
        await Execution(db,w,true).TimingExceptionAsync(w.Scene.Assignment,new(current.AssignmentRevision,new TimeOnly(0,0),"Synthetic timing after substitute exchange",Guid.NewGuid()),default);
        var letter=await SignLetter(db,w,w.Scene.Replacement);var report=await SignReport(db,w,w.Scene.Replacement);var signed=await db.Set<DutySignedDocument>().AsNoTracking().SingleAsync(x=>x.Id==report);
        DutyDocumentIntegrity.Validate(signed).GetProperty("SignaturePosition").GetString().Should().Be("SubstituteTeacher");
        await Export(db,w,"substitute-letter",letter,w.Scene.Replacement);await Export(db,w,"substitute-report",report,w.Scene.Replacement);
        var snapshot=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(signed.SnapshotJson)!;
        var facts=Enumerable.Range(0,22).Select(n=>new {StudentName=$"الطالب الاصطناعي ذو الاسم الطويل عبدالرحمن عبدالله محمد {n+1}",Classroom="الثالث / أ",ObservedAt="13:45:00",Action="متابعة الطالب والتواصل لترتيب مغادرته مع ولي الأمر",Notes="بيانات تحقق اصطناعية فقط"}).ToArray();
        var lines=Enumerable.Range(0,13).Select(n=>new {Student=new {Name=$"طالب مخالفة اصطناعي {n+1}",Classroom="الرابع / ب"},Line=new {Time="13:46:00",Action="توجيه الطالب وفق الإجراء المدرسي",Notes="ملاحظة متابعة اصطناعية"}}).ToArray();
        snapshot["Payload"]=JsonSerializer.SerializeToElement(new {Content=new DutyReportContent(false,false,[],string.Join("\n",Enumerable.Repeat("تمت متابعة الطلاب وتنظيم خروجهم وفق الإجراءات المدرسية، مع توثيق الملاحظات وتنسيق التسليم في اليوم التالي.",35)),new TimeOnly(14,27),"ملاحظات المناوب الأصلي","ملاحظات البديل"),LateStudents=facts,Violations=lines});
        signed.SnapshotJson=JsonSerializer.Serialize(snapshot);signed.SnapshotHash=DutyDocumentIntegrity.Hash(signed.SnapshotJson);
        ExportBytes("multipage-report",new DutyPdfRenderer().Signed(signed));
    }
    [DutySqlFact] public async Task Same_name_teachers_keep_distinct_stable_readable_folders_and_emergency_history()
    {
        var w=await Setup();await using var db=fixture.Db();var old=await SignLetter(db,w);var frozen=await Reader(db,w).ContentAsync(old,false,default);
        var day=await db.Set<DutyAssignment>().SingleAsync(x=>x.Id==w.Scene.Assignment);
        var manager=new DutyService(new DutyRepository(db),new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,w.Scene.Manager,w.Scene.School,true));
        var change=new DutyChangeRequest(day.Id,day.Revision,w.Scene.Replacement,null,"Emergency replacement with retained author evidence","Emergency",true,null,Guid.NewGuid());
        await manager.ChangePreviewAsync(change,default);await manager.ChangeAsync(change,default);
        var next=await SignLetter(db,w,w.Scene.Replacement);await Processor(db,w).ProcessBatchAsync(default);db.ChangeTracker.Clear();
        var folders=await db.StorageFolders.Where(x=>x.SchoolId==w.Scene.School&&x.Kind==StorageFolderKind.DutyArchive&&x.DutyIdentity!.Contains("/teacher:")&&!x.DutyIdentity.Contains("/date:")).ToListAsync();
        folders.Should().HaveCount(2);folders.Select(x=>x.DisplayName).Distinct().Should().HaveCount(2);folders.Should().OnlyContain(x=>x.DisplayName.Contains("(معلم "));
        (await Reader(db,w).ContentAsync(old,false,default)).Bytes.Should().Equal(frozen.Bytes);
        await FluentActions.Invoking(()=>Reader(db,w,user:w.Scene.Replacement).ContentAsync(old,false,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
        await FluentActions.Invoking(()=>Reader(db,w).ContentAsync(next,false,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
        w.Drive.Uploads.Should().HaveCount(2);
        var history=await Reader(db,w,true).ListAsync(w.Scene.Assignment,default);history.Documents.Count(x=>x.IsCurrent).Should().Be(1);history.Documents.Single(x=>x.DocumentId==old).IsCurrent.Should().BeFalse();
    }
    [DutySqlFact] public async Task Retry_processes_only_missing_report_and_does_not_repeat_letter_facts_or_notices()
    {
        var w=await Setup();await using var db=fixture.Db();await SignLetter(db,w);await Processor(db,w).ProcessBatchAsync(default);
        var report=await SignReport(db,w,populated:true);var messages=await db.ConversationMessages.CountAsync(x=>x.SchoolId==w.Scene.School);
        w.Drive.FailNextUpload=true;await Processor(db,w).ProcessBatchAsync(default);w.Drive.Uploads.Should().HaveCount(1);
        await Reader(db,w,true).RetryAsync(report,default);await Processor(db,w).ProcessBatchAsync(default);await Processor(db,w).ProcessBatchAsync(default);
        w.Drive.Uploads.Should().HaveCount(2);(await db.Set<StudentDismissalLateness>().CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(1);
        (await db.ConversationMessages.CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(messages);
        (await db.Set<DutySignedDocument>().CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(2);
        await FluentActions.Invoking(()=>Reader(db,w,true).ContentAsync(report,true,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyArchiveUnavailable");
    }
    [DutySqlFact] public async Task Stale_lease_is_fenced_and_transient_failures_stop_after_five_attempts()
    {
        var w=await Setup();await using var db=fixture.Db();var id=await SignLetter(db,w);var repo=new DutyDocumentRepository(db);
        var old=await repo.ClaimAsync(w.Scene.School,id,Guid.NewGuid(),DateTimeOffset.UtcNow.AddMinutes(-3),default);
        await using(var other=fixture.Db()){(await new DutyDocumentRepository(other).ClaimAsync(w.Scene.School,id,Guid.NewGuid(),DateTimeOffset.UtcNow,default)).Should().NotBeNull();}
        old!.ErrorCode="Stale write";await FluentActions.Invoking(()=>repo.SaveAsync(old,default)).Should().ThrowAsync<StorageConflictException>();
        db.ChangeTracker.Clear();var active=await repo.IntentAsync(w.Scene.School,id,default);await repo.FailAsync(w.Scene.School,id,active!.LeaseToken!.Value,"DeliveryUnavailable",DateTimeOffset.UtcNow,default);
        for(var n=0;n<3;n++){await RetryDue(w);w.Drive.FailNextUpload=true;await Processor(db,w).ProcessBatchAsync(default);}
        db.ChangeTracker.Clear();var stopped=await repo.IntentAsync(w.Scene.School,id,default);stopped!.Attempts.Should().Be(5);stopped.State.Should().Be("NeedsAttention");stopped.NextAttemptAt.Should().BeNull();
        await Processor(db,w).ProcessBatchAsync(default);w.Drive.Uploads.Should().BeEmpty();(await Reader(db,w).ContentAsync(id,false,default)).Hash.Should().NotBeEmpty();
    }
    [DutySqlFact] public async Task Renamed_or_missing_file_and_parent_folder_are_detected_without_automatic_recreation()
    {
        foreach(var mutation in new[]{"rename-file","missing-file","rename-folder","missing-folder"})
        {
            var w=await Setup();await using var db=fixture.Db();var id=await SignLetter(db,w);await Processor(db,w).ProcessBatchAsync(default);db.ChangeTracker.Clear();
            var i=await new DutyDocumentRepository(db).IntentAsync(w.Scene.School,id,default);var a=await new DutyDocumentRepository(db).ArtifactAsync(w.Scene.School,id,default);
            if(mutation=="rename-file")await w.Drive.RenameAsync(w.Scene.School,i!.ProviderItemId!,"Externally renamed.pdf");
            if(mutation=="missing-file")w.Drive.RemoveExternally(i!.ProviderItemId!);
            if(mutation=="rename-folder")await w.Drive.RenameAsync(w.Scene.School,i!.FolderItemId!,"Externally renamed");
            if(mutation=="missing-folder")w.Drive.RemoveExternally(i!.FolderItemId!);
            await FluentActions.Invoking(()=>Drive(db,w).VerifyAsync(i!,a!,default)).Should().ThrowAsync<DutyException>();
            await Processor(db,w).ProcessBatchAsync(default);w.Drive.Uploads.Should().HaveCount(1);
            (await Reader(db,w).ContentAsync(id,false,default)).Hash.Should().Be(a!.Hash);
        }
    }
    [DutySqlFact] public async Task SQL_rejects_inserted_artifact_with_missing_signed_provenance_or_wrong_hash()
    {
        var w=await Setup(false);await using var db=fixture.Db();var id=await SignLetter(db,w);
        var doc=await db.Set<DutySignedDocument>().AsNoTracking().SingleAsync(x=>x.Id==id);var pdf=new DutyPdfRenderer().Signed(doc);
        var a=new DutyPdfArtifact{SchoolId=w.Scene.School,DocumentId=id,Bytes=pdf.Bytes,Hash=pdf.Hash,RendererVersion=pdf.RendererVersion,FileName=pdf.FileName,ProvenanceJson="{}",ProvenanceHash=DutyDocumentIntegrity.Hash("{}"),PreparedAt=DateTimeOffset.UtcNow};
        db.Add(a);await FluentActions.Invoking(()=>db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();db.ChangeTracker.Clear();
        a.Id=0;a.ProvenanceHash=new string('0',64);db.Add(a);await FluentActions.Invoking(()=>db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
        (await db.Set<DutyPdfArtifact>().CountAsync(x=>x.DocumentId==id)).Should().Be(0);
    }
    [DutySqlFact] public async Task D3_idempotent_migration_script_applies_twice_to_fresh_D2_and_preserves_sentinel()
    {
        var connection=new SqlConnectionStringBuilder(fixture.Connection);connection.InitialCatalog+="_D3Script_"+Guid.NewGuid().ToString("N");
        var masterConnection=new SqlConnectionStringBuilder(connection.ConnectionString){InitialCatalog="master"};
        await using(var master=new SqlConnection(masterConnection.ConnectionString))
        {
            await master.OpenAsync();await using var check=master.CreateCommand();check.CommandText="SELECT COUNT(*) FROM sys.databases WHERE name=@name";check.Parameters.AddWithValue("@name",connection.InitialCatalog);
            if(Convert.ToInt32(await check.ExecuteScalarAsync())!=0)throw new InvalidOperationException("Existing test database rejected before ANY migration");
        }
        await using var db=new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection.ConnectionString).Options);
        var migrator=db.GetService<IMigrator>();await migrator.MigrateAsync("20261013135411_AddSchoolDutyD2");
        var school=new School{Name="D3 script sentinel",City="Synthetic",Stage=SchoolStage.Primary};db.Add(school);await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchoolGoogleDrives (SchoolId,CredentialType,SchoolGoogleEmail,ProtectedCredential,RootFolderId,RootFolderDisplayName,IsEnabled,ConnectedAtUtc,UpdatedAtUtc) VALUES ({school.Id},1,'','script-sentinel-credential','script-sentinel-root','',1,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET())");
        var script=migrator.GenerateScript("20261013135411_AddSchoolDutyD2","20261013165202_AddSchoolDutyD3",MigrationsSqlGenerationOptions.Idempotent);
        var path=Environment.GetEnvironmentVariable("ALFALAH_DUTY_D3_PDF_DIR");if(path is not null)await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"d3-tests","d3-migration.sql"),script);
        await db.Database.OpenConnectionAsync();
        for(var n=0;n<2;n++)foreach(var batch in System.Text.RegularExpressions.Regex.Split(script,@"^GO\s*$",System.Text.RegularExpressions.RegexOptions.Multiline).Where(x=>!string.IsNullOrWhiteSpace(x)))await db.Database.ExecuteSqlRawAsync(batch);
        await db.Database.CloseConnectionAsync();
        (await db.Schools.SingleAsync()).Name.Should().Be("D3 script sentinel");var drive=await db.SchoolGoogleDrives.SingleAsync();drive.ProtectedCredential.Should().Be("script-sentinel-credential");drive.RootFolderId.Should().Be("script-sentinel-root");drive.DutyArchiveEnabled.Should().BeFalse();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();db.Database.HasPendingModelChanges().Should().BeFalse();
    }
    [DutySqlFact] public async Task Lost_folder_create_response_reconciles_persisted_reservation_without_duplicate_folders()
    {
        var w=await Setup();await using var db=fixture.Db();await SignLetter(db,w);w.Drive.LoseNextFolderResponse=true;
        await Processor(db,w).ProcessBatchAsync(default);db.ChangeTracker.Clear();var reserved=await db.StorageFolders.SingleAsync(x=>x.SchoolId==w.Scene.School);reserved.DutyProvisioned.Should().BeFalse();
        var providerId=reserved.DriveItemId;w.Drive.ChildIdsOf(w.Root).Should().ContainSingle().Which.Should().Be(providerId);
        await RetryDue(w);await Processor(db,w).ProcessBatchAsync(default);db.ChangeTracker.Clear();
        (await db.StorageFolders.SingleAsync(x=>x.SchoolId==w.Scene.School&&x.ParentFolderId==null)).DriveItemId.Should().Be(providerId);
        (await db.StorageFolders.CountAsync(x=>x.SchoolId==w.Scene.School)).Should().Be(5);w.Drive.ChildIdsOf(w.Root).Should().ContainSingle();w.Drive.Uploads.Should().ContainSingle();
    }
    [DutySqlFact] public async Task New_and_legacy_storage_boundaries_deny_duty_content_even_after_external_move_and_property_removal()
    {
        var w=await Setup();await using var db=fixture.Db();await SignLetter(db,w);await Processor(db,w).ProcessBatchAsync(default);db.ChangeTracker.Clear();
        var intent=await db.Set<DutyArchiveIntent>().SingleAsync(x=>x.SchoolId==w.Scene.School);var file=await db.StoredFiles.SingleAsync(x=>x.SchoolId==w.Scene.School);
        var storage=new StorageRepository(db);var manager=new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,w.Scene.Manager,w.Scene.School,true);
        var authorization=new StorageAuthorizationService(storage,manager,new StorageDriveBoundary(new TeacherDriveFolderGuard(w.Drive,new VisitArchiveRepository(db))),TimeProvider.System);
        foreach(var mutation in new[]{false,true})await FluentActions.Invoking(()=>authorization.RequireFileAsync(w.Scene.School,file.Id,mutation)).Should().ThrowAsync<AlFalah.Application.Common.UnauthorizedSchoolAccessException>();
        var profile=await db.InstructorProfiles.SingleAsync(x=>x.UserId==w.Scene.Teacher);var grant="legacy-d3-grant-"+w.Scene.School;
        w.Drive.AddFolder(grant,"Synthetic teacher grant",w.Root);db.TeacherDriveFolders.Add(new(){SchoolId=w.Scene.School,TeacherId=profile.Id,RootItemId=grant,DriveId="",FolderDisplayName="Synthetic teacher grant"});await db.SaveChangesAsync();
        var teacher=new TeacherDriveHarness.TestCurrentUser(RoleNames.Instructor,w.Scene.Teacher,w.Scene.School,true);
        var guard=new TeacherDriveFolderGuard(w.Drive,new VisitArchiveRepository(db));var audit=new AuditLogWriter(db,new HttpContextAccessor(),NullLogger<AuditLogWriter>.Instance);
        var mappings=new TeacherDriveMappingService(db,new SchoolScopeGuard(db,teacher,NullLogger<SchoolScopeGuard>.Instance),w.Drive,guard,audit,teacher);
        var browser=new GoogleDriveBrowserService(new TeacherDriveIdentityService(db,teacher),mappings,w.Drive,guard,db,audit);
        w.Drive.AddFile(intent.ProviderItemId!,"Externally replaced.pdf",grant,"bytes without application properties");
        (await browser.ListAsync(new(null,null,null,null,null))).Items.Should().BeEmpty();
        await FluentActions.Invoking(()=>browser.DownloadAsync(intent.ProviderItemId!)).Should().ThrowAsync<AlFalah.Application.Common.Exceptions.TeacherDriveAccessDeniedException>();
        await FluentActions.Invoking(()=>browser.GetItemAsync(intent.ProviderItemId!)).Should().ThrowAsync<AlFalah.Application.Common.Exceptions.TeacherDriveAccessDeniedException>();
        w.Drive.MoveExternally(intent.FolderItemId!,grant);
        await FluentActions.Invoking(()=>browser.ListAsync(new(intent.FolderItemId!,null,null,null,null))).Should().ThrowAsync<AlFalah.Application.Common.Exceptions.TeacherDriveAccessDeniedException>();
        var adminMappings=new TeacherDriveMappingService(db,new SchoolScopeGuard(db,manager,NullLogger<SchoolScopeGuard>.Instance),w.Drive,guard,audit,manager);
        await FluentActions.Invoking(()=>adminMappings.BrowseFoldersAsync(profile.Id,new(intent.FolderItemId!,null))).Should().ThrowAsync<AlFalah.Application.Common.Exceptions.TeacherDriveAccessDeniedException>();
    }
    private async Task Export(AlFalahDbContext db,World w,string name,int id,string? user=null)=>ExportBytes(name,await Reader(db,w,user:user).ContentAsync(id,false,default));
    private static void ExportBytes(string name,DutyPdf pdf)
    {
        var path=Environment.GetEnvironmentVariable("ALFALAH_DUTY_D3_PDF_DIR");if(string.IsNullOrWhiteSpace(path))return;
        Directory.CreateDirectory(path);File.WriteAllBytes(Path.Combine(path,name+".pdf"),pdf.Bytes);
        File.WriteAllText(Path.Combine(path,name+".json"),JsonSerializer.Serialize(new {pdf.Hash,pdf.RendererVersion,pdf.FileName,Bytes=pdf.Bytes.Length,Synthetic=true},new JsonSerializerOptions{WriteIndented=true}));
    }
}
