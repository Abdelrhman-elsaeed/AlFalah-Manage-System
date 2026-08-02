using AlFalah.Application.DTOs.Timetables;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Common;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class SchoolTimetableServiceTests
{
    [Fact]
    public void Documents_generate_A4_color_and_monochrome_pdfs_and_round_trip_excel_template()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var documents = new SchoolTimetableDocumentService();
        var teachers = new[]
        {
            new TimetableTeacherDto(1, "teacher-1", "أحمد محمد", "T-1", "رياضيات", new[] { "3/1" }, true)
        };
        var entries = new[]
        {
            new TimetableEntryDto(1, TimetableDay.Saturday, 1, TimetableEntryType.Lesson, "3/1", "رياضيات"),
            new TimetableEntryDto(1, TimetableDay.Saturday, 2, TimetableEntryType.Standby, null, null)
        };
        var capabilities = new TimetableCapabilitiesDto(true, true, true);
        var timetable = new SchoolTimetableDto(1, 1, 1, "العام 2026-2027", TimetableSemester.First,
            "الفصل الدراسي الأول", "الجدول", true, DateTimeOffset.UtcNow, 2, DateTimeOffset.UtcNow,
            entries, new[] { new TimetableTeacherSummaryDto(1, 1, 1) }, capabilities);
        var catalog = new TimetableCatalogDto(1, "مدرسة الفلاح",
            new[] { new TimetableAcademicYearDto(1, "2026-2027", "العام 2026-2027", true) },
            new[] { new TimetableOptionDto(1, "الفصل الدراسي الأول") },
            Enum.GetValues<TimetableDay>().Select(x => new TimetableOptionDto((int)x, x.ToString())).ToList(),
            8, teachers, Array.Empty<TimetableModeratorDto>(), capabilities);

        var colorPdf = documents.BuildPdf(timetable, catalog, TimetablePdfColorMode.Color);
        var monochromePdf = documents.BuildPdf(timetable, catalog, TimetablePdfColorMode.Monochrome);
        colorPdf.ContentType.Should().Be("application/pdf");
        colorPdf.FileName.Should().Contain("-A4-");
        monochromePdf.FileName.Should().Contain("-A4-");
        System.Text.Encoding.ASCII.GetString(colorPdf.Bytes, 0, 4).Should().Be("%PDF");
        System.Text.Encoding.ASCII.GetString(monochromePdf.Bytes, 0, 4).Should().Be("%PDF");
        monochromePdf.Bytes.Should().NotEqual(colorPdf.Bytes);

        var template = documents.BuildImportTemplate(timetable, catalog);
        using var stream = new MemoryStream(template.Bytes);
        var parsed = documents.ParseImport(stream, catalog);
        parsed.Warnings.Should().BeEmpty();
        parsed.Rows.Single().Entries.Should().BeEquivalentTo(new[]
        {
            new SaveTimetableEntryRequest(1, TimetableDay.Saturday, 1, TimetableEntryType.Lesson, "3/1", "رياضيات"),
            new SaveTimetableEntryRequest(1, TimetableDay.Saturday, 2, TimetableEntryType.Standby, null, null)
        });
    }

    [Fact]
    public async Task Save_rejects_class_conflict_but_allows_parallel_standby()
    {
        await using var harness = await TimetableHarness.CreateAsync();
        var service = harness.Service(harness.Manager());
        var timetable = await service.CreateAsync(new(1, TimetableSemester.First, "الجدول"), null);

        var conflict = new SaveSchoolTimetableRequest("الجدول", timetable.Revision, new[]
        {
            Lesson(1, "3/1", "رياضيات"),
            Lesson(2, "3/1", "علوم")
        });
        await service.Invoking(x => x.SaveAsync(timetable.Id, conflict))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*مسند لأكثر من معلم*");

        var standby = new SaveSchoolTimetableRequest("الجدول", timetable.Revision, new[]
        {
            new SaveTimetableEntryRequest(1, TimetableDay.Saturday, 1, TimetableEntryType.Standby, null, null),
            new SaveTimetableEntryRequest(2, TimetableDay.Saturday, 1, TimetableEntryType.Standby, null, null)
        });
        var saved = await service.SaveAsync(timetable.Id, standby);
        saved.Entries.Should().HaveCount(2).And.OnlyContain(x => x.EntryType == TimetableEntryType.Standby);
    }

    [Fact]
    public async Task Instructor_sees_only_published_schedule_and_then_receives_live_saved_changes()
    {
        await using var harness = await TimetableHarness.CreateAsync();
        var manager = harness.Service(harness.Manager());
        var timetable = await manager.CreateAsync(new(1, TimetableSemester.First, "الجدول"), null);
        timetable = await manager.SaveAsync(timetable.Id, new("الجدول", timetable.Revision, new[]
        {
            Lesson(1, "3/1", "رياضيات"),
            new SaveTimetableEntryRequest(2, TimetableDay.Saturday, 2, TimetableEntryType.Lesson, "4/1", "علوم")
        }));

        var instructor = harness.Service(harness.Instructor());
        var catalog = await instructor.GetCatalogAsync(null);
        catalog.Teachers.Should().ContainSingle().Which.InstructorProfileId.Should().Be(1);
        (await instructor.GetCurrentAsync(1, TimetableSemester.First, null)).Should().BeNull();

        timetable = await manager.PublishAsync(timetable.Id, new(timetable.Revision));
        var published = await instructor.GetCurrentAsync(1, TimetableSemester.First, null);
        published.Should().NotBeNull();
        published!.Entries.Should().OnlyContain(x => x.InstructorProfileId == 1);
        published!.Entries.Single().Subject.Should().Be("رياضيات");
        (await instructor.GetByIdAsync(timetable.Id)).Entries.Should().OnlyContain(x => x.InstructorProfileId == 1);

        timetable = await manager.SaveAsync(timetable.Id, new("الجدول المعدل", timetable.Revision, new[]
        {
            Lesson(1, "3/1", "علوم"),
            new SaveTimetableEntryRequest(2, TimetableDay.Saturday, 2, TimetableEntryType.Lesson, "4/1", "لغة عربية")
        }));
        var live = await instructor.GetCurrentAsync(1, TimetableSemester.First, null);
        live!.Title.Should().Be("الجدول المعدل");
        live.Entries.Should().OnlyContain(x => x.InstructorProfileId == 1);
        live.Entries.Single().Subject.Should().Be("علوم");
    }

    [Fact]
    public async Task Granted_moderator_can_manage_and_restore_creates_a_new_version()
    {
        await using var harness = await TimetableHarness.CreateAsync();
        var moderator = harness.Service(harness.Moderator());
        await moderator.Invoking(x => x.CreateAsync(new(1, TimetableSemester.First, "ممنوع"), null))
            .Should().ThrowAsync<UnauthorizedSchoolAccessException>();

        var manager = harness.Service(harness.Manager());
        await manager.UpdateGrantsAsync(new(new[] { TimetableHarness.ModeratorId }), null);
        var timetable = await moderator.CreateAsync(new(1, TimetableSemester.First, "النسخة الأولى"), null);
        timetable = await moderator.SaveAsync(timetable.Id, new("النسخة الأولى", timetable.Revision, new[] { Lesson(1, "3/1", "رياضيات") }));
        timetable = await moderator.SaveAsync(timetable.Id, new("النسخة الثانية", timetable.Revision, new[] { Lesson(1, "3/1", "علوم") }));

        var restored = await moderator.RestoreAsync(timetable.Id, 2, new(timetable.Revision));
        restored.Title.Should().Be("النسخة الأولى");
        restored.Entries.Single().Subject.Should().Be("رياضيات");
        var versions = await moderator.GetVersionsAsync(timetable.Id);
        versions.First().ChangeKind.Should().Be(TimetableChangeKind.Restored);
        versions.First().RestoredFromVersionNumber.Should().Be(2);
    }

    private static SaveTimetableEntryRequest Lesson(int teacherId, string classLabel, string subject) =>
        new(teacherId, TimetableDay.Saturday, 1, TimetableEntryType.Lesson, classLabel, subject);

    private sealed class TimetableHarness : IAsyncDisposable
    {
        public const string ManagerId = "manager";
        public const string ModeratorId = "moderator";
        public const string InstructorId = "teacher-1";
        private readonly AlFalahDbContext _context;

        private TimetableHarness(AlFalahDbContext context) => _context = context;

        public static async Task<TimetableHarness> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<AlFalahDbContext>()
                .UseInMemoryDatabase($"timetable-{Guid.NewGuid()}")
                .Options;
            var context = new AlFalahDbContext(options);
            var managerRole = new ApplicationRole { Id = "role-manager", Name = RoleNames.SchoolManager, NormalizedName = RoleNames.SchoolManager.ToUpperInvariant() };
            var moderatorRole = new ApplicationRole { Id = "role-moderator", Name = RoleNames.Moderator, NormalizedName = RoleNames.Moderator.ToUpperInvariant() };
            var instructorRole = new ApplicationRole { Id = "role-instructor", Name = RoleNames.Instructor, NormalizedName = RoleNames.Instructor.ToUpperInvariant() };
            var manager = User(ManagerId, "مدير", "المدرسة");
            var moderator = User(ModeratorId, "مشرف", "الجدول");
            var teacher1 = User(InstructorId, "أحمد", "محمد");
            var teacher2 = User("teacher-2", "محمود", "علي");
            context.AddRange(managerRole, moderatorRole, instructorRole, manager, moderator, teacher1, teacher2);
            context.Schools.Add(new School { Id = 1, Name = "مدرسة الفلاح", City = "القاهرة", Stage = SchoolStage.Primary, IsActive = true, ManagerUserId = ManagerId });
            context.AcademicYears.Add(new AcademicYear { Id = 1, Code = "2026-2027", NameAr = "العام 2026-2027", StartsOn = new(2026, 8, 1), EndsOn = new(2027, 7, 31), IsActive = true });
            context.UserSchoolRoles.AddRange(
                Assignment(ManagerId, managerRole.Id),
                Assignment(ModeratorId, moderatorRole.Id),
                Assignment(InstructorId, instructorRole.Id),
                Assignment("teacher-2", instructorRole.Id));
            context.InstructorProfiles.AddRange(
                new InstructorProfile { Id = 1, UserId = InstructorId, SchoolId = 1, EmployeeNumber = "T-1", SubjectSpecialization = "رياضيات", IsActive = true },
                new InstructorProfile { Id = 2, UserId = "teacher-2", SchoolId = 1, EmployeeNumber = "T-2", SubjectSpecialization = "علوم", IsActive = true });
            await context.SaveChangesAsync();
            return new TimetableHarness(context);
        }

        public ISchoolTimetableService Service(ICurrentUserService currentUser)
        {
            var repository = new SchoolTimetableRepository(_context);
            var guard = new SchoolScopeGuard(_context, currentUser, NullLogger<SchoolScopeGuard>.Instance);
            return new SchoolTimetableService(repository, new StubDocuments(), currentUser, guard);
        }

        public ICurrentUserService Manager() => new TestCurrentUser(ManagerId, RoleNames.SchoolManager);
        public ICurrentUserService Moderator() => new TestCurrentUser(ModeratorId, RoleNames.Moderator);
        public ICurrentUserService Instructor() => new TestCurrentUser(InstructorId, RoleNames.Instructor);
        public ValueTask DisposeAsync() => _context.DisposeAsync();

        private static ApplicationUser User(string id, string firstName, string lastName) =>
            new() { Id = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(), FirstName = firstName, LastName = lastName, IsActive = true };

        private static UserSchoolRole Assignment(string userId, string roleId) =>
            new() { UserId = userId, SchoolId = 1, RoleId = roleId, IsActive = true };
    }

    private sealed class TestCurrentUser(string userId, string role) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? Username => userId;
        public int? ActiveSchoolId => 1;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == role;
        public bool HasPermission(string permissionName) => true;
        public IEnumerable<string> GetRoles() => new[] { role };
        public IEnumerable<string> GetPermissions() => new[] { PermissionNames.TimetableView };
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class StubDocuments : ISchoolTimetableDocumentService
    {
        public TimetableFileDto BuildPdf(
            SchoolTimetableDto timetable,
            TimetableCatalogDto catalog,
            TimetablePdfColorMode colorMode) => throw new NotSupportedException();
        public TimetableFileDto BuildImportTemplate(SchoolTimetableDto timetable, TimetableCatalogDto catalog) => throw new NotSupportedException();
        public TimetableImportRows ParseImport(Stream stream, TimetableCatalogDto catalog) => throw new NotSupportedException();
    }
}
