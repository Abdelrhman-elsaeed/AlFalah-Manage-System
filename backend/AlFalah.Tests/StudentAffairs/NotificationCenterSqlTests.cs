using AlFalah.Application.Notifications;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Notifications;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

[Collection("Inbox SQL")]
public sealed class NotificationCenterSqlTests(InboxSqlFixture fixture)
{
    private readonly IOptions<InboxOptions> inbox = Options.Create(new InboxOptions { CenterEnabled = true, JournalEnabled = true });
    private readonly IOptions<WebPushOptions> push = Options.Create(new WebPushOptions { DispatchEnabled = true, VapidKeyId = "synthetic" });
    private readonly IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
    private AlFalahDbContext Db()
    {
        using var original = fixture.Db();
        return new(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(original.Database.GetConnectionString()).Options,
            inbox, TimeProvider.System, push);
    }
    private CenterRepository Center(AlFalahDbContext db) => new(db, inbox, TimeProvider.System);
    private PushRepository Push(AlFalahDbContext db) => new(db, protection, Center(db), push, TimeProvider.System);
    private static Notification Notice(InboxScope scope) => new() { SchoolId = scope.SchoolId, UserId = scope.UserId, Title = "Synthetic N2",
        Message = "Synthetic local SQL evidence", DeliveryStatus = NotificationDeliveryStatus.Delivered };

    [InboxSqlFact]
    public async Task ConcurrentAccountsCannotOwnOneEndpointAndDeviceFailuresAreIndependent()
    {
        var first = await fixture.SeedAsync(); var second = await fixture.SeedAsync();
        var endpoint = "https://fcm.googleapis.com/ownership-" + Guid.NewGuid();
        await using var a = Db(); await using var b = Db();
        var registrations = await Task.WhenAll(
            Push(a).RegisterAsync(first, new(Guid.NewGuid(), "synthetic", new(endpoint, new("test", "test"))), default),
            Push(b).RegisterAsync(second, new(Guid.NewGuid(), "synthetic", new(endpoint, new("test", "test"))), default));
        registrations.Count(x => x is not null).Should().Be(1);
        var scope = registrations[0] is not null ? first : second;
        await using var db = Db();
        await Push(db).RegisterAsync(scope, new(Guid.NewGuid(), "synthetic", new(endpoint + "-second-device", new("test", "test"))), default);
        db.Add(Notice(scope)); await db.SaveChangesAsync();
        var entry = await db.Set<PersonalNotification>().SingleAsync(x => x.UserId == scope.UserId);
        (await db.Set<BrowserPushTarget>().CountAsync(x => x.EntryId == entry.Id)).Should().Be(2);
        var accepted = (await Push(db).ClaimAsync(default)).Single();
        await Push(db).CompleteAsync(accepted, new("ProviderAccepted"), default);
        var invalid = (await Push(db).ClaimAsync(default)).Single();
        await Push(db).CompleteAsync(invalid, new("InvalidSubscription", 410), default);
        (await db.Set<BrowserPushTarget>().AsNoTracking().SingleAsync(x => x.Id == accepted.Id)).State.Should().Be("ProviderAccepted");
        (await Push(db).DevicesAsync(scope, default)).Should().ContainSingle();
        (await db.Set<BrowserPushAttempt>().CountAsync(x => x.Target.EntryId == entry.Id)).Should().Be(2);
        (await Center(db).CountAsync(scope, default)).Should().Be(1);
    }

