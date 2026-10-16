using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class TeachingStatementTests
{
    [Fact]
    public void Explicit_categories_co_split_and_stage_scope_reconcile_without_text_inference()
    {
        var source=Source();var first=source.Assignments[0];
        var assignments=source.Assignments.ToArray();
        assignments[0]=first with {Mode="CoTeaching",Members=[new(1,1,"",4,1,"Specialization"),new(2,2,"",4,1,"Other")]};
        assignments[1]=assignments[1] with {Mode="SplitQuota",Members=[new(2,2,"",3,1,"LifeSkills"),new(3,3,"",1,0,"Activity")]};
        var snapshot=TeachingStatementService.Project(source with {Assignments=assignments},"all");
        snapshot.Teachers.Single(t=>t.Id==2).Allocations.Sum(a=>a.Periods).Should().Be(7);
        snapshot.Teachers.Single(t=>t.Id==2).Allocations.Should().Contain(a=>a.Category=="Other"&&a.Mode=="CoTeaching"&&a.Periods==4);
        snapshot.Teachers.Single(t=>t.Id==3).Allocations.Should().Contain(a=>a.Pairs==0&&a.Periods==1);
        snapshot.Header.Year.Should().Be("1448 - 1449");
        TeachingStatementService.Project(source,"stage:2").Teachers.Should().HaveCount(24);
    }
    [Theory]
    [InlineData(null)] [InlineData("Guessed")]
    public void Unclassified_or_unknown_categories_never_become_specialization_by_name(string? category)
    {
        var source=Source();var cells=source.Assignments.ToArray();cells[0]=cells[0] with {Members=[cells[0].Members[0] with {StatementCategory=category}]};
        var action=()=>TeachingStatementService.Project(source with {Assignments=cells},"all");action.Should().Throw<ArgumentException>();
    }
    [Fact]
    public void Invalid_pairs_missing_dependencies_and_overload_fail_before_rendering()
    {
        var source=Source();
        var cells=source.Assignments.ToArray();cells[0]=cells[0] with {Members=[cells[0].Members[0] with {AllocatedPeriodCount=1,AllocatedPairedBlockCount=1}]};
        FluentActions.Invoking(()=>TeachingStatementService.Project(source with {Assignments=cells},"all")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(()=>TeachingStatementService.Project(source with {Classes=[]},"all")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(()=>TeachingStatementService.Project(source with {Teachers=source.Teachers.Select(t=>t with {MaximumWeeklyPeriods=1}).ToArray()},"all")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(()=>TeachingStatementService.Project(source,"stage:1")).Should().Throw<ArgumentException>();
    }
    [Fact]
    public async Task Retained_bytes_survive_later_metadata_revision_and_allocation_changes_and_corruption_is_rejected()
    {
        var repository=new MemoryRepository(Source());var renderer=new FakeRenderer();var service=new TeachingStatementService(repository,renderer);
        var first=await service.GetAsync(1,1,7,"all","manager",default);
        repository.Source=repository.Source with {Revision=8,Assignments=[],Header=repository.Source.Header with {School="Renamed"}};
        var again=await service.GetAsync(1,1,7,"all","manager",default);
        again.Content.Should().Equal(first.Content);again.Hash.Should().Be(first.Hash);renderer.Calls.Should().Be(1);
        await FluentActions.Invoking(()=>service.GetAsync(1,1,6,"all","manager",default)).Should().ThrowAsync<BellScheduleConflictException>();
        await FluentActions.Invoking(()=>service.GetAsync(1,1,7,"search-results","manager",default)).Should().ThrowAsync<ArgumentException>();
        repository.Retained!.Pdf=[1,2,3];await FluentActions.Invoking(()=>service.GetAsync(1,1,7,"all","manager",default)).Should().ThrowAsync<InvalidOperationException>();
    }
    [Theory]
    [InlineData(RoleNames.Instructor,true)] [InlineData(RoleNames.Guardian,true)] [InlineData(RoleNames.Secretary,false)]
    public async Task Existing_permission_and_excluded_roles_are_enforced_before_any_retained_read(string role,bool permission)
    {
        var repository=new MemoryRepository(Source());var handler=new TeachingStatementHandler(new(repository,new FakeRenderer()),new User(role,permission));
        (await handler.Handle(new(1,7,"all"),default)).IsSuccess.Should().BeFalse();repository.Reads.Should().Be(0);
    }
    [Fact]
    public async Task Classification_is_persisted_and_compatible_allocations_do_not_clear_it_or_invalidate_a_timetable()
    {
        await using var db=new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var schedule=await BellScheduleTestData.SeedAsync(db);db.Users.Add(new(){Id="teacher",FirstName="Teacher",IsActive=true});
        db.InstructorProfiles.Add(new(){Id=1,UserId="teacher",SchoolId=1,IsActive=true});
        db.TeacherTimetableProfiles.Add(new(){Id=1,SchoolId=1,TimetableSetupProfileId=1,InstructorProfileId=1,BellScheduleRevisionId=schedule.Id,MaximumWeeklyPeriods=24});
        db.Classrooms.Add(new(){Id=1,SchoolId=1,AcademicYearId=1,ClassLabel="1/1",IsActive=true});
        db.Add(new SubjectDefinition {Id=1,SchoolId=1,Name="Math"});db.Add(new ClassSubjectRequirement {Id=1,SchoolId=1,TimetableSetupProfileId=1,ClassroomId=1,SubjectId=1,IndividualPeriodCount=2,PairedBlockCount=1});
        await db.SaveChangesAsync();var service=new TeachingAssignmentService(new TeachingAssignmentRepository(db),new(new SubjectRepository(db),new BellScheduleRepository(db)));
        var initial=await service.SaveAsync(1,1,new(1,[new(1,"SingleTeacher",[new(1,4,1)])]),"manager",default);
        var timetable=new SchoolTimetable {SchoolId=1,AcademicYearId=1,TimetableSetupProfileId=1,Title="Draft",Revision=9,TimingsRequireRevalidation=false};db.Add(timetable);await db.SaveChangesAsync();
        var classified=await service.SaveAsync(1,1,new(initial.Revision,[new(1,"SingleTeacher",[new(1,4,1,"Other")])]),"manager",default);
        timetable.Revision.Should().Be(9);timetable.TimingsRequireRevalidation.Should().BeFalse();
        classified.Assignments.Single().Members.Single().StatementCategory.Should().Be("Other");
        var olderClient=await service.SaveAsync(1,1,new(classified.Revision,[new(1,"SingleTeacher",[new(1,4,1)])]),"manager",default);
        olderClient.Assignments.Single().Members.Single().StatementCategory.Should().Be("Other");
        await FluentActions.Invoking(()=>service.SaveAsync(1,1,new(olderClient.Revision,[new(1,"SingleTeacher",[new(1,4,1,"Unknown")])]),"manager",default)).Should().ThrowAsync<ArgumentException>();
    }
    [Theory]
    [InlineData("reference-24",24,false,false)] [InlineData("large-120",120,false,false)]
    [InlineData("long-names",24,true,false)] [InlineData("dense-appendix",24,false,true)]
    [InlineData("primary-six",24,false,false)] [InlineData("multi-stage",24,false,false)] [InlineData("co-split-other",24,false,false)]
    [InlineData("prefixed-year",24,false,false)]
    public void Source_renderer_outputs_complete_reference_continuations_and_appendices(string name,int teachers,bool longNames,bool dense)
    {
        QuestPDF.Settings.License=LicenseType.Community;
        var source=Source(teachers);
        if(name=="prefixed-year")source=source with {Header=source.Header with {Year="العام الدراسي 2026 - 2027"}};
        if(name is "primary-six" or "multi-stage")source=source with {Classes=source.Classes.Select(c=>name=="primary-six"||c.Id<=12 ? c with {
            Stage=1,Grade=(c.Id-1)%6+1,Code=$"{(c.Id-1)%6+1}/{(c.Id-1)/6+1}",Label=$"الصف {(c.Id-1)%6+1} شعبة {(c.Id-1)/6+1}"}:c).ToArray()};
        if(name=="co-split-other"){
            var cells=source.Assignments.ToArray();cells[0]=cells[0] with {Mode="CoTeaching",Members=[new(1,1,"",4,1,"Specialization"),new(2,2,"",4,1,"Other")]};
            cells[1]=cells[1] with {Mode="SplitQuota",Members=[new(2,2,"",3,1,"LifeSkills"),new(3,3,"",1,0,"Activity")]};source=source with {Assignments=cells};
        }
        if(longNames)source=source with {Teachers=source.Teachers.Select(t=>t with {Name=string.Join(" ",Enumerable.Repeat(t.Name,5))}).ToArray()};
        if(dense)source=source with {Assignments=source.Assignments.Select((a,i)=>a with {Members=[a.Members[0] with {TeacherTimetableProfileId=1,StatementCategory=i%2==0?"Specialization":"Other"}]}).ToArray(),Teachers=source.Teachers.Select(t=>t with {MaximumWeeklyPeriods=200}).ToArray()};
        var snapshot=TeachingStatementService.Project(source,"all");var bytes=new TeachingStatementRenderer().Render(snapshot);
        System.Text.Encoding.ASCII.GetString(bytes.AsSpan(0,4)).Should().Be("%PDF");bytes.Length.Should().BeGreaterThan(10000);
        var path=Environment.GetEnvironmentVariable("ALFALAH_STATEMENT_OUTPUT");if(path is not null){Directory.CreateDirectory(path);File.WriteAllBytes(Path.Combine(path,name+".pdf"),bytes);File.WriteAllText(Path.Combine(path,name+".json"),System.Text.Json.JsonSerializer.Serialize(snapshot));}
    }
    internal static StatementSource Source(int count=24)
    {
        var classes=Enumerable.Range(1,count).Select(i=>new StatementClass(i,2,(i-1)%3+1,$"الصف {(i-1)%3+1} شعبة {(i-1)/3+1}",$"{(i-1)%3+1}/{(i-1)/3+1}")).ToArray();
        return new(7,new("مدرسة الفلاح التجريبية","1448 - 1449",1,"إعداد تجريبي","المملكة العربية السعودية\nوزارة التعليم\nإدارة التعليم","","مدير تجريبي"),classes,[new(1,"رياضيات","#12563a",1)],
            classes.Select(c=>new StatementRequirement(c.Id,1,c.Id,2,1)).ToArray(),classes.Select(c=>new TeachingAssignmentDto(c.Id,c.Id,"SingleTeacher",[new(c.Id,c.Id,"",4,1,"Specialization")])).ToArray(),
            classes.Select(c=>new TeachingTeacherDto(c.Id,c.Id,$"خالد عبدالله السالم {c.Id:000}","رياضيات",true,4,24,20,1,1,false,[])).ToArray());
    }
    private sealed class MemoryRepository(StatementSource source):ITeachingStatementRepository
    {
        public StatementSource Source=source;public TeachingAssignmentStatement? Retained;public int Reads;
        public Task<TeachingAssignmentStatement?> FindAsync(int school,int setup,int revision,string scope,string renderer,CancellationToken ct){Reads++;return Task.FromResult(Retained?.Revision==revision&&Retained.Scope==scope?Retained:null);}
        public Task<StatementSource> CaptureAsync(int school,int setup,CancellationToken ct)=>Task.FromResult(Source);
        public Task<TeachingAssignmentStatement> StoreAsync(TeachingAssignmentStatement row,CancellationToken ct){Retained=row;return Task.FromResult(row);}
    }
    private sealed class FakeRenderer:ITeachingStatementRenderer {public int Calls;public string Version=>"test";public byte[] Render(TeachingStatementSnapshot s){Calls++;return [37,80,68,70,45,1,2];}}
    private sealed class User(string role,bool permission):ICurrentUserService
    {
        public string? UserId=>"manager";public string? Username=>"manager";public int? ActiveSchoolId=>1;public string? PreferredLanguage=>"ar";public bool IsAuthenticated=>true;
        public bool IsInRole(string name)=>name==role;public bool HasPermission(string name)=>permission&&name==PermissionNames.TimetableManage;
        public IEnumerable<string> GetRoles()=>[role];public IEnumerable<string> GetPermissions()=>[];public bool IsGlobalAdmin()=>false;public bool IsSchoolScopedRole()=>true;
    }
}
