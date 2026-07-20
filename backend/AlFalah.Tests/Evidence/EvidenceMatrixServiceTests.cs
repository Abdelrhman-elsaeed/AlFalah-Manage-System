using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Evidence;

/// <summary>Regression coverage for the evidence-matrix invariants.</summary>
public sealed class EvidenceMatrixServiceTests
{
    [Fact]
    public async Task Uploading_Task_X_Updates_Only_Cell_X()
    {
        await using var fixture = await Fixture.CreateAsync();

        await fixture.UploadAsync(teacherId: 1, taskId: 1, requestId: "request-x", driveItemId: "drive-x");

        var statuses = await fixture.Context.TeacherTaskStatuses.OrderBy(x => x.TaskId).ToListAsync();
        statuses.Should().ContainSingle(x => x.TaskId == 1 && x.ActiveFilesCount == 1 && x.CellStatus == EvidenceCellStatus.PendingReview);
        statuses.Should().NotContain(x => x.TaskId == 2);
    }

    [Fact]
    public async Task Repeating_The_Same_Request_Does_Not_Duplicate_File_Or_Total()
    {
        await using var fixture = await Fixture.CreateAsync();

        await fixture.UploadAsync(1, 1, "same-request", "drive-same");
        var retry = await fixture.Service.ReserveUploadAsync(1, 1, 1, "same-request");

        retry.ExistingResult.Should().NotBeNull();
        (await fixture.Context.TeacherEvidenceSubmissions.CountAsync()).Should().Be(1);
        var status = await fixture.Context.TeacherTaskStatuses.SingleAsync();
        status.ActiveFilesCount.Should().Be(1);
    }

    [Fact]
    public async Task Three_Files_For_One_Task_Count_As_One_Task()
    {
        await using var fixture = await Fixture.CreateAsync();

        await fixture.UploadAsync(1, 1, "r1", "item-1");
        await fixture.UploadAsync(1, 1, "r2", "item-2");
        await fixture.UploadAsync(1, 1, "r3", "item-3");

        var status = await fixture.Context.TeacherTaskStatuses.SingleAsync(x => x.TeacherId == 1 && x.TaskId == 1);
        status.ActiveFilesCount.Should().Be(3);
        (await fixture.CountCompletedTasksAsync(1, 1)).Should().Be(1);
    }

    [Fact]
    public async Task Deleting_The_Last_File_Removes_The_Checkmark()
    {
        await using var fixture = await Fixture.CreateAsync();

        var submissionId = await fixture.UploadAsync(1, 1, "r1", "item-1");
        await fixture.Service.MarkDeletedAsync(1, submissionId, "TEACHER-A");

        var status = await fixture.Context.TeacherTaskStatuses.SingleAsync();
        status.ActiveFilesCount.Should().Be(0);
        status.CellStatus.Should().Be(EvidenceCellStatus.NotUploaded);
        (await fixture.CountCompletedTasksAsync(1, 1)).Should().Be(0);
    }

    [Fact]
    public async Task Deleting_One_Of_Multiple_Files_Keeps_The_Checkmark()
    {
        await using var fixture = await Fixture.CreateAsync();

        var first = await fixture.UploadAsync(1, 1, "r1", "item-1");
        await fixture.UploadAsync(1, 1, "r2", "item-2");
        await fixture.Service.MarkDeletedAsync(1, first, "TEACHER-A");

        var status = await fixture.Context.TeacherTaskStatuses.SingleAsync();
        status.ActiveFilesCount.Should().Be(1);
        status.CellStatus.Should().Be(EvidenceCellStatus.PendingReview);
        (await fixture.CountCompletedTasksAsync(1, 1)).Should().Be(1);
    }

    [Fact]
    public async Task Teacher_A_Data_Does_Not_Appear_In_Teacher_B_Row()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.UploadAsync(1, 1, "a-file", "item-a");
        var matrix = await fixture.Matrix.GetAsync(new EvidenceMatrixFilterDto { AcademicYearId = 1 });

