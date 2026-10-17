using AlFalah.Application.Notifications;
using AlFalah.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Notifications;

public sealed partial class InboxRepository
{
    public async Task<IReadOnlyList<RealtimeLease>> ClaimAsync(int limit, CancellationToken ct)
    {
        if (!options.Value.JournalEnabled || !options.Value.RealtimeEnabled) return [];
        var now = time.GetUtcNow();
        var until = now.AddSeconds(30);
        limit = Math.Clamp(limit, 1, 50);
        List<NotificationDeliveryIntent> rows;
        if (db.Database.IsSqlServer())
        {
            // A pooled SQL connection may retain a previous workflow's isolation level.
            // READPAST requires an explicit compatible level, including after Duty transactions.
            await using var claimTransaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
            rows = await db.Set<NotificationDeliveryIntent>().FromSqlInterpolated($"""
                ;WITH due AS (
                    SELECT TOP ({limit}) * FROM [NotificationDeliveryIntents] WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
                    WHERE [CompletedAtUtc] IS NULL AND [NextAttemptAtUtc] <= {now}
                        AND [ExpiresAtUtc] > {now} AND [Attempts] < 8
                        AND ([LeaseUntilUtc] IS NULL OR [LeaseUntilUtc] <= {now})
                    ORDER BY [Id]
                )
                UPDATE due SET [LeaseId] = NEWID(), [LeaseUntilUtc] = {until}, [Attempts] = [Attempts] + 1
                OUTPUT inserted.*;
                """).AsNoTracking().ToListAsync(ct);
            await claimTransaction.CommitAsync(ct);
        }
        else
        {
            rows = await db.Set<NotificationDeliveryIntent>().Where(i => i.CompletedAtUtc == null
                && i.NextAttemptAtUtc <= now && i.ExpiresAtUtc > now && i.Attempts < 8
                && (i.LeaseUntilUtc == null || i.LeaseUntilUtc <= now)).OrderBy(i => i.Id).Take(limit).ToListAsync(ct);
            foreach (var row in rows) { row.LeaseId = Guid.NewGuid(); row.LeaseUntilUtc = until; row.Attempts++; }
            await db.SaveChangesAsync(ct);
        }
        return rows.Select(i => new RealtimeLease(i.Id, i.EventId, new(i.SchoolId, i.UserId),
            i.LeaseId!.Value, i.CreatedAtUtc, i.Attempts)).ToArray();
    }

    public async Task CompleteAsync(RealtimeLease lease, bool success, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var retry = now.AddSeconds(Math.Min(60, Math.Pow(2, lease.Attempt)));
        var query = db.Set<NotificationDeliveryIntent>().Where(i => i.Id == lease.Id && i.LeaseId == lease.LeaseId
            && i.LeaseUntilUtc > now && i.CompletedAtUtc == null);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        int changed;
        if (db.Database.IsRelational())
            changed = await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.CompletedAtUtc, success ? (DateTimeOffset?)now : null)
                .SetProperty(i => i.NextAttemptAtUtc, retry)
                .SetProperty(i => i.LeaseId, (Guid?)null).SetProperty(i => i.LeaseUntilUtc, (DateTimeOffset?)null), ct);
        else
        {
            var row = await query.SingleOrDefaultAsync(ct);
            changed = row is null ? 0 : 1;
            if (row is not null)
            {
                row.CompletedAtUtc = success ? now : null; row.NextAttemptAtUtc = retry;
                row.LeaseId = null; row.LeaseUntilUtc = null;
            }
        }
        if (changed > 0)
        {
            db.Add(new InboxDeliveryAttempt { IntentId = lease.Id, LeaseId = lease.LeaseId, CompletedAtUtc = now, Succeeded = success });
            await db.SaveChangesAsync(ct);
        }
        if (tx is not null) await tx.CommitAsync(ct);
    }

    public async Task PruneAsync(CancellationToken ct)
    {
        if (!options.Value.JournalEnabled || !db.Database.IsSqlServer()) return;
        var cutoff = time.GetUtcNow().AddDays(-Math.Clamp(options.Value.RetentionDays, 1, 90));
        // One recipient per sweep bounds the lock duration. A stale cursor is explicitly
        // reset through REST; old message/notification records are never deleted here.
        await using (var cleanup = await db.Database.BeginTransactionAsync(ct))
        {
            var ids = await db.Set<NotificationDeliveryIntent>().Where(i => i.CreatedAtUtc < cutoff && i.ExpiresAtUtc < cutoff)
                .OrderBy(i => i.Id).Select(i => i.Id).Take(500).ToArrayAsync(ct);
            await db.Set<InboxDeliveryAttempt>().Where(a => ids.Contains(a.IntentId) && a.CompletedAtUtc < cutoff).ExecuteDeleteAsync(ct);
            await db.Set<NotificationDeliveryIntent>().Where(i => ids.Contains(i.Id)
                && i.CreatedAtUtc < cutoff && i.ExpiresAtUtc < cutoff).ExecuteDeleteAsync(ct);
            await cleanup.CommitAsync(ct);
        }
        var recipient = await db.Set<InboxChange>().AsNoTracking().Where(c => c.OccurredAtUtc < cutoff)
            .OrderBy(c => c.Id).Select(c => new { c.SchoolId, c.UserId }).FirstOrDefaultAsync(ct);
        if (recipient is null) return;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @watermark bigint;
            SELECT @watermark = [CommittedSequence] FROM [RecipientInboxStreams] WITH (UPDLOCK, HOLDLOCK)
                WHERE [SchoolId] = {recipient.SchoolId} AND [UserId] = {recipient.UserId};
            SELECT @watermark = MAX([Sequence]) FROM [InboxChanges]
                WHERE [SchoolId] = {recipient.SchoolId} AND [UserId] = {recipient.UserId} AND [OccurredAtUtc] < {cutoff};
            UPDATE [RecipientInboxStreams] SET [MinimumRetainedSequence] = @watermark
                WHERE [SchoolId] = {recipient.SchoolId} AND [UserId] = {recipient.UserId} AND @watermark IS NOT NULL;
            DELETE FROM [InboxChanges] WHERE [SchoolId] = {recipient.SchoolId} AND [UserId] = {recipient.UserId}
                AND [Sequence] <= @watermark;
            """, ct);
        await transaction.CommitAsync(ct);
    }
}
