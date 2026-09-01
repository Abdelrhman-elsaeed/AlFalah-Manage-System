using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.Attendance;
using AlFalah.Application.StudentAffairs.Attendance.Handlers;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.MorningDelays;
using AlFalah.Application.StudentAffairs.MorningDelays.Handlers;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class AttendanceAndDelayWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 6, 45, 0, TimeSpan.Zero);

    [Fact]
    public async Task AttendanceAggregates_WriteEventsToTransactionalOutbox()
    {
        await using var context = CreateContext();
        var attendance = NewAttendance(StudentAttendanceStatus.Absent);
        attendance.AppendDomainEvent(new StudentAbsentRecordedEvent(
            Guid.NewGuid(), 0, 17, 42, 4, 12, new DateOnly(2026, 8, 30), "secretary", Now, Now));
        var excuse = NewExcuse(attendance);
        excuse.AppendDomainEvent(new AcbXX3KgvqD7B8Y4WjCu6yNx1Prfu5cNHz(
            Guid.NewGuid(), 0, 0, 17, 42, 4, 9, AbsenceExcuseType.Medical, Now, Now));
        var delay = NewDelay();
        delay.AppendDomainEvent(new MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3(
            Guid.NewGuid(), 0, 17, 42, 4, Now, new DateOnly(2026, 8, 30), new TimeOnly(6, 30),
            15, "ImmediateGuardian", Now));

        context.DailyStudentAttendances.Add(attendance);
        context.AbsenceExcuses.Add(excuse);
        context.MorningArrivalDelays.Add(delay);
        await context.SaveChangesAsync();

        var eventTypes = await context.OutboxMessages.Select(message => message.EventType).ToListAsync();
        eventTypes.Should().Contain(type => type.EndsWith(nameof(StudentAbsentRecordedEvent)));
        eventTypes.Should().Contain(type => type.EndsWith(nameof(AcbXX3KgvqD7B8Y4WjCu6yNx1Prfu5cNHz)));
        eventTypes.Should().Contain(type => type.EndsWith(nameof(MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3)));
    }

    [Fact]
    public async Task SaveSheet_MarksSubmittedStudentAbsentAndEveryoneElsePresent()
    {
        var repository = new FakeAttendanceRepository
        {
            Roster = new[]
            {
                new AttendanceRosterStudentSnapshot(17, 4),
                new AttendanceRosterStudentSnapshot(18, 4),
                new AttendanceRosterStudentSnapshot(19, 4)
            }
        };
        var handler = new SaveStudentAttendanceSheetCommandHandler(
            repository,
            CurrentUser(RoleNames.Secretary, PermissionNames.AttendanceManageStudents),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(
            new SubmitAbsentRosterCommand(
                new SubmitAbsentRosterRequestDto(
                    new DateOnly(2026, 8, 30), 12, new[] { 18 }, "revision-1"),
                "sheet-1"),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        repository.AddedAttendances.Should().HaveCount(3);
        repository.AddedAttendances.Single(row => row.StudentId == 18).Status
            .Should().Be(StudentAttendanceStatus.Absent);
        repository.AddedAttendances.Where(row => row.StudentId != 18)
            .Should().OnlyContain(row => row.Status == StudentAttendanceStatus.Present);
        repository.AddedAttendances.SelectMany(row => row.DomainEvents)
            .Should().ContainSingle().Which.Should().BeOfType<StudentAbsentRecordedEvent>();
        repository.SchoolIds.Should().OnlyContain(schoolId => schoolId == 42);
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task SubmitExcuse_RequiresGuardianCapabilityAndAppendsPendingEvent()
    {
        var attendance = NewAttendance(StudentAttendanceStatus.Absent);
        attendance.Id = 5;
        var repository = new FakeAttendanceRepository
        {
            TrackedAttendance = attendance,
            GuardianLink = new GuardianExcuseLinkSnapshot(
                9, true, true, true, new DateOnly(2026, 1, 1), null)
        };
        var file = new FakeFileStorage();
        var handler = new SubmitAbsenceExcuseCommandHandler(
            repository,
            file,
            CurrentUser(RoleNames.Guardian, PermissionNames.AttendanceSubmitExcuse),
            new FixedTimeProvider(Now));
        await using var content = new MemoryStream(new byte[] { 1, 2, 3 });

        var response = await handler.Handle(new SubmitAbsenceExcuseCommand(
            attendance.Id,
            new SubmitAbsenceExcuseRequestDto(AbsenceExcuseType.Medical, "Medical note"),
            "excuse-1",
            content,
            "excuse.pdf",
            "application/pdf",
            3), CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        repository.AddedExcuse.Should().NotBeNull();
        repository.AddedExcuse!.Status.Should().Be(AbsenceExcuseStatus.Pending);
        repository.AddedExcuse.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AcbXX3KgvqD7B8Y4WjCu6yNx1Prfu5cNHz>();
        attendance.ExcuseStatus.Should().Be(AbsenceExcuseStatus.Pending);
        file.StoredSchoolId.Should().Be(42);
    }

    [Fact]
    public async Task AcceptExcuse_UpdatesExcuseSnapshotButPreservesOfficialAbsentStatus()
    {
        var attendance = NewAttendance(StudentAttendanceStatus.Absent);
        attendance.Id = 5;
        var excuse = NewExcuse(attendance);
        excuse.Id = 6;
        excuse.RowVersion = new byte[] { 1, 2, 3 };
        var repository = new FakeAttendanceRepository { TrackedExcuse = excuse };
        var handler = new ReviewAbsenceExcuseCommandHandler(
            repository,
            CurrentUser(RoleNames.StudentAffairsOfficer, PermissionNames.AttendanceReviewExcuse),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(
            new AcceptAbsenceExcuseCommand(
                excuse.Id,
                new ReviewAbsenceExcuseRequestDto("Accepted", Convert.ToBase64String(excuse.RowVersion))),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        excuse.Status.Should().Be(AbsenceExcuseStatus.Accepted);
        attendance.Status.Should().Be(StudentAttendanceStatus.Absent);
        attendance.ExcuseStatus.Should().Be(AbsenceExcuseStatus.Accepted);
        excuse.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AbsenceExcuseAcceptedEvent>();
        repository.ExpectedRowVersion.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ReviewExcuse_WithStaleVersion_DoesNotMutateOrSave()
    {
        var attendance = NewAttendance(StudentAttendanceStatus.Absent);
        var excuse = NewExcuse(attendance);
        excuse.Id = 6;
        excuse.RowVersion = new byte[] { 1 };
        var repository = new FakeAttendanceRepository { TrackedExcuse = excuse };
        var handler = new ReviewAbsenceExcuseCommandHandler(
            repository,
            CurrentUser(RoleNames.StudentAffairsOfficer, PermissionNames.AttendanceReviewExcuse),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(
            new AcceptAbsenceExcuseCommand(
                excuse.Id,
                new ReviewAbsenceExcuseRequestDto(null, Convert.ToBase64String(new byte[] { 2 }))),
            CancellationToken.None);

        response.IsSuccess.Should().BeFalse();
        response.Errors.Should().ContainSingle("Absence excuse was modified by another user");
        excuse.Status.Should().Be(AbsenceExcuseStatus.Pending);
        attendance.ExcuseStatus.Should().BeNull();
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task BiometricDelay_UsesActiveSchoolAndAppendsEvent()
    {
        var repository = new FakeMorningDelayRepository
        {
            Enrollment = new MorningDelayEnrollmentSnapshot(4)
        };
        var handler = new LYG5YdYkoGF2AAQsjBx849E1QZt1wSrKra(
            repository,
            CurrentUser("System", "MorningDelay.RecordBiometric"),
            new FixedTimeProvider(Now));

        var response = await handler.Handle(new RecordBiometricMorningArrivalDelayCommand(
            17,
            Now,
            new DateOnly(2026, 8, 30),
            new TimeOnly(6, 30),
            15,
            null), CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
        repository.Added.Should().NotBeNull();
        repository.Added!.SchoolId.Should().Be(42);
        repository.Added.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3>();
        repository.SchoolIds.Should().OnlyContain(schoolId => schoolId == 42);
    }

    private static DailyStudentAttendance NewAttendance(StudentAttendanceStatus status) => new()
    {
        SchoolId = 42,
        StudentId = 17,
        AcademicTermId = 4,
        ClassroomId = 12,
        AttendanceDate = new DateOnly(2026, 8, 30),
        Status = status,
        RecordedByUserId = "secretary",
        CreatedByUserId = "secretary",
        UpdatedByUserId = "secretary"
    };

    private static AbsenceExcuse NewExcuse(DailyStudentAttendance attendance) => new()
    {
        SchoolId = 42,
        DailyStudentAttendance = attendance,
        DailyStudentAttendanceId = attendance.Id,
        GuardianProfileId = 9,
        IdempotencyKey = "excuse-1",
        ExcuseType = AbsenceExcuseType.Medical,
        Status = AbsenceExcuseStatus.Pending,
        CreatedByUserId = "guardian",
        UpdatedByUserId = "guardian"
    };

    private static MorningArrivalDelay NewDelay() => new()
    {
        SchoolId = 42,
        StudentId = 17,
        AcademicTermId = 4,
        ArrivalAt = Now,
        SchoolLocalDate = new DateOnly(2026, 8, 30),
        CutoffTimeSnapshot = new TimeOnly(6, 30),
        DelayMinutes = 15,
        CreatedByUserId = "system",
        UpdatedByUserId = "system"
    };

    private static TestCurrentUser CurrentUser(string role, string permission) =>
        new(42, "actor", role, permission);

    private static AlFalahDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"attendance-{Guid.NewGuid()}")
            .Options;
        return new AlFalahDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestCurrentUser(
        int schoolId,
        string userId,
        string role,
        string permission) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? Username => userId;
        public int? ActiveSchoolId => schoolId;
        public string? PreferredLanguage => "en";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == role;
        public bool HasPermission(string permissionName) => permissionName == permission;
        public IEnumerable<string> GetRoles() => new[] { role };
        public IEnumerable<string> GetPermissions() => new[] { permission };
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class FakeFileStorage : IFileStorageService
    {
        public int? StoredSchoolId { get; private set; }

        public Task<StoredFileResult> StoreAsync(
            int schoolId,
            Stream content,
            string originalFileName,
            string contentType,
            CancellationToken cancellationToken)
        {
            StoredSchoolId = schoolId;
            return Task.FromResult(new StoredFileResult("Fake", "key", new string('a', 64), content.Length));
        }

        public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<byte[]?> ReadBytesAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<byte[]?>(new byte[] { 1, 2, 3 });
    }

    private sealed class FakeAttendanceRepository : IAttendanceWorkflowRepository
    {
        public IReadOnlyList<AttendanceRosterStudentSnapshot> Roster { get; init; } =
            Array.Empty<AttendanceRosterStudentSnapshot>();
        public IReadOnlyList<DailyStudentAttendance> ExistingRows { get; init; } =
            Array.Empty<DailyStudentAttendance>();
        public DailyStudentAttendance? TrackedAttendance { get; init; }
        public GuardianExcuseLinkSnapshot? GuardianLink { get; init; }
        public AbsenceExcuse? TrackedExcuse { get; init; }
        public List<DailyStudentAttendance> AddedAttendances { get; } = new();
        public AbsenceExcuse? AddedExcuse { get; private set; }
        public List<int> SchoolIds { get; } = new();
        public byte[]? ExpectedRowVersion { get; private set; }
        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<AttendanceRosterStudentSnapshot>> GetActiveRosterAsync(
            int schoolId, int classroomId, DateOnly attendanceDate, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Roster);
        }

        public Task<IReadOnlyList<DailyStudentAttendance>> GetAttendanceSheetForUpdateAsync(
            int schoolId, int classroomId, DateOnly attendanceDate, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(ExistingRows);
        }

        public Task<DailyStudentAttendance?> GetAttendanceForUpdateAsync(
            int schoolId, int attendanceId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(TrackedAttendance);
        }

        public Task<GuardianExcuseLinkSnapshot?> GetGuardianExcuseLinkAsync(
            int schoolId, string guardianUserId, int studentId, DateOnly onDate,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(GuardianLink);
        }

        public Task<AbsenceExcuseDto?> GetExcuseByIdempotencyKeyAsync(
            int schoolId, int guardianProfileId, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult<AbsenceExcuseDto?>(null);
        }

        public Task<AbsenceExcuse?> GetExcuseForUpdateAsync(
            int schoolId, int excuseId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(TrackedExcuse);
        }

        public void AddAttendance(DailyStudentAttendance attendance)
        {
            attendance.Id = 100 + AddedAttendances.Count;
            AddedAttendances.Add(attendance);
        }

        public void AddExcuse(AbsenceExcuse excuse)
        {
            excuse.Id = 200;
            excuse.RowVersion = new byte[] { 9 };
            AddedExcuse = excuse;
        }

        public void SetExpectedRowVersion(AbsenceExcuse excuse, byte[] rowVersion) =>
            ExpectedRowVersion = rowVersion;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }

        public Task<StudentAttendanceSheetDto?> GetAttendanceSheetDtoAsync(
            int schoolId, int classroomId, DateOnly attendanceDate, string rosterRevision,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult<StudentAttendanceSheetDto?>(new StudentAttendanceSheetDto(
                attendanceDate,
                new ClassroomSummaryDto(classroomId, "1/A", "Primary", 1, "A"),
                rosterRevision,
                true,
                Array.Empty<StudentAttendanceSheetRowDto>()));
        }

        public Task<AbsenceExcuseDto?> GetExcuseDtoAsync(
            int schoolId, int excuseId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            var excuse = AddedExcuse ?? TrackedExcuse;
            if (excuse is null) return Task.FromResult<AbsenceExcuseDto?>(null);
            return Task.FromResult<AbsenceExcuseDto?>(new AbsenceExcuseDto(
                excuse.Id,
                excuse.ExcuseType,
                excuse.Status,
                new GuardianSummaryDto(9, "Guardian", GuardianRelationshipType.Father, true, true),
                excuse.SubmittedAt,
                null,
                excuse.ReviewedAt,
                excuse.ReviewReason,
                Array.Empty<AttachmentDto>(),
                Convert.ToBase64String(excuse.RowVersion)));
        }

        public Task<IReadOnlyList<AbsenceExcuseDto>> GetExcusesByAttendanceIdAsync(
            int schoolId, int attendanceId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            var excuse = AddedExcuse ?? TrackedExcuse;
            if (excuse is null) return Task.FromResult<IReadOnlyList<AbsenceExcuseDto>>(Array.Empty<AbsenceExcuseDto>());
            return Task.FromResult<IReadOnlyList<AbsenceExcuseDto>>(new[]
            {
                new AbsenceExcuseDto(
                    excuse.Id,
                    excuse.ExcuseType,
                    excuse.Status,
                    new GuardianSummaryDto(9, "Guardian", GuardianRelationshipType.Father, true, true),
                    excuse.SubmittedAt,
                    null,
                    excuse.ReviewedAt,
                    excuse.ReviewReason,
                    Array.Empty<AttachmentDto>(),
                    Convert.ToBase64String(excuse.RowVersion))
            });
        }

        public Task<PagedResult<StudentAttendanceRecordDto>> GetAttendanceRecordsAsync(
            int schoolId, StudentAttendanceRecordsQuery query, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(new PagedResult<StudentAttendanceRecordDto>
            {
                Items = Array.Empty<StudentAttendanceRecordDto>(),
                TotalCount = 0,
                Page = query.PageNumber,
                PageSize = query.PageSize
            });
        }

        public Task<StudentAttendanceHistoryDto?> GetStudentAttendanceHistoryAsync(
            int schoolId, int studentId, int? academicTermId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult<StudentAttendanceHistoryDto?>(new StudentAttendanceHistoryDto(
                new StudentSummaryDto(studentId, "S-1", "Student", 1, "1/A", true, null),
                new AcademicTermSummaryDto(1, "Term 1", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), true),
                Array.Empty<StudentAttendanceRecordDto>(),
                new MetricBadgeDto(StudentTermMetricCode.PenaltyAbsenceDay, 0, 0, null, "None", null, DateTimeOffset.UtcNow)));
        }

        public Task<StudentAttendanceRecordDto?> GetAttendanceRecordDtoAsync(
            int schoolId, int attendanceId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult<StudentAttendanceRecordDto?>(new StudentAttendanceRecordDto(
                attendanceId,
                new StudentSummaryDto(1, "S-1", "Student", 1, "1/A", true, null),
                new DateOnly(2026, 8, 30),
                StudentAttendanceStatus.Present,
                null,
                new ActorSummaryDto("sec-1", "Secretary", RoleNames.Secretary),
                DateTimeOffset.UtcNow,
                Convert.ToBase64String(new byte[] { 1 })));
        }

        public Task<(AbsenceExcuseAttachment Attachment, AbsenceExcuse Excuse)?> GetExcuseAttachmentAsync(
            int schoolId, int excuseId, int attachmentId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult<(AbsenceExcuseAttachment Attachment, AbsenceExcuse Excuse)?>(null);
        }
    }

    private sealed class FakeMorningDelayRepository : IMorningDelayWorkflowRepository
    {
        public MorningDelayEnrollmentSnapshot? Enrollment { get; init; }
        public MorningArrivalDelay? Added { get; private set; }
        public List<int> SchoolIds { get; } = new();

        public Task<MorningDelayEnrollmentSnapshot?> GetActiveEnrollmentAsync(
            int schoolId, int studentId, DateOnly onDate, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Enrollment);
        }

        public Task<MorningDelayDto?> GetExistingAsync(
            int schoolId, int studentId, DateOnly schoolLocalDate, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult<MorningDelayDto?>(null);
        }

        public void Add(MorningArrivalDelay delay)
        {
            delay.Id = 300;
            Added = delay;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);

        public Task<MorningDelayDto?> GetDtoAsync(
            int schoolId, int delayId, CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            var delay = Added!;
            return Task.FromResult<MorningDelayDto?>(new MorningDelayDto(
                delay.Id,
                new StudentSummaryDto(delay.StudentId, "S-1", "Student", 12, "1/A", true, null),
                delay.ArrivalAt,
                delay.ArrivalAt.ToString("HH:mm"),
                "UTC",
                delay.CutoffTimeSnapshot,
                delay.DelayMinutes,
                delay.Reason,
                new MetricBadgeDto(
                    StudentTermMetricCode.MorningArrivalDelay, 1, 1, 10, "None", delay.ArrivalAt, delay.ArrivalAt),
                null,
                string.Empty));
        }
    }
}
