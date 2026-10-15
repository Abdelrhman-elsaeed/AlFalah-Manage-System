using AlFalah.Application.Classrooms;
using AlFalah.Application.Common;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Classrooms;

public sealed class VisitClassroomSelectionTests
{
    private readonly FakeClassroomCatalogRepository repository = new() {
        Context = new(SchoolStage.Primary,true,true,[new(1,"Previous",false,new(2025,8,1),new(2026,7,31)),new(2,"Current",true,new(2026,8,1),new(2027,7,31))]),
        Classes = [new Classroom{Id=1,SchoolId=42,AcademicYearId=1,ClassLabel="Previous class"},new Classroom{Id=2,SchoolId=42,AcademicYearId=2,ClassLabel="Current class"},
            new Classroom{Id=3,SchoolId=99,AcademicYearId=2,ClassLabel="Foreign"},new Classroom{Id=4,SchoolId=42,AcademicYearId=2,ClassLabel="Inactive",IsActive=false},
            new Classroom{Id=5,SchoolId=42,AcademicYearId=2,ClassLabel="Deleted",IsDeleted=true}]
    };
    private readonly TeacherDriveHarness.TestCurrentUser user = new(RoleNames.SchoolManager,"manager",42);
    private VisitClassroomSelectionService Service => new(repository,user);
    private static DateTimeOffset Current => new(2026,10,15,0,0,0,TimeSpan.Zero);

    [Fact] public async Task Date_selects_the_school_year_instead_of_the_globally_active_year()
    {
        (await Service.ResolveAsync(42,new(2026,6,1,0,0,0,TimeSpan.Zero),1,null,default)).Label.Should().Be("Previous class");
        var options=await new ClassroomCatalogService(repository,user).OptionsAsync(null,1,true,default,new(2026,6,1));
        options.Items.Select(x=>x.Id).Should().Equal(1);
        await FluentActions.Invoking(()=>new ClassroomCatalogService(repository,user).OptionsAsync(2,1,true,default,new(2026,6,1))).Should().ThrowAsync<ArgumentException>();
    }
    [Theory] [InlineData(null)] [InlineData(0)] [InlineData(1)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(100)]
    public async Task Missing_foreign_wrong_year_inactive_and_deleted_selection_cannot_be_saved(int? id) =>
        await FluentActions.Invoking(()=>Service.ResolveAsync(42,Current,id,null,default)).Should().ThrowAsync<ArgumentException>();
    [Fact] public async Task An_unchanged_ID_keeps_the_recorded_report_label_after_rename()
    {
        var existing=new Visit{ClassroomId=2,GradeClass="Original signed label"};
        var reference=await Service.ResolveAsync(42,Current,2,existing,default);
        reference.Id.Should().Be(2);reference.Label.Should().Be("Original signed label");
        (await Service.ResolveAsync(42,Current,2,null,default)).Label.Should().Be("Current class");
    }
    [Fact] public async Task Inactive_existing_reference_can_be_retained_but_a_new_one_cannot_be_added()
    {
        var existing=new Visit{ClassroomId=4,GradeClass="Retained inactive"};
        (await Service.ResolveAsync(42,Current,4,existing,default)).Label.Should().Be("Retained inactive");
        await FluentActions.Invoking(()=>Service.ResolveAsync(42,Current,4,null,default)).Should().ThrowAsync<ArgumentException>();
    }
    [Fact] public async Task Foreign_school_and_ambiguous_or_missing_year_are_denied()
    {
        await FluentActions.Invoking(()=>Service.ResolveAsync(99,Current,3,null,default)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        repository.Context=repository.Context with{Years=[new(1,"One",true,new(2026,1,1),new(2027,1,1)),new(2,"Two",true,new(2026,1,1),new(2027,1,1))]};
        await FluentActions.Invoking(()=>Service.ResolveAsync(42,Current,2,null,default)).Should().ThrowAsync<ArgumentException>();
        repository.Context=repository.Context with{Years=[]};
        await FluentActions.Invoking(()=>Service.ResolveAsync(42,Current,2,null,default)).Should().ThrowAsync<ArgumentException>();
    }
}
