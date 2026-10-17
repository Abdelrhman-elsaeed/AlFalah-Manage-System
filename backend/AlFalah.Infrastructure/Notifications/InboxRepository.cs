using System.Data;
using System.Security.Cryptography;
using System.Text;
using AlFalah.Application.Notifications;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Entities.Notifications;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AlFalah.Infrastructure.Notifications;

public sealed partial class InboxRepository(AlFalahDbContext db, IOptions<InboxOptions> options,
    TimeProvider time) : IInboxRepository
{
    public async Task<InboxAccess?> GetAccessAsync(InboxScope scope, string? securityStamp, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(x => x.Id == scope.UserId && x.IsActive)
            .Select(x => new { x.SecurityStamp }).SingleOrDefaultAsync(ct);
        if (user is null || securityStamp is not null && securityStamp != user.SecurityStamp) return null;
        var assignments = db.UserSchoolRoles.AsNoTracking().Where(x => x.SchoolId == scope.SchoolId
            && x.UserId == scope.UserId && x.IsActive && x.Role.IsActive && !x.School.IsDeleted && x.School.IsActive);
        var roles = await assignments.Select(x => x.Role.Name!).Distinct().ToArrayAsync(ct);
        if (roles.Length == 0) return null;
        if (roles.Contains(RoleNames.Guardian)
            && !await db.GuardianProfiles.AnyAsync(x => x.SchoolId == scope.SchoolId
                && x.ApplicationUserId == scope.UserId && x.IsActive && !x.IsDeleted, ct)) return null;
        var roleIds = assignments.Select(x => x.RoleId);
        var permissions = await db.RolePermissions.AsNoTracking().Where(x => roleIds.Contains(x.RoleId))
            .Select(x => x.Permission.Name).Distinct().ToArrayAsync(ct);
        var messaging = permissions.Contains(PermissionNames.MessagingViewOwn);
        var notifications = permissions.Contains(PermissionNames.NotificationViewOwn);
        if (!messaging && !notifications) return null;
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('|', roles.Order()) + ":" + string.Join('|', permissions.Order())
            + ":" + await GuardianBindingAsync(scope, roles.Contains(RoleNames.Guardian), ct))));
        return new(messaging, notifications, fingerprint);
    }

    private async Task<string> GuardianBindingAsync(InboxScope scope, bool guardian, CancellationToken ct)
    {
        if (!guardian) return "";
        var date = await DutyGuardianScope.CurrentDate(db, scope.SchoolId, ct);
        var links = await db.StudentGuardians.AsNoTracking().Where(g => g.SchoolId == scope.SchoolId
            && g.GuardianProfile.ApplicationUserId == scope.UserId && !g.IsDeleted
            && g.GuardianProfile.IsActive && !g.GuardianProfile.IsDeleted && g.Student.IsActive && !g.Student.IsDeleted
            && g.ValidFrom <= date && (g.ValidTo == null || g.ValidTo >= date))
            .OrderBy(g => g.StudentId).Select(g => g.StudentId).ToArrayAsync(ct);
        return string.Join(',', links);
    }

    private async Task<IQueryable<ConversationThread>> ThreadsAsync(InboxScope scope, CancellationToken ct)
    {
        var roles = await db.UserSchoolRoles.AsNoTracking().Where(x => x.SchoolId == scope.SchoolId
            && x.UserId == scope.UserId && x.IsActive && x.Role.IsActive && x.User.IsActive)
            .Select(x => x.Role.Name!).Distinct().ToArrayAsync(ct);
        var guardian = roles.Contains(RoleNames.Guardian);
        var allowed = new List<ConversationThreadType>();
        if (guardian || roles.Contains(RoleNames.Instructor)) allowed.Add(ConversationThreadType.GuardianTeacher);
        if (guardian || roles.Contains(RoleNames.StudentAffairsOfficer)) allowed.Add(ConversationThreadType.GuardianStudentAffairs);
        if (guardian || roles.Contains(RoleNames.SocialWorker)) allowed.Add(ConversationThreadType.GuardianSocialWorker);
        if (guardian) allowed.Add(ConversationThreadType.SystemNotice);
        var date = await DutyGuardianScope.CurrentDate(db, scope.SchoolId, ct);
        return db.ConversationThreads.AsNoTracking().Where(x => x.SchoolId == scope.SchoolId && !x.IsDeleted
            && allowed.Contains(x.ThreadType)
            && x.Participants.Any(p => p.ApplicationUserId == scope.UserId && !p.IsDeleted && p.LeftAt == null)
            && (!guardian || db.StudentGuardians.Any(g => g.SchoolId == scope.SchoolId && g.StudentId == x.StudentId
                && !g.IsDeleted && g.GuardianProfile.ApplicationUserId == scope.UserId && g.GuardianProfile.IsActive
                && !g.GuardianProfile.IsDeleted && g.GuardianProfile.ApplicationUser.IsActive
                && !g.Student.IsDeleted && g.Student.IsActive
                && g.ValidFrom <= date && (g.ValidTo == null || g.ValidTo >= date))));
    }

    public async Task<InboxSnapshot> GetStateAsync(InboxScope scope, InboxAccess access, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var state = await ReadStateAsync(scope, access, !options.Value.JournalEnabled || attempt >= 3, ct);
                if (state is not null) return state;
            }
            catch (SqlException ex) when (ex.Number == 1205 && attempt < 5)
            { await Task.Delay(50 * (attempt + 1), ct); }
        }
    }

    private async Task<InboxSnapshot?> ReadStateAsync(InboxScope scope, InboxAccess access, bool serialized, CancellationToken ct)
    {
        // With the journal active, equal before/after committed recipient versions prove
        // that no count-affecting transaction crossed these reads. Retry an overlapping
        // commit; fall back to a locked snapshot under sustained contention or no journal.
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(serialized ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, ct) : null;
        var before = await SequenceAsync(scope, ct);
        int? unreadMessages = null, unreadConversations = null, unreadNotifications = null;
        if (access.Messaging)
        {
            var threads = await ThreadsAsync(scope, ct);
            var receipts = db.MessageReceipts.AsNoTracking().Where(r => r.SchoolId == scope.SchoolId
                && r.RecipientUserId == scope.UserId && r.DeliveryState == MessageDeliveryState.Delivered
                && r.ReadAt == null && !r.ConversationMessage.IsDeleted && r.ConversationMessage.SenderUserId != scope.UserId
                && threads.Any(t => t.Id == r.ConversationMessage.ConversationThreadId));
            unreadMessages = await receipts.CountAsync(ct);
            unreadConversations = await receipts.Select(r => r.ConversationMessage.ConversationThreadId).Distinct().CountAsync(ct);
        }
        if (access.Notifications)
            unreadNotifications = await new NotificationWorkflowRepository(db).GetUnreadCountAsync(scope.SchoolId, scope.UserId, ct);
        var sequence = await SequenceAsync(scope, ct);
        var result = new InboxSnapshot(unreadMessages, unreadConversations, unreadNotifications, sequence, time.GetUtcNow());
        if (transaction is not null) await transaction.CommitAsync(ct);
        return !serialized && before != sequence ? null : result;
    }

    private async Task<long> SequenceAsync(InboxScope scope, CancellationToken ct) => options.Value.JournalEnabled
        ? await db.Set<RecipientInboxStream>().AsNoTracking().Where(x => x.SchoolId == scope.SchoolId && x.UserId == scope.UserId)
            .Select(x => (long?)x.CommittedSequence).SingleOrDefaultAsync(ct) ?? 0 : 0;

    public async Task<InboxChangesPage> GetChangesAsync(InboxScope scope, long sequence, int limit, CancellationToken ct)
    {
        if (!options.Value.JournalEnabled) return new([], 0, false, true);
        var stream = await db.Set<RecipientInboxStream>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.SchoolId == scope.SchoolId && x.UserId == scope.UserId, ct);
        if (sequence < 0 || sequence > (stream?.CommittedSequence ?? 0)
            || sequence < (stream?.MinimumRetainedSequence ?? 0))
            return new([], stream?.CommittedSequence ?? 0, false, true);
        var rows = await db.Set<InboxChange>().AsNoTracking().Where(x => x.SchoolId == scope.SchoolId
            && x.UserId == scope.UserId && x.Sequence > sequence)
            .OrderBy(x => x.Sequence).Take(limit + 1)
            .Select(x => new InboxChangeDto(x.EventId, x.Sequence, x.OccurredAtUtc)).ToArrayAsync(ct);
        var items = rows.Take(limit).ToArray();
        return new(items, items.LastOrDefault()?.Sequence ?? sequence, rows.Length > limit, false);
    }

    public async Task<IReadOnlyList<MessageWindowRow>?> GetWindowAsync(InboxScope scope, int conversationId,
        MessagePosition? before, int limit, CancellationToken ct)
    {
        var threads = await ThreadsAsync(scope, ct);
        if (!await threads.AnyAsync(x => x.Id == conversationId, ct)) return null;
        var query = db.ConversationMessages.AsNoTracking().Where(m => m.SchoolId == scope.SchoolId
            && m.ConversationThreadId == conversationId && !m.IsDeleted
            && (m.SenderUserId == scope.UserId || m.Receipts.Any(r => r.RecipientUserId == scope.UserId
                && r.DeliveryState == MessageDeliveryState.Delivered)))
            .Select(m => new { Message = m, VisibleAt = m.SenderUserId == scope.UserId ? m.QueuedAt
                : m.Receipts.Where(r => r.RecipientUserId == scope.UserId && r.DeliveryState == MessageDeliveryState.Delivered)
                    .Select(r => r.DeliveredAt ?? r.CreatedAt).First() });
        if (before is not null)
            query = query.Where(x => x.VisibleAt < before.VisibleAtUtc
                || x.VisibleAt == before.VisibleAtUtc && x.Message.Id < before.MessageId);
        var positions = await query.OrderByDescending(x => x.VisibleAt).ThenByDescending(x => x.Message.Id)
            .Take(Math.Clamp(limit, 1, 101)).Select(x => new { x.Message.Id, x.VisibleAt }).ToArrayAsync(ct);
        var ids = positions.Select(x => x.Id).ToArray();
        var messages = await db.ConversationMessages.AsNoTracking().Where(m => ids.Contains(m.Id) && m.SchoolId == scope.SchoolId)
            .Include(m => m.SenderUser).Include(m => m.Receipts).ThenInclude(r => r.RecipientUser).ToDictionaryAsync(m => m.Id, ct);
        return positions.Select(p => new MessageWindowRow(Map(messages[p.Id], scope.UserId, p.VisibleAt), p.VisibleAt)).ToArray();
    }

    private static ConversationMessageDto Map(ConversationMessage m, string user, DateTimeOffset visibleAt) => new(
        m.Id, m.ConversationThreadId,
        new ActorSummaryDto(m.SenderUserId ?? "system", m.IsSystemOrigin ? "النظام / System"
            : $"{m.SenderUser?.FirstName} {m.SenderUser?.LastName}".Trim(), m.IsSystemOrigin ? "System" : m.SenderUserId == user ? "Me" : "Sender"),
        m.Body, m.ReplyToMessageId, m.SentAt ?? m.CreatedAt,
        m.Receipts.All(r => r.DeliveryState == MessageDeliveryState.Delivered) ? MessageDeliveryState.Delivered
            : m.Receipts.Any(r => r.DeliveryState == MessageDeliveryState.Failed) ? MessageDeliveryState.Failed : MessageDeliveryState.Pending,
        m.OfficeHoursDisposition, m.NextEligibleSendAt,
        m.Receipts.Select(r => new NotificationDeliveryDto($"{r.RecipientUser?.FirstName} {r.RecipientUser?.LastName}".Trim(),
            "Recipient", r.DeliveryState == MessageDeliveryState.Delivered ? NotificationDeliveryStatus.Delivered
                : r.DeliveryState == MessageDeliveryState.Failed ? NotificationDeliveryStatus.Failed : NotificationDeliveryStatus.Pending,
            r.DeliveredAt, r.ReadAt)).ToArray()) { VisibleAtUtc = visibleAt };
}
