using AlFalah.Application.Notifications;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AlFalah.Infrastructure.Notifications;

public sealed class WebPushDeliveryClient(HttpClient http, IOptions<WebPushOptions> options, IPushEndpointPolicy endpoints, TimeProvider time) : IWebPushClient
{
    public async Task<PushResult> SendAsync(PushMaterial material, Guid entryId, DateTimeOffset expires, CancellationToken ct)
    {
        if (!endpoints.IsValid(material)) return new("InvalidSubscription");
        if (expires <= time.GetUtcNow()) return new("Expired");
        var config = options.Value;
        if (string.IsNullOrEmpty(config.PrivateKey) || string.IsNullOrEmpty(config.Subject)) return new("ConfigurationError");
        var subscription = new PushSubscription { Endpoint = material.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, material.Keys.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, material.Keys.Auth);
        var url = $"/notification-entry?target={entryId:D}";
        var payload = JsonSerializer.Serialize(new { notification = new {
            title = "مدارس الفلاح", body = "لديك تحديث جديد من المدرسة. افتح التطبيق للاطلاع عليه.",
            tag = entryId.ToString("N"), dir = "rtl", lang = "ar", icon = "/assets/pwa/icon-192.png",
            data = new { contractVersion = 1, targetKey = entryId, onActionClick = new { @default = new { operation = "navigateLastFocusedOrOpen", url } } }
        } });
        try
        {
            using var authentication = new VapidAuthentication(config.PublicKey, config.PrivateKey) { Subject = config.Subject };
            var client = new PushServiceClient(http) { AutoRetryAfter = false };
            await client.RequestPushMessageDeliveryAsync(subscription, new PushMessage(payload) {
                TimeToLive = (int)Math.Clamp((expires - time.GetUtcNow()).TotalSeconds, 0, 86400), Urgency = PushMessageUrgency.Normal
            }, authentication, ct);
            return new("ProviderAccepted");
        }
        catch (PushServiceClientException ex)
        {
            var status = (int)ex.StatusCode;
            var retry = ex.Headers?.RetryAfter?.Delta ?? (ex.Headers?.RetryAfter?.Date is { } date ? date - time.GetUtcNow() : null);
            return Classify(status, retry);
        }
        catch (System.Security.Cryptography.CryptographicException) { return new("ConfigurationError"); }
        catch (ArgumentException) { return new("ConfigurationError"); }
    }
    internal static PushResult Classify(int status, TimeSpan? retry = null) => status switch {
        404 or 410 => new("InvalidSubscription", status),
        401 or 403 => new("ConfigurationError", status),
        429 or >= 500 => new("Retry", status, retry),
        _ => new("InvalidPayload", status)
    };
}
