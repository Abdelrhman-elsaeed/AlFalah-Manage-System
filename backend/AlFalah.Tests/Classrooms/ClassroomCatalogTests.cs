using AlFalah.Application.Classrooms;
using AlFalah.Application.Common;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Xunit;
namespace AlFalah.Tests.Classrooms;

public sealed class FakeClassroomCatalogRepository : IClassroomCatalogRepository
{
    public CatalogContext Context = new(SchoolStage.Primary, true, true, [new(3, "Synthetic year", true)]);
    public List<Classroom> Classes = [];
    public List<InstructorClass> Links = [];
    public List<AuditLog> Audits = [];
    public Action<IReadOnlyList<Classroom>>? OnAdd;
    public List<ClassroomLabelHistory> Labels = [];
    public Task<CatalogContext> ContextAsync(int school, string actor, CancellationToken ct) => Task.FromResult(Context);
    public Task<IReadOnlyList<Classroom>> ClassroomsAsync(int school, int year, CancellationToken ct) => Task.FromResult<IReadOnlyList<Classroom>>(Classes.Where(c => c.SchoolId == school && c.AcademicYearId == year && !c.IsDeleted).ToArray());
    public async Task<IReadOnlyList<Classroom>> PageAsync(int school, int year, int page, bool inactive, CancellationToken ct) =>
        (await ClassroomsAsync(school, year, ct)).Where(c => inactive || c.IsActive).OrderBy(c => c.SectionNumber).Skip((page-1)*100).Take(101).ToArray();
    public Task<IReadOnlyList<InstructorClass>> AssociationsAsync(int teacher, CancellationToken ct) => Task.FromResult<IReadOnlyList<InstructorClass>>(Links.Where(x => x.InstructorProfileId == teacher && !x.IsDeleted).ToArray());
    public Task AddAsync(IReadOnlyList<Classroom> rows, CancellationToken ct) { Classes.AddRange(rows); OnAdd?.Invoke(rows); return Task.CompletedTask; }
    public Task<Classroom?> ForUpdateAsync(int school, int id, CancellationToken ct) => Task.FromResult(Classes.SingleOrDefault(c => c.SchoolId == school && c.Id == id));
    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<ClassroomLabelHistory>> LabelsAsync(int school, int year, CancellationToken ct) => Task.FromResult<IReadOnlyList<ClassroomLabelHistory>>(Labels.Where(l => l.SchoolId == school && l.AcademicYearId == year).ToArray());
    public void AddLabel(ClassroomLabelHistory label) => Labels.Add(label);
    public void AddAssociation(InstructorClass row) => Links.Add(row);
    public void Audit(AuditLog row) => Audits.Add(row);
}

