using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.DTOs.Timetables;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Validators.IntelligentTimetable;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class ScheduleBreakPhase3Tests
{
    [Theory]
    [InlineData("06:30", "07:00", true)]
    [InlineData("07:45", "07:50", true)]
    [InlineData("08:35", "09:00", true)]
    [InlineData("09:10", "09:25", true)]
    [InlineData("07:44", "07:50", false)]
    [InlineData("07:45", "07:51", false)]
    [InlineData("07:00", "07:00", false)]
    [InlineData("23:50", "00:10", false)]
    [InlineData("06:00", "09:00", false)]
    public void Exact_intervals_allow_adjacency_outside_lessons_and_gaps(string start, string end, bool valid)
    {
        var request = Request() with { DefaultBreaks = [Break(start, end)] };
        new SaveBellScheduleValidator().Validate(request).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Every_conflicting_break_and_lesson_is_reported()
    {
        var result = new SaveBellScheduleValidator().Validate(Request() with { DefaultBreaks = [Break("07:40", "07:49"), Break("07:48", "07:50")] });
        result.Errors.Should().Contain(x => x.PropertyName == "DefaultBreaks[0]")
            .And.Contain(x => x.PropertyName == "DefaultBreaks[1]")
            .And.Contain(x => x.PropertyName == "DefaultPeriods[1]");
        new SaveBellScheduleValidator().Validate(Request() with { DefaultBreaks = [Break("09:00", "09:10"), Break("09:10", "09:20")] }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Blank_names_categories_holidays_and_duplicate_days_are_rejected()
    {
        var validator = new SaveBellScheduleValidator();
        validator.Validate(Request() with { DefaultBreaks = [Break() with { Name = "  " }] }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { DefaultBreaks = [Break() with { Category = "unknown" }] }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { Days = Request().Days.Select(x => x.Day == 7 ? x with { IsStudyDay = false, UsesDefaultBreaks = false, Breaks = [Break()] } : x).ToArray() }).IsValid.Should().BeFalse();
        new SaveScheduleBreaksValidator().Validate(new SaveScheduleBreaksRequest(1, [], [new(1, true, []), new(1, true, [])])).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Invalid_multi_day_save_is_atomic_and_compares_each_effective_day()
    {
        using var provider = Provider();
        var db = provider.GetRequiredService<AlFalahDbContext>();
        var original = await BellScheduleTestData.SeedAsync(db);
        var mediator = provider.GetRequiredService<IMediator>();
        var timings = Request() with { Revision = 1, Days = Request().Days.Select(x => x.Day == 3
            ? x with { UsesDefaultSchedule = false, Periods = [Period(1, "07:40", "08:00")] } : x).ToArray() };
        var saved = await mediator.Send(new SaveBellScheduleCommand(original.BellScheduleTemplateId, timings));
        saved.IsSuccess.Should().BeTrue();
        var revisionCount = db.Set<BellScheduleRevision>().Count();
        var response = await mediator.Send(new SaveScheduleBreaksCommand(original.BellScheduleTemplateId,
            new(2, [], Enumerable.Range(1, 7).Select(day => new ScheduleBreakDayDto(day, day is not (2 or 3), day is 2 or 3 ? [Break()] : [])).ToArray())));
        response.IsSuccess.Should().BeFalse();
        db.Set<BellScheduleRevision>().Count().Should().Be(revisionCount);
        db.ScheduleBreakDefinitions.Should().BeEmpty();
        db.Set<BellScheduleTemplate>().Single().Revision.Should().Be(2);
    }

    [Fact]
    public async Task Default_inheritance_variations_revision_history_and_period_edits_remain_independent()
    {
        using var provider = Provider();
        var db = provider.GetRequiredService<AlFalahDbContext>();
        var original = await BellScheduleTestData.SeedAsync(db, published: true);
        var mediator = provider.GetRequiredService<IMediator>();
        var id = original.BellScheduleTemplateId;
        var request = new SaveScheduleBreaksRequest(1, [Break() with { Name = "  فسحة الصباح  " }],
            Enumerable.Range(1, 7).Select(day => new ScheduleBreakDayDto(day, day != 3, day == 3 ? [Break("09:00", "09:15")] : [])).ToArray());
        var saved = await mediator.Send(new SaveScheduleBreaksCommand(id, request));
        saved.IsSuccess.Should().BeTrue();
        saved.Data!.DefaultBreaks!.Single().Name.Should().Be("فسحة الصباح");
        BellScheduleResolver.EffectiveBreaks(saved.Data, TimetableDay.Sunday).Single().StartLocalTime.Should().Be(new TimeOnly(7, 45));
        BellScheduleResolver.EffectiveBreaks(saved.Data, TimetableDay.Monday).Single().StartLocalTime.Should().Be(new TimeOnly(9, 0));
        BellScheduleResolver.EffectiveBreaks(saved.Data, TimetableDay.Friday).Should().BeEmpty();
        db.ScheduleBreakDefinitions.Count().Should().Be(2); // Each timing variation has its own record.
        db.SchoolTimetables.Single().BellScheduleRevisionId.Should().Be(original.Id);
        db.SchoolTimetables.Single().TimingsRequireRevalidation.Should().BeTrue();
        (await new BellScheduleRepository(db).GetRevisionAsync(1, original.Id, default))!.DefaultBreaks.Should().BeEmpty();
        (await mediator.Send(new SaveScheduleBreaksCommand(id, request))).Errors.Should().Contain(TimetableSettingsHandlerSupport.ConcurrencyConflict);

        // Simulate an older periods-only client. Omission must preserve breaks and still prevent overlap.
        var periodsOnly = Request() with { Revision = 2, DefaultPeriods = [Period(1, "07:00", "07:46")] };
        (await mediator.Send(new SaveBellScheduleCommand(id, periodsOnly))).IsSuccess.Should().BeFalse();
        var moved = await mediator.Send(new SaveBellScheduleCommand(id, periodsOnly with { DefaultPeriods = [Period(1, "06:30", "07:30")] }));
        moved.IsSuccess.Should().BeTrue();
        moved.Data!.DefaultBreaks.Should().BeEquivalentTo(saved.Data.DefaultBreaks);
        var stream = await mediator.Send(new GetEffectiveScheduleQuery(id, (int)TimetableDay.Sunday));
        stream.Data!.Select(x => x.Kind).Should().Equal("Lesson", "Break");
        stream.Data!.Last().PeriodSequence.Should().BeNull();
        db.SchoolTimetableEntries.Should().BeEmpty();

        var deleted = await mediator.Send(new SaveScheduleBreaksCommand(id, new(3, [], Enumerable.Range(1, 7).Select(day => new ScheduleBreakDayDto(day, true, [])).ToArray())));
        deleted.IsSuccess.Should().BeTrue();
        deleted.Data!.DefaultBreaks.Should().BeEmpty();
        (await new BellScheduleRepository(db).GetRevisionAsync(1, saved.Data.RevisionId, default))!.DefaultBreaks.Should().ContainSingle();
    }

    [Theory]
    [InlineData(2, true, RoleNames.Secretary)]
    [InlineData(1, false, RoleNames.Secretary)]
    [InlineData(1, true, RoleNames.Instructor)]
    public async Task Writes_enforce_school_scope_and_permissions(int school, bool manage, string role)
    {
        using var provider = Provider(new User(school, manage, role));
        var db = provider.GetRequiredService<AlFalahDbContext>();
        var original = await BellScheduleTestData.SeedAsync(db);
        var response = await provider.GetRequiredService<IMediator>().Send(new SaveScheduleBreaksCommand(original.BellScheduleTemplateId,
            new(1, [Break()], Enumerable.Range(1, 7).Select(day => new ScheduleBreakDayDto(day, true, [])).ToArray())));
        response.IsSuccess.Should().BeFalse();
        db.ScheduleBreakWindows.Should().BeEmpty();
    }

    [Fact]
    public async Task Breaks_are_immutable_and_current_context_never_returns_a_lesson_in_them()
    {
        using var provider = Provider();
        var db = provider.GetRequiredService<AlFalahDbContext>();
        var original = await BellScheduleTestData.SeedAsync(db, published: true);
        var mediator = provider.GetRequiredService<IMediator>();
        var saved = await mediator.Send(new SaveScheduleBreaksCommand(original.BellScheduleTemplateId,
            new(1, [Break()], Enumerable.Range(1, 7).Select(day => new ScheduleBreakDayDto(day, true, [])).ToArray())));
        var schedule = saved.Data!;
        BellScheduleResolver.CurrentPeriod(schedule, new(2026, 9, 1, 4, 45, 0, TimeSpan.Zero)).Should().BeNull();
        BellScheduleResolver.CurrentPeriod(schedule, new(2026, 9, 1, 4, 50, 0, TimeSpan.Zero))!.Sequence.Should().Be(2);
        // Operational consumers use the published revision, not the template's latest draft.
        var timetable = db.SchoolTimetables.Single();
        timetable.BellScheduleRevisionId = schedule.RevisionId;
        db.SchoolTimetableEntries.Add(new() { SchoolId = 1, SchoolTimetableId = timetable.Id, InstructorProfileId = 10,
            Day = TimetableDay.Tuesday, Period = 2, EntryType = TimetableEntryType.Lesson, ClassroomId = 20, ClassLabel = "1/A", Subject = "Math" });
        await db.SaveChangesAsync();
        var gatePass = new GatePassWorkflowRepository(db);
        (await gatePass.ResolvePublishedTimetableAsync(1, 1, TimetableSemester.First, 20, "1/A", new(2026, 9, 1, 4, 45, 0, TimeSpan.Zero), default)).Should().BeNull();
        db.ScheduleBreakWindows.Single().EndLocalTime = new(9, 0);
        await db.Invoking(x => x.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>().WithMessage("*immutable*");
    }

    [Fact]
    public void Breaks_render_in_pdf_without_becoming_excel_assignment_columns()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var request = Request();
        var schedule = new BellScheduleDto(1, 1, 1, 1, TimetableSemester.First, "التوقيت", 1, "Africa/Cairo",
            request.DefaultPeriods, request.Days, [], [Break()]);
        var capabilities = new TimetableCapabilitiesDto(true, true, true);
        var teachers = new[] { new TimetableTeacherDto(1, "teacher", "المعلم", "T1", "رياضيات", ["1/A"], true) };
        var timetable = new SchoolTimetableDto(1, 1, 1, "2026", TimetableSemester.First, "الأول", "الجدول", true,
            DateTimeOffset.UtcNow, 1, DateTimeOffset.UtcNow, [], [], capabilities, BellSchedule: schedule);
        var catalog = new TimetableCatalogDto(1, "المدرسة", [], [], [], 2, teachers, [], capabilities, BellSchedule: schedule);
        var documents = new SchoolTimetableDocumentService();
        var pdf = documents.BuildPdf(timetable, catalog, TimetablePdfColorMode.Color);
        System.Text.Encoding.ASCII.GetString(pdf.Bytes, 0, 4).Should().Be("%PDF");
        using var stream = new MemoryStream(documents.BuildImportTemplate(timetable, catalog).Bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        workbook.Worksheet(1).LastColumnUsed()!.ColumnNumber().Should().Be(14); // 2 teacher columns + 6 * 2 lessons.
    }

    private static ServiceProvider Provider(ICurrentUserService? user = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AlFalahDbContext>(x => x.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IBellScheduleRepository, BellScheduleRepository>();
        services.AddScoped<ITimetableSettingsRepository, TimetableSettingsRepository>();
        services.AddSingleton(user ?? new User(1));
        services.AddSingleton(TimeProvider.System);
        services.AddMediatR(x => x.RegisterServicesFromAssemblyContaining<SaveScheduleBreaksCommand>());
        return services.BuildServiceProvider();
    }
    private static ScheduleBreakDto Break(string start = "07:45", string end = "07:50") => new("فسحة", "Recess", TimeOnly.Parse(start), TimeOnly.Parse(end));
    private static BellPeriodDto Period(int sequence, string start, string end) => new(sequence, null, TimeOnly.Parse(start), TimeOnly.Parse(end));
    private static SaveBellScheduleRequest Request() => new(1, TimetableSemester.First, "توقيت الاستراحات", 0, "Africa/Cairo",
        [Period(1, "07:00", "07:45"), Period(2, "07:50", "08:35")],
        Enumerable.Range(1, 7).Select(day => new BellScheduleDayDto(day, day != 7, true, [])).ToArray());
    private sealed class User(int school, bool manage = true, string role = RoleNames.Secretary) : ICurrentUserService
    {
        public string? UserId => "manager";
        public string? Username => "manager";
        public int? ActiveSchoolId => school;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string name) => role == name;
        public bool HasPermission(string name) => name == PermissionNames.TimetableView || (manage && name == PermissionNames.TimetableManage);
        public IEnumerable<string> GetRoles() => [role];
        public IEnumerable<string> GetPermissions() => [PermissionNames.TimetableView];
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }
}
