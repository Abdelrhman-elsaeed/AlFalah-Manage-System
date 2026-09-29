using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Application.StudentAffairs.DTOs.Dashboards;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.GatePasses;
using AlFalah.Application.StudentAffairs.GatePasses.Handlers;
using AlFalah.Application.StudentAffairs.Messaging;
using AlFalah.Application.StudentAffairs.Messaging.Handlers;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class GatePassAndMessagingMediatRTests
{
    [Fact]
    public void ApplicationAssembly_Registers_All_GatePass_And_Messaging_Handlers()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(StudentAffairsAssemblyMarker).Assembly));

        // Stub dependencies
        services.AddSingleton<IGatePassWorkflowRepository, StubGatePassWorkflowRepository>();
        services.AddSingleton<IMessagingWorkflowRepository, StubMessagingWorkflowRepository>();
        services.AddSingleton<ICurrentLessonResolver, StubCurrentLessonResolver>();
        services.AddSingleton<ICurrentUserService>(new StubCurrentUser("user-1", 1, PermissionNames.GatePassView, PermissionNames.MessagingViewOwn));
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        var provider = services.BuildServiceProvider();

        // Gate pass handlers
        provider.GetService<IRequestHandler<GetGatePassesQuery, ApiResponse<PagedResult<GatePassDto>>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetMyGatePassesQuery, ApiResponse<PagedResult<GatePassDto>>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetSecurityGatePassQueueQuery, ApiResponse<SecurityGatePassQueuePageDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetSecurityGatePassByIdQuery, ApiResponse<SecurityGatePassDetailDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetGatePassByIdQuery, ApiResponse<GatePassDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<ApproveGatePassCommand, ApiResponse<GatePassDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<RejectGatePassCommand, ApiResponse<GatePassDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<CancelGatePassCommand, ApiResponse<GatePassDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<AcknowledgeGatePassByTeacherCommand, ApiResponse<GatePassDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<AcknowledgeGatePassBySecurityCommand, ApiResponse<SecurityGatePassDetailDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<ExecuteGatePassCommand, ApiResponse<SecurityGatePassDetailDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetGatePassHistoryQuery, ApiResponse<GatePassHistoryDto>>>().Should().NotBeNull();

        // Messaging handlers
        provider.GetService<IRequestHandler<GetConversationsQuery, ApiResponse<PagedResult<ConversationDto>>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetGuardianStaffOptionsQuery, ApiResponse<IReadOnlyList<GuardianStaffOptionDto>>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<CreateConversationCommand, ApiResponse<ConversationDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetConversationByIdQuery, ApiResponse<ConversationDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetConversationMessagesQuery, ApiResponse<PagedResult<ConversationMessageDto>>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<SendConversationMessageCommand, ApiResponse<SendMessageResultDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<MarkConversationReadCommand, ApiResponse<bool>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<CloseConversationCommand, ApiResponse<ConversationDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetEligibleOfficeHoursQuery, ApiResponse<OfficeHoursAggregateDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetMyOfficeHoursQuery, ApiResponse<OfficeHoursAggregateDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<UpdateMyOfficeHoursCommand, ApiResponse<OfficeHoursAggregateDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<GetTeacherOfficeHoursQuery, ApiResponse<OfficeHoursAggregateDto>>>().Should().NotBeNull();
        provider.GetService<IRequestHandler<OverrideTeacherOfficeHoursCommand, ApiResponse<OfficeHoursAggregateDto>>>().Should().NotBeNull();
    }

    [Fact]
    public async Task GetGatePassesQueryHandler_Returns_Success_When_Authorized()
    {
        var stubRepo = new StubGatePassWorkflowRepository();
        var currentUser = new StubCurrentUser("user-1", 1, PermissionNames.GatePassView);
        var handler = new GetGatePassesQueryHandler(stubRepo, currentUser);

        var result = await handler.Handle(new GetGatePassesQuery(new GatePassListQuery()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetConversationsQueryHandler_Returns_Success_When_Authorized()
    {
        var stubRepo = new StubMessagingWorkflowRepository();
        var currentUser = new StubCurrentUser("user-1", 1, PermissionNames.MessagingViewOwn);
        var handler = new GetConversationsQueryHandler(stubRepo, currentUser);

        var result = await handler.Handle(new GetConversationsQuery(new ConversationListQuery()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task SendConversationMessageCommandHandler_Denies_NonParticipant()
    {
        var repository = new StubMessagingWorkflowRepository { IsParticipant = false };
        var currentUser = new StubCurrentUser("outsider", 1, PermissionNames.MessagingSend);
        var handler = new SendConversationMessageCommandHandler(repository, currentUser);

        var result = await handler.Handle(
            new SendConversationMessageCommand(25, new SendMessageRequestDto("Unauthorized", null, "message-1")),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Conversation was not found");
        repository.SendWasCalled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMyOfficeHoursCommandHandler_RequiresExactInstructorRole()
    {
        var repository = new StubMessagingWorkflowRepository();
        var currentUser = new StubCurrentUser("manager", 1, PermissionNames.OfficeHoursManageOwn);
        currentUser.Roles.Remove(RoleNames.Instructor);
        var handler = new UpdateMyOfficeHoursCommandHandler(repository, currentUser);

        var result = await handler.Handle(
            new UpdateMyOfficeHoursCommand(new UpdateMyOfficeHoursRequestDto(Array.Empty<string>(), new DateOnly(2026, 9, 28), "new")),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.Contains("permission"));
    }

    [Fact]
    public async Task GetGuardianTeacherOptionsQueryHandler_RequiresGuardianRole()
    {
        var repository = new StubMessagingWorkflowRepository();
        var currentUser = new StubCurrentUser("teacher", 1, PermissionNames.MessagingStartGuardianTeacher);
        currentUser.Roles.Remove(RoleNames.Guardian);
        var handler = new GetGuardianTeacherOptionsQueryHandler(repository, currentUser, TimeProvider.System);

        var result = await handler.Handle(new GetGuardianTeacherOptionsQuery(10), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.Contains("permission"));
    }

    [Fact]
    public async Task CreateConversation_RequiresPermissionForSelectedThreadType()
    {
        var repository = new StubMessagingWorkflowRepository();
        var currentUser = new StubCurrentUser(
            "guardian", 1, PermissionNames.MessagingStartGuardianTeacher);
        var handler = new CreateConversationCommandHandler(repository, currentUser, TimeProvider.System);
        var request = new CreateConversationRequestDto(
            10,
            ConversationThreadType.GuardianStudentAffairs,
            null,
            RoleNames.StudentAffairsOfficer,
            "officer-1",
            "Question",
            "Please contact me",
            "conversation-1");

        var result = await handler.Handle(
            new CreateConversationCommand(request), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.Contains("permission"));
    }

    private sealed class StubGatePassWorkflowRepository : IGatePassWorkflowRepository
    {
        public Task<bool> IsActiveGuardianAsync(int schoolId, string guardianUserId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> IsOwnedByGuardianAsync(int schoolId, int gatePassId, string guardianUserId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<GuardianGatePassLinkSnapshot?> GetGuardianLinkAsync(int schoolId, string guardianUserId, int studentId, CancellationToken cancellationToken) =>
            Task.FromResult<GuardianGatePassLinkSnapshot?>(null);

        public Task<bool> IsGuardianLinkActiveAsync(int schoolId, int guardianProfileId, int studentId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<GatePassEnrollmentSnapshot?> GetActiveEnrollmentAsync(int schoolId, int studentId, DateOnly onDate, CancellationToken cancellationToken) =>
            Task.FromResult<GatePassEnrollmentSnapshot?>(null);

        public Task<GatePassDto?> GetByIdempotencyKeyAsync(int schoolId, int guardianProfileId, string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult<GatePassDto?>(null);

        public Task<bool> HasOverlappingActivePassAsync(int schoolId, int studentId, DateTimeOffset windowStartsAt, DateTimeOffset windowEndsAt, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<AlFalah.Domain.Entities.StudentAffairs.GatePass?> GetForUpdateAsync(int schoolId, int gatePassId, CancellationToken cancellationToken) =>
            Task.FromResult<AlFalah.Domain.Entities.StudentAffairs.GatePass?>(null);

        public Task<int?> GetInstructorProfileIdAsync(int schoolId, string teacherUserId, CancellationToken cancellationToken) =>
            Task.FromResult<int?>(null);

        public Task<DateOnly?> GetPublishedStudyDateAsync(int schoolId, DateTimeOffset instant, CancellationToken ct) => Task.FromResult<DateOnly?>(DateOnly.FromDateTime(instant.DateTime));

        public Task<GatePassTimetableSnapshot?> ResolvePublishedTimetableAsync(int schoolId, int academicYearId, TimetableSemester semester, int classroomId, string classroomLabel, DateTimeOffset instant, CancellationToken cancellationToken) =>
            Task.FromResult<GatePassTimetableSnapshot?>(null);

        public void Add(AlFalah.Domain.Entities.StudentAffairs.GatePass gatePass) { }
        public void SetExpectedRowVersion(AlFalah.Domain.Entities.StudentAffairs.GatePass gatePass, byte[] rowVersion) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);

        public Task<GatePassDto?> GetDtoAsync(int schoolId, int gatePassId, CancellationToken cancellationToken) =>
            Task.FromResult<GatePassDto?>(new GatePassDto(
                gatePassId,
                new StudentSummaryDto(1, "STU-1", "Test Student", 1, "Class 1", true, null),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(2),
                "Doctor appointment",
                new PickupPersonDto("Parent", "Father", null),
                GatePassStatus.Requested,
                null, null, null, null, null, null,
                Array.Empty<NotificationDeliveryDto>(),
                Convert.ToBase64String(new byte[] { 1, 2, 3 })));

        public Task<PagedResult<GatePassDto>> GetGatePassesAsync(int schoolId, GatePassListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<GatePassDto>
            {
                Items = new List<GatePassDto>
                {
                    new(1, new StudentSummaryDto(1, "STU-1", "Test Student", 1, "Class 1", true, null),
                        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2), "Reason",
                        new PickupPersonDto("Father", "Parent", null), GatePassStatus.Requested,
                        null, null, null, null, null, null, Array.Empty<NotificationDeliveryDto>(), "AQID")
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 20
            });

        public Task<PagedResult<GatePassDto>> GetMyGatePassesAsync(int schoolId, string guardianUserId, GatePassListQuery query, CancellationToken cancellationToken) =>
            GetGatePassesAsync(schoolId, query, cancellationToken);

        public Task<SecurityGatePassQueuePageDto> GetSecurityGatePassQueueAsync(
            int schoolId, GatePassListQuery query, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(new SecurityGatePassQueuePageDto(
                new List<SecurityGatePassQueueItemDto>
                {
                    new(1, new StudentSummaryDto(1, "STU-1", "Test Student", null, "Class 1", true, null),
                        "Class 1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2),
                        new PickupPersonDto("Father", "Parent", null), "Officer", DateTimeOffset.UtcNow,
                        null, GatePassStatus.Approved, "AQID")
                },
                1, 1, 20, now));

        public Task<SecurityGatePassDetailDto?> GetSecurityDetailAsync(
            int schoolId, int gatePassId, CancellationToken cancellationToken) =>
            Task.FromResult<SecurityGatePassDetailDto?>(null);

        public Task<SecurityStudentAffairsDashboardDto> GetSecurityDashboardAsync(
            int schoolId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(new SecurityStudentAffairsDashboardDto(
                Array.Empty<SecurityGatePassQueueItemDto>(), Array.Empty<DashboardCountDto>(), now));

        public Task<GatePassHistoryDto?> GetHistoryAsync(int schoolId, int gatePassId, CancellationToken cancellationToken) =>
            Task.FromResult<GatePassHistoryDto?>(new GatePassHistoryDto(Array.Empty<TransitionDto>(), Array.Empty<NotificationDeliveryDto>()));
    }

    private sealed class StubMessagingWorkflowRepository : IMessagingWorkflowRepository
    {
        public bool IsParticipant { get; set; } = true;
        public bool SendWasCalled { get; private set; }

        public Task<MessageReleaseResult> ReleaseDueMessageAsync(int messageId, CancellationToken cancellationToken) =>
            Task.FromResult(new MessageReleaseResult(true, null));

        public Task ReconcileOfficeHoursAsync(int schoolId, IReadOnlyCollection<string>? teacherUserIds, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<bool> IsParticipantAsync(int schoolId, string userId, int conversationId, CancellationToken cancellationToken) =>
            Task.FromResult(IsParticipant);

        public Task<bool> IsConversationTargetAllowedAsync(int schoolId, string creatorUserId, CreateConversationRequestDto request, DateTimeOffset instant, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<PagedResult<ConversationDto>> GetConversationsAsync(int schoolId, string userId, ConversationListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<ConversationDto>
            {
                Items = new List<ConversationDto>
                {
                    new(1, new StudentSummaryDto(1, "STU-1", "Test Student", null, null, true, null),
                        "Inquiry", ConversationThreadType.GuardianTeacher, ConversationThreadStatus.Open,
                        Array.Empty<ConversationParticipantDto>(), 0, DateTimeOffset.UtcNow, "AQID")
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 20
            });

        public Task<IReadOnlyList<GuardianTeacherOptionDto>> GetGuardianTeacherOptionsAsync(int schoolId, string guardianUserId, int studentId, DateTimeOffset instant, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GuardianTeacherOptionDto>>(Array.Empty<GuardianTeacherOptionDto>());

        public Task<IReadOnlyList<GuardianStaffOptionDto>> GetGuardianStaffOptionsAsync(int schoolId, string guardianUserId, int studentId, DateTimeOffset instant, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GuardianStaffOptionDto>>(Array.Empty<GuardianStaffOptionDto>());

        public Task<ConversationDto?> GetConversationByIdAsync(int schoolId, string userId, int conversationId, CancellationToken cancellationToken) =>
            Task.FromResult<ConversationDto?>(new ConversationDto(conversationId, new StudentSummaryDto(1, "STU-1", "Test Student", null, null, true, null),
                "Inquiry", ConversationThreadType.GuardianTeacher, ConversationThreadStatus.Open,
                Array.Empty<ConversationParticipantDto>(), 0, DateTimeOffset.UtcNow, "AQID"));

        public Task<PagedResult<ConversationMessageDto>> GetConversationMessagesAsync(int schoolId, string userId, int conversationId, ConversationMessageQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<ConversationMessageDto>
            {
                Items = new List<ConversationMessageDto>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            });

        public Task<ConversationDto> CreateConversationAsync(int schoolId, string creatorUserId, CreateConversationRequestDto request, CancellationToken cancellationToken) =>
            Task.FromResult(new ConversationDto(1, new StudentSummaryDto(1, "STU-1", "Test Student", null, null, true, null),
                request.Subject, request.ThreadType, ConversationThreadStatus.Open,
                Array.Empty<ConversationParticipantDto>(), 0, DateTimeOffset.UtcNow, "AQID"));

        public Task<SendMessageResultDto> SendMessageAsync(int schoolId, string senderUserId, int conversationId, SendMessageRequestDto request, CancellationToken cancellationToken)
        {
            SendWasCalled = true;
            return Task.FromResult(new SendMessageResultDto(
                new ConversationMessageDto(1, conversationId, new ActorSummaryDto(senderUserId, "User", "Sender"), request.Body, request.ReplyToMessageId, DateTimeOffset.UtcNow, MessageDeliveryState.Delivered, OfficeHoursDisposition.SentImmediately, null, Array.Empty<NotificationDeliveryDto>()),
                OfficeHoursDisposition.SentImmediately, null));
        }

        public Task<bool> MarkConversationReadAsync(int schoolId, string userId, int conversationId, long throughMessageId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<ConversationDto?> CloseConversationAsync(int schoolId, string userId, int conversationId, CloseConversationRequestDto request, CancellationToken cancellationToken) =>
            GetConversationByIdAsync(schoolId, userId, conversationId, cancellationToken);

        public Task<OfficeHoursAggregateDto> GetEligibleOfficeHoursAsync(int schoolId, string userId, CancellationToken cancellationToken) =>
            Task.FromResult(EmptyOfficeHours());

        public Task<OfficeHoursAggregateDto> GetMyOfficeHoursAsync(int schoolId, string userId, CancellationToken cancellationToken) =>
            Task.FromResult(EmptyOfficeHours());

        public Task<OfficeHoursAggregateDto> UpdateMyOfficeHoursAsync(int schoolId, string userId, UpdateMyOfficeHoursRequestDto request, CancellationToken cancellationToken) =>
            Task.FromResult(EmptyOfficeHours());

        public Task<OfficeHoursAggregateDto> GetTeacherOfficeHoursAsync(int schoolId, string requesterUserId, int instructorId, CancellationToken cancellationToken) =>
            Task.FromResult(EmptyOfficeHours());

        public Task<OfficeHoursAggregateDto> OverrideTeacherOfficeHoursAsync(int schoolId, string adminUserId, int instructorId, OverrideTeacherOfficeHoursRequestDto request, CancellationToken cancellationToken) =>
            Task.FromResult(EmptyOfficeHours());

        private static OfficeHoursAggregateDto EmptyOfficeHours() => new(
            null, 1, 1, 1, 1, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, "new", TeacherOfficeHourSource.DerivedFromPublishedTimetable,
            string.Empty, DateTimeOffset.UtcNow, null, Array.Empty<OfficeHourSlotDto>());
    }

    private sealed class StubCurrentLessonResolver : ICurrentLessonResolver
    {
        public Task<CurrentLessonResolution> ResolveForClassroomAsync(int schoolId, DateTimeOffset instant, int classroomId, string? classroomLabel, CancellationToken cancellationToken) =>
            Task.FromResult(NoSchedule());

        public Task<CurrentLessonResolution> ResolveForInstructorAsync(int schoolId, DateTimeOffset instant, string instructorUserId, CancellationToken cancellationToken) =>
            Task.FromResult(NoSchedule());

        private static CurrentLessonResolution NoSchedule() => new(
            CurrentLessonResolutionKind.NoPublishedSchedule,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            "No published schedule");
    }

    private sealed class StubCurrentUser(
        string userId,
        int schoolId,
        params string[] permissions) : ICurrentUserService
    {
        public HashSet<string> Roles { get; } = new(StringComparer.Ordinal)
        {
            RoleNames.Guardian,
            RoleNames.Instructor,
            RoleNames.StudentAffairsOfficer,
            RoleNames.SocialWorker,
            RoleNames.SchoolManager
        };
        public string? UserId => userId;
        public string? Username => "test.user";
        public int? ActiveSchoolId => schoolId;
        public string? PreferredLanguage => "en";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => Roles.Contains(roleName);
        public bool HasPermission(string permissionName) => permissions.Contains(permissionName);
        public IEnumerable<string> GetRoles() => Roles;
        public IEnumerable<string> GetPermissions() => permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }
}
