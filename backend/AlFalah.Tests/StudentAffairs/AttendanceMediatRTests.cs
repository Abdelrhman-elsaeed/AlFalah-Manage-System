using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs;
using AlFalah.Application.StudentAffairs.Attendance;
using AlFalah.Application.StudentAffairs.Attendance.Handlers;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class AttendanceMediatRTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ApplicationAssembly_Registers_All_Attendance_And_Excuse_Handlers()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(StudentAffairsAssemblyMarker).Assembly));

        // Stub dependencies
        services.AddSingleton<IAttendanceWorkflowRepository, StubAttendanceWorkflowRepository>();
        services.AddSingleton<IFileStorageService, StubFileStorageService>();
        services.AddSingleton<ICurrentUserService>(new StubCurrentUser(
            "officer-1",
            1,
            PermissionNames.AttendanceViewStudents,
            PermissionNames.AttendanceManageStudents,
            PermissionNames.AttendanceReviewExcuse,
            PermissionNames.AttendanceOverrideCorrection,
            PermissionNames.AttendanceSubmitExcuse,
            PermissionNames.NoorExport));
        services.AddSingleton<INoorExportRepository, StubNoorExportRepository>();
        services.AddSingleton<INoorWorkbookWriter, StubNoorWorkbookWriter>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        var provider = services.BuildServiceProvider();

        // Queries
        provider.GetService<IRequestHandler<GetAbsenceExcusesQuery, ApiResponse<IReadOnlyList<AbsenceExcuseDto>>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetStudentAttendanceRecordsQuery, ApiResponse<PagedResult<StudentAttendanceRecordDto>>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetStudentAttendanceSheetQuery, ApiResponse<StudentAttendanceSheetDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetStudentAttendanceHistoryQuery, ApiResponse<StudentAttendanceHistoryDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<DownloadAbsenceExcuseAttachmentQuery, AuthorizedFileDto>>().Should().NotBeNull();

        // Commands
        provider.GetService<IRequestHandler<SubmitAbsentRosterCommand, ApiResponse<StudentAttendanceSheetDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<SubmitAbsenceExcuseCommand, ApiResponse<AbsenceExcuseDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<AcceptAbsenceExcuseCommand, ApiResponse<AbsenceExcuseDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<RejectAbsenceExcuseCommand, ApiResponse<AbsenceExcuseDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<CorrectStudentAttendanceCommand, ApiResponse<StudentAttendanceRecordDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<ExportNoorAbsenceCorrectionsCommand, ApiResponse<NoorExportFileDto>>>().Should().NotBeNull();
    }

    [Fact]
    public async Task GetAbsenceExcusesQueryHandler_Returns_Excuses_When_Authorized()
    {
        var stubRepo = new StubAttendanceWorkflowRepository();
        var currentUser = new StubCurrentUser("officer-1", 1, PermissionNames.AttendanceViewStudents);
        var handler = new GetAbsenceExcusesQueryHandler(stubRepo, currentUser);

        var result = await handler.Handle(new GetAbsenceExcusesQuery(42), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Should().HaveCount(1);
        result.Data![0].Id.Should().Be(101);
    }

    [Fact]
    public async Task GetAbsenceExcusesQueryHandler_Returns_Fail_When_Unauthorized()
    {
        var stubRepo = new StubAttendanceWorkflowRepository();
        var currentUser = new StubCurrentUser("stranger-1", 1); // No attendance permissions
        var handler = new GetAbsenceExcusesQueryHandler(stubRepo, currentUser);

        var result = await handler.Handle(new GetAbsenceExcusesQuery(42), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(AttendanceHandlerSupport.PermissionDenied);
    }

    [Fact]
    public async Task GetStudentAttendanceRecordsQueryHandler_Returns_Paged_Records_When_Authorized()
    {
        var stubRepo = new StubAttendanceWorkflowRepository();
        var currentUser = new StubCurrentUser("officer-1", 1, PermissionNames.AttendanceViewStudents);
        var handler = new GetStudentAttendanceRecordsQueryHandler(stubRepo, currentUser);

        var result = await handler.Handle(
            new GetStudentAttendanceRecordsQuery(new StudentAttendanceRecordsQuery
            {
                ExcuseStatus = AbsenceExcuseStatus.Pending,
                PageNumber = 1,
                PageSize = 25
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items.Should().HaveCount(1);
        result.Data.Items[0].ExcuseStatus.Should().Be(AbsenceExcuseStatus.Pending);
    }

    [Fact]
    public async Task GetStudentAttendanceSheetQueryHandler_Returns_Sheet_When_Authorized()
    {
        var stubRepo = new StubAttendanceWorkflowRepository();
        var currentUser = new StubCurrentUser("officer-1", 1, PermissionNames.AttendanceViewStudents);
        var handler = new GetStudentAttendanceSheetQueryHandler(stubRepo, currentUser);

        var result = await handler.Handle(
            new GetStudentAttendanceSheetQuery(new DateOnly(2026, 8, 30), 10),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Classroom.Id.Should().Be(10);
    }

    [Fact]
    public async Task GetStudentAttendanceHistoryQueryHandler_Returns_History_When_Authorized()
    {
        var stubRepo = new StubAttendanceWorkflowRepository();
        var currentUser = new StubCurrentUser("officer-1", 1, PermissionNames.AttendanceViewStudents);
        var handler = new GetStudentAttendanceHistoryQueryHandler(stubRepo, currentUser);

        var result = await handler.Handle(
            new GetStudentAttendanceHistoryQuery(5, 1),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Student.Id.Should().Be(5);
    }

    [Fact]
    public async Task CorrectStudentAttendanceCommandHandler_Updates_Status_And_Returns_Dto()
    {
        var attendance = new DailyStudentAttendance
        {
            Id = 50,
            SchoolId = 1,
            StudentId = 5,
            AcademicTermId = 1,
            ClassroomId = 10,
            AttendanceDate = new DateOnly(2026, 8, 30),
            Status = StudentAttendanceStatus.Absent,
            ExcuseStatus = AbsenceExcuseStatus.Pending,
            RowVersion = new byte[] { 1, 2, 3 }
        };
        var stubRepo = new StubAttendanceWorkflowRepository { TrackedAttendance = attendance };
        var currentUser = new StubCurrentUser("officer-1", 1, PermissionNames.AttendanceOverrideCorrection);
        var handler = new CorrectStudentAttendanceCommandHandler(
            stubRepo,
            currentUser,
            TimeProvider.System);

        var result = await handler.Handle(
            new CorrectStudentAttendanceCommand(
                50,
                new CorrectStudentAttendanceRequestDto(
                    StudentAttendanceStatus.Present,
                    "Student attended first period",
                    Convert.ToBase64String(attendance.RowVersion))),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        attendance.Status.Should().Be(StudentAttendanceStatus.Present);
        attendance.ExcuseStatus.Should().BeNull();
        attendance.CorrectionReason.Should().Be("Student attended first period");
    }

    [Fact]
    public async Task DownloadAbsenceExcuseAttachmentQueryHandler_Returns_Authorized_File()
    {
        var stubRepo = new StubAttendanceWorkflowRepository();
        var stubStorage = new StubFileStorageService();
        var currentUser = new StubCurrentUser("officer-1", 1, PermissionNames.AttendanceViewStudents);
        var handler = new DownloadAbsenceExcuseAttachmentQueryHandler(stubRepo, stubStorage, currentUser);

        var file = await handler.Handle(
            new DownloadAbsenceExcuseAttachmentQuery(101, 201),
            CancellationToken.None);

        file.Should().NotBeNull();
        file.FileName.Should().Be("medical_note.pdf");
        file.ContentType.Should().Be("application/pdf");
        file.Content.Should().NotBeEmpty();
    }

    // ─── Test Stubs ─────────────────────────────────────────────────────────

    private sealed class StubCurrentUser : ICurrentUserService
    {
        private readonly HashSet<string> _permissions;

        public StubCurrentUser(string? userId, int? activeSchoolId, params string[] permissions)
        {
            UserId = userId;
            ActiveSchoolId = activeSchoolId;
            _permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
        }

        public string? UserId { get; }
        public string? Username => UserId;
        public int? ActiveSchoolId { get; }
        public string? Role => RoleNames.StudentAffairsOfficer;
        public string? PreferredLanguage => "en";
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);
        public bool HasPermission(string permission) => _permissions.Contains(permission);
        public bool HasAllPermissions(params string[] permissions) => permissions.All(_permissions.Contains);
        public bool HasAnyPermission(params string[] permissions) => permissions.Any(_permissions.Contains);
        public bool IsInRole(string role) => true;
        public IEnumerable<string> GetRoles() => new[] { RoleNames.StudentAffairsOfficer };
        public IEnumerable<string> GetPermissions() => _permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class StubFileStorageService : IFileStorageService
    {
        public Task<StoredFileResult> StoreAsync(int schoolId, Stream content, string originalFileName, string contentType, CancellationToken cancellationToken) =>
            Task.FromResult(new StoredFileResult("Local", "key", new string('a', 64), 100));

        public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<byte[]?> ReadBytesAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<byte[]?>(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF header
    }

    private sealed class StubAttendanceWorkflowRepository : IAttendanceWorkflowRepository
    {
        public DailyStudentAttendance? TrackedAttendance { get; set; }

        public Task<IReadOnlyList<AttendanceRosterStudentSnapshot>> GetActiveRosterAsync(int schoolId, int classroomId, DateOnly attendanceDate, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AttendanceRosterStudentSnapshot>>(Array.Empty<AttendanceRosterStudentSnapshot>());

        public Task<IReadOnlyList<DailyStudentAttendance>> GetAttendanceSheetForUpdateAsync(int schoolId, int classroomId, DateOnly attendanceDate, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DailyStudentAttendance>>(Array.Empty<DailyStudentAttendance>());

        public Task<DailyStudentAttendance?> GetAttendanceForUpdateAsync(int schoolId, int attendanceId, CancellationToken cancellationToken) =>
            Task.FromResult(TrackedAttendance);

        public Task<GuardianExcuseLinkSnapshot?> GetGuardianExcuseLinkAsync(int schoolId, string guardianUserId, int studentId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult<GuardianExcuseLinkSnapshot?>(null);

        public Task<AbsenceExcuseDto?> GetExcuseByIdempotencyKeyAsync(int schoolId, int guardianProfileId, string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult<AbsenceExcuseDto?>(null);

        public Task<AbsenceExcuse?> GetExcuseForUpdateAsync(int schoolId, int excuseId, CancellationToken cancellationToken) =>
            Task.FromResult<AbsenceExcuse?>(null);

        public void AddAttendance(DailyStudentAttendance attendance) { }
        public void AddExcuse(AbsenceExcuse excuse) { }
        public void SetExpectedRowVersion(AbsenceExcuse excuse, byte[] rowVersion) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);

        public Task<StudentAttendanceSheetDto?> GetAttendanceSheetDtoAsync(int schoolId, int classroomId, DateOnly attendanceDate, string rosterRevision, CancellationToken cancellationToken) =>
            Task.FromResult<StudentAttendanceSheetDto?>(new StudentAttendanceSheetDto(
                attendanceDate,
                new ClassroomSummaryDto(classroomId, "1/A", "Primary", 1, "A"),
                rosterRevision,
                true,
                Array.Empty<StudentAttendanceSheetRowDto>()));

        public Task<AbsenceExcuseDto?> GetExcuseDtoAsync(int schoolId, int excuseId, CancellationToken cancellationToken) =>
            Task.FromResult<AbsenceExcuseDto?>(null);

        public Task<IReadOnlyList<AbsenceExcuseDto>> GetExcusesByAttendanceIdAsync(int schoolId, int attendanceId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AbsenceExcuseDto>>(new[]
            {
                new AbsenceExcuseDto(
                    101,
                    AbsenceExcuseType.Medical,
                    AbsenceExcuseStatus.Pending,
                    new GuardianSummaryDto(1, "Father", GuardianRelationshipType.Father, true, true),
                    Now,
                    null,
                    null,
                    null,
                    new[]
                    {
                        new AttachmentDto(201, "medical_note.pdf", "application/pdf", 1024, Now, new ActorSummaryDto("g-1", "Father", RoleNames.Guardian), "/attachments/201")
                    },
                    Convert.ToBase64String(new byte[] { 1, 2, 3 }))
            });

        public Task<PagedResult<StudentAttendanceRecordDto>> GetAttendanceRecordsAsync(int schoolId, StudentAttendanceRecordsQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<StudentAttendanceRecordDto>
            {
                Items = new List<StudentAttendanceRecordDto>
                {
                    new StudentAttendanceRecordDto(
                        42,
                        new StudentSummaryDto(5, "STU-005", "Ahmed Ali", 10, "1/A", true, null),
                        new DateOnly(2026, 8, 30),
                        StudentAttendanceStatus.Absent,
                        AbsenceExcuseStatus.Pending,
                        new ActorSummaryDto("sec-1", "Secretary", RoleNames.Secretary),
                        Now,
                        Convert.ToBase64String(new byte[] { 1, 2, 3 }))
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 25
            });

        public Task<StudentAttendanceHistoryDto?> GetStudentAttendanceHistoryAsync(int schoolId, int studentId, int? academicTermId, CancellationToken cancellationToken) =>
            Task.FromResult<StudentAttendanceHistoryDto?>(new StudentAttendanceHistoryDto(
                new StudentSummaryDto(studentId, "STU-005", "Ahmed Ali", 10, "1/A", true, null),
                new AcademicTermSummaryDto(1, "Term 1", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), true),
                Array.Empty<StudentAttendanceRecordDto>(),
                new MetricBadgeDto(StudentTermMetricCode.PenaltyAbsenceDay, 0, 0, null, "None", null, Now)));

        public Task<StudentAttendanceRecordDto?> GetAttendanceRecordDtoAsync(int schoolId, int attendanceId, CancellationToken cancellationToken) =>
            Task.FromResult<StudentAttendanceRecordDto?>(new StudentAttendanceRecordDto(
                attendanceId,
                new StudentSummaryDto(5, "STU-005", "Ahmed Ali", 10, "1/A", true, null),
                new DateOnly(2026, 8, 30),
                StudentAttendanceStatus.Present,
                null,
                new ActorSummaryDto("sec-1", "Secretary", RoleNames.Secretary),
                Now,
                Convert.ToBase64String(new byte[] { 1, 2, 3 })));

        public Task<(AbsenceExcuseAttachment Attachment, AbsenceExcuse Excuse)?> GetExcuseAttachmentAsync(int schoolId, int excuseId, int attachmentId, CancellationToken cancellationToken)
        {
            var excuse = new AbsenceExcuse
            {
                Id = excuseId,
                SchoolId = schoolId,
                DailyStudentAttendanceId = 42,
                GuardianProfileId = 1,
                Status = AbsenceExcuseStatus.Pending
            };
            var attachment = new AbsenceExcuseAttachment
            {
                Id = attachmentId,
                AbsenceExcuseId = excuseId,
                SchoolId = schoolId,
                OriginalFileName = "medical_note.pdf",
                ContentType = "application/pdf",
                StorageKey = "1/2026/08/sample.pdf",
                AbsenceExcuse = excuse
            };
            return Task.FromResult<(AbsenceExcuseAttachment Attachment, AbsenceExcuse Excuse)?>((attachment, excuse));
        }
    }

    private sealed class StubNoorExportRepository : INoorExportRepository
    {
        public Task<NoorAbsenceCorrectionBatch?> GetBatchAsync(int schoolId, string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult<NoorAbsenceCorrectionBatch?>(null);

        public Task<IReadOnlyList<NoorAcceptedExcuseSnapshot>> GetAcceptedExcusesAsync(int schoolId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NoorAcceptedExcuseSnapshot>>(Array.Empty<NoorAcceptedExcuseSnapshot>());

        public void Add(NoorAbsenceCorrectionBatch batch) { }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class StubNoorWorkbookWriter : INoorWorkbookWriter
    {
        public byte[] Write(IReadOnlyList<NoorWorkbookRow> rows) => new byte[] { 1, 2, 3 };
    }
}
