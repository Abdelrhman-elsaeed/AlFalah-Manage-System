using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Notifications;

public sealed class PushService(IPushRepository repository, ICenterRepository center, ICurrentUserService user,
    IPushEndpointPolicy endpoints, IOptions<WebPushOptions> options)
{
    private InboxScope? Scope => user.ActiveSchoolId is { } school && user.UserId is { Length: > 0 } id ? new(school, id) : null;
    public PushConfig Config() => new(options.Value.RegistrationEnabled && options.Value.PublicKey.Length > 0,
        options.Value.PwaUiEnabled, options.Value.PublicKey, options.Value.VapidKeyId);
    public async Task<ApiResponse<PushDevice>> RegisterAsync(RegisterPush request, CancellationToken ct)
    {
        if (!options.Value.RegistrationEnabled || Scope is not { } scope || !await center.HasAccessAsync(scope, ct)
            || request.InstallationId == Guid.Empty || request.VapidKeyId != options.Value.VapidKeyId || !endpoints.IsValid(request.Subscription))
            return ApiResponse<PushDevice>.Fail("تعذر تفعيل إشعارات الجهاز.");
        var device = await repository.RegisterAsync(scope, request, ct);
        return device is null ? ApiResponse<PushDevice>.Fail("أوقف اشتراك الحساب السابق على هذا المتصفح أولاً.") : ApiResponse<PushDevice>.Success(device);
    }
    public async Task<ApiResponse<IReadOnlyList<PushDevice>>> DevicesAsync(CancellationToken ct) => Scope is { } scope && await center.HasAccessAsync(scope, ct)
        ? ApiResponse<IReadOnlyList<PushDevice>>.Success(await repository.DevicesAsync(scope, ct)) : ApiResponse<IReadOnlyList<PushDevice>>.Success([]);
    public async Task<ApiResponse<bool>> RevokeAsync(Guid id, CancellationToken ct)
    {
        if (Scope is { } scope) await repository.RevokeAsync(scope, id, ct);
        return ApiResponse<bool>.Success(true);
    }
}

public sealed class PushDeliveryService(IPushRepository repository, IWebPushClient client, IOptions<WebPushOptions> options)
{
    public async Task SweepAsync(CancellationToken ct)
    {
        if (!options.Value.DispatchEnabled) { await repository.CancelDisabledAsync(ct); return; }
        // Claim one at a time: no claimed job waits behind another HTTP request beyond its lease.
        foreach (var lease in await repository.ClaimAsync(ct))
        {
            PushResult result;
            var eligible = await repository.EligibleAsync(lease, ct);
            if (eligible is null) result = new("Cancelled");
            else
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                try { result = await client.SendAsync(eligible.Value.Material, lease.EntryId, lease.ExpiresAtUtc, timeout.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { result = new("Retry"); }
                catch (HttpRequestException) { result = new("Retry"); }
            }
            await repository.CompleteAsync(lease, result, ct);
        }
    }
}
