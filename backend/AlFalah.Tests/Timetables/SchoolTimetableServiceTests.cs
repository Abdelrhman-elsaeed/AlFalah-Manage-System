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
    public async Task Instructor_sees_only_published_schedule_and_published_snapshot_rejects_direct_edits()
    {
        await using var harness = await TimetableHarness.CreateAsync();
        await harness.ConfigureReviewAsync();
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

        await manager.Invoking(x => x.SaveAsync(timetable.Id, new("الجدول المعدل", timetable.Revision, new[]
            {
                Lesson(1, "3/1", "رياضيات"),
                new SaveTimetableEntryRequest(2, TimetableDay.Saturday, 2, TimetableEntryType.Lesson, "4/1", "علوم")
            })))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*لقطة ثابتة*");

        var live = await instructor.GetCurrentAsync(1, TimetableSemester.First, null);
        live!.Title.Should().Be("الجدول");
        live.Entries.Should().OnlyContain(x => x.InstructorProfileId == 1);
        live.Entries.Single().Subject.Should().Be("رياضيات");
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

    [Fact]
    public async Task Manager_current_schedule_prefers_the_generated_populated_timetable_over_an_empty_draft()
    {
        await using var harness = await TimetableHarness.CreateAsync();
        await harness.SeedEmptyDraftAndGeneratedTimetableAsync();

        var current = await harness.Service(harness.Manager())
            .GetCurrentAsync(1, TimetableSemester.First, null);

        current.Should().NotBeNull();
        current!.Title.Should().Be("الجدول المولد");
        current.Entries.Should().ContainSingle();
        current.Entries[0].Id.Should().NotBeNull();
        current.Entries[0].ClassSubjectRequirementId.Should().Be(1);
        current.Entries[0].RoomName.Should().Be("معمل الرياضيات");
    }

    [Fact]
    public async Task Regenerate_builds_a_complete_draft_and_records_a_version_without_user_side_effects_before_the_call()
    {
        await using var harness = await TimetableHarness.CreateAsync();
        await harness.ConfigureReviewAsync();
        var service = harness.Service(harness.Manager());
        var timetable = await service.CreateAsync(new(1, TimetableSemester.First, "الجدول"), null);
        timetable.Entries.Should().BeEmpty();

        var regenerated = await service.RegenerateAsync(timetable.Id, new(timetable.Revision));

        regenerated.IsPublished.Should().BeFalse();
        regenerated.TimetableSetupProfileId.Should().NotBeNull();
        regenerated.Entries.Should().HaveCount(2);
        regenerated.Entries.Should().OnlyContain(x => x.ClassSubjectRequirementId.HasValue && x.SubjectId.HasValue && x.ClassroomId.HasValue);
        (await service.GetVersionsAsync(timetable.Id)).First().ChangeKind.Should().Be(TimetableChangeKind.Regenerated);
    }

    [Fact]
    public async Task Regenerate_places_configured_fixed_slots_and_returns_the_setup_identity_to_the_UI()
    {
        await using var harness = await TimetableHarness.CreateAsync();
        await harness.ConfigureReviewAsync();
        await harness.PinRequirementAsync(requirementId: 1, day: 3, period: 2);
        var service = harness.Service(harness.Manager());
        var timetable = await service.CreateAsync(new(1, TimetableSemester.First, "الجدول"), null);

        var regenerated = await service.RegenerateAsync(timetable.Id, new(timetable.Revision));

        regenerated.TimetableSetupProfileId.Should().BeGreaterThan(0);
        regenerated.Entries.Should().ContainSingle(x => x.ClassSubjectRequirementId == 1 &&
            x.Day == TimetableDay.Monday && x.Period == 2);
    }

    [Fact]
    public async Task Regenerate_turns_a_published_schedule_into_a_reviewable_draft_and_keeps_version_history()
    {
        await using var harness = await TimetableHarness.CreateAsync();
        await harness.ConfigureReviewAsync();
        var service = harness.Service(harness.Manager());
        var timetable = await service.CreateAsync(new(1, TimetableSemester.First, "الجدول"), null);
        timetable = await service.RegenerateAsync(timetable.Id, new(timetable.Revision));
        timetable = await service.PublishAsync(timetable.Id, new(timetable.Revision));
        timetable.IsPublished.Should().BeTrue();
        var versionsBefore = await service.GetVersionsAsync(timetable.Id);

        var regenerated = await service.RegenerateAsync(timetable.Id, new(timetable.Revision));

        regenerated.IsPublished.Should().BeFalse();
        regenerated.PublishedAt.Should().BeNull();
        regenerated.Revision.Should().Be(timetable.Revision + 1);
        (await service.GetVersionsAsync(timetable.Id)).Should().HaveCount(versionsBefore.Count + 1);
        (await service.GetVersionsAsync(timetable.Id)).First().ChangeKind.Should().Be(TimetableChangeKind.Regenerated);
    }

    [Fact]
    public async Task Regenerate_rejects_a_stale_revision_without_changing_entries_or_versions()
    {
        await using var harness = await TimetableHarness.CreateAsync();
        await harness.ConfigureReviewAsync();
        var service = harness.Service(harness.Manager());
        var timetable = await service.CreateAsync(new(1, TimetableSemester.First, "الجدول"), null);
        var versionsBefore = await service.GetVersionsAsync(timetable.Id);

        await service.Invoking(x => x.RegenerateAsync(timetable.Id, new(timetable.Revision - 1)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*قديمة*");

        var unchanged = await service.GetByIdAsync(timetable.Id);
        unchanged.Revision.Should().Be(timetable.Revision);
        unchanged.Entries.Should().BeEmpty();
        (await service.GetVersionsAsync(timetable.Id)).Should().HaveCount(versionsBefore.Count);
    }

    [Fact]
    public async Task Regenerate_rejects_unauthorized_users_before_modifying_the_schedule()
    {
        await using var harness = await TimetableHarness.CreateAsync();
        await harness.ConfigureReviewAsync();
        var manager = harness.Service(harness.Manager());
        var timetable = await manager.CreateAsync(new(1, TimetableSemester.First, "الجدول"), null);

        await harness.Service(harness.Moderator()).Invoking(x => x.RegenerateAsync(timetable.Id, new(timetable.Revision)))
            .Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await harness.Service(harness.Instructor()).Invoking(x => x.RegenerateAsync(timetable.Id, new(timetable.Revision)))
            .Should().ThrowAsync<UnauthorizedSchoolAccessException>();

        (await manager.GetByIdAsync(timetable.Id)).Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Failed_regeneration_keeps_the_existing_timetable_and_version_history_unchanged()
    {
        await using var harness = await TimetableHarness.CreateAsync();
        await harness.ConfigureReviewAsync();
        var service = harness.Service(harness.Manager());
        var timetable = await service.CreateAsync(new(1, TimetableSemester.First, "الجدول"), null);
        timetable = await service.RegenerateAsync(timetable.Id, new(timetable.Revision));
        var originalEntries = timetable.Entries.ToArray();
        var versionsBefore = await service.GetVersionsAsync(timetable.Id);
        await harness.MakeFixedTeacherConflictAsync();

        await service.Invoking(x => x.RegenerateAsync(timetable.Id, new(timetable.Revision)))
            .Should().ThrowAsync<ArgumentException>();

        var unchanged = await service.GetByIdAsync(timetable.Id);
        unchanged.Revision.Should().Be(timetable.Revision);
        unchanged.Entries.Should().BeEquivalentTo(originalEntries);
        (await service.GetVersionsAsync(timetable.Id)).Should().HaveCount(versionsBefore.Count);
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
            await BellScheduleTestData.SeedAsync(context);
            return new TimetableHarness(context);
        }

        public ISchoolTimetableService Service(ICurrentUserService currentUser)
        {
            var repository = new SchoolTimetableRepository(_context);
            var reviewRepository = new TimetableReviewRepository(_context);
            var validation = new AlFalah.Application.IntelligentTimetable.TimetableValidationEngine();
            var guard = new SchoolScopeGuard(_context, currentUser, NullLogger<SchoolScopeGuard>.Instance);
            return new SchoolTimetableService(repository, new StubDocuments(), currentUser, guard, new BellScheduleRepository(_context),
                new AlFalah.Application.IntelligentTimetable.TeacherAvailabilityService(new TeacherAvailabilityRepository(_context)),
                new AlFalah.Application.IntelligentTimetable.SubjectAssignmentService(new SubjectRepository(_context)),
                new AlFalah.Application.IntelligentTimetable.TimetableReviewService(reviewRepository, validation,
                    new AlFalah.Application.IntelligentTimetable.TimetableRepairEngine(validation), currentUser),
                new AlFalah.Application.IntelligentTimetable.TimetableGenerationService(reviewRepository,
                    new AlFalah.Application.IntelligentTimetable.TimetableGenerationEngine(), currentUser));
        }

        public async Task ConfigureReviewAsync()
        {
            var setup = await _context.TimetableSetupProfiles.SingleAsync();
            var timing = await _context.Set<BellScheduleRevision>().SingleAsync();
            foreach (var i in new[] { 1, 2 })
            {
                _context.Classrooms.Add(new() { Id = i, SchoolId = 1, AcademicYearId = 1, ClassLabel = i == 1 ? "3/1" : "4/1" });
                _context.Add(new SubjectDefinition { Id = i, SchoolId = 1, Name = i == 1 ? "رياضيات" : "علوم" });
                _context.TeacherTimetableProfiles.Add(new() { Id = i, SchoolId = 1, TimetableSetupProfileId = setup.Id,
                    InstructorProfileId = i, BellScheduleRevisionId = timing.Id, MaximumWeeklyPeriods = 20 });
                _context.Add(new ClassSubjectRequirement { Id = i, SchoolId = 1, TimetableSetupProfileId = setup.Id,
                    ClassroomId = i, SubjectId = i, IndividualPeriodCount = 1 });
                _context.Add(new TeachingAssignment { Id = i, SchoolId = 1, TimetableSetupProfileId = setup.Id, ClassSubjectRequirementId = i,
                    Members = [new() { SchoolId = 1, TimetableSetupProfileId = setup.Id, TeacherTimetableProfileId = i, AllocatedPeriodCount = 1 }] });
            }
            await _context.SaveChangesAsync();
        }

        public async Task PinRequirementAsync(int requirementId, int day, int period)
        {
            var requirement = await _context.Set<ClassSubjectRequirement>().SingleAsync(x => x.Id == requirementId);
            requirement.FixedSlots.Add(new ClassSubjectFixedSlot
            {
                SchoolId = requirement.SchoolId,
                TimetableSetupProfileId = requirement.TimetableSetupProfileId,
                ClassSubjectRequirementId = requirement.Id,
                ClassroomId = requirement.ClassroomId,
                SubjectId = requirement.SubjectId,
                Day = day,
                Period = period
            });
            await _context.SaveChangesAsync();
        }

        public async Task MakeFixedTeacherConflictAsync()
        {
            await PinRequirementAsync(1, 3, 2);
            await PinRequirementAsync(2, 3, 2);
            var secondMember = await _context.Set<TeachingAssignmentMember>()
                .SingleAsync(x => x.TeachingAssignmentId == 2);
            secondMember.TeacherTimetableProfileId = 1;
            await _context.SaveChangesAsync();
        }

        public async Task SeedEmptyDraftAndGeneratedTimetableAsync()
        {
            var originalSetup = await _context.TimetableSetupProfiles.SingleAsync();
            var timing = await _context.Set<BellScheduleRevision>().SingleAsync();
            originalSetup.Revision = 1;

            var generatedSetup = new TimetableSetupProfile
            {
                SchoolId = 1,
                AcademicYearId = 1,
                Semester = TimetableSemester.First,
                Name = "ملف التوليد",
                BellScheduleTemplateId = timing.BellScheduleTemplateId,
                Status = TimetableSetupStatus.Generated,
                Revision = 4,
                CreatedByUserId = ManagerId,
                UpdatedByUserId = ManagerId
            };
            _context.TimetableSetupProfiles.Add(generatedSetup);
            _context.Set<TimetableRoom>().Add(new TimetableRoom
            {
                Id = 1,
                SchoolId = 1,
                Name = "معمل الرياضيات"
            });
            await _context.SaveChangesAsync();

            _context.SchoolTimetables.Add(new SchoolTimetable
            {
                SchoolId = 1,
                AcademicYearId = 1,
                TimetableSetupProfileId = originalSetup.Id,
                BellScheduleRevisionId = timing.Id,
                SetupRevision = originalSetup.Revision,
                Semester = TimetableSemester.First,
                Title = "مسودة فارغة قديمة",
                CreatedByUserId = ManagerId,
                UpdatedByUserId = ManagerId,
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
            });
            await _context.SaveChangesAsync();

            _context.SchoolTimetables.Add(
                new SchoolTimetable
                {
                    SchoolId = 1,
                    AcademicYearId = 1,
                    TimetableSetupProfileId = generatedSetup.Id,
                    BellScheduleRevisionId = timing.Id,
                    SetupRevision = generatedSetup.Revision,
                    Semester = TimetableSemester.First,
                    Title = "الجدول المولد",
                    IsPublished = true,
                    PublishedAt = DateTimeOffset.UtcNow,
                    PublishedByUserId = ManagerId,
                    CreatedByUserId = ManagerId,
                    UpdatedByUserId = ManagerId,
                    Entries =
                    [
                        new SchoolTimetableEntry
                        {
                            SchoolId = 1,
                            InstructorProfileId = 1,
                            Day = TimetableDay.Sunday,
                            Period = 1,
                            EntryType = TimetableEntryType.Lesson,
                            ClassLabel = "الأول - أ",
                            Subject = "الرياضيات",
                            ClassSubjectRequirementId = 1,
                            RoomId = 1
                        }
                    ]
                });
            await _context.SaveChangesAsync();
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
        public bool HasPermission(string permissionName) => permissionName == PermissionNames.TimetableView || role == RoleNames.SchoolManager;
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
