using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs;
using AlFalah.Application.StudentAffairs.DTOs.Settings;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.Settings;
using AlFalah.Application.StudentAffairs.Settings.Handlers;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using FluentAssertions;
using MediatR;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class StudentAffairsSettingsHandlerTests
{
    [Fact]
    public void ApplicationAssembly_Contains_GetStudentAffairsSettingsQueryHandler()
    {
        var handlerContract = typeof(IRequestHandler<
            GetStudentAffairsSettingsQuery,
            ApiResponse<SchoolStudentAffairsSettingsDto>>);

        typeof(StudentAffairsAssemblyMarker).Assembly
            .GetTypes()
            .Should()
            .Contain(type => !type.IsAbstract && handlerContract.IsAssignableFrom(type));
    }

    [Fact]
    public async Task GetSettings_WhenNoSettingsExist_ReturnsDefaultBaseline()
    {
        var repo = new StubSettingsRepository();
        var currentUser = new StubCurrentUser("manager-1", 10, PermissionNames.StudentAffairsSettingsView);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 6, 0, 0, TimeSpan.Zero));
        var handler = new GetStudentAffairsSettingsQueryHandler(repo, currentUser, timeProvider);

        var result = await handler.Handle(new GetStudentAffairsSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().BeNull();
        result.Data.UsesLockedDefaults.Should().BeTrue();
        result.Data.AbsenceVisualAlertThresholdPerTerm.Should().Be(3);
        result.Data.AbsenceReferralThresholdPerTerm.Should().Be(5);
        result.Data.AbsenceChildRightsThresholdPerTerm.Should().Be(10);
        result.Data.MorningDelayThresholdPerTerm.Should().Be(10);
        result.Data.BehaviorIncidentMultiplePerTerm.Should().Be(10);
        result.Data.AcademicConcernThresholdPerTerm.Should().Be(3);
        result.Data.ClassroomEntryPermitThresholdPerTerm.Should().Be(5);
        result.Data.BehaviorCountabilityPolicy.Should().Be("all-upheld");
    }

    [Fact]
    public async Task GetSettings_WhenSettingsExist_ReturnsCustomSettings()
    {
        var customDto = new SchoolStudentAffairsSettingsDto(
            Id: 42,
            MorningDelayThresholdPerTerm: 7,
            BehaviorIncidentMultiplePerTerm: 12,
            AcademicConcernThresholdPerTerm: 4,
            ClassroomEntryPermitThresholdPerTerm: 6,
            AbsenceVisualAlertThresholdPerTerm: 2,
            AbsenceReferralThresholdPerTerm: 4,
            AbsenceChildRightsThresholdPerTerm: 8,
            BehaviorCountabilityPolicy: "all-upheld",
            ArrivalCutoffLocalTime: new TimeOnly(7, 30),
            ArrivalGraceMinutes: 15,
            EffectiveVersion: 3,
            EffectiveFrom: DateTimeOffset.UtcNow,
            UsesLockedDefaults: false,
            RowVersion: "QUJDREVGR0g="
        );
        var repo = new StubSettingsRepository { SettingsDto = customDto };
        var currentUser = new StubCurrentUser("manager-1", 10, PermissionNames.StudentAffairsSettingsView);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 6, 0, 0, TimeSpan.Zero));
        var handler = new GetStudentAffairsSettingsQueryHandler(repo, currentUser, timeProvider);

        var result = await handler.Handle(new GetStudentAffairsSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(42);
        result.Data.UsesLockedDefaults.Should().BeFalse();
        result.Data.AbsenceVisualAlertThresholdPerTerm.Should().Be(2);
        result.Data.AbsenceReferralThresholdPerTerm.Should().Be(4);
        result.Data.AbsenceChildRightsThresholdPerTerm.Should().Be(8);
        result.Data.ArrivalCutoffLocalTime.Should().Be(new TimeOnly(7, 30));
    }

    [Fact]
    public async Task CreateSettings_WithInvalidThresholdOrder_FailsValidation()
    {
        var repo = new StubSettingsRepository();
        var currentUser = new StubCurrentUser("officer-1", 10, PermissionNames.StudentAffairsSettingsManage);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 6, 0, 0, TimeSpan.Zero));
        var handler = new CreateStudentAffairsSettingsCommandHandler(repo, currentUser, timeProvider);

        // Visual (5) >= Referral (5) -> Invalid
        var request = new CreateStudentAffairsSettingsRequestDto(
            MorningDelayThresholdPerTerm: 10,
            BehaviorIncidentMultiplePerTerm: 10,
            AcademicConcernThresholdPerTerm: 3,
            ClassroomEntryPermitThresholdPerTerm: 5,
            AbsenceVisualAlertThresholdPerTerm: 5,
            AbsenceReferralThresholdPerTerm: 5,
            AbsenceChildRightsThresholdPerTerm: 10,
            BehaviorCountabilityPolicy: "all-upheld",
            ArrivalCutoffLocalTime: new TimeOnly(7, 0),
            ArrivalGraceMinutes: 0
        );

        var result = await handler.Handle(new CreateStudentAffairsSettingsCommand(request), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainMatch("*Absence escalation thresholds must strictly follow*");
    }

    [Fact]
    public async Task ResetSettings_ReturnsDefaultBaseline()
    {
        var existingEntity = new SchoolStudentAffairsSettings
        {
            Id = 1,
            SchoolId = 10,
            RowVersion = new byte[] { 1, 2, 3, 4 }
        };
        var repo = new StubSettingsRepository { ExistingEntity = existingEntity };
        var currentUser = new StubCurrentUser("officer-1", 10, PermissionNames.StudentAffairsSettingsManage);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 6, 0, 0, TimeSpan.Zero));
        var handler = new ResetStudentAffairsSettingsCommandHandler(repo, currentUser, timeProvider);

        var request = new ResetStudentAffairsSettingsRequestDto(
            Reason: "Reverting to default baseline",
            RowVersion: Convert.ToBase64String(new byte[] { 1, 2, 3, 4 })
        );

        var result = await handler.Handle(new ResetStudentAffairsSettingsCommand(request), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.UsesLockedDefaults.Should().BeTrue();
        existingEntity.IsDeleted.Should().BeTrue();
    }

    private sealed class StubSettingsRepository : IStudentAffairsSettingsRepository
    {
        public SchoolStudentAffairsSettings? ExistingEntity { get; set; }
        public SchoolStudentAffairsSettingsDto? SettingsDto { get; set; }
        public SchoolStudentAffairsSettings? AddedEntity { get; private set; }

        public Task<SchoolStudentAffairsSettings?> GetSettingsAsync(int schoolId, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingEntity);

        public Task<SchoolStudentAffairsSettings?> GetSettingsForUpdateAsync(int schoolId, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingEntity);

        public Task<SchoolStudentAffairsSettingsDto?> GetSettingsDtoAsync(int schoolId, CancellationToken cancellationToken) =>
            Task.FromResult(SettingsDto);

        public Task<PagedResult<StudentAffairsSettingsHistoryDto>> GetHistoryAsync(
            int schoolId, StudentAffairsPageQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<StudentAffairsSettingsHistoryDto>());

        public void AddSettings(SchoolStudentAffairsSettings settings) =>
            AddedEntity = settings;

        public void SetExpectedRowVersion(SchoolStudentAffairsSettings settings, byte[] rowVersion) { }

        public void WriteAudit(int schoolId, string userId, string action, string? entityId, string? reason, object? oldValues, object? newValues) { }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(1);
    }

    private sealed class StubCurrentUser(
        string userId,
        int schoolId,
        params string[] permissions) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? Username => "test.user";
        public int? ActiveSchoolId => schoolId;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => true;
        public bool HasPermission(string permissionName) => permissions.Contains(permissionName);
        public IEnumerable<string> GetRoles() => new[] { RoleNames.SchoolManager, RoleNames.StudentAffairsOfficer };
        public IEnumerable<string> GetPermissions() => permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
