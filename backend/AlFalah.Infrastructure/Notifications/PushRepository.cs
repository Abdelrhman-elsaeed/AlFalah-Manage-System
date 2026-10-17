using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.Notifications;
using AlFalah.Domain.Entities.Notifications;
using AlFalah.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AlFalah.Infrastructure.Notifications;

public sealed class PushRepository(AlFalahDbContext db, IDataProtectionProvider protection, ICenterRepository center,
    IOptions<WebPushOptions> options, TimeProvider time) : IPushRepository
{
    private readonly IDataProtector protector = protection.CreateProtector("AlFalah.BrowserPush.Material.v1");
    private static PushDevice Dto(BrowserPushSubscription x) => new(x.Id, x.InstallationId, x.BindingVersion, x.ConfirmedAtUtc);

    public async Task<PushDevice?> RegisterAsync(InboxScope scope, RegisterPush request, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Subscription.Endpoint)));
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (db.Database.IsSqlServer())
        foreach (var resource in new[] { "AlFalah:PushEndpoint:" + hash, "AlFalah:PushInstallation:" + request.InstallationId.ToString("N") }.Order())
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Push binding lock timeout',1;", ct);
        var installation = await db.Set<BrowserPushSubscription>().SingleOrDefaultAsync(x => x.InstallationId == request.InstallationId && x.RevokedAtUtc == null, ct);
        if (installation is not null && (installation.UserId != scope.UserId || installation.SchoolId != scope.SchoolId)) return null;
        // Endpoint ownership, not InstallationId, is the authorization boundary.
        var row = await db.Set<BrowserPushSubscription>().SingleOrDefaultAsync(x => x.EndpointHash == hash, ct);
        if (row is not null && row.RevokedAtUtc is null && (row.UserId != scope.UserId || row.SchoolId != scope.SchoolId)) return null;
        if (installation is not null && installation.EndpointHash != hash) { installation.RevokedAtUtc = time.GetUtcNow(); installation.BindingVersion++; }
        if (row is null)
        {
            if (await db.Set<BrowserPushSubscription>().CountAsync(x => x.UserId == scope.UserId && x.RevokedAtUtc == null, ct) >= 20) return null;
            row = new() { EndpointHash = hash, CreatedAtUtc = time.GetUtcNow() }; db.Add(row);
        }
        else
        {
            PushMaterial? previous = null;
            try { previous = JsonSerializer.Deserialize<PushMaterial>(protector.Unprotect(row.ProtectedMaterial)); }
            catch (Exception ex) when (ex is CryptographicException or JsonException) { }
            if (row.RevokedAtUtc is not null || row.VapidKeyId != request.VapidKeyId || previous != request.Subscription)
                row.BindingVersion++;
        }
        row.SchoolId = scope.SchoolId; row.UserId = scope.UserId; row.InstallationId = request.InstallationId;
        row.VapidKeyId = request.VapidKeyId; row.ConfirmedAtUtc = time.GetUtcNow(); row.RevokedAtUtc = null;
        row.ProtectedMaterial = protector.Protect(JsonSerializer.Serialize(request.Subscription));
        await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
        return Dto(row);
    }
    public async Task<IReadOnlyList<PushDevice>> DevicesAsync(InboxScope scope, CancellationToken ct) => await db.Set<BrowserPushSubscription>().AsNoTracking()
        .Where(x => x.SchoolId == scope.SchoolId && x.UserId == scope.UserId && x.RevokedAtUtc == null)
        .OrderByDescending(x => x.ConfirmedAtUtc).Take(20).Select(x => new PushDevice(x.Id, x.InstallationId, x.BindingVersion, x.ConfirmedAtUtc)).ToArrayAsync(ct);

    public async Task RevokeAsync(InboxScope scope, Guid id, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var row = await db.Set<BrowserPushSubscription>().SingleOrDefaultAsync(x => x.Id == id && x.UserId == scope.UserId && x.SchoolId == scope.SchoolId, ct);
        if (row is null) return;
        row.RevokedAtUtc = time.GetUtcNow(); row.BindingVersion++;
        await db.SaveChangesAsync(ct);
        if (db.Database.IsRelational()) await db.Set<BrowserPushTarget>().Where(x => x.SubscriptionId == id && x.State == "Pending")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Cancelled").SetProperty(x => x.LeaseId, (Guid?)null), ct);
        else foreach (var target in await db.Set<BrowserPushTarget>().Where(x => x.SubscriptionId == id && x.State == "Pending").ToArrayAsync(ct)) target.State = "Cancelled";
        await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<PushLease>> ClaimAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (db.Database.IsRelational()) await db.Set<BrowserPushTarget>().Where(x => x.State == "Pending" && x.ExpiresAtUtc <= now)
            .OrderBy(x => x.Id).Take(100).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Expired").SetProperty(x => x.LeaseId, (Guid?)null), ct);
        var due = db.Set<BrowserPushTarget>().Where(x => x.State == "Pending" && x.NextAttemptAtUtc <= now && x.ExpiresAtUtc > now
            && (x.LeaseUntilUtc == null || x.LeaseUntilUtc < now) && x.Attempts < 8);
        var id = await due.OrderBy(x => x.NextAttemptAtUtc).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
        if (id is null) return [];
        var token = Guid.NewGuid();
        if (db.Database.IsRelational())
        {
            var claimed = await due.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseId, token)
                .SetProperty(x => x.LeaseUntilUtc, now.AddSeconds(30)).SetProperty(x => x.Attempts, x => x.Attempts + 1), ct);
            if (claimed == 0) return [];
        }
        else
        {
            var target = await due.SingleAsync(x => x.Id == id, ct); target.LeaseId = token; target.LeaseUntilUtc = now.AddSeconds(30); target.Attempts++;
            await db.SaveChangesAsync(ct);
        }
        return await db.Set<BrowserPushTarget>().AsNoTracking().Where(x => x.Id == id && x.LeaseId == token)
            .Select(x => new PushLease(x.Id, token, x.EntryId, x.SubscriptionId, x.BindingVersion, x.Attempts, x.ExpiresAtUtc)).ToArrayAsync(ct);
    }
    public async Task<(PersonalNotification Entry, PushMaterial Material)?> EligibleAsync(PushLease lease, CancellationToken ct)
    {
        if (!options.Value.DispatchEnabled) return null;
        var target = await db.Set<BrowserPushTarget>().AsNoTracking().Where(x => x.Id == lease.Id && x.State == "Pending" && x.LeaseId == lease.LeaseId
            && x.LeaseUntilUtc > time.GetUtcNow() && x.ExpiresAtUtc > time.GetUtcNow()).Include(x => x.Entry).Include(x => x.Subscription).SingleOrDefaultAsync(ct);
        if (target is null || target.Subscription.RevokedAtUtc is not null || target.Subscription.BindingVersion != lease.BindingVersion
            || target.Subscription.UserId != target.Entry.UserId || target.Subscription.SchoolId != target.Entry.SchoolId
            || target.Subscription.VapidKeyId != options.Value.VapidKeyId) return null;
        var scope = new InboxScope(target.Entry.SchoolId, target.Entry.UserId);
        if (!await center.HasAccessAsync(scope, ct) || await center.FindAsync(scope, lease.EntryId, ct) is not { } entry) return null;
        if (entry.SourceType == "DutyAssignment" && !await db.Set<AlFalah.Domain.Entities.Duty.DutyAssignment>().AnyAsync(
            x => x.Id == entry.SourceId && x.SchoolId == entry.SchoolId && entry.SourceKey == "duty-assignment:" + x.Id.ToString() + ":" + x.Revision.ToString(), ct)) return null;
        if (entry.SourceType == "DutyReport" && await center.TaskStateAsync(entry, ct) == "correction") return null;
        // Pending-task alerts expire when the original task resolves; informative decisions remain valid.
        var requiresPending = entry.SourceType is "Visit" or "DispatchReview"
            || entry.Audience == "Officer" && (entry.SourceKey.EndsWith(":Pending") || entry.SourceKey.EndsWith(":Requested"))
            || entry.Audience == "PermitTeacher" && entry.SourceKey.EndsWith(":Issued")
            || entry.Audience is "GateTeacher" or "Security" && entry.SourceKey.EndsWith(":Approved");
        if (requiresPending && await center.TaskStateAsync(entry, ct) != "pending") return null;
        try
        {
            var material = JsonSerializer.Deserialize<PushMaterial>(protector.Unprotect(target.Subscription.ProtectedMaterial));
            return material is null ? null : (entry, material);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException) { return null; }
    }
    public async Task CompleteAsync(PushLease lease, PushResult result, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var row = await db.Set<BrowserPushTarget>().SingleOrDefaultAsync(x => x.Id == lease.Id && x.State == "Pending" && x.LeaseId == lease.LeaseId && x.LeaseUntilUtc > now, ct);
        if (row is null) return;
        var state = result.State == "Retry" && lease.Attempt >= 8 ? "DeadLetter" : result.State;
        var retry = result.RetryAfter is { } delay && delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(Math.Min(900, Math.Pow(2, lease.Attempt)) + Random.Shared.NextDouble());
        row.State = state == "Retry" ? "Pending" : state;
        row.NextAttemptAtUtc = now + retry; row.LastHttpStatus = result.HttpStatus;
        if (row.State == "Pending" && row.NextAttemptAtUtc >= row.ExpiresAtUtc) row.State = "Expired";
        row.LeaseId = null; row.LeaseUntilUtc = null;
        if (state == "ProviderAccepted") row.ProviderAcceptedAtUtc = now;
        if (db.Database.IsRelational())
        {
            var updated = await db.Set<BrowserPushTarget>().Where(x => x.Id == lease.Id && x.State == "Pending" && x.LeaseId == lease.LeaseId
                && x.LeaseUntilUtc > now && x.BindingVersion == lease.BindingVersion)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, row.State).SetProperty(x => x.NextAttemptAtUtc, row.NextAttemptAtUtc)
                    .SetProperty(x => x.LastHttpStatus, result.HttpStatus).SetProperty(x => x.ProviderAcceptedAtUtc, row.ProviderAcceptedAtUtc)
                    .SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null), ct);
            db.Entry(row).State = EntityState.Detached;
            if (updated == 0) return;
        }
        if (state == "InvalidSubscription")
        {
            var device = await db.Set<BrowserPushSubscription>().SingleAsync(x => x.Id == lease.SubscriptionId, ct);
            if (device.BindingVersion == lease.BindingVersion) { device.RevokedAtUtc = now; device.BindingVersion++; }
            if (db.Database.IsRelational()) await db.Set<BrowserPushTarget>().Where(x => x.SubscriptionId == lease.SubscriptionId && x.BindingVersion == lease.BindingVersion && x.State == "Pending")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Cancelled").SetProperty(x => x.LeaseId, (Guid?)null), ct);
        }
        db.Add(new BrowserPushAttempt { TargetId = lease.Id, LeaseId = lease.LeaseId, CompletedAtUtc = now, State = state, HttpStatus = result.HttpStatus });
        await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
    }
    public async Task CancelDisabledAsync(CancellationToken ct)
    {
        if (db.Database.IsRelational()) await db.Set<BrowserPushTarget>().Where(x => x.State == "Pending").ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Cancelled"), ct);
    }
}
