using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Application.StudentAffairs.Referrals;
using AlFalah.Application.StudentAffairs.Referrals.Handlers;
using AlFalah.Application.StudentAffairs.Summons;
using AlFalah.Application.StudentAffairs.Summons.Handlers;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class SocialWorkerWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetReferralsQuery_WhenAuthorized_ReturnsPagedResult()
    {
        var repository = new FakeReferralRepository();
        var handler = new GetReferralsQueryHandler(
            repository,
            UserWithRole(RoleNames.SocialWorker, PermissionNames.ReferralView));

        var result = await handler.Handle(
            new GetReferralsQuery(new ReferralListQuery { PageNumber = 1, PageSize = 10 }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Items.Should().NotBeNull();
        repository.SchoolIds.Should().OnlyContain(id => id == 42);
    }

    [Fact]
    public async Task GetReferralsQuery_WhenUnauthorized_ReturnsFail()
    {
        var repository = new FakeReferralRepository();
        var handler = new GetReferralsQueryHandler(
            repository,
            UserWithRole(RoleNames.Instructor));

        var result = await handler.Handle(
            new GetReferralsQuery(new ReferralListQuery()),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(ReferralHandlerSupport.PermissionDenied);
    }

    [Fact]
    public async Task CreateReferralCommand_WithValidEnrollment_CreatesAndReturnsDto()
    {
        var repository = new FakeReferralRepository
        {
            Enrollment = new ReferralEnrollmentSnapshot(4, 12, "1/A")
        };
        var handler = new CreateReferralCommandHandler(
            repository,
            UserWithRole(RoleNames.StudentAffairsOfficer, PermissionNames.ReferralCreate),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new CreateReferralCommand(
                new CreateReferralRequestDto(17, "Repeated unexcused absence", ReferralSourceType.Absence, ReferralPriority.High),
                "idem-1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        repository.AddedReferral.Should().NotBeNull();
        repository.AddedReferral!.StudentId.Should().Be(17);
        repository.AddedReferral.Status.Should().Be(StudentReferralStatus.Open);
        repository.AddedReferral.Priority.Should().Be(ReferralPriority.High);
    }

    [Fact]
    public async Task AssignReferralCommand_WhenAuthorized_AssignsSocialWorkerAndAddsAction()
    {
        var referral = NewReferral(StudentReferralStatus.Open);
        var repository = new FakeReferralRepository
        {
            Referral = referral,
            IsSocialWorker = true
        };
        var handler = new AssignReferralCommandHandler(
            repository,
            UserWithRole(RoleNames.StudentAffairsOfficer, PermissionNames.ReferralAssign),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new AssignReferralCommand(
                referral.Id,
                new AssignReferralRequestDto("worker-1", "Assigned for counseling", Convert.ToBase64String(referral.RowVersion))),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        referral.AssignedSocialWorkerUserId.Should().Be("worker-1");
        referral.Status.Should().Be(StudentReferralStatus.Assigned);
        repository.AddedActions.Should().ContainSingle();
    }

    [Fact]
    public async Task AcceptReferralCommand_MovesStatusToInProgress()
    {
        var referral = NewReferral(StudentReferralStatus.Assigned);
        referral.AssignedSocialWorkerUserId = "worker-1";
        var repository = new FakeReferralRepository
        {
            Referral = referral,
            IsAssigned = true
        };
        var handler = new AcceptReferralCommandHandler(
            repository,
            UserWithIdAndRole("worker-1", RoleNames.SocialWorker, PermissionNames.ReferralManage),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new AcceptReferralCommand(
                referral.Id,
                new AcceptReferralRequestDto(Convert.ToBase64String(referral.RowVersion))),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        referral.Status.Should().Be(StudentReferralStatus.InProgress);
    }

    [Fact]
    public async Task AddReferralActionCommand_AddsActionAndAdvancesStatus()
    {
        var referral = NewReferral(StudentReferralStatus.Assigned);
        var repository = new FakeReferralRepository
        {
            Referral = referral,
            IsAssigned = true
        };
        var handler = new AddReferralActionCommandHandler(
            repository,
            UserWithIdAndRole("worker-1", RoleNames.SocialWorker, PermissionNames.ReferralManage),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new AddReferralActionCommand(
                referral.Id,
                new AddReferralActionRequestDto(
                    StudentCaseActionType.CounselingSession,
                    "Conducted individual counseling session",
                    Now,
                    "Student agreed to action plan",
                    Convert.ToBase64String(referral.RowVersion))),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        referral.Status.Should().Be(StudentReferralStatus.InProgress);
        repository.AddedActions.Should().ContainSingle();
        repository.AddedActions[0].ActionType.Should().Be(StudentCaseActionType.CounselingSession);
    }

    [Fact]
    public async Task ResolveAndReopenReferralCommands_FollowWorkflow()
    {
        var referral = NewReferral(StudentReferralStatus.InProgress);
        var repository = new FakeReferralRepository
        {
            Referral = referral,
            IsAssigned = true
        };
        var time = new FixedTimeProvider(Now);
        var resolveHandler = new ResolveReferralCommandHandler(
            repository,
            UserWithIdAndRole("worker-1", RoleNames.SocialWorker, PermissionNames.ReferralManage),
            time);

        var resolveResult = await resolveHandler.Handle(
            new ResolveReferralCommand(
                referral.Id,
                new ResolveReferralRequestDto("Case resolved successfully after student showed improvement", Convert.ToBase64String(referral.RowVersion))),
            CancellationToken.None);

        resolveResult.IsSuccess.Should().BeTrue();
        referral.Status.Should().Be(StudentReferralStatus.Resolved);
        referral.ResolutionNotes.Should().Contain("improvement");

        var reopenHandler = new ReopenReferralCommandHandler(
            repository,
            UserWithIdAndRole("worker-1", RoleNames.SocialWorker, PermissionNames.ReferralManage),
            time);

        var reopenResult = await reopenHandler.Handle(
            new ReopenReferralCommand(
                referral.Id,
                new ReopenReferralRequestDto("Student relapsed into delay behavior", Convert.ToBase64String(referral.RowVersion))),
            CancellationToken.None);

        reopenResult.IsSuccess.Should().BeTrue();
        referral.Status.Should().Be(StudentReferralStatus.InProgress);
    }

    [Fact]
    public async Task GetSummonsQuery_WhenAuthorized_ReturnsPagedResult()
    {
        var repository = new FakeSummonRepository();
        var handler = new GetSummonsQueryHandler(
            repository,
            UserWithRole(RoleNames.SocialWorker, PermissionNames.SummonView));

        var result = await handler.Handle(
            new GetSummonsQuery(new SummonListQuery { PageNumber = 1, PageSize = 10 }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        repository.SchoolIds.Should().OnlyContain(id => id == 42);
    }

    [Fact]
    public async Task CreateSummonCommand_WithValidLinkedGuardian_CreatesSummon()
    {
        var repository = new FakeSummonRepository
        {
            GuardianLinkIsActive = true,
            Enrollment = new SummonEnrollmentSnapshot(4)
        };
        var handler = new CreateSummonCommandHandler(
            repository,
            UserWithRole(RoleNames.SocialWorker, PermissionNames.SummonCreate),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new CreateSummonCommand(
                new CreateSummonRequestDto(17, 101, "Attendance review meeting", ReferralPriority.High, 9),
                "idem-2"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.AddedSummon.Should().NotBeNull();
        repository.AddedSummon!.StudentId.Should().Be(17);
        repository.AddedSummon.GuardianProfileId.Should().Be(9);
        repository.AddedSummon.Status.Should().Be(GuardianSummonStatus.Pending);
    }

    [Fact]
    public async Task ReviewSummonAutomationImpactCommand_WhenAuthorized_UpdatesDecision()
    {
        var summon = NewSummon(GuardianSummonStatus.Pending);
        summon.RequiresOfficerReview = true;
        var repository = new FakeSummonRepository { Summon = summon };
        var handler = new ReviewSummonAutomationImpactCommandHandler(
            repository,
            UserWithRole(RoleNames.StudentAffairsOfficer, PermissionNames.SummonReviewAutomationImpact),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new ReviewSummonAutomationImpactCommand(
                summon.Id,
                new ReviewSummonAutomationImpactRequestDto(OfficerReviewDecision.Retain, "Impact justified by persistent pattern", Convert.ToBase64String(summon.RowVersion))),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        summon.RequiresOfficerReview.Should().BeFalse();
        summon.OfficerReviewDecision.Should().Be(OfficerReviewDecision.Retain);
        summon.OfficerReviewReason.Should().Contain("justified");
    }

    private static StudentReferral NewReferral(StudentReferralStatus status) => new()
    {
        Id = 101,
        SchoolId = 42,
        StudentId = 17,
        AcademicTermId = 4,
        SourceType = ReferralSourceType.Absence,
        Priority = ReferralPriority.High,
        Status = status,
        RowVersion = new byte[] { 1, 2, 3 },
        CreatedByUserId = "officer",
        UpdatedByUserId = "officer",
        CreatedAt = Now,
        UpdatedAt = Now
    };

    private static GuardianSummon NewSummon(GuardianSummonStatus status) => new()
    {
        Id = 201,
        SchoolId = 42,
        StudentId = 17,
        AcademicTermId = 4,
        GuardianProfileId = 9,
        CreatedReason = "Manual",
        Status = status,
        RowVersion = new byte[] { 1, 2, 3 },
        CreatedByUserId = "worker",
        UpdatedByUserId = "worker",
        CreatedAt = Now,
        UpdatedAt = Now
    };

    private static TestCurrentUser UserWithRole(string role, params string[] permissions) =>
        new(42, "worker-1", role, permissions);

    private static TestCurrentUser UserWithIdAndRole(string userId, string role, params string[] permissions) =>
        new(42, userId, role, permissions);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestCurrentUser(
        int schoolId,
        string userId,
        string role,
        string[] permissions) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? Username => userId;
        public int? ActiveSchoolId => schoolId;
        public string? PreferredLanguage => "en";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == role;
        public bool HasPermission(string permissionName) => permissions.Contains(permissionName);
        public IEnumerable<string> GetRoles() => new[] { role };
        public IEnumerable<string> GetPermissions() => permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class FakeReferralRepository : IReferralWorkflowRepository
    {
        public StudentReferral? Referral { get; init; }
        public StudentReferral? AddedReferral { get; private set; }
        public List<StudentCaseAction> AddedActions { get; } = new();
        public ReferralEnrollmentSnapshot? Enrollment { get; init; }
        public bool IsSocialWorker { get; init; } = true;
        public bool IsAssigned { get; init; } = true;
        public List<int> SchoolIds { get; } = new();
        public byte[]? ExpectedRowVersion { get; private set; }

        public Task<PagedResult<ReferralDto>> GetReferralsAsync(
            int schoolId,
            ReferralListQuery query,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(new PagedResult<ReferralDto>
            {
                Items = new List<ReferralDto>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 10
            });
        }

        public Task<ReferralDto?> GetDtoAsync(
            int schoolId,
            int referralId,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            var r = Referral ?? AddedReferral;
            if (r is null) return Task.FromResult<ReferralDto?>(null);
            return Task.FromResult<ReferralDto?>(new ReferralDto(
                r.Id,
                new StudentSummaryDto(r.StudentId, "S-1", "Student", 12, "1/A", true, null),
                new ReferralSourceSnapshotDto(r.SourceType, r.SourceEntityId, r.CountSnapshot, r.ThresholdSnapshot),
                null,
                r.Priority,
                r.Status,
                new ActorSummaryDto("worker-1", "Social Worker", RoleNames.SocialWorker),
                new List<StudentCaseActionDto>(),
                r.ResolutionNotes,
                r.CreatedAt,
                Convert.ToBase64String(r.RowVersion)));
        }

        public Task<StudentReferral?> GetForUpdateAsync(
            int schoolId,
            int referralId,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Referral);
        }

        public Task<ReferralEnrollmentSnapshot?> GetActiveEnrollmentAsync(
            int schoolId,
            int studentId,
            DateOnly onDate,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Enrollment);
        }

        public Task<bool> IsSocialWorkerAsync(
            int schoolId,
            string socialWorkerUserId,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(IsSocialWorker);
        }

        public Task<bool> IsAssignedToAsync(
            int schoolId,
            int referralId,
            string socialWorkerUserId,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(IsAssigned);
        }

        public void Add(StudentReferral referral)
        {
            referral.Id = 101;
            referral.RowVersion = new byte[] { 1, 2, 3 };
            AddedReferral = referral;
        }

        public void AddAction(StudentCaseAction action)
        {
            action.Id = 301;
            AddedActions.Add(action);
        }

        public void SetExpectedRowVersion(StudentReferral referral, byte[] rowVersion) =>
            ExpectedRowVersion = rowVersion;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(1);
    }

    private sealed class FakeSummonRepository : ISummonWorkflowRepository
    {
        public GuardianSummon? Summon { get; init; }
        public GuardianSummon? AddedSummon { get; private set; }
        public SummonEnrollmentSnapshot? Enrollment { get; init; }
        public bool GuardianLinkIsActive { get; init; } = true;
        public bool IsAssigned { get; init; } = true;
        public List<int> SchoolIds { get; } = new();
        public byte[]? ExpectedRowVersion { get; private set; }

        public Task<PagedResult<SummonDto>> GetSummonsAsync(
            int schoolId,
            SummonListQuery query,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(new PagedResult<SummonDto>
            {
                Items = new List<SummonDto>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 10
            });
        }

        public Task<PagedResult<SummonDto>> GetMySummonsAsync(
            int schoolId,
            string guardianUserId,
            SummonListQuery query,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(new PagedResult<SummonDto>
            {
                Items = new List<SummonDto>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 10
            });
        }

        public Task<SummonDto?> GetDtoAsync(
            int schoolId,
            int summonId,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            var s = Summon ?? AddedSummon;
            if (s is null) return Task.FromResult<SummonDto?>(null);
            return Task.FromResult<SummonDto?>(new SummonDto(
                s.Id,
                new StudentSummaryDto(s.StudentId, "S-1", "Student", 12, "1/A", true, null),
                s.StudentReferralId,
                s.CreatedReason,
                s.Priority,
                s.SourceCountSnapshot,
                s.ThresholdSnapshot,
                s.Status,
                s.ScheduledAt,
                s.Location,
                s.Instructions,
                new GuardianSummaryDto(s.GuardianProfileId, "Guardian", GuardianRelationshipType.Father, true, true),
                new ActorSummaryDto("worker-1", "Worker", RoleNames.SocialWorker),
                s.RequiresOfficerReview,
                s.OfficerReviewReason,
                s.GuardianNotifiedAt,
                Convert.ToBase64String(s.RowVersion)));
        }

        public Task<SummonHistoryDto?> GetHistoryAsync(
            int schoolId,
            int summonId,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult<SummonHistoryDto?>(new SummonHistoryDto(new List<TransitionDto>()));
        }

        public Task<GuardianSummon?> GetForUpdateAsync(
            int schoolId,
            int summonId,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Summon);
        }

        public Task<bool> IsGuardianLinkActiveAsync(
            int schoolId,
            int guardianProfileId,
            int studentId,
            DateOnly onDate,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(GuardianLinkIsActive);
        }

        public Task<bool> IsAssignedToAsync(
            int schoolId,
            int summonId,
            string socialWorkerUserId,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(IsAssigned);
        }

        public Task<SummonEnrollmentSnapshot?> GetActiveEnrollmentAsync(
            int schoolId,
            int studentId,
            DateOnly onDate,
            CancellationToken cancellationToken)
        {
            SchoolIds.Add(schoolId);
            return Task.FromResult(Enrollment);
        }

        public void Add(GuardianSummon summon)
        {
            summon.Id = 201;
            summon.RowVersion = new byte[] { 1, 2, 3 };
            AddedSummon = summon;
        }

        public void SetExpectedRowVersion(GuardianSummon summon, byte[] rowVersion) =>
            ExpectedRowVersion = rowVersion;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(1);
    }
}