        var teacherA = matrix.Rows.Single(x => x.TeacherId == 1);
        var teacherB = matrix.Rows.Single(x => x.TeacherId == 2);
        teacherA.Cells.Single(x => x.TaskId == 1).IsChecked.Should().BeTrue();
        teacherB.Cells.Single(x => x.TaskId == 1).IsChecked.Should().BeFalse();
        teacherB.CompletedTasksCount.Should().Be(0);
    }

    [Fact]
    public async Task Academic_Years_Are_Isolated()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.UploadAsync(1, 1, "year-one", "item-y1");

        var firstYear = await fixture.Context.AcademicYears.SingleAsync(x => x.Id == 1);
        firstYear.IsActive = false;
        (await fixture.Context.AcademicYears.SingleAsync(x => x.Id == 2)).IsActive = true;
        await fixture.Context.SaveChangesAsync();
        await fixture.UploadAsync(1, 2, "year-two", "item-y2");

        var matrixYearOne = await fixture.Matrix.GetAsync(new EvidenceMatrixFilterDto { AcademicYearId = 1 });
        var matrixYearTwo = await fixture.Matrix.GetAsync(new EvidenceMatrixFilterDto { AcademicYearId = 2 });
        matrixYearOne.Rows.Single(x => x.TeacherId == 1).Cells.Single(x => x.TaskId == 1).IsChecked.Should().BeTrue();
        matrixYearOne.Rows.Single(x => x.TeacherId == 1).Cells.Single(x => x.TaskId == 2).IsChecked.Should().BeFalse();
        matrixYearTwo.Rows.Single(x => x.TeacherId == 1).Cells.Single(x => x.TaskId == 1).IsChecked.Should().BeFalse();
        matrixYearTwo.Rows.Single(x => x.TeacherId == 1).Cells.Single(x => x.TaskId == 2).IsChecked.Should().BeTrue();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly HttpContextAccessor _httpContextAccessor = new();
        public AlFalahDbContext Context { get; }
        public EvidenceSubmissionService Service { get; }
        public EvidenceMatrixService Matrix { get; }

        private Fixture(AlFalahDbContext context, EvidenceSubmissionService service, EvidenceMatrixService matrix)
        {
            Context = context;
            Service = service;
            Matrix = matrix;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<AlFalahDbContext>()
                .UseInMemoryDatabase($"evidence-matrix-{Guid.NewGuid()}")
                .Options;
            var context = new AlFalahDbContext(options);
            var school = new School { Id = 1, Name = "مدرسة الاختبار", City = "الرياض", IsActive = true };
            var userA = User("TEACHER-A", "المعلم", "أ");
            var userB = User("TEACHER-B", "المعلم", "ب");
            context.AddRange(
                school,
                userA,
                userB,
                new InstructorProfile { Id = 1, UserId = userA.Id, SchoolId = 1, User = userA, School = school, IsActive = true },
                new InstructorProfile { Id = 2, UserId = userB.Id, SchoolId = 1, User = userB, School = school, IsActive = true },
                new AcademicYear { Id = 1, Code = "Y1", NameAr = "السنة الأولى", StartsOn = new DateOnly(2025, 8, 1), EndsOn = new DateOnly(2026, 7, 31), IsActive = true },
                new AcademicYear { Id = 2, Code = "Y2", NameAr = "السنة الثانية", StartsOn = new DateOnly(2026, 8, 1), EndsOn = new DateOnly(2027, 7, 31), IsActive = false },
                new EvidenceTask { Id = 1, Code = "TASK-1", NameAr = "المهمة الأولى", Category = "فئة", CategorySortOrder = 1, SortOrder = 1, IsActive = true },
                new EvidenceTask { Id = 2, Code = "TASK-2", NameAr = "المهمة الثانية", Category = "فئة", CategorySortOrder = 1, SortOrder = 2, IsActive = true });
            await context.SaveChangesAsync();

            var manager = CurrentUser(RoleNames.SchoolManager, "MANAGER-1", 1);
            var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            var audit = new AuditLogWriter(context, accessor, NullLogger<AuditLogWriter>.Instance);
            var service = new EvidenceSubmissionService(context, audit);
            var guard = new SchoolScopeGuard(context, manager, NullLogger<SchoolScopeGuard>.Instance);
            var matrix = new EvidenceMatrixService(context, manager, guard, audit, service);
            return new Fixture(context, service, matrix);
        }

        public async Task<long> UploadAsync(int teacherId, int taskId, string requestId, string driveItemId)
        {
            var reservation = await Service.ReserveUploadAsync(teacherId, 1, taskId, requestId);
            var result = await Service.RecordCompletedUploadAsync(reservation.OperationId, teacherId, 1, "drive-1", "root",
                new AlFalah.Application.DTOs.TeacherDrive.DriveItemDto(driveItemId, $"{driveItemId}.pdf", false, null, "pdf", "application/pdf", 42,
                    DateTimeOffset.UtcNow, null, "https://example.test/file", "etag", null));
            return result.SubmissionId;
        }

        public Task<int> CountCompletedTasksAsync(int teacherId, int academicYearId) => Context.TeacherTaskStatuses
            .CountAsync(x => x.TeacherId == teacherId && x.AcademicYearId == academicYearId && x.ActiveFilesCount > 0);

        public ValueTask DisposeAsync() => Context.DisposeAsync();

        private static ApplicationUser User(string id, string firstName, string lastName) => new()
        {
            Id = id, UserName = id, NormalizedUserName = id, FirstName = firstName, LastName = lastName
        };

        private static AlFalah.Application.Interfaces.ICurrentUserService CurrentUser(string role, string userId, int schoolId) =>
            new TestCurrentUser(role, userId, schoolId);
    }

    private sealed class TestCurrentUser : AlFalah.Application.Interfaces.ICurrentUserService
    {
        private readonly string _role;
        public TestCurrentUser(string role, string userId, int schoolId) { _role = role; UserId = userId; ActiveSchoolId = schoolId; }
        public string? UserId { get; }
        public string? Username => UserId;
        public int? ActiveSchoolId { get; }
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => _role == roleName;
        public bool HasPermission(string permissionName) => true;
        public IEnumerable<string> GetRoles() => [_role];
        public IEnumerable<string> GetPermissions() => [];
        public bool IsGlobalAdmin() => _role is RoleNames.SuperAdmin or RoleNames.MainManager;
        public bool IsSchoolScopedRole() => !IsGlobalAdmin();
    }
}
