using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class MessagingDeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 20, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ConversationThreadType.GuardianTeacher, RoleNames.Instructor)]
    [InlineData(ConversationThreadType.GuardianStudentAffairs, RoleNames.StudentAffairsOfficer)]
    [InlineData(ConversationThreadType.GuardianSocialWorker, RoleNames.SocialWorker)]
    public async Task StaffCanSendOutsideOfficeHoursWithoutAnyConfiguredOccurrence(
        ConversationThreadType threadType, string staffRole)
    {
        await using var context = await CreateContextAsync(threadType, staffRole);
        var repository = new MessagingWorkflowRepository(context, new BellScheduleRepository(context), new FixedTimeProvider());

        var result = await repository.SendMessageAsync(1, "staff", 1,
            new SendMessageRequestDto("رد خارج الساعات المكتبية", null, "staff-after-hours"), CancellationToken.None);

        result.Disposition.Should().Be(OfficeHoursDisposition.SentImmediately);
        result.Message.DeliveryState.Should().Be(MessageDeliveryState.Delivered);
        result.NextEligibleSendAt.Should().BeNull();
        var stored = await context.ConversationMessages.Include(message => message.Receipts).SingleAsync();
        stored.ReleasedAt.Should().Be(Now);
        stored.Receipts.Should().ContainSingle(receipt => receipt.DeliveredAt == Now
            && receipt.RecipientUserId == "guardian");
        var notification = await context.OutboxMessages.SingleAsync();
        notification.NextAttemptAt.Should().Be(Now);
    }

    [Fact]
    public async Task GuardianMessageWithoutConfiguredOfficeHoursIsAcceptedAndRemainsPending()
    {
        await using var context = await CreateContextAsync(ConversationThreadType.GuardianTeacher, RoleNames.Instructor);
        var repository = new MessagingWorkflowRepository(context, new BellScheduleRepository(context), new FixedTimeProvider());
        var request = new SendMessageRequestDto("متابعة مستوى الطالب", null, "guardian-after-hours");

        var result = await repository.SendMessageAsync(1, "guardian", 1, request, CancellationToken.None);
        var replay = await repository.SendMessageAsync(1, "guardian", 1, request, CancellationToken.None);

        result.Disposition.Should().Be(OfficeHoursDisposition.QueuedUntilOfficeHours);
        result.Message.DeliveryState.Should().Be(MessageDeliveryState.Pending);
        result.NextEligibleSendAt.Should().BeNull();
        replay.Message.Id.Should().Be(result.Message.Id);
        var stored = await context.ConversationMessages.Include(message => message.Receipts).SingleAsync();
        stored.Body.Should().Be(request.Body);
        stored.ReleasedAt.Should().BeNull();
        stored.Receipts.Should().ContainSingle(receipt => receipt.DeliveredAt == null
            && receipt.RecipientUserId == "staff");
        (await context.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GuardianQueueUsesEarliestRecurringOfficeHourAndReleasesOnlyWhenDue()
    {
        await using var context = await CreateContextAsync(ConversationThreadType.GuardianTeacher, RoleNames.Instructor);
        await ConfigureOfficeHoursAsync(context);
        var repository = new MessagingWorkflowRepository(context, new BellScheduleRepository(context), new FixedTimeProvider());
        var next = new DateTimeOffset(2026, 10, 4, 7, 50, 0, TimeSpan.FromHours(3));

        var result = await repository.SendMessageAsync(1, "guardian", 1,
            new SendMessageRequestDto("رسالة مسائية", null, "guardian-queued-next"), CancellationToken.None);

        result.Disposition.Should().Be(OfficeHoursDisposition.QueuedUntilOfficeHours);
        result.NextEligibleSendAt.Should().Be(next);
        (await context.OutboxMessages.SingleAsync()).NextAttemptAt.Should().Be(next);
        var earlyRelease = await repository.ReleaseDueMessageAsync((int)result.Message.Id, CancellationToken.None);
        earlyRelease.Completed.Should().BeFalse();
        (await context.Set<MessageReceipt>().SingleAsync()).DeliveredAt.Should().BeNull();

        var dueRepository = new MessagingWorkflowRepository(context, new BellScheduleRepository(context), new FixedTimeProvider(next));
        await dueRepository.ReleaseDueMessageAsync((int)result.Message.Id, CancellationToken.None);
        await dueRepository.ReleaseDueMessageAsync((int)result.Message.Id, CancellationToken.None);
        var receipt = await context.Set<MessageReceipt>().SingleAsync();
        receipt.DeliveryState.Should().Be(MessageDeliveryState.Delivered);
        receipt.DeliveredAt.Should().Be(next);
        (await context.ConversationMessages.SingleAsync()).NextEligibleSendAt.Should().BeNull();
    }

    [Theory]
    [InlineData("2026-10-04T04:50:00Z", OfficeHoursDisposition.SentImmediately)]
    [InlineData("2026-10-04T05:00:00Z", OfficeHoursDisposition.SentImmediately)]
    [InlineData("2026-10-04T05:30:00Z", OfficeHoursDisposition.QueuedUntilOfficeHours)]
    public async Task GuardianDeliveryRespectsOfficeHourStartAndEnd(string instant, OfficeHoursDisposition expected)
    {
        await using var context = await CreateContextAsync(ConversationThreadType.GuardianTeacher, RoleNames.Instructor);
        await ConfigureOfficeHoursAsync(context);
        var repository = new MessagingWorkflowRepository(context, new BellScheduleRepository(context),
            new FixedTimeProvider(DateTimeOffset.Parse(instant)));

        var result = await repository.SendMessageAsync(1, "guardian", 1,
            new SendMessageRequestDto("متابعة الطالب", null, "guardian-boundary"), CancellationToken.None);

        result.Disposition.Should().Be(expected);
        if (expected == OfficeHoursDisposition.QueuedUntilOfficeHours)
            result.NextEligibleSendAt.Should().Be(new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.FromHours(3)));
    }

    [Fact]
    public async Task TeacherWithFutureOfficeHoursStillSendsImmediately()
    {
        await using var context = await CreateContextAsync(ConversationThreadType.GuardianTeacher, RoleNames.Instructor);
        await ConfigureOfficeHoursAsync(context);
        var repository = new MessagingWorkflowRepository(context, new BellScheduleRepository(context), new FixedTimeProvider());
        var result = await repository.SendMessageAsync(1, "staff", 1,
            new SendMessageRequestDto("رد مسائي", null, "teacher-future-hours"), CancellationToken.None);
        result.Disposition.Should().Be(OfficeHoursDisposition.SentImmediately);
        result.Message.DeliveryState.Should().Be(MessageDeliveryState.Delivered);
        result.NextEligibleSendAt.Should().BeNull();
    }

    private static async Task ConfigureOfficeHoursAsync(AlFalahDbContext context)
    {
        context.AcademicYears.Add(new AcademicYear { Id = 1, Code = "2026", NameAr = "2026" });
        context.AcademicTerms.Add(new AcademicTerm
        {
            Id = 1, SchoolId = 1, AcademicYearId = 1, Semester = TimetableSemester.First,
            StartsOn = new DateOnly(2026, 9, 1), EndsOn = new DateOnly(2026, 12, 1), IsActive = true
        });
        context.Add(new BellScheduleRevision
        {
            Id = 1, SchoolId = 1, Revision = 1, Name = "Test bells", SchoolTimeZoneId = "Africa/Cairo",
            Template = new BellScheduleTemplate { Id = 1, SchoolId = 1, AcademicYearId = 1, Semester = TimetableSemester.First, Name = "Test bells" },
            Days = new List<BellScheduleDay>
            {
                new()
                {
                    Day = 0,
                    Periods = new List<BellPeriod>
                    {
                        new() { Sequence = 1, StartLocalTime = new TimeOnly(7, 50), EndLocalTime = new TimeOnly(8, 30) },
                        new() { Sequence = 2, StartLocalTime = new TimeOnly(9, 0), EndLocalTime = new TimeOnly(9, 40) }
                    }
                },
                new() { Day = (int)TimetableDay.Sunday, IsStudyDay = true, UsesDefaultSchedule = true }
            }
        });
        context.SchoolTimetables.Add(new SchoolTimetable
        {
            Id = 1, SchoolId = 1, AcademicYearId = 1, Semester = TimetableSemester.First,
            BellScheduleRevisionId = 1, IsPublished = true, Revision = 1
        });
        context.TeacherOfficeHourConfigurations.Add(new TeacherOfficeHourConfiguration
        {
            Id = 1, SchoolId = 1, InstructorProfileId = 1, AcademicTermId = 1,
            SchoolTimetableId = 1, TimetableRevision = 1, BellScheduleRevisionId = 1,
            EffectiveFrom = new DateOnly(2026, 9, 1),
            // Persist the later selection first to verify the nearest occurrence is chosen by time.
            Slots = new List<TeacherOfficeHour>
            {
                new() { Id = 1, SchoolId = 1, InstructorProfileId = 1, AcademicTermId = 1, Day = TimetableDay.Sunday, Period = 2, StableSlotKey = "later" },
                new() { Id = 2, SchoolId = 1, InstructorProfileId = 1, AcademicTermId = 1, Day = TimetableDay.Sunday, Period = 1, StableSlotKey = "earlier" }
            }
        });
        await context.SaveChangesAsync();
    }

    private static async Task<AlFalahDbContext> CreateContextAsync(ConversationThreadType threadType, string staffRole)
    {
        var context = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Schools.Add(new School { Id = 1, Name = "Messaging test school" });
        context.Users.AddRange(
            new ApplicationUser { Id = "staff", UserName = "staff", FirstName = "Staff", LastName = "User" },
            new ApplicationUser { Id = "guardian", UserName = "guardian", FirstName = "Guardian", LastName = "User" });
        context.InstructorProfiles.Add(new InstructorProfile { Id = 1, SchoolId = 1, UserId = "staff" });
        context.ConversationThreads.Add(new ConversationThread
        {
            Id = 1,
            SchoolId = 1,
            ThreadType = threadType,
            Subject = "متابعة الطالب",
            Participants = new List<ConversationParticipant>
            {
                new() { SchoolId = 1, ApplicationUserId = "staff", ParticipantRoleSnapshot = staffRole },
                new() { SchoolId = 1, ApplicationUserId = "guardian", ParticipantRoleSnapshot = RoleNames.Guardian }
            }
        });
        await context.SaveChangesAsync();
        return context;
    }

    private sealed class FixedTimeProvider(DateTimeOffset? instant = null) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant ?? Now;
    }
}
