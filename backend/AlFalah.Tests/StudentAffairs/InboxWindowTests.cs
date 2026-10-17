using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Notifications;
using AlFalah.Application.Notifications;
using Microsoft.Extensions.Options;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class InboxWindowTests
{
    [Fact]
    public async Task InitialChatWindowContainsLatestFiftyOfSixtyMessages()
    {
        await using var db = await SeedAsync();
        var repo = Repository(db);
        var result = await repo.GetWindowAsync(new(1, "staff"), 1, null, 51, default);
        result!.Take(50).Reverse().Select(x => x.Message.Id).Should().Equal(Enumerable.Range(11, 50).Select(x => (long)x));
        var oldest = result!.Take(50).Last();
        var older = await repo.GetWindowAsync(new(1, "staff"), 1, new(oldest.VisibleAtUtc, oldest.Message.Id), 51, default);
        older!.Reverse().Select(x => x.Message.Id).Should().Equal(Enumerable.Range(1, 10).Select(x => (long)x));
        // The additive endpoint intentionally preserves legacy callers' ascending page contract.
        var legacy = new MessagingWorkflowRepository(db, new BellScheduleRepository(db), TimeProvider.System);
        var oldPage = await legacy.GetConversationMessagesAsync(1, "staff", 1, new() { PageSize = 50 }, default);
        oldPage.Items.First().Id.Should().Be(1);
    }

    [Fact]
    public async Task PendingMessagesAreVisibleToSenderAndLateReleaseUsesRecipientTimeInsteadOfId()
    {
        await using var db = await SeedAsync(60);
        db.Users.Add(new ApplicationUser { Id = "sender", UserName = "sender" });
        var low = await db.ConversationMessages.SingleAsync(x => x.Id == 1);
        low.SenderUserId = "sender";
        low.Receipts.Add(new MessageReceipt { SchoolId = 1, RecipientUserId = "staff", DeliveryState = MessageDeliveryState.Pending });
        await db.SaveChangesAsync();
        var repo = Repository(db);
        var pending = await repo.GetWindowAsync(new(1, "staff"), 1, null, 51, default);
        pending!.Should().NotContain(x => x.Message.Id == 1);
        low.Receipts.Single().DeliveryState = MessageDeliveryState.Delivered;
        low.Receipts.Single().DeliveredAt = low.QueuedAt.AddDays(1);
        await db.SaveChangesAsync();
        var released = await repo.GetWindowAsync(new(1, "staff"), 1, null, 51, default);
        released!.First().Message.Id.Should().Be(1);
        released!.First().VisibleAtUtc.Should().Be(low.QueuedAt.AddDays(1));
        var own = await db.ConversationMessages.SingleAsync(x => x.Id == 60);
        own.SentAt = null;
        own.Receipts.Add(new MessageReceipt { SchoolId = 1, RecipientUserId = "sender" });
        await db.SaveChangesAsync();
        var senderPending = await repo.GetWindowAsync(new(1, "staff"), 1, null, 101, default);
        senderPending!.Single(x => x.Message.Id == 60).Message.DeliveryState.Should().Be(MessageDeliveryState.Pending);
    }

    [Fact]
    public async Task AggregateCountsAllOneHundredTwentyOneThreadsWithoutLeakingOtherSchoolsOrPendingReceipts()
    {
        await using var db = await SeedAsync(0);
        db.Users.Add(new ApplicationUser { Id = "sender", UserName = "sender" });
        for (var id = 1; id <= 121; id++)
        {
            if (id > 1) db.ConversationThreads.Add(new ConversationThread
            {
                Id = id, SchoolId = 1, ThreadType = ConversationThreadType.GuardianTeacher,
                Participants = [new() { SchoolId = 1, ApplicationUserId = "staff" }]
            });
            db.ConversationMessages.Add(new ConversationMessage
            {
                SchoolId = 1, ConversationThreadId = id, SenderUserId = "sender",
                Receipts = [new() { SchoolId = 1, RecipientUserId = "staff", DeliveryState = MessageDeliveryState.Delivered,
                    DeliveredAt = DateTimeOffset.UtcNow }]
            });
        }
        db.ConversationMessages.Add(new ConversationMessage { SchoolId = 1, ConversationThreadId = 1, SenderUserId = "sender",
            Receipts = [new() { SchoolId = 1, RecipientUserId = "staff", DeliveryState = MessageDeliveryState.Pending }] });
        await db.SaveChangesAsync();
        var state = await Repository(db).GetStateAsync(new(1, "staff"), new(true, false, "test"), default);
        state.UnreadMessages.Should().Be(121);
        state.UnreadConversations.Should().Be(121);
        state.UnreadNotifications.Should().BeNull();
        var other = await Repository(db).GetStateAsync(new(2, "staff"), new(true, false, "test"), default);
        other.UnreadMessages.Should().Be(0);
        await new MessagingWorkflowRepository(db, new BellScheduleRepository(db), TimeProvider.System)
            .MarkConversationReadAsync(1, "staff", 1, long.MaxValue, default);
        (await Repository(db).GetStateAsync(new(1, "staff"), new(true, false, "test"), default)).UnreadMessages.Should().Be(120);
    }

    [Fact]
    public async Task JournalDoesNotPublishPendingApprovalAndReadTransitionsRemainSeparateFromCanonicalDutyDelivery()
    {
        await using var db = await SeedAsync(0);
        db.InboxJournalEnabled = true;
        var n = new Notification { SchoolId = 1, UserId = "staff", RequiresApproval = true, CorrelationId = Guid.NewGuid() };
        db.Add(n); await db.SaveChangesAsync();
        (await db.Set<AlFalah.Domain.Entities.Notifications.InboxChange>().CountAsync()).Should().Be(0);
        n.RequiresApproval = false; n.DeliveryStatus = NotificationDeliveryStatus.Delivered;
        await db.SaveChangesAsync();
        n.IsRead = true; n.ReadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        (await db.Set<AlFalah.Domain.Entities.Notifications.InboxChange>().CountAsync()).Should().Be(2);
        (await db.Set<AlFalah.Domain.Entities.Notifications.NotificationDeliveryIntent>().CountAsync()).Should().Be(2);
    }

    private static InboxRepository Repository(AlFalahDbContext db) => new(db, Options.Create(new InboxOptions()), TimeProvider.System);

    [Fact]
    public async Task ProjectionAndDispatcherUseTheSameInjectedClock()
    {
        var clock = new FixedClock(new(2026, 9, 30, 8, 0, 0, TimeSpan.Zero));
        var options = Options.Create(new InboxOptions { JournalEnabled = true, RealtimeEnabled = true });
        await using var db = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, options, clock);
        db.Add(new Notification { SchoolId = 1, UserId = "synthetic", DeliveryStatus = NotificationDeliveryStatus.Delivered });
        await db.SaveChangesAsync();
        var changes = await db.Set<AlFalah.Domain.Entities.Notifications.InboxChange>().SingleAsync();
        changes.OccurredAtUtc.Should().Be(clock.GetUtcNow());
        (await new InboxRepository(db, options, clock).ClaimAsync(10, default)).Should().ContainSingle();
    }
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }

    internal static async Task<AlFalahDbContext> SeedAsync(int messages = 60)
    {
        var db = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Schools.Add(new School { Id = 1, Name = "Inbox test" });
        db.Users.Add(new ApplicationUser { Id = "staff", UserName = "staff", IsActive = true });
        db.Roles.Add(new ApplicationRole { Id = "instructor", Name = RoleNames.Instructor });
        db.UserSchoolRoles.Add(new UserSchoolRole { SchoolId = 1, UserId = "staff", RoleId = "instructor" });
        db.ConversationThreads.Add(new ConversationThread
        {
            Id = 1, SchoolId = 1, ThreadType = ConversationThreadType.GuardianTeacher,
            Participants = [new ConversationParticipant { SchoolId = 1, ApplicationUserId = "staff" }]
        });
        var time = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        for (var id = 1; id <= messages; id++)
            db.ConversationMessages.Add(new ConversationMessage
            {
                Id = id, SchoolId = 1, ConversationThreadId = 1, SenderUserId = "staff",
                Body = $"message {id}", QueuedAt = time, CreatedAt = time, SentAt = time
            });
        await db.SaveChangesAsync();
        return db;
    }
}
