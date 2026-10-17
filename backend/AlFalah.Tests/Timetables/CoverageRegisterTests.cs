using AlFalah.Application.IntelligentTimetable;
using AlFalah.Domain.Entities;
using FluentAssertions;
using Xunit;
using System.Security.Cryptography;
using System.Text;
using AlFalah.Infrastructure.Services;
using AlFalah.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

namespace AlFalah.Tests.Timetables;

public sealed class CoverageRegisterTests
{
    internal static CoverageRegisterHeader Header => new("مدرسة الاختبار", "1448 - 1449", 1, null);
    [Fact]
    public void Unsent_draft_is_never_exported_as_executed_work_or_a_signature()
    {
        var need = new DailyCoverageNeed { Id = 1, SchoolId = 1, LocalDate = new(2026,10,18), PeriodsJson = "[1,2]",
            OriginalTeacherId = 1, OriginalTeacherName = "المعلم الغائب", AssignedUserId = "teacher", AssignedTeacherName = "البديل",
            ActiveSubstitutionId = 4, State = "Acknowledged", Narrative = "مسودة لم تُرسل", Notes = "ملاحظات خاصة بالمسودة" };
        var result = CoverageRegisterProjector.Project(Header, need.LocalDate, [need]);
        result.Rows.Should().HaveCount(2); result.IsComplete.Should().BeFalse();
        result.Rows.Should().OnlyContain(r => r.Narrative == "" && r.Notes == "" && r.Execution == null && r.Receipt == null);
    }
    [Fact]
    public void Only_current_assignment_evidence_is_exported_and_correction_keeps_last_signed_work()
    {
        var n = Need(); n.Records.Add(Evidence(1,"Acknowledgement")); n.Records.Add(Evidence(2,"Execution"));
        n.Records.Add(Evidence(3,"Execution",substitution:3));
        n.Records.Add(Evidence(4,"Execution",actor:"unrelated"));
        var signed = CoverageRegisterProjector.Project(Header,n.LocalDate,[n]);
        signed.IsComplete.Should().BeTrue(); signed.Rows[0].Receipt!.RecordId.Should().Be(1);
        signed.Rows[0].Execution!.RecordId.Should().Be(2); signed.Rows[0].Narrative.Should().Be("تنفيذ موقّع محفوظ");
        n.SubmittedAt = null; n.State = "Acknowledged"; n.CorrectionReason = "تصحيح"; n.Narrative = "مسودة تصحيح لم تُرسل";
        var corrected = CoverageRegisterProjector.Project(Header,n.LocalDate,[n]);
        corrected.IsComplete.Should().BeFalse(); corrected.Rows[0].CorrectionOpen.Should().BeTrue();
        corrected.Rows[0].Narrative.Should().Be("تنفيذ موقّع محفوظ"); corrected.Rows[0].Execution!.RecordId.Should().Be(2);
    }
    internal static DailyCoverageNeed Need() => new() { Id=1,SchoolId=1,SchoolTimetableId=1,LocalDate=new(2026,10,18),OriginalTeacherId=1,
        OriginalTeacherName="المعلم الغائب",Classroom="1/1",Subject="رياضيات",AssignedTeacherName="المعلم البديل",AssignedUserId="teacher",
        ActiveSubstitutionId=4,PeriodsJson="[1]",State="Executed",SubmittedAt=new(2026,10,18,8,0,0,TimeSpan.Zero) };
    internal static CoverageRecord Evidence(int id,string kind,int substitution=4,string actor="teacher")
    {
        var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"TestAssets","Duty","signature-0.png"));
        return new() {Id=id,SchoolId=1,DailyCoverageNeedId=1,SubstitutionId=substitution,ActorUserId=actor,Kind=kind,
            SignatureBytes=bytes,SignatureHash=Convert.ToHexString(SHA256.HashData(bytes)),SnapshotJson="{}",SnapshotHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("{}"))),
            Narrative="تنفيذ موقّع محفوظ",Notes="ملاحظات موقعة",CreatedAt=new(2026,10,18,8,0,0,TimeSpan.Zero)};
    }
    [Fact]
    public async Task Issued_copy_is_replayed_unchanged_after_edits_and_stale_uncached_sources_are_rejected()
    {
        await using var db=await DailyCoverageTests.Seed();var clock=new DailyCoverageTests.Clock();await DailyCoverageTests.Assign(db,clock);
        var repository=new TimetableSubstitutionRepository(db);var renderer=new FakeRenderer();
        var service=new CoverageRegisterService(repository,renderer,new(repository,new DailyCoverageTests.User()),clock);
        var context=await service.ContextAsync(DailyCoverageTests.Date,1,10,default);
        var info=await service.IssueAsync(new(context.Date,context.SourceRevision!),default);
        var original=await service.ContentAsync(info.Id,default);
        var need=await db.Set<DailyCoverageNeed>().SingleAsync();need.Narrative="Unsent later draft";need.Revision++;await db.SaveChangesAsync();
        var replay=await service.IssueAsync(new(context.Date,context.SourceRevision!),default);
        replay.Id.Should().Be(info.Id);renderer.Calls.Should().Be(1);
        (await service.ContentAsync(info.Id,default)).Bytes.Should().Equal(original.Bytes);
        var changed=await service.ContextAsync(context.Date,1,10,default);
        need.Revision++;await db.SaveChangesAsync();
        await service.Invoking(s=>s.IssueAsync(new(context.Date,changed.SourceRevision!),default)).Should().ThrowAsync<BellScheduleConflictException>();
        var latest=await service.ContextAsync(context.Date,1,10,default);var second=await service.IssueAsync(new(context.Date,latest.SourceRevision!),default);
        second.Revision.Should().Be(2);(await service.ContextAsync(context.Date,1,1,default)).History.TotalCount.Should().Be(2);
        await new CoverageRegisterService(repository,renderer,new(repository,DailyCoverageTests.Teacher),clock).Invoking(s=>s.ContentAsync(info.Id,default)).Should().ThrowAsync<UnauthorizedAccessException>();
        await new CoverageRegisterService(repository,renderer,new(repository,new DailyCoverageTests.User(School:999)),clock).Invoking(s=>s.ContentAsync(info.Id,default)).Should().ThrowAsync<KeyNotFoundException>();
        var row=await db.Set<CoverageRegisterArtifact>().FirstAsync();row.FileName="tamper.pdf";
        await db.Invoking(d=>d.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();
    }
    [Theory]
    [InlineData("one",1,1,false)] [InlineData("three",3,8,false)] [InlineData("four",4,8,false)]
    [InlineData("period-nine",1,9,false)] [InlineData("long-signed",1,1,true)] [InlineData("signed",3,2,true)]
    public void Actual_renderer_handles_source_capacity_and_readable_continuations(string name,int teachers,int periods,bool signed)
    {
        QuestPDF.Settings.License=LicenseType.Community;
        QuestPDF.Settings.EnableDebugging=false;
        var needs=new List<DailyCoverageNeed>();
        for(var teacher=1;teacher<=teachers;teacher++)for(var period=1;period<=periods;period++){
            var n=Need();n.Id=teacher*100+period;n.OriginalTeacherId=teacher;n.OriginalTeacherName=$"المعلم الغائب {teacher:000}";
            n.PeriodsJson=$"[{period}]";n.State=signed?"Executed":"Assigned";
            if(name=="long-signed"){
                n.OriginalTeacherName=string.Concat(Enumerable.Repeat("اسم المعلم الغائب الطويل ",12));
                n.AssignedTeacherName=string.Concat(Enumerable.Repeat("اسم المعلم المنتظر الطويل ",12));n.Classroom=new string('ص',80);n.Subject=new string('م',120);
            }
            if(signed){foreach(var record in new[]{Evidence(n.Id*10,"Acknowledgement"),Evidence(n.Id*10+1,"Execution")}){
                    record.DailyCoverageNeedId=n.Id;
                    if(name=="long-signed"){record.Narrative=string.Concat(Enumerable.Repeat("نص التنفيذ العربي الكامل والتفاعل مع الطلاب. ",210))+"نهاية التنفيذ المؤكدة END-EXECUTION-210";record.Notes=string.Concat(Enumerable.Repeat("ملاحظة كاملة. ",120))+"نهاية الملاحظات المؤكدة END-NOTES-120";}
                    n.Records.Add(record);
                }}else n.SubmittedAt=null;
            needs.Add(n);
        }
        var snapshot=CoverageRegisterProjector.Project(Header,new(2026,10,18),needs);
        var pdf=new CoverageRegisterRenderer().Render(snapshot,1,new(2026,10,18,9,0,0,TimeSpan.Zero));
        Encoding.ASCII.GetString(pdf,0,5).Should().Be("%PDF-");pdf.Length.Should().BeGreaterThan(1000);
        var output=Environment.GetEnvironmentVariable("ALFALAH_REGISTER_PDF_OUTPUT");
        if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,name+".pdf"),pdf);}
    }
    private sealed class FakeRenderer:ICoverageRegisterRenderer
    {
        public int Calls;public string Version=>"test-v1";
        public byte[] Render(CoverageRegisterSnapshot snapshot,int revision,DateTimeOffset at){Calls++;return Encoding.ASCII.GetBytes("%PDF-"+new string('a',150)+revision);}
    }
}