    [InboxSqlFact]
    public async Task SourceCenterJournalAndDeviceSnapshotShareRollbackAndOptInDoesNotBackfill()
    {
        var scope = await fixture.SeedAsync();
        await using var db = Db();
        var first = Notice(scope); db.Add(first); await db.SaveChangesAsync();
        (await db.Set<PersonalNotification>().SingleAsync(x => x.NotificationId == first.Id)).PushDisposition.Should().Be("NoActiveSubscription");
        var device = await Push(db).RegisterAsync(scope, new(Guid.NewGuid(), "synthetic", new("https://fcm.googleapis.com/sql-" + Guid.NewGuid(), new("synthetic", "synthetic"))), default);
        (await db.Set<BrowserPushTarget>().CountAsync(x => x.Entry.UserId == scope.UserId)).Should().Be(0);
        var before = await db.Set<InboxChange>().CountAsync();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            db.Add(Notice(scope)); await db.SaveChangesAsync();
            (await db.Set<PersonalNotification>().CountAsync(x => x.UserId == scope.UserId)).Should().Be(2);
            (await db.Set<BrowserPushTarget>().CountAsync(x => x.Entry.UserId == scope.UserId)).Should().Be(1);
            await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        (await db.Set<PersonalNotification>().CountAsync(x => x.UserId == scope.UserId)).Should().Be(1);
        (await db.Set<InboxChange>().CountAsync()).Should().Be(before);
        (await db.Set<BrowserPushTarget>().CountAsync(x => x.Entry.UserId == scope.UserId)).Should().Be(0);
        db.Add(Notice(scope)); await db.SaveChangesAsync();
        (await db.Set<BrowserPushTarget>().CountAsync(x => x.Entry.UserId == scope.UserId)).Should().Be(1);
        var entry = await db.Set<PersonalNotification>().SingleAsync(x => x.UserId == scope.UserId && x.NotificationId != first.Id);
        await Center(db).ReadAsync(scope, entry.Id, default);
        (await db.Set<BrowserPushTarget>().CountAsync(x => x.Entry.UserId == scope.UserId)).Should().Be(1);
        (await Center(db).CountAsync(scope, default)).Should().Be(1);
        push.Value.DispatchEnabled = false;
        await Push(db).CancelDisabledAsync(default);
        push.Value.DispatchEnabled = true;
        (await db.Set<BrowserPushTarget>().AsNoTracking().CountAsync(x => x.Entry.UserId == scope.UserId && x.State == "Pending")).Should().Be(0);
        (await db.Set<BrowserPushTarget>().AsNoTracking().CountAsync(x => x.Entry.UserId == scope.UserId && x.State == "Cancelled")).Should().Be(1);
        await Push(db).RevokeAsync(scope, device!.Id, default);
    }

    [InboxSqlFact]
    public async Task CenterHistoryCountsAndTargetsRecheckPermissionsWithoutFirstPageCap()
    {
        var scope = await fixture.SeedAsync();
        await using var db = Db();
        db.AddRange(Enumerable.Range(0, 121).Select(_ => Notice(scope))); await db.SaveChangesAsync();
        var repo = Center(db);
        (await repo.CountAsync(scope, default)).Should().Be(121);
        var first = await repo.ListAsync(scope, new(Limit: 30), null, default);
        (await repo.TaskStatesAsync(first, default)).Should().HaveCount(30);
        var position = new CenterPosition(first.Last().CreatedAtUtc, first.Last().Id, scope.SchoolId, scope.UserId, null, false);
        var next = await repo.ListAsync(scope, new(Limit: 30), position, default);
        next.Should().HaveCount(30); next.Select(x => x.Id).Should().NotIntersectWith(first.Select(x => x.Id));
        await repo.ReadAsync(scope, null, default);
        (await repo.CountAsync(scope, default)).Should().Be(0);
        var role = await db.UserSchoolRoles.SingleAsync(x => x.UserId == scope.UserId && x.SchoolId == scope.SchoolId);
        role.IsActive = false; await db.SaveChangesAsync();
        (await repo.ListAsync(scope, new(), null, default)).Should().BeEmpty();
        (await repo.FindAsync(scope, first[0].Id, default)).Should().BeNull();
    }

    [InboxSqlFact]
    public async Task PushClaimRacesAndStaleCompletionCannotOverwriteANewerLease()
    {
        var scope = await fixture.SeedAsync();
        await using var db = Db();
        await Push(db).RegisterAsync(scope, new(Guid.NewGuid(), "synthetic", new("https://fcm.googleapis.com/sql-" + Guid.NewGuid(), new("synthetic", "synthetic"))), default);
        db.Add(Notice(scope)); await db.SaveChangesAsync();
        await using var a = Db(); await using var b = Db();
        var claims = await Task.WhenAll(Push(a).ClaimAsync(default), Push(b).ClaimAsync(default));
        var lease = claims.SelectMany(x => x).Single();
        await db.Set<BrowserPushTarget>().Where(x => x.Id == lease.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntilUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
        var newer = (await Push(b).ClaimAsync(default)).Single();
        await Push(a).CompleteAsync(lease, new("ProviderAccepted"), default);
        (await db.Set<BrowserPushTarget>().AsNoTracking().SingleAsync(x => x.Id == lease.Id)).State.Should().Be("Pending");
        await Push(b).CompleteAsync(newer, new("ProviderAccepted"), default);
        var accepted = await db.Set<BrowserPushTarget>().AsNoTracking().SingleAsync(x => x.Id == lease.Id);
        accepted.State.Should().Be("ProviderAccepted"); accepted.ProviderAcceptedAtUtc.Should().NotBeNull();
        (await db.Set<BrowserPushAttempt>().CountAsync(x => x.TargetId == lease.Id)).Should().Be(1);
        (await Center(db).CountAsync(scope, default)).Should().Be(1);
    }
}
