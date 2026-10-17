using AlFalah.Application.Notifications;
using AlFalah.Application.StudentAffairs.DTOs.Notifications;
using AlFalah.Shared.Models;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Notifications;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using AlFalah.Infrastructure.Repositories;
using MediatR;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class InboxSqlFactAttribute : FactAttribute
{
    public InboxSqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ALFALAH_INBOX_TEST_CONNECTION") is null)
            Skip = "Requires a fresh explicitly isolated AlFalahInboxTests_* LocalDB database.";
    }
}

[CollectionDefinition("Inbox SQL", DisableParallelization = true)]
public sealed class InboxSqlCollection : ICollectionFixture<InboxSqlFixture>;

public sealed class InboxSqlFixture : IAsyncLifetime
{
    private string connection = "";
    public bool BaselinePreserved { get; private set; }
    public AlFalahDbContext Db(bool journal = true) => new(new DbContextOptionsBuilder<AlFalahDbContext>()
        .UseSqlServer(connection).Options) { InboxJournalEnabled = journal };

    public async Task InitializeAsync()
    {
        var value = Environment.GetEnvironmentVariable("ALFALAH_INBOX_TEST_CONNECTION");
        if (value is null) return;
        var builder = new SqlConnectionStringBuilder(value);
        if (!builder.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)
            || !builder.InitialCatalog.StartsWith("AlFalahInboxTests_", StringComparison.Ordinal)
            || !builder.IntegratedSecurity || builder.MultipleActiveResultSets)
            throw new InvalidOperationException("A fresh Inbox test LocalDB with MARS disabled is required.");
        connection = builder.ConnectionString;
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync();
            await using var command = master.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name=@name";
            command.Parameters.AddWithValue("@name", builder.InitialCatalog);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("Refusing to reset an existing database. Choose a fresh test name.");
        }
        await using var db = Db(false);
        await db.GetService<IMigrator>().MigrateAsync("20261017094734_AddCoverageRegisterScope");
        db.Schools.Add(new School { Name = "N1 migration sentinel" });
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        BaselinePreserved = await db.Schools.AnyAsync(s => s.Name == "N1 migration sentinel")
            && !await db.Set<InboxChange>().AnyAsync() && !await db.Set<NotificationDeliveryIntent>().AnyAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask; // Retain isolated evidence; never reset another database.

    public async Task<InboxScope> SeedAsync()
    {
        await using var db = Db(false);
        var id = Guid.NewGuid().ToString("N");
        var role = new ApplicationRole { Id = id, Name = RoleNames.Instructor, NormalizedName = id };
        var school = new School { Name = "N1 synthetic " + id };
        var user = new ApplicationUser { Id = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(), SecurityStamp = id };
        db.AddRange(role, school, user); await db.SaveChangesAsync();
        db.UserSchoolRoles.Add(new() { SchoolId = school.Id, UserId = id, RoleId = id });
        foreach (var name in new[] { PermissionNames.MessagingViewOwn, PermissionNames.NotificationViewOwn })
        {
            var permission = await db.Permissions.SingleOrDefaultAsync(p => p.Name == name);
            if (permission is null) { permission = new() { Name = name }; db.Add(permission); await db.SaveChangesAsync(); }
            db.RolePermissions.Add(new() { RoleId = id, PermissionId = permission.Id });
        }
        await db.SaveChangesAsync();
        return new(school.Id, id);
    }
}

[Collection("Inbox SQL")]
public sealed class InboxSqlTests(InboxSqlFixture fixture)
{
    private static readonly IOptions<InboxOptions> Options = Microsoft.Extensions.Options.Options.Create(new InboxOptions
        { JournalEnabled = true, RealtimeEnabled = true });
    private static InboxRepository Repo(AlFalahDbContext db, TimeProvider? time = null) => new(db, Options, time ?? TimeProvider.System);
    private static Notification Notification(InboxScope scope, Guid? correlation = null) => new()
    {
        SchoolId = scope.SchoolId, UserId = scope.UserId, Title = "Synthetic N1", Message = "Synthetic",
        DeliveryStatus = NotificationDeliveryStatus.Delivered, DeliveredAt = DateTimeOffset.UtcNow, CorrelationId = correlation
    };

    [InboxSqlFact]
    public async Task AdditiveMigrationPreservesExistingSchoolAndDoesNotBroadcastHistory()
        => fixture.BaselinePreserved.Should().BeTrue();