public sealed class ClassroomCatalogTests
{
    private readonly FakeClassroomCatalogRepository repository = new();
    private ClassroomCatalogService Service(bool teacher = false, int school = 42) => new(repository,
        new TeacherDriveHarness.TestCurrentUser(teacher ? RoleNames.Instructor : RoleNames.SchoolManager, teacher ? "teacher" : "manager", school));
    private static Classroom Row(int id, int year = 3, int school = 42) => new() { Id=id, SchoolId=school, AcademicYearId=year,
        Stage=SchoolStage.Primary, GradeLevel=1, SectionNumber=id, Section=id.ToString(), ClassLabel=$"class-{id}", PhysicalLocation="Room" };
    private static InstructorProfile Profile() => new() {Id=8, SchoolId=42, UserId="teacher"};
    [Theory]
    [InlineData(" ٠١ ",1)] [InlineData("۱۲",12)] [InlineData("10",10)]
    public void Digits_are_normalized_without_guessing_letter_sections(string value, int number) => ClassroomIdentity.Section(value).Should().Be(number);
    [Theory]
    [InlineData("أ")] [InlineData("A")] [InlineData("0")] [InlineData("-1")] [InlineData("1.5")] [InlineData("2147483648")]
    public void Invalid_sections_are_rejected(string section) => FluentActions.Invoking(() => ClassroomIdentity.Section(section)).Should().Throw<ArgumentException>();
    [Fact]
    public async Task Batch_is_atomic_and_school_stage_local_and_keeps_physical_location_separate()
    {
        repository.Context = repository.Context with { Stage=SchoolStage.Intermediate };
        await FluentActions.Invoking(() => Service().CreateAsync(new(3,4,["1"],"Room"),default)).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Service().CreateAsync(new(3,1,["1","١"],"Room"),default)).Should().ThrowAsync<ArgumentException>();
        repository.Classes.Should().BeEmpty();
        var rows=await Service().CreateAsync(new(3,1,["٢","10"]," Room "),default);
        rows.Select(x=>x.SectionNumber).Should().Equal(2,10); rows.Should().OnlyContain(x=>x.PhysicalLocation=="Room" && x.Stage==SchoolStage.Intermediate);
        await FluentActions.Invoking(() => Service().CreateAsync(new(3,1,["2","3"],"Room"),default)).Should().ThrowAsync<ArgumentException>();
        repository.Classes.Should().HaveCount(2);
    }
    [Fact]
    public async Task Teacher_selection_is_immediate_ID_based_revisioned_and_preserves_prior_year_history()
    {
        repository.Classes.AddRange([Row(1),Row(2),Row(3,2)]);
        repository.Links.Add(new(){InstructorProfileId=8, SchoolId=42, ClassroomId=3, ClassLabel="Prior year"});
        repository.Links.Add(new(){InstructorProfileId=8, SchoolId=42, ClassLabel="Unresolved old text"});
        var profile=Profile(); await Service(true).ApplyTeacherSelectionAsync(profile,[1,2],null,3,1,default);
        profile.TeachingRevision.Should().Be(2);
        repository.Links.Where(x=>!x.IsDeleted).Select(x=>x.ClassroomId).Should().BeEquivalentTo(new int?[]{1,2,3});
        repository.Links.Single(x=>x.ClassroomId==null).IsDeleted.Should().BeTrue();
        repository.Audits.Single().Reason.Should().Be("Self");
        await FluentActions.Invoking(()=>Service(true).ApplyTeacherSelectionAsync(profile,[1],null,3,1,default)).Should().ThrowAsync<ClassroomCatalogConflictException>();
        repository.Links.Count(x=>!x.IsDeleted).Should().Be(3);
    }
    [Fact]
    public async Task Foreign_year_school_deleted_inactive_and_duplicate_IDs_are_rejected_before_mutation()
    {
        repository.Classes.AddRange([Row(1),Row(2,school:99),Row(3,2),Row(4),Row(5)]);
        repository.Classes.Single(x=>x.Id==4).IsActive=false;
        repository.Classes.Single(x=>x.Id==5).IsDeleted=true;
        foreach (var ids in new[]{new[]{1,2},new[]{1,3},new[]{1,4},new[]{1,5},new[]{1,1}})
            await FluentActions.Invoking(()=>Service(true).ApplyTeacherSelectionAsync(Profile(),ids,null,3,1,default)).Should().ThrowAsync<ArgumentException>();
        repository.Links.Should().BeEmpty(); repository.Audits.Should().BeEmpty();
        await FluentActions.Invoking(()=>Service(true,99).ApplyTeacherSelectionAsync(Profile(),[1],null,3,1,default)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }
    [Fact]
    public async Task Partial_update_and_unchanged_legacy_preserve_links_but_new_free_text_is_rejected()
    {
        var profile=Profile(); repository.Links.Add(new(){InstructorProfileId=8,ClassLabel="Legacy",SchoolId=42});
        await Service(true).ApplyTeacherSelectionAsync(profile,null,null,null,null,default);
        profile.TeachingRevision.Should().Be(1); repository.Links.Single().IsDeleted.Should().BeFalse();
        await Service(true).ApplyTeacherSelectionAsync(profile,null,["Legacy"],null,null,default);
        repository.Links.Single().IsDeleted.Should().BeFalse();
        await FluentActions.Invoking(()=>Service(true).ApplyTeacherSelectionAsync(profile,null,["New arbitrary"],null,null,default)).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(()=>Service(true).ApplyTeacherSelectionAsync(profile,null,[],null,null,default)).Should().ThrowAsync<ArgumentException>();
        repository.Links.Single().IsDeleted.Should().BeFalse();
    }
    [Fact]
    public async Task Member_can_read_past_one_hundred_but_only_authorized_staff_can_write()
    {
        repository.Classes.AddRange(Enumerable.Range(1,205).Select(i=>Row(i)));
        repository.Context=repository.Context with {IsManager=false};
        var first=await Service(true).OptionsAsync(null,1,true,default); first.Items.Should().HaveCount(100); first.HasMore.Should().BeTrue();
        var last=await Service(true).OptionsAsync(null,3,true,default); last.Items.Should().HaveCount(5); last.HasMore.Should().BeFalse();
        await FluentActions.Invoking(()=>Service(true).CreateAsync(new(3,2,["1"],"Room"),default)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        repository.Context=repository.Context with {IsMember=false};
        await FluentActions.Invoking(()=>Service(true).OptionsAsync(null,1,true,default)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }
    [Fact]
    public async Task Explicit_rename_keeps_classroom_ID_and_associations_and_rejects_stale_edits()
    {
        var row=Row(7); row.SectionNumber=null; row.Section="A"; repository.Classes.Add(row);
        repository.Links.Add(new(){InstructorProfileId=8,SchoolId=42,ClassroomId=7,ClassLabel="Frozen previous label"});
        var revision=ClassroomCatalogService.Option(row).Revision;
        var updated=await Service().UpdateAsync(7,new("١",true,"Second floor",revision),default);
        updated.Id.Should().Be(7); updated.SectionNumber.Should().Be(1);
        repository.Links.Single().ClassroomId.Should().Be(7); repository.Links.Single().ClassLabel.Should().Be("Frozen previous label");
        await FluentActions.Invoking(()=>Service().UpdateAsync(7,new("2",true,"Room",revision),default)).Should().ThrowAsync<ClassroomCatalogConflictException>();
    }
    [Fact]
    public async Task Rename_retains_legacy_name_bindings_and_reserves_them_against_reuse()
    {
        var row=Row(7); repository.Classes.Add(row);
        await Service().UpdateAsync(7,new("1",true,"Room",row.UpdatedAt.ToString("O")),default);
        repository.Labels.Select(x=>x.Label).Should().Contain("class-7").And.Contain(row.ClassLabel);
        repository.Labels.Should().OnlyContain(x=>x.ClassroomId==7 && x.SchoolId==42 && x.AcademicYearId==3);
        await FluentActions.Invoking(()=>Service().CreateAsync(new(3,1,["1"],"Room"),default)).Should().ThrowAsync<ArgumentException>();
    }
    [Fact]
    public async Task Ordinary_member_cannot_replace_another_teachers_selection()
    {
        repository.Context=repository.Context with {IsManager=false}; repository.Classes.Add(Row(1));
        var other=Profile(); other.UserId="another-teacher";
        await FluentActions.Invoking(()=>Service(true).ApplyTeacherSelectionAsync(other,[1],null,3,1,default)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        repository.Links.Should().BeEmpty();
    }
}
