using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Notifications;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums.StudentAffairs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AlFalah.Infrastructure.Data;

public partial class AlFalahDbContext
{
    // Persistence projection only: existing Application workflows still decide delivery,
    // approval and office hours. Capture before SaveChanges resets original values.
    private List<InboxTransition> CaptureInboxTransitions()
    {
        if (!InboxJournalEnabled) return [];
        var transitions = new List<InboxTransition>();
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is Notification n && n.SchoolId is { } school)
            {
                var visible = !n.IsDeleted && !n.RequiresApproval && !n.IsSuppressed
                    && n.DeliveryStatus == NotificationDeliveryStatus.Delivered;
                var wasVisible = entry.State != EntityState.Added && !Original<bool>(entry, nameof(n.IsDeleted))
                    && !Original<bool>(entry, nameof(n.RequiresApproval)) && !Original<bool>(entry, nameof(n.IsSuppressed))
                    && Original<NotificationDeliveryStatus>(entry, nameof(n.DeliveryStatus)) == NotificationDeliveryStatus.Delivered;
                if (visible || wasVisible)
                    transitions.Add(new(school, n.UserId, n, visible && !wasVisible ? n.CorrelationId : null));
            }
            else if (entry.Entity is MessageReceipt r)
            {
                var visible = r.DeliveryState == MessageDeliveryState.Delivered;
                var wasVisible = entry.State != EntityState.Added
                    && Original<MessageDeliveryState>(entry, nameof(r.DeliveryState)) == MessageDeliveryState.Delivered;
                if (visible || wasVisible) transitions.Add(new(r.SchoolId, r.RecipientUserId, r, null));
            }
            else if (entry.Entity is ConversationMessage m && m.SenderUserId is { Length: > 0 } sender)
                transitions.Add(new(m.SchoolId, sender, m, null));
            else if (entry.Entity is ConversationThread thread && entry.State != EntityState.Added
                && (Original<ConversationThreadStatus>(entry, nameof(thread.Status)) != thread.Status
                    || Original<bool>(entry, nameof(thread.IsDeleted)) != thread.IsDeleted))
                transitions.Add(new(thread.SchoolId, thread.CreatedByUserId, thread, null));
        }
        return transitions;
    }

    private static T Original<T>(EntityEntry entry, string name) => (T)entry.OriginalValues[name]!;

    private async Task AppendInboxChangesAsync(List<InboxTransition> transitions, CancellationToken ct)
    {
        // A receipt's sender may not be tracked by the releasing worker.
        foreach (var transition in transitions.ToArray())
        {
            if (transition.Source is ConversationThread thread)
            {
                transitions.Remove(transition);
                var users = await ConversationParticipants.AsNoTracking().Where(p => p.SchoolId == thread.SchoolId
                    && p.ConversationThreadId == thread.Id && !p.IsDeleted && p.LeftAt == null)
                    .Select(p => p.ApplicationUserId).ToArrayAsync(ct).ConfigureAwait(false);
                transitions.AddRange(users.Select(user => new InboxTransition(thread.SchoolId, user, thread, null)));
                continue;
            }
            if (transition.Source is ConversationMessage changed)
            {
                var users = await MessageReceipts.AsNoTracking().Where(r => r.SchoolId == changed.SchoolId
                    && r.ConversationMessageId == changed.Id && r.DeliveryState == MessageDeliveryState.Delivered)
                    .Select(r => r.RecipientUserId).ToArrayAsync(ct).ConfigureAwait(false);
                transitions.AddRange(users.Select(user => new InboxTransition(changed.SchoolId, user, changed, null)));
                continue;
            }
            if (transition.Source is not MessageReceipt receipt) continue;
            var message = receipt.ConversationMessage ?? await ConversationMessages.AsNoTracking()
                .SingleAsync(m => m.Id == receipt.ConversationMessageId && m.SchoolId == receipt.SchoolId, ct).ConfigureAwait(false);
            if (message.SenderUserId is { Length: > 0 } sender)
                transitions.Add(new(message.SchoolId, sender, message, null));
            // Duty message and dashboard surfaces share this canonical fact/revision identity.
            if (message.SystemCorrelationId is { } correlation && receipt.ReadAt is null)
                transition.CorrelationId = correlation;
        }
        var now = inboxTime.GetUtcNow();
        foreach (var group in transitions.GroupBy(t => (t.SchoolId, t.UserId))
                     .OrderBy(g => g.Key.SchoolId).ThenBy(g => g.Key.UserId, StringComparer.Ordinal))
        {
            var (school, user) = group.Key;
            long sequence;
            if (Database.IsSqlServer())
            {
                // Range locks cover first-writer initialization; the update lock is held by
                // the SAME transaction as business changes, journal and channel intent.
                await Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE [RecipientInboxStreams] WITH (UPDLOCK, SERIALIZABLE)
                    SET [CommittedSequence] = [CommittedSequence] + 1
                    WHERE [SchoolId] = {school} AND [UserId] = {user};
                    IF @@ROWCOUNT = 0
                        INSERT INTO [RecipientInboxStreams] ([SchoolId],[UserId],[CommittedSequence],[MinimumRetainedSequence])
                        VALUES ({school},{user},1,0);
                    """, ct).ConfigureAwait(false);
                sequence = await Set<RecipientInboxStream>().AsNoTracking()
                    .Where(s => s.SchoolId == school && s.UserId == user)
                    .Select(s => s.CommittedSequence).SingleAsync(ct).ConfigureAwait(false);
            }
            else
            {
                var stream = await Set<RecipientInboxStream>().SingleOrDefaultAsync(s => s.SchoolId == school && s.UserId == user, ct).ConfigureAwait(false);
                if (stream is null) { stream = new() { SchoolId = school, UserId = user }; Add(stream); }
                sequence = ++stream.CommittedSequence;
            }
            var eventId = Guid.NewGuid();
            Add(new InboxChange { SchoolId = school, UserId = user, Sequence = sequence, EventId = eventId, OccurredAtUtc = now });
            foreach (var sourceKey in group.Select(t => t.CorrelationId is { } correlation
                         ? $"fact:{correlation:N}" : $"change:{eventId:N}").Distinct())
            {
                var existing = await Set<NotificationDeliveryIntent>().SingleOrDefaultAsync(i => i.SchoolId == school && i.UserId == user
                    && i.SourceKey == sourceKey && i.Channel == "Realtime", ct).ConfigureAwait(false);
                if (existing is not null)
                {
                    // A second Duty surface can commit after the first hint was sent.
                    // Reuse its canonical identity, but make the latest invalidation due
                    // again and fence an older in-flight completion with a new lease.
                    existing.EventId = eventId;
                    existing.CompletedAtUtc = null;
                    existing.NextAttemptAtUtc = now;
                    existing.ExpiresAtUtc = now.AddMinutes(5);
                    existing.LeaseId = null;
                    existing.LeaseUntilUtc = null;
                    existing.Attempts = 0;
                    Entry(existing).State = EntityState.Modified;
                    continue;
                }
                Add(new NotificationDeliveryIntent
                {
                    SchoolId = school, UserId = user, SourceKey = sourceKey, EventId = eventId,
                    CreatedAtUtc = now, NextAttemptAtUtc = now, ExpiresAtUtc = now.AddMinutes(5)
                });
            }
        }
    }

    private sealed record InboxTransition(int SchoolId, string UserId, object Source, Guid? InitialCorrelation)
    {
        public Guid? CorrelationId { get; set; } = InitialCorrelation;
    }
}