    [InboxSqlFact]
    public async Task RollbackRemovesBusinessRowJournalCounterAndIntentTogether()
    {
        var scope = await fixture.SeedAsync();
        await using (var db = fixture.Db())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            db.Add(Notification(scope)); await db.SaveChangesAsync();
            (await db.Set<InboxChange>().CountAsync(c => c.UserId == scope.UserId)).Should().Be(1);
            await tx.RollbackAsync();
        }
        await using var verify = fixture.Db();
        (await verify.Notifications.CountAsync(n => n.UserId == scope.UserId)).Should().Be(0);
        (await verify.Set<InboxChange>().CountAsync(c => c.UserId == scope.UserId)).Should().Be(0);
        (await verify.Set<RecipientInboxStream>().CountAsync(c => c.UserId == scope.UserId)).Should().Be(0);
        (await verify.Set<NotificationDeliveryIntent>().CountAsync(c => c.UserId == scope.UserId)).Should().Be(0);
    }

    [InboxSqlFact]
    public async Task ConcurrentWritersCannotCommitHigherRecipientSequenceAheadOfLowerSequence()
    {
        var scope = await fixture.SeedAsync();
        await using var first = fixture.Db();
        await using var tx = await first.Database.BeginTransactionAsync();
        first.Add(Notification(scope)); await first.SaveChangesAsync();
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = Task.Run(async () =>
        {
            await using var db = fixture.Db();
            db.Add(Notification(scope)); secondStarted.SetResult(); await db.SaveChangesAsync();
        });
        await secondStarted.Task;
        await Task.Delay(200);
        second.IsCompleted.Should().BeFalse("the first recipient allocation has not committed");
        await tx.CommitAsync();
        await second.WaitAsync(TimeSpan.FromSeconds(15));
        await using var verify = fixture.Db();
        var page = await Repo(verify).GetChangesAsync(scope, 0, 1, default);
        page.Items.Single().Sequence.Should().Be(1); page.HasMore.Should().BeTrue();
        var next = await Repo(verify).GetChangesAsync(scope, page.Sequence, 1, default);
        next.Items.Single().Sequence.Should().Be(2); next.HasMore.Should().BeFalse();
    }

    [InboxSqlFact]
    public async Task ClaimRaceIsExclusiveAndOldLeaseCannotCompleteAfterReclaim()
    {
        var scope = await fixture.SeedAsync();
        await using (var seed = fixture.Db()) { seed.Add(Notification(scope)); await seed.SaveChangesAsync(); }
        await using var first = fixture.Db(); await using var second = fixture.Db();
        var claims = await Task.WhenAll(Repo(first).ClaimAsync(50, default), Repo(second).ClaimAsync(50, default));
        var leases = claims.SelectMany(x => x).Where(l => l.Scope == scope).ToArray();
        leases.Should().ContainSingle();
        var old = leases.Single();
        var future = new FixedTime(DateTimeOffset.UtcNow.AddSeconds(31));
        var reclaimed = (await Repo(second, future).ClaimAsync(50, default)).Single(l => l.Scope == scope);
        reclaimed.LeaseId.Should().NotBe(old.LeaseId);
        await Repo(first, future).CompleteAsync(old, true, default);
        (await first.Set<InboxDeliveryAttempt>().CountAsync(a => a.IntentId == old.Id)).Should().Be(0);
        await Repo(second, future).CompleteAsync(reclaimed, true, default);
        await Repo(second, future).CompleteAsync(reclaimed, true, default);
        (await first.Set<InboxDeliveryAttempt>().CountAsync(a => a.IntentId == old.Id)).Should().Be(1);
    }

    [InboxSqlFact]
    public async Task CanonicalCorrelationSharesIntentButReadChangesHaveNewIdentity()
    {
        var scope = await fixture.SeedAsync(); var correlation = Guid.NewGuid();
        await using var db = fixture.Db();
        var dashboard = Notification(scope, correlation); db.Add(dashboard); await db.SaveChangesAsync();
        var firstLease = (await Repo(db).ClaimAsync(50, default)).Single(l => l.Scope == scope);
        await Repo(db).CompleteAsync(firstLease, true, default);
        db.Add(Notification(scope, correlation)); await db.SaveChangesAsync();
        (await db.Set<NotificationDeliveryIntent>().CountAsync(i => i.UserId == scope.UserId)).Should().Be(1);
        (await db.Set<InboxChange>().CountAsync(i => i.UserId == scope.UserId)).Should().Be(2);
        var secondLease = (await Repo(db).ClaimAsync(50, default)).Single(l => l.Scope == scope);
        secondLease.Id.Should().Be(firstLease.Id);
        secondLease.EventId.Should().NotBe(firstLease.EventId);
        await Repo(db).CompleteAsync(firstLease, true, default);
        (await db.Set<NotificationDeliveryIntent>().AsNoTracking().SingleAsync(i => i.UserId == scope.UserId))
            .CompletedAtUtc.Should().BeNull("an old completion must not consume the later surface's hint");
        dashboard.IsRead = true; dashboard.ReadAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        (await db.Set<NotificationDeliveryIntent>().CountAsync(i => i.UserId == scope.UserId)).Should().Be(2);
        var existing = await db.Set<NotificationDeliveryIntent>().FirstAsync(i => i.UserId == scope.UserId);
        db.Add(new NotificationDeliveryIntent { SchoolId = scope.SchoolId, UserId = scope.UserId,
            SourceKey = existing.SourceKey, Channel = existing.Channel, EventId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5), NextAttemptAtUtc = DateTimeOffset.UtcNow });
        await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    [InboxSqlFact]
    public async Task SnapshotCountsAreUncappedAndPermissionsAreResolvedFromCurrentDatabase()
    {
        var scope = await fixture.SeedAsync(); await using var db = fixture.Db();
        var sender = new ApplicationUser { Id = Guid.NewGuid().ToString("N"), UserName = Guid.NewGuid().ToString("N") };
        db.Add(sender); await db.SaveChangesAsync();
        for (var i = 0; i < 121; i++)
            db.Add(new ConversationThread { SchoolId = scope.SchoolId, ThreadType = ConversationThreadType.GuardianTeacher,
                CreatedByUserId = scope.UserId, UpdatedByUserId = scope.UserId,
                Participants = [new() { SchoolId = scope.SchoolId, ApplicationUserId = scope.UserId,
                    CreatedByUserId = scope.UserId, UpdatedByUserId = scope.UserId }],
                Messages = [new() { SchoolId = scope.SchoolId, SenderUserId = sender.Id, OfficeHoursDisposition = OfficeHoursDisposition.SentImmediately,
                    CreatedByUserId = sender.Id, UpdatedByUserId = sender.Id,
                    Receipts = [new() { SchoolId = scope.SchoolId, RecipientUserId = scope.UserId,
                        DeliveryState = MessageDeliveryState.Delivered, DeliveredAt = DateTimeOffset.UtcNow }] }] });
        await db.SaveChangesAsync();
        var access = await Repo(db).GetAccessAsync(scope, scope.UserId, default);
        access.Should().NotBeNull();
        var state = await Repo(db).GetStateAsync(scope, access!, default);
        state.UnreadMessages.Should().Be(121); state.UnreadConversations.Should().Be(121);
        var visible = Notification(scope); db.Add(visible); db.Add(Notification(scope));
        var pending = Notification(scope); pending.RequiresApproval = true; db.Add(pending);
        var suppressed = Notification(scope); suppressed.IsSuppressed = true; db.Add(suppressed);
        await db.SaveChangesAsync();
        (await Repo(db).GetStateAsync(scope, access!, default)).UnreadNotifications.Should().Be(2);
        var notifications = new NotificationWorkflowRepository(db);
        foreach (var item in await notifications.GetAllOwnUnreadForUpdateAsync(scope.SchoolId, scope.UserId, default))
        { item.IsRead = true; item.ReadAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesAsync();
        (await Repo(db).GetStateAsync(scope, access!, default)).UnreadNotifications.Should().Be(0);
        pending.IsRead.Should().BeFalse(); suppressed.IsRead.Should().BeFalse();
        var threadId = await db.ConversationThreads.Where(t => t.SchoolId == scope.SchoolId).Select(t => t.Id).FirstAsync();
        (await Repo(db).GetWindowAsync(scope, threadId, null, 51, default))!.Should().ContainSingle();
        var role = await db.Roles.SingleAsync(r => r.Id == scope.UserId); role.IsActive = false; await db.SaveChangesAsync();
        (await Repo(db).GetAccessAsync(scope, scope.UserId, default)).Should().BeNull();
    }

    [InboxSqlFact]
    public async Task RetentionForcesExplicitResyncAndKeepsBusinessHistory()
    {
        var scope = await fixture.SeedAsync(); await using var db = fixture.Db();
        db.Add(Notification(scope)); await db.SaveChangesAsync();
        await db.Set<InboxChange>().Where(c => c.UserId == scope.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.OccurredAtUtc, DateTimeOffset.UtcNow.AddDays(-8)));
        await Repo(db).PruneAsync(default);
        var changes = await Repo(db).GetChangesAsync(scope, 0, 100, default);
        changes.ResyncRequired.Should().BeTrue();
        (await db.Notifications.CountAsync(n => n.UserId == scope.UserId)).Should().Be(1);
    }

    [InboxSqlFact]
    public async Task ChannelFailureAndRetryDoNotReexecuteBusinessOrChangeReadState()
    {
        var scope = await fixture.SeedAsync(); await using var db = fixture.Db();
        db.Add(Notification(scope)); await db.SaveChangesAsync();
        var repo = Repo(db);
        var lease = (await repo.ClaimAsync(50, default)).Single(x => x.Scope == scope);
        await new InboxDeliveryService(repo, new Publisher(true), NullLogger<InboxDeliveryService>.Instance).DeliverAsync(lease, default);
        var futureRepo = Repo(db, new FixedTime(DateTimeOffset.UtcNow.AddSeconds(10)));
        var retry = (await futureRepo.ClaimAsync(50, default)).Single(x => x.Scope == scope);
        retry.Attempt.Should().Be(2);
        await new InboxDeliveryService(futureRepo, new Publisher(false), NullLogger<InboxDeliveryService>.Instance).DeliverAsync(retry, default);
        (await db.Notifications.CountAsync(n => n.UserId == scope.UserId)).Should().Be(1);
        (await db.Notifications.SingleAsync(n => n.UserId == scope.UserId)).IsRead.Should().BeFalse();
        (await db.Set<InboxChange>().CountAsync(c => c.UserId == scope.UserId)).Should().Be(1);
        (await db.Set<InboxDeliveryAttempt>().CountAsync(a => a.IntentId == lease.Id)).Should().Be(2);
    }

    private sealed class Publisher(bool fail) : IInboxPublisher
    {
        public Task PublishAsync(RealtimeLease lease, CancellationToken ct) => fail
            ? Task.FromException(new TimeoutException("Synthetic transport timeout")) : Task.CompletedTask;
        public Task RevalidateConnectionsAsync(CancellationToken ct) => Task.CompletedTask;
    }

    [InboxSqlFact]
    public async Task SnapshotRetriesACommitBetweenMessageAndNotificationCounts()
    {
        var scope = await fixture.SeedAsync(); var sender = await fixture.SeedAsync();
        int threadId;
        await using (var seed = fixture.Db(false))
        {
            var thread = new ConversationThread { SchoolId = scope.SchoolId,
                ThreadType = ConversationThreadType.GuardianTeacher, CreatedByUserId = scope.UserId, UpdatedByUserId = scope.UserId,
                Participants = [new() { SchoolId = scope.SchoolId, ApplicationUserId = scope.UserId,
                    CreatedByUserId = scope.UserId, UpdatedByUserId = scope.UserId }] };
            seed.Add(thread); await seed.SaveChangesAsync(); threadId = thread.Id;
        }
        var pause = new PauseNotificationCount();
        await using var connectionSource = fixture.Db(false);
        await using var reader = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseSqlServer(connectionSource.Database.GetConnectionString()).AddInterceptors(pause).Options);
        var reading = Repo(reader).GetStateAsync(scope, new(true, true, "test"), default);
        await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await using var writer = fixture.Db();
            writer.Add(new ConversationMessage { SchoolId = scope.SchoolId, ConversationThreadId = threadId,
                SenderUserId = sender.UserId, CreatedByUserId = sender.UserId, UpdatedByUserId = sender.UserId,
                OfficeHoursDisposition = OfficeHoursDisposition.SentImmediately,
                Receipts = [new() { SchoolId = scope.SchoolId, RecipientUserId = scope.UserId,
                    DeliveryState = MessageDeliveryState.Delivered, DeliveredAt = DateTimeOffset.UtcNow }] });
            writer.Add(Notification(scope)); await writer.SaveChangesAsync();
        }
        finally { pause.Continue.SetResult(); }
        var state = await reading.WaitAsync(TimeSpan.FromSeconds(10));
        state.UnreadMessages.Should().Be(1); state.UnreadNotifications.Should().Be(1); state.Sequence.Should().Be(1);
    }

    private sealed class PauseNotificationCount : DbCommandInterceptor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int paused;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("COUNT(", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("[Notifications]", StringComparison.Ordinal)
                && Interlocked.Exchange(ref paused, 1) == 0)
            { Started.SetResult(); await Continue.Task.WaitAsync(ct); }
            return result;
        }
    }

    [InboxSqlFact]
    public async Task CleanupCannotDeleteAnIntentRearmedAfterItsCandidateScan()
    {
        var scope = await fixture.SeedAsync(); var correlation = Guid.NewGuid();
        await using var source = fixture.Db(); source.Add(Notification(scope, correlation)); await source.SaveChangesAsync();
        await source.Set<NotificationDeliveryIntent>().Where(i => i.UserId == scope.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.CreatedAtUtc, DateTimeOffset.UtcNow.AddDays(-8))
                .SetProperty(i => i.ExpiresAtUtc, DateTimeOffset.UtcNow.AddDays(-8)));
        source.ChangeTracker.Clear();
        var pause = new PauseIntentDeletion();
        await using var pruning = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseSqlServer(source.Database.GetConnectionString()).AddInterceptors(pause).Options);
        var cleanup = Repo(pruning).PruneAsync(default);
        await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { source.Add(Notification(scope, correlation)); await source.SaveChangesAsync(); }
        finally { pause.Continue.SetResult(); }
        await cleanup.WaitAsync(TimeSpan.FromSeconds(10));
        (await source.Set<NotificationDeliveryIntent>().AsNoTracking().CountAsync(i => i.UserId == scope.UserId)).Should().Be(1);
        (await Repo(source).ClaimAsync(50, default)).Should().Contain(i => i.Scope == scope);
    }

    private sealed class PauseIntentDeletion : DbCommandInterceptor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (command.CommandText.TrimStart().StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("[NotificationDeliveryIntents]", StringComparison.Ordinal))
            { Started.TrySetResult(); await Continue.Task.WaitAsync(ct); }
            return result;
        }
    }

    [InboxSqlFact]
    public async Task RealSqlDeadlockRetriesWholeCommandWithFreshScopeAndCommitsOnce()
    {
        var scope = await fixture.SeedAsync(); var other = await fixture.SeedAsync();
        await using var victim = fixture.Db(false);
        await using var tx = await victim.Database.BeginTransactionAsync();
        await victim.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY LOW");
        await victim.Database.ExecuteSqlInterpolatedAsync($"UPDATE Schools SET City='victim' WHERE Id={scope.SchoolId}");
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var winner = Task.Run(async () =>
        {
            await using var db = fixture.Db(false); await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Schools SET City='winner' WHERE Id={other.SchoolId}");
            locked.SetResult();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Schools SET City='winner' WHERE Id={scope.SchoolId}");
            await transaction.CommitAsync();
        });
        await locked.Task;
        var proof = new RetryProof();
        var services = new ServiceCollection();
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<InboxService>());
        services.AddTransient<IRequestHandler<MarkAllNotificationsReadCommand, ApiResponse<bool>>>(
            _ => new RetryHandler(fixture, scope, proof));
        await using var provider = services.BuildServiceProvider();
        var behavior = new InboxCommandRetryBehavior<MarkAllNotificationsReadCommand, ApiResponse<bool>>(
            provider.GetRequiredService<IServiceScopeFactory>());
        var originalRuns = 0;
        var result = await behavior.Handle(new(), async () =>
        {
            originalRuns++;
            await victim.Database.ExecuteSqlInterpolatedAsync($"UPDATE Schools SET City='victim' WHERE Id={other.SchoolId}");
            throw new InvalidOperationException("The low-priority transaction must be the deadlock victim.");
        }, default);
        await winner.WaitAsync(TimeSpan.FromSeconds(15));
        result.IsSuccess.Should().BeTrue(); originalRuns.Should().Be(1); proof.Calls.Should().Be(1);
        await using var verify = fixture.Db();
        (await verify.Notifications.CountAsync(n => n.UserId == scope.UserId)).Should().Be(1);
        (await verify.Set<InboxChange>().CountAsync(c => c.UserId == scope.UserId)).Should().Be(1);
    }

    private sealed class RetryProof { public int Calls { get; set; } }
    private sealed class RetryHandler(InboxSqlFixture fixture, InboxScope scope, RetryProof proof)
        : IRequestHandler<MarkAllNotificationsReadCommand, ApiResponse<bool>>
    {
        public async Task<ApiResponse<bool>> Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
        {
            proof.Calls++;
            await using var db = fixture.Db(); db.Add(Notification(scope)); await db.SaveChangesAsync(ct);
            return ApiResponse<bool>.Success(true);
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }
}
