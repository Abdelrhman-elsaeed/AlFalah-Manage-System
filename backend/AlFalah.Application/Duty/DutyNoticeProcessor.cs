using AlFalah.Domain.Entities.Duty;

namespace AlFalah.Application.Duty;

public sealed class DutyNoticeProcessor(IDutyRepository duty, IDutyExecutionRepository repo)
{
    public async Task ProcessAsync(DismissalNoticeBatch pending, CancellationToken ct)
    {
        var school = pending.SchoolId;
        await duty.AtomicAsync(school, async () =>
        {
            var batch = await repo.BatchAsync(school, pending.Id, ct);
            if (batch is null || batch.State == "Delivered") return false;
            var fact = (await repo.FactAsync(school, batch.FactId, ct))!;
            var eligible = await repo.RecipientsAsync(school, fact.StudentId, DutyService.Today(fact.TimeZoneId), ct);
            var existing = await repo.NoticeRecipientsAsync(school, batch.Id, ct);
            foreach (var user in eligible.Where(user => existing.All(x => x.RecipientUserId != user)))
                duty.Add(new DismissalNoticeRecipient { SchoolId = school, BatchId = batch.Id, RecipientUserId = user });
            batch.State = eligible.Count == 0 ? "NoEligibleGuardian" : "Pending";
            batch.Attempts++; batch.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(5);
            return true;
        }, ct);
        var recipients = await repo.NoticeRecipientsAsync(school, pending.Id, ct);
        foreach (var recipient in recipients)
            foreach (var surface in new[] { "Dashboard", "Message" })
            {
                try
                {
                    await duty.AtomicAsync(school, async () =>
                    {
                        var batch = (await repo.BatchAsync(school, pending.Id, ct))!;
                        var fact = (await repo.FactAsync(school, batch.FactId, ct))!;
                        var row = (await repo.NoticeRecipientsAsync(school, batch.Id, ct)).Single(x => x.Id == recipient.Id);
                        if ((surface == "Dashboard" ? row.DashboardState : row.MessageState) == "Delivered") return true;
                        var eligible = await repo.RecipientsAsync(school, fact.StudentId, DutyService.Today(fact.TimeZoneId), ct);
                        if (!eligible.Contains(row.RecipientUserId))
                        {
                            if (surface == "Dashboard") row.DashboardState = "NoEligibleGuardian"; else row.MessageState = "NoEligibleGuardian";
                            return false;
                        }
                        if (surface == "Dashboard") await repo.DeliverDashboardAsync(fact, batch, row, ct);
                        else await repo.DeliverMessageAsync(fact, batch, row, ct);
                        if (row.DashboardState == "Delivered" && row.MessageState == "Delivered") row.ErrorCode = null;
                        return true;
                    }, ct);
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    await duty.AtomicAsync(school, async () =>
                    {
                        var row = (await repo.NoticeRecipientsAsync(school, pending.Id, ct)).Single(x => x.Id == recipient.Id);
                        if (surface == "Dashboard") row.DashboardState = "RetryScheduled"; else row.MessageState = "RetryScheduled";
                        row.ErrorCode = "DutyNoticeRetry"; return true;
                    }, ct);
                }
            }
        await duty.AtomicAsync(school, async () =>
        {
            var batch = (await repo.BatchAsync(school, pending.Id, ct))!;
            var rows = await repo.NoticeRecipientsAsync(school, batch.Id, ct);
            var missing = rows.SelectMany(x => new[] { x.DashboardState, x.MessageState }).Where(x => x != "Delivered").ToArray();
            batch.State = rows.Count == 0 || missing.Length > 0 && missing.All(x => x == "NoEligibleGuardian")
                ? "NoEligibleGuardian" : missing.Length == 0 ? "Delivered"
                : missing.Contains("RetryScheduled") ? batch.Attempts >= 8 ? "NeedsAttention" : "RetryScheduled" : "Pending";
            batch.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, 5 * Math.Pow(2, Math.Min(10, batch.Attempts))));
            return true;
        }, ct);
    }
}
