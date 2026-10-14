using System.Text.Json;
using AlFalah.Application.Duty;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace AlFalah.Tests.Duty;

[Collection("Duty SQL")]
public sealed class DutyExecutionSqlTests(DutySqlFixture fixture)
{
    public sealed record Scene(int School, int Term, int Assignment, string Manager, string Teacher, string Replacement, string Guardian,
        DateOnly Date, int Student, int Enrollment, int Classroom);
    private DutyExecutionService Service(AlFalahDbContext db, Scene s, string? user = null, string role = RoleNames.Instructor) =>
        new(new DutyRepository(db), new DutyExecutionRepository(db), new TeacherDriveHarness.TestCurrentUser(role, user ?? s.Teacher, s.School, true));
    private static string Signature(byte variant = 0)
    {
        var bytes = new byte[80]; new byte[] {137,80,78,71,13,10,26,10}.CopyTo(bytes,0); "IHDR"u8.CopyTo(bytes.AsSpan(12));
        bytes[19] = 100; bytes[23] = 50; bytes[79] = variant; return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }
    public async Task<Scene> Seed(bool guardian = true, int students = 1, bool timing = true)
    {
        await using var db = fixture.Db(); var tag=Guid.NewGuid().ToString("N");var manager=tag+"m";var teacher=tag+"t";var replacement=tag+"r";var parent=tag+"g";
        foreach(var id in new[]{manager,teacher,replacement,parent})db.Users.Add(new(){Id=id,UserName=id,FirstName="Synthetic",LastName=id[^1].ToString(),IsActive=true});
        if(!await db.Roles.AnyAsync(x=>x.Name==RoleNames.Guardian))db.Roles.Add(new(){Id=RoleNames.Guardian,Name=RoleNames.Guardian,NormalizedName=RoleNames.Guardian.ToUpperInvariant()});
        var school=new School{Name="D2 synthetic "+tag,City="Isolated",Stage=SchoolStage.Primary,ManagerUserId=manager};db.Add(school);
        var date=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow,"Africa/Cairo").DateTime);
        var year=new AcademicYear{Code=tag,NameAr="D2 synthetic",StartsOn=new(date.Year,1,1),EndsOn=new(date.Year,12,31),IsActive=true};db.Add(year);await db.SaveChangesAsync();
        var term=new AcademicTerm{SchoolId=school.Id,AcademicYearId=year.Id,Semester=TimetableSemester.First,StartsOn=year.StartsOn,EndsOn=year.EndsOn,IsActive=true,CreatedByUserId=manager,UpdatedByUserId=manager};db.Add(term);
        foreach(var (user,role) in new[]{(manager,RoleNames.SchoolManager),(teacher,RoleNames.Instructor),(replacement,RoleNames.Instructor),(parent,RoleNames.Guardian)})db.UserSchoolRoles.Add(new(){SchoolId=school.Id,UserId=user,RoleId=role});
        foreach(var user in new[]{teacher,replacement}){db.InstructorProfiles.Add(new(){SchoolId=school.Id,UserId=user,IsActive=true});db.UserSignatures.Add(new(){UserId=user,SignatureDrawnData=Signature(user==teacher?(byte)1:(byte)2)});}
        await db.SaveChangesAsync();
        var classroom=new Classroom{SchoolId=school.Id,AcademicYearId=year.Id,Stage=SchoolStage.Primary,GradeLevel=3,Section="A",ClassLabel="3/A",CreatedByUserId=manager,UpdatedByUserId=manager};db.Add(classroom);await db.SaveChangesAsync();
        var list=new List<(Student Student,StudentEnrollment Enrollment)>();
        for(var i=0;i<students;i++){
            var student=new Student{SchoolId=school.Id,StudentNumber="s"+i,IdentityNumber=tag+i,FirstName="Duplicate",LastName="Student",CreatedByUserId=manager,UpdatedByUserId=manager};db.Add(student);await db.SaveChangesAsync();
            var enrollment=new StudentEnrollment{SchoolId=school.Id,AcademicTermId=term.Id,ClassroomId=classroom.Id,StudentId=student.Id,EnrolledOn=term.StartsOn,CreatedByUserId=manager,UpdatedByUserId=manager};db.Add(enrollment);list.Add((student,enrollment));
        }
        var profile=new GuardianProfile{SchoolId=school.Id,ApplicationUserId=parent,CreatedByUserId=manager,UpdatedByUserId=manager};db.Add(profile);await db.SaveChangesAsync();
        if(guardian)foreach(var row in list)db.StudentGuardians.Add(new(){SchoolId=school.Id,StudentId=row.Student.Id,GuardianProfileId=profile.Id,RelationshipType=GuardianRelationshipType.Father,IsPrimary=true,ReceivesNotifications=true,ValidFrom=term.StartsOn,CreatedByUserId=manager,UpdatedByUserId=manager});
        await db.SaveChangesAsync();
        var settings=new DutySettings(term.Id,date,date.AddDays(2),[0,1,2,3,4,5,6],[teacher,replacement],[],[],date,date.AddDays(2),true);
        var calendar=new BellScheduleTemplate{SchoolId=school.Id,AcademicYearId=year.Id,Semester=term.Semester,Name="Synthetic execution calendar",CreatedByUserId=manager,UpdatedByUserId=manager};db.Add(calendar);await db.SaveChangesAsync();
        db.Add(new BellScheduleRevision{SchoolId=school.Id,BellScheduleTemplateId=calendar.Id,Revision=1,Name="Synthetic execution calendar",SchoolTimeZoneId="Africa/Cairo",CreatedByUserId=manager,
            Days=Enumerable.Range(1,7).Select(day=>new BellScheduleDay{Day=day,IsStudyDay=true}).ToList()});
        db.Add(new TimetableSetupProfile{SchoolId=school.Id,AcademicYearId=year.Id,Semester=term.Semester,Name="Synthetic execution setup",BellScheduleTemplateId=calendar.Id,CreatedByUserId=manager,UpdatedByUserId=manager});await db.SaveChangesAsync();
        var planning=new DutyService(new DutyRepository(db),new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,manager,school.Id,true));
        var preview=await planning.PreviewAsync(new(settings,0),default);await planning.PublishAsync(new(settings,0,preview.Digest,Guid.NewGuid()),default);
        var assignment=await db.Set<DutyAssignment>().SingleAsync(x=>x.SchoolId==school.Id&&x.Date==date);
        var s=new Scene(school.Id,term.Id,assignment.Id,manager,teacher,replacement,parent,date,list[0].Student.Id,list[0].Enrollment.Id,classroom.Id);
        if(timing)await Service(db,s,manager,RoleNames.SchoolManager).TimingExceptionAsync(s.Assignment,new(assignment.Revision,new TimeOnly(0,0),"Synthetic timing fixture",Guid.NewGuid()),default);
        return s;
    }
    private async Task<DutyExecution> Ack(AlFalahDbContext db,Scene s)=>await Service(db,s).AcknowledgeAsync(s.Assignment,new((await Service(db,s).GetAsync(s.Assignment,default)).AssignmentRevision,Guid.NewGuid()),default);
    private static DutyLateRequest Late(Scene s,int revision=1)=>new(revision,s.Student,s.Enrollment,new(0,1),null,"Arrange pickup","Synthetic observation",Guid.NewGuid());
    private async Task Process(AlFalahDbContext db,Scene s)
    {
        var processor=new DutyNoticeProcessor(new DutyRepository(db),new DutyExecutionRepository(db));
        foreach(var batch in await db.Set<DismissalNoticeBatch>().AsNoTracking().Where(x=>x.SchoolId==s.School).OrderBy(x=>x.Id).ToListAsync())await processor.ProcessAsync(batch,default);
    }
    [DutySqlFact] public async Task Opening_letter_does_not_sign_and_actual_teacher_signature_is_frozen_across_profile_changes()
    {
        var s=await Seed();await using var db=fixture.Db();var service=Service(db,s);
        (await service.GetAsync(s.Assignment,default)).Acknowledged.Should().BeFalse();
        (await db.Set<DutySignedDocument>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(0);
        var ack=await Ack(db,s);ack.Acknowledged.Should().BeTrue();
        var original=await db.Set<DutySignedDocument>().AsNoTracking().SingleAsync(x=>x.SchoolId==s.School);
        db.ChangeTracker.Clear();var signature=await db.UserSignatures.SingleAsync(x=>x.UserId==s.Teacher);signature.SignatureDrawnData=Signature(9);
        var user=await db.Users.SingleAsync(x=>x.Id==s.Teacher);user.FirstName="Changed";await db.SaveChangesAsync();
        var frozen=await db.Set<DutySignedDocument>().AsNoTracking().SingleAsync(x=>x.Id==original.Id);
        frozen.SignatureBytes.Should().Equal(original.SignatureBytes);frozen.SnapshotJson.Should().Be(original.SnapshotJson);frozen.AuthorUserId.Should().Be(s.Teacher);
        await FluentActions.Invoking(()=>Service(db,s,s.Manager,RoleNames.SchoolManager).AcknowledgeAsync(s.Assignment,new(1,Guid.NewGuid()),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
        (await db.Set<DutyArchiveIntent>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(1);
        await FluentActions.Invoking(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE DutySignedDocuments SET SnapshotJson='changed' WHERE Id={frozen.Id}")).Should().ThrowAsync<Exception>();
    }
    [DutySqlFact] public async Task Missing_signature_retains_server_draft_and_concurrent_save_retains_winner()
    {
        var s=await Seed();await using var db=fixture.Db();var service=Service(db,s);var original=await service.GetAsync(s.Assignment,default);
        var content=new DutyReportContent(true,true,[],"Saved before signature",null);
        var saved=await service.SaveDraftAsync(s.Assignment,new(original.AssignmentRevision,0,content),default);
        var sig=await db.UserSignatures.SingleAsync(x=>x.UserId==s.Teacher);db.Remove(sig);await db.SaveChangesAsync();
        await FluentActions.Invoking(()=>service.AcknowledgeAsync(s.Assignment,new(original.AssignmentRevision,Guid.NewGuid()),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutySignatureRequired");
        (await service.GetAsync(s.Assignment,default)).Draft!.Content.Observations.Should().Be(content.Observations);
        var next=await service.SaveDraftAsync(s.Assignment,new(original.AssignmentRevision,saved.Draft!.Version,content with{Observations="Winner"}),default);
        await FluentActions.Invoking(()=>service.SaveDraftAsync(s.Assignment,new(original.AssignmentRevision,saved.Draft.Version,content with{Observations="Loser"}),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==409);
        (await service.GetAsync(s.Assignment,default)).Draft!.Content.Observations.Should().Be("Winner");
        next.Draft!.SavedAt.Should().BeOnOrAfter(saved.Draft.SavedAt);
        (await db.Set<DismissalNoticeBatch>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(0);
    }
    [DutySqlFact] public async Task Explicit_empty_categories_and_departure_validation_and_lost_submit_response_are_real()
    {
        var s=await Seed();await using var db=fixture.Db();var service=Service(db,s);var ack=await Ack(db,s);
        var saved=await service.SaveDraftAsync(s.Assignment,new(ack.AssignmentRevision,0,new(false,false,[],"No observations",new TimeOnly(0,3))),default);
        await FluentActions.Invoking(()=>service.SubmitAsync(s.Assignment,new(ack.AssignmentRevision,Guid.NewGuid(),saved.Draft!.Version),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyIncompleteReport");
        saved=await service.SaveDraftAsync(s.Assignment,new(ack.AssignmentRevision,saved.Draft!.Version,new(true,true,[],"No observations",new TimeOnly(0,3))),default);
        var request=new DutySignRequest(ack.AssignmentRevision,Guid.NewGuid(),saved.Draft!.Version);
        var submitted=await service.SubmitAsync(s.Assignment,request,default);var replay=await service.SubmitAsync(s.Assignment,request,default);
        replay.Draft!.SubmittedAt.Should().Be(submitted.Draft!.SubmittedAt);submitted.Draft.SubmittedAt.Should().BeAfter(s.Date.ToDateTime(TimeOnly.MinValue));
        (await db.Set<DutySignedDocument>().CountAsync(x=>x.SchoolId==s.School&&x.Kind=="Report")).Should().Be(1);
        (await db.Set<DutyArchiveIntent>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(2);
        await FluentActions.Invoking(()=>service.SubmitAsync(s.Assignment,request with{DraftVersion=999},default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyIdempotencyConflict");
    }
    [DutySqlFact] public async Task Roster_is_whole_school_minimal_paginated_and_more_than_seven_report_rows_are_frozen()
    {
        var s=await Seed(students:30);await using var db=fixture.Db();var service=Service(db,s);var ack=await Ack(db,s);
        var roster=await service.RosterAsync(new(s.Assignment,"Duplicate",3,s.Classroom),default);
        roster.Students.TotalCount.Should().Be(30);roster.Students.Items.Count.Should().Be(24);
        var second=await service.RosterAsync(new(s.Assignment,null,null,null,2),default);second.Students.Items.Count.Should().Be(6);
        var json=JsonSerializer.Serialize(roster);json.Should().NotContain("IdentityNumber").And.NotContain("Guardian").And.NotContain("NationalId");
        var lines=roster.Students.Items.Take(9).Select(x=>new DutyReportLine(x.StudentId,x.EnrollmentId,new(0,1),"Observation","", "Forged name", "Forged class")).ToArray();
        var saved=await service.SaveDraftAsync(s.Assignment,new(ack.AssignmentRevision,0,new(true,false,lines,"Overflow",new TimeOnly(0,2))),default);
        saved.Draft!.Content.Violations.Should().HaveCount(9);saved.Draft.Content.Violations[0].StudentName.Should().Be("Duplicate  Student");
        await service.SubmitAsync(s.Assignment,new(ack.AssignmentRevision,Guid.NewGuid(),saved.Draft.Version),default);
        var report=await db.Set<DutySignedDocument>().SingleAsync(x=>x.SchoolId==s.School&&x.Kind=="Report");
        using var snapshot=JsonDocument.Parse(report.SnapshotJson);snapshot.RootElement.GetProperty("Payload").GetProperty("Violations").GetArrayLength().Should().Be(9);
    }
    [DutySqlFact] public async Task Explicit_confirmation_is_unique_under_concurrency_and_history_statistics_precede_submission()
    {
        var s=await Seed();await using(var db=fixture.Db())await Ack(db,s);
        async Task<DutyLateDto> Confirm(){await using var db=fixture.Db();return await Service(db,s).ConfirmLateAsync(s.Assignment,Late(s),default);}
        var results=await Task.WhenAll(Confirm(),Confirm());results[0].Id.Should().Be(results[1].Id);
        await using var read=fixture.Db();
        (await read.Set<StudentDismissalLateness>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(1);
        (await read.Set<DismissalLatenessRevision>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(1);
        (await read.Set<DismissalNoticeBatch>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(1);
        (await new StudentWorkflowRepository(read).GetStudentTimelineAsync(s.School,s.Student,new(),default)).Items.Single().FactId.Should().Be(results[0].Id);
        var stats=await Service(read,s,s.Manager,RoleNames.SchoolManager).StatisticsAsync(new(s.Date,s.Date),default);stats.Incidents.Should().Be(1);stats.UniqueStudents.Should().Be(1);
        var overview=await new SchoolManagerOperationsRepository(read).ReadAsync(new(s.School,s.Date,DateTimeOffset.UtcNow,true,false,false),default);overview.DismissalLatenessToday.Should().Be(1);
        (await read.Set<DutySignedDocument>().CountAsync(x=>x.SchoolId==s.School&&x.Kind=="Report")).Should().Be(0);
        (await read.MorningArrivalDelays.CountAsync(x=>x.SchoolId==s.School)).Should().Be(0);
    }
    [DutySqlFact] public async Task Guardian_dual_surface_delivery_is_system_authored_deduplicated_and_read_independent()
    {
        var s=await Seed();await using var db=fixture.Db();await Ack(db,s);var fact=await Service(db,s).ConfirmLateAsync(s.Assignment,Late(s),default);
        await Process(db,s);await Process(db,s);
        var message=await db.ConversationMessages.AsNoTracking().SingleAsync(x=>x.SchoolId==s.School);
        message.Body.Should().Contain("Please arrange pickup.").And.NotContain("The student departed");
        message.SenderUserId.Should().BeNull();message.IsSystemOrigin.Should().BeTrue();message.SourceFactId.Should().Be(fact.Id);message.SourceFactRevision.Should().Be(1);message.NextEligibleSendAt.Should().BeNull();
        (await db.Notifications.CountAsync(x=>x.SchoolId==s.School&&x.Type=="DismissalLateness")).Should().Be(1);
        (await db.Notifications.CountAsync(x=>x.SchoolId==s.School&&x.Type=="ConversationMessage")).Should().Be(0);
        var notices=await Service(db,s,s.Guardian,RoleNames.Guardian).GuardianNoticesAsync(default);notices.Single().MessageRoute.Should().Contain("conversationId=");
        await Service(db,s,s.Guardian,RoleNames.Guardian).MarkGuardianNoticeReadAsync(notices[0].NotificationId,default);
        (await db.MessageReceipts.SingleAsync(x=>x.SchoolId==s.School)).ReadAt.Should().BeNull();
        var messaging=new MessagingWorkflowRepository(db,null!,TimeProvider.System);
        var messages=await messaging.GetConversationMessagesAsync(s.School,s.Guardian,message.ConversationThreadId,new(),default);
        messages.Items.Single().Sender.RoleSnapshot.Should().Be("System");
        await messaging.MarkConversationReadAsync(s.School,s.Guardian,message.ConversationThreadId,message.Id,default);
        (await db.MessageReceipts.SingleAsync(x=>x.SchoolId==s.School)).ReadAt.Should().NotBeNull();
        var status=await new DutyExecutionRepository(db).NoticeStatusAsync(s.School,fact.Id,default);status.Single().DashboardRead.Should().Be(1);status.Single().MessagesRead.Should().Be(1);
        (await messaging.IsParticipantAsync(s.School,s.Teacher,message.ConversationThreadId,default)).Should().BeFalse();
        (await messaging.IsParticipantAsync(s.School+10000,s.Guardian,message.ConversationThreadId,default)).Should().BeFalse();
        await FluentActions.Invoking(()=>messaging.SendMessageAsync(s.School,s.Guardian,message.ConversationThreadId,new("Spoof System",null,Guid.NewGuid().ToString()),default)).Should().ThrowAsync<InvalidOperationException>();
    }
    [DutySqlFact] public async Task Effective_expired_missing_guardians_and_revoked_thread_access_are_enforced()
    {
        var s=await Seed(guardian:false);await using var db=fixture.Db();await Ack(db,s);var fact=await Service(db,s).ConfirmLateAsync(s.Assignment,Late(s),default);await Process(db,s);
        (await new DutyExecutionRepository(db).NoticeStatusAsync(s.School,fact.Id,default)).Single().State.Should().Be("NoEligibleGuardian");
        var gp=await db.GuardianProfiles.SingleAsync(x=>x.SchoolId==s.School);db.StudentGuardians.Add(new(){SchoolId=s.School,StudentId=s.Student,GuardianProfileId=gp.Id,RelationshipType=GuardianRelationshipType.Father,ValidFrom=s.Date.AddDays(-2),ValidTo=s.Date.AddDays(-1),CreatedByUserId=s.Manager,UpdatedByUserId=s.Manager});await db.SaveChangesAsync();
        await Process(db,s);(await db.ConversationMessages.CountAsync(x=>x.SchoolId==s.School)).Should().Be(0);
        var link=await db.StudentGuardians.SingleAsync(x=>x.SchoolId==s.School);link.ValidTo=null;await db.SaveChangesAsync();await Process(db,s);
        var message=await db.ConversationMessages.SingleAsync(x=>x.SchoolId==s.School);var messaging=new MessagingWorkflowRepository(db,null!,TimeProvider.System);
        (await messaging.IsParticipantAsync(s.School,s.Guardian,message.ConversationThreadId,default)).Should().BeTrue();
        link=await db.StudentGuardians.SingleAsync(x=>x.SchoolId==s.School);link.ValidTo=s.Date.AddDays(-1);await db.SaveChangesAsync();
        (await messaging.IsParticipantAsync(s.School,s.Guardian,message.ConversationThreadId,default)).Should().BeFalse();
        (await Service(db,s,s.Guardian,RoleNames.Guardian).GuardianNoticesAsync(default)).Should().BeEmpty();
    }
    [DutySqlFact] public async Task Fact_correction_retraction_retains_versions_notices_and_opens_teacher_resignature()
    {
        var s=await Seed();await using var db=fixture.Db();var service=Service(db,s);var ack=await Ack(db,s);var fact=await service.ConfirmLateAsync(s.Assignment,Late(s),default);await Process(db,s);
        var saved=await service.SaveDraftAsync(s.Assignment,new(ack.AssignmentRevision,0,new(false,true,[],"Initial",new TimeOnly(0,2))),default);
        await service.SubmitAsync(s.Assignment,new(ack.AssignmentRevision,Guid.NewGuid(),saved.Draft!.Version),default);
        var prior=(await db.Set<DutySignedDocument>().AsNoTracking().SingleAsync(x=>x.SchoolId==s.School&&x.Kind=="Report")).SnapshotJson;
        var manager=Service(db,s,s.Manager,RoleNames.SchoolManager);
        await manager.CorrectLateAsync(fact.Id,new(fact.Revision,new(0,1),null,"Correction","","Reasoned retraction",true,Guid.NewGuid()),default);
        (await manager.StatisticsAsync(new(s.Date,s.Date),default)).Incidents.Should().Be(0);
        var correction=await service.GetAsync(s.Assignment,default);correction.Draft!.SubmittedAt.Should().BeNull();correction.Draft.CorrectionReason.Should().Be("Reasoned retraction");
        correction=await service.SaveDraftAsync(s.Assignment,new(ack.AssignmentRevision,correction.Draft.Version,correction.Draft.Content with{NoLateStudents=true}),default);
        await service.SubmitAsync(s.Assignment,new(ack.AssignmentRevision,Guid.NewGuid(),correction.Draft!.Version),default);await Process(db,s);
        (await db.Set<DutySignedDocument>().AsNoTracking().Where(x=>x.SchoolId==s.School&&x.Kind=="Report").OrderBy(x=>x.Id).FirstAsync()).SnapshotJson.Should().Be(prior);
        (await db.Set<DismissalLatenessRevision>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(2);
        (await db.ConversationMessages.CountAsync(x=>x.SchoolId==s.School)).Should().Be(2);
        (await db.Notifications.CountAsync(x=>x.SchoolId==s.School&&x.Type=="DismissalLateness")).Should().Be(2);
    }
    [DutySqlFact] public async Task Emergency_reassignment_preserves_authorship_and_blocks_stale_teacher_mutation()
    {
        var s=await Seed();await using var db=fixture.Db();var ack=await Ack(db,s);var service=Service(db,s);
        await service.SaveDraftAsync(s.Assignment,new(ack.AssignmentRevision,0,new(true,true,[],"Original draft",null)),default);
        var planning=new DutyService(new DutyRepository(db),new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,s.Manager,s.School,true));
        var workspace=await planning.WorkspaceAsync(s.Term,default);
        await planning.ChangeAsync(new(s.Assignment,workspace.PlanRevision,s.Replacement,null,"Reviewed emergency","Emergency",true,null,Guid.NewGuid()),default);
        await FluentActions.Invoking(()=>service.SaveDraftAsync(s.Assignment,new(ack.AssignmentRevision,1,new(true,true,[],"Stale",null)),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status == 403 || x.Status == 409);
        var replacement=Service(db,s,s.Replacement);var current=await replacement.GetAsync(s.Assignment,default);current.Acknowledged.Should().BeFalse();current.Draft.Should().BeNull();current.Documents.Should().BeEmpty();
        await replacement.AcknowledgeAsync(s.Assignment,new(current.AssignmentRevision,Guid.NewGuid()),default);
        var letters=await db.Set<DutySignedDocument>().AsNoTracking().Where(x=>x.SchoolId==s.School).OrderBy(x=>x.Id).ToListAsync();letters.Should().HaveCount(2);
        letters[0].AuthorUserId.Should().Be(s.Teacher);letters[1].AuthorUserId.Should().Be(s.Replacement);letters[0].SignatureHash.Should().NotBe(letters[1].SignatureHash);
        (await service.GetAsync(s.Assignment,default)).Draft!.Content.Observations.Should().Be("Original draft");
    }
    private sealed class FailOn<T> : SaveChangesInterceptor where T:class
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(eventData.Context!.ChangeTracker.Entries<T>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Synthetic persistence failure");
            return ValueTask.FromResult(result);
        }
    }
    [DutySqlFact] public async Task Confirmation_rolls_back_fact_audit_and_intent_atomically()
    {
        var s=await Seed();await using(var db=fixture.Db())await Ack(db,s);
        var options=new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(fixture.Connection).AddInterceptors(new FailOn<DismissalNoticeBatch>()).Options;
        await using(var failed=new AlFalahDbContext(options))await FluentActions.Invoking(()=>Service(failed,s).ConfirmLateAsync(s.Assignment,Late(s),default)).Should().ThrowAsync<InvalidOperationException>();
        await using var read=fixture.Db();(await read.Set<StudentDismissalLateness>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(0);
        (await read.Set<DismissalLatenessRevision>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(0);
        (await read.Set<DismissalNoticeBatch>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(0);
    }
    [DutySqlFact] public async Task Dashboard_success_message_failure_retries_only_missing_surface()
    {
        var s=await Seed();await using(var db=fixture.Db()){await Ack(db,s);await Service(db,s).ConfirmLateAsync(s.Assignment,Late(s),default);}
        var options=new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(fixture.Connection).AddInterceptors(new FailOn<ConversationMessage>()).Options;
        await using(var failed=new AlFalahDbContext(options))await Process(failed,s);
        await using var read=fixture.Db();var status=await new DutyExecutionRepository(read).NoticeStatusAsync(s.School,(await read.Set<StudentDismissalLateness>().SingleAsync(x=>x.SchoolId==s.School)).Id,default);
        status.Single().DashboardDelivered.Should().Be(1);status.Single().MessagesDelivered.Should().Be(0);status.Single().State.Should().Be("RetryScheduled");
        await Process(read,s);await Process(read,s);
        (await read.Notifications.CountAsync(x=>x.SchoolId==s.School&&x.Type=="DismissalLateness")).Should().Be(1);(await read.ConversationMessages.CountAsync(x=>x.SchoolId==s.School)).Should().Be(1);
    }
    [DutySqlFact] public async Task Forged_student_enrollment_class_and_school_are_rejected_without_profile_disclosure()
    {
        var s=await Seed();var other=await Seed();await using var db=fixture.Db();await Ack(db,s);
        await FluentActions.Invoking(()=>Service(db,s).ConfirmLateAsync(s.Assignment,Late(s) with{StudentId=other.Student,EnrollmentId=other.Enrollment},default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyRosterUnavailable");
        (await Service(db,s).RosterAsync(new(s.Assignment,null,null,other.Classroom),default)).Students.TotalCount.Should().Be(0);
        await FluentActions.Invoking(()=>Service(db,s).GetAsync(other.Assignment,default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==404);
        await FluentActions.Invoking(()=>Service(db,s,s.Replacement).RosterAsync(new(s.Assignment),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
    }
    [DutySqlFact] public async Task Missing_dismissal_never_fabricates_reference_or_notice_and_observation_departure_must_be_real()
    {
        var s=await Seed(timing:false);await using var db=fixture.Db();var service=Service(db,s);var ack=await Ack(db,s);
        ack.DismissalAt.Should().BeNull();
        await FluentActions.Invoking(()=>service.ConfirmLateAsync(s.Assignment,Late(s),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyDismissalMissing");
        (await db.Set<DismissalNoticeBatch>().CountAsync(x=>x.SchoolId==s.School)).Should().Be(0);
        await Service(db,s,s.Manager,RoleNames.SchoolManager).TimingExceptionAsync(s.Assignment,new(ack.AssignmentRevision,new(0,0),"Reviewed missing timing",Guid.NewGuid()),default);
        await FluentActions.Invoking(()=>service.ConfirmLateAsync(s.Assignment,Late(s) with{DepartureAt=new TimeOnly(0,0)},default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyDepartureInvalid");
        await FluentActions.Invoking(()=>service.ConfirmLateAsync(s.Assignment,Late(s) with{ObservedAt=new TimeOnly(0,0)},default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyNotDismissalLate");
        var zone=TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");var local=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,zone);
        if(local.Hour<23){var future=TimeOnly.FromDateTime(local.DateTime.AddHours(1));await FluentActions.Invoking(()=>service.ConfirmLateAsync(s.Assignment,Late(s) with{ObservedAt=future},default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyFutureObservation");}
    }
    [DutySqlFact] public async Task Real_bell_reference_enforces_last_departure_and_reasoned_manager_exception_is_required()
    {
        var s=await Seed(timing:false);await using var db=fixture.Db();
        var term=await db.AcademicTerms.SingleAsync(x=>x.Id==s.Term);var template=new BellScheduleTemplate{SchoolId=s.School,AcademicYearId=term.AcademicYearId,Semester=term.Semester,Name="Synthetic bell",CreatedByUserId=s.Manager,UpdatedByUserId=s.Manager};db.Add(template);await db.SaveChangesAsync();
        var revision=new BellScheduleRevision{SchoolId=s.School,BellScheduleTemplateId=template.Id,Revision=1,Name="Synthetic bell",SchoolTimeZoneId="Africa/Cairo",CreatedByUserId=s.Manager};
        var day=new BellScheduleDay{Day=((int)s.Date.DayOfWeek+1)%7+1,IsStudyDay=true,UsesDefaultSchedule=false};day.Periods.Add(new(){Sequence=1,StartLocalTime=new(0,0),EndLocalTime=new(0,1)});revision.Days.Add(day);db.Add(revision);await db.SaveChangesAsync();
        var plan=await db.Set<DutyPlan>().SingleAsync(x=>x.SchoolId==s.School);plan.BellScheduleRevisionId=revision.Id;await db.SaveChangesAsync();
        var service=Service(db,s);var ack=await Ack(db,s);ack.DismissalAt.Should().Be(new TimeOnly(0,1));
        await service.ConfirmLateAsync(s.Assignment,Late(s) with{ObservedAt=new(0,3)},default);
        var draft=await service.SaveDraftAsync(s.Assignment,new(ack.AssignmentRevision,0,new(false,true,[],"Before observation",new TimeOnly(0,2))),default);
        await FluentActions.Invoking(()=>service.SubmitAsync(s.Assignment,new(ack.AssignmentRevision,Guid.NewGuid(),draft.Draft!.Version),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyDepartureInvalid");
        await Service(db,s,s.Manager,RoleNames.SchoolManager).TimingExceptionAsync(s.Assignment,new(ack.AssignmentRevision,null,"Reviewed clock discrepancy",Guid.NewGuid()),default);
        (await service.SubmitAsync(s.Assignment,new(ack.AssignmentRevision,Guid.NewGuid(),draft.Draft!.Version),default)).Draft!.SubmittedAt.Should().NotBeNull();
    }
    [DutySqlFact] public async Task Attendance_correction_and_regeneration_cannot_silently_replace_acknowledged_execution()
    {
        var s=await Seed();await using var db=fixture.Db();await Ack(db,s);
        db.AttendanceRecords.Add(new(){SchoolId=s.School,UserId=s.Teacher,AttendanceDate=s.Date,Status=AttendanceStatus.Absent,RecordedByUserId=s.Manager});await db.SaveChangesAsync();
        var state=await new DutyRepository(db).LoadAsync(s.School,default);new DutyReconciler(new DutyRepository(db)).Reconcile(state,s.School,s.Date);await db.SaveChangesAsync();
        var assignment=await db.Set<DutyAssignment>().SingleAsync(x=>x.Id==s.Assignment);assignment.EffectiveTeacherId.Should().Be(s.Teacher);assignment.ExceptionCode.Should().Be("ManualAbsenceReview");
        (await db.Set<DutySignedDocument>().SingleAsync(x=>x.SchoolId==s.School)).AuthorUserId.Should().Be(s.Teacher);
        var planner=new DutyService(new DutyRepository(db),new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,s.Manager,s.School,true));
        var workspace=await planner.WorkspaceAsync(s.Term,default);var preview=await planner.PreviewAsync(new(workspace.Settings! with{TeacherIds=new[]{s.Replacement,s.Teacher}},workspace.PlanRevision),default);
        preview.Days.Single(x=>x.Date==s.Date).Preserved.Should().BeTrue();
    }
    [DutySqlFact] public async Task System_origin_payload_forgery_is_rejected_by_public_message_contract()
    {
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        FluentActions.Invoking(()=>JsonSerializer.Deserialize<SendMessageRequestDto>("{\"body\":\"spoof\",\"replyToMessageId\":null,\"idempotencyKey\":\"abcdefgh\",\"isSystemOrigin\":true,\"senderUserId\":\"system\"}",options)).Should().Throw<JsonException>();
        var s=await Seed();await using var db=fixture.Db();await Ack(db,s);await Service(db,s).ConfirmLateAsync(s.Assignment,Late(s),default);await Process(db,s);
        var message=await db.ConversationMessages.SingleAsync(x=>x.SchoolId==s.School);message.SenderUserId=s.Teacher;
        await FluentActions.Invoking(()=>db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }
    [DutySqlFact] public async Task Message_success_dashboard_failure_stays_truthful_and_retries_only_dashboard()
    {
        var s=await Seed();await using(var db=fixture.Db()){await Ack(db,s);await Service(db,s).ConfirmLateAsync(s.Assignment,Late(s),default);}
        var options=new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(fixture.Connection).AddInterceptors(new FailOn<Notification>()).Options;
        await using(var failed=new AlFalahDbContext(options))await Process(failed,s);
        await using var read=fixture.Db();var fact=await read.Set<StudentDismissalLateness>().SingleAsync(x=>x.SchoolId==s.School);
        var statuses=await new DutyExecutionRepository(read).NoticeStatusAsync(s.School,fact.Id,default);
        statuses.Single().State.Should().Be("RetryScheduled");statuses.Single().MessagesDelivered.Should().Be(1);statuses.Single().DashboardDelivered.Should().Be(0);
        await Service(read,s,s.Manager,RoleNames.SchoolManager).RetryNoticesAsync(fact.Id,default);
        (await read.Set<DismissalNoticeBatch>().SingleAsync(x=>x.SchoolId==s.School)).NextAttemptAt.Should().BeOnOrBefore(DateTimeOffset.UtcNow);
        await Process(read,s);await Process(read,s);
        (await read.ConversationMessages.CountAsync(x=>x.SchoolId==s.School)).Should().Be(1);
        (await read.Notifications.CountAsync(x=>x.SchoolId==s.School&&x.Type=="DismissalLateness")).Should().Be(1);
    }
    [DutySqlFact] public async Task Correction_reason_teacher_resignature_and_retained_student_timeline_are_required()
    {
        var s=await Seed();await using var db=fixture.Db();var teacher=Service(db,s);var ack=await Ack(db,s);
        var fact=await teacher.ConfirmLateAsync(s.Assignment,Late(s),default);
        var draft=await teacher.SaveDraftAsync(s.Assignment,new(ack.AssignmentRevision,0,new(false,true,[],"Original",new(0,2))),default);
        var submitted=await teacher.SubmitAsync(s.Assignment,new(ack.AssignmentRevision,Guid.NewGuid(),draft.Draft!.Version),default);
        var manager=Service(db,s,s.Manager,RoleNames.SchoolManager);
        await FluentActions.Invoking(()=>manager.CorrectReportAsync(s.Assignment,new(ack.AssignmentRevision,submitted.Draft!.Version,"",Guid.NewGuid()),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Code=="DutyReasonRequired");
        var corrected=await manager.CorrectReportAsync(s.Assignment,new(ack.AssignmentRevision,submitted.Draft!.Version,"Approved correction",Guid.NewGuid()),default);
        corrected.Draft!.SubmittedAt.Should().BeNull();
        await FluentActions.Invoking(()=>manager.SubmitAsync(s.Assignment,new(ack.AssignmentRevision,Guid.NewGuid(),corrected.Draft.Version),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
        await manager.CorrectLateAsync(fact.Id,new(fact.Revision,new(0,1),new(0,2),"Pickup completed","","Reasoned fact correction",false,Guid.NewGuid()),default);
        var notice=await db.Set<DismissalNoticeBatch>().AsNoTracking().SingleAsync(x=>x.SchoolId==s.School&&x.FactId==fact.Id&&x.Revision==2);
        notice.Content.Should().Contain("The student departed at 00:02.").And.NotContain("Please arrange pickup.");
        var timeline=await new StudentWorkflowRepository(db).GetStudentTimelineAsync(s.School,s.Student,new(),default);
        timeline.Items.Should().HaveCount(2);timeline.Items.First().Description.Should().Contain("Reasoned fact correction");timeline.Items.Last().Title.Should().Contain("Retained version");
        timeline.Items.Select(x=>x.FactId).Should().OnlyContain(x=>x==fact.Id);
    }
    [DutySqlFact] public async Task Range_statistics_distinguish_incidents_from_distinct_students_with_term_class_and_page_filters()
    {
        var s=await Seed(students:26);await using var db=fixture.Db();var teacher=Service(db,s);await Ack(db,s);await teacher.ConfirmLateAsync(s.Assignment,Late(s),default);
        // Synthetic historical data represents an earlier operating day; no external notice is dispatched.
        var current=await db.Set<StudentDismissalLateness>().AsNoTracking().SingleAsync(x=>x.SchoolId==s.School);
        var plan=await db.Set<DutyPlan>().SingleAsync(x=>x.SchoolId==s.School);
        var previous=new DutyAssignment{SchoolId=s.School,DutyPlanId=plan.Id,Date=s.Date.AddDays(-1),OriginalTeacherId=s.Teacher,EffectiveTeacherId=s.Teacher};db.Add(previous);await db.SaveChangesAsync();
        foreach(var enrollment in await db.StudentEnrollments.AsNoTracking().Where(x=>x.SchoolId==s.School).ToListAsync())
            db.Add(new StudentDismissalLateness{SchoolId=s.School,StudentId=enrollment.StudentId,EnrollmentId=enrollment.Id,ClassroomId=enrollment.ClassroomId,AcademicTermId=s.Term,
                AssignmentId=previous.Id,AssignmentRevision=1,DutyDate=previous.Date,AuthorUserId=s.Teacher,StudentName="Synthetic historical student",Classroom="3/A",ObservedAt=new(0,1),DismissalAt=new(0,0),TimeZoneId="Africa/Cairo",Action="Synthetic historical fact",RecordedAt=DateTimeOffset.UtcNow});
        await db.SaveChangesAsync();
        var manager=Service(db,s,s.Manager,RoleNames.SchoolManager);
        var range=await manager.StatisticsAsync(new(s.Date.AddDays(-1),s.Date,s.Term,s.Classroom),default);
        range.Incidents.Should().Be(27);range.UniqueStudents.Should().Be(26);range.Items.Items.Should().HaveCount(24);
        (await manager.StatisticsAsync(new(s.Date.AddDays(-1),s.Date,s.Term,s.Classroom,2),default)).Items.Items.Should().HaveCount(3);
        (await manager.StatisticsAsync(new(s.Date.AddDays(-1),s.Date,s.Term+10000,s.Classroom),default)).Incidents.Should().Be(0);
    }
    [Fact] public async Task Signing_assets_freeze_embedded_bytes_and_do_not_fetch_remote_or_outside_files()
    {
        var environment=new Microsoft.Extensions.Hosting.Internal.HostingEnvironment{ContentRootPath=Path.GetTempPath()};
        var assets=new AlFalah.Infrastructure.Services.DutyDocumentAssets(environment);
        var frozen=await assets.FreezeLogoAsync(Signature(1),default);frozen.State.Should().Be("Frozen");
        var changed=await assets.FreezeLogoAsync(Signature(9),default);changed.Hash.Should().NotBe(frozen.Hash);
        frozen.Data.Should().Be(Signature(1));
        (await assets.FreezeLogoAsync("https://example.invalid/mutable-logo.png",default)).State.Should().Be("UnavailableAtSigning");
        (await assets.FreezeLogoAsync("/uploads/../../private.png",default)).Data.Should().BeNull();
    }
    [DutySqlFact] public async Task Repeated_emergency_return_keeps_the_earlier_unsigned_draft_as_immutable_history()
    {
        var s=await Seed();await using var db=fixture.Db();var teacher=Service(db,s);var ack=await Ack(db,s);
        await teacher.SaveDraftAsync(s.Assignment,new(ack.AssignmentRevision,0,new(true,true,[],"Earlier A draft",null)),default);
        var planner=new DutyService(new DutyRepository(db),new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,s.Manager,s.School,true));
        async Task Reassign(string user){var context=await planner.WorkspaceAsync(s.Term,default);await planner.ChangeAsync(new(s.Assignment,context.PlanRevision,user,null,"Reviewed emergency return","Emergency",true,null,Guid.NewGuid()),default);}
        await Reassign(s.Replacement);var b=Service(db,s,s.Replacement);var replacement=await b.GetAsync(s.Assignment,default);
        await b.AcknowledgeAsync(s.Assignment,new(replacement.AssignmentRevision,Guid.NewGuid()),default);
        await Reassign(s.Teacher);var returned=await teacher.GetAsync(s.Assignment,default);returned.Acknowledged.Should().BeFalse();
        await teacher.SaveDraftAsync(s.Assignment,new(returned.AssignmentRevision,returned.Draft!.Version,returned.Draft.Content with{Observations="New A draft"}),default);
        var view=await teacher.GetAsync(s.Assignment,default);view.Draft!.Content.Observations.Should().Be("New A draft");
        view.PriorDrafts.Should().Contain(x=>x.AuthorUserId==s.Teacher&&x.Content.Observations=="Earlier A draft");
        (await db.Set<DutyAssignmentChange>().AsNoTracking().Where(x=>x.SchoolId==s.School&&x.Kind=="Emergency").FirstAsync()).SettingsSnapshotJson.Should().Contain("Earlier A draft");
    }
}
