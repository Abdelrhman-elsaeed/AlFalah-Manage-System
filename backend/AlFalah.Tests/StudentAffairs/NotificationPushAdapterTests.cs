using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using AlFalah.Application.Notifications;
using AlFalah.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

/// <summary>Real encryption/VAPID library, intercepted HTTP only. No provider or device evidence.</summary>
public sealed class NotificationPushAdapterTests
{
    private static string Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    [Theory]
    [InlineData(201, "ProviderAccepted")]
    [InlineData(404, "InvalidSubscription")]
    [InlineData(410, "InvalidSubscription")]
    [InlineData(401, "ConfigurationError")]
    [InlineData(403, "ConfigurationError")]
    [InlineData(429, "Retry")]
    [InlineData(503, "Retry")]
    [InlineData(400, "InvalidPayload")]
    public async Task AdapterEncryptsGenericPayloadSignsVapidAndClassifiesInterceptedResponses(int status, string state)
    {
        using var signing = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keys = signing.ExportParameters(true);
        var options = Options.Create(new WebPushOptions { PublicKey = Url([4, ..keys.Q.X!, ..keys.Q.Y!]),
            PrivateKey = Url(keys.D!), Subject = "mailto:synthetic@example.invalid", AllowedProviderHosts = ["fcm.googleapis.com"] });
        using var receiver = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var receiverKey = receiver.ExportParameters(false);
        var material = new PushMaterial("https://fcm.googleapis.com/synthetic-test", new(Url([4, ..receiverKey.Q.X!, ..receiverKey.Q.Y!]), Url(RandomNumberGenerator.GetBytes(16))));
        using var handler = new Capture(status);
        using var http = new HttpClient(handler);
        var client = new WebPushDeliveryClient(http, options, new PushEndpointPolicy(options), TimeProvider.System);
        var id = Guid.NewGuid();
        var result = await client.SendAsync(material, id, DateTimeOffset.UtcNow.AddSeconds(45), default);
        result.State.Should().Be(state);
        handler.Count.Should().Be(1, "retry belongs to our durable target, not a hidden library loop");
        handler.Authorization.Should().StartWith("vapid ");
        handler.Ttl.Should().BeInRange(1, 45);
        handler.Encoding.Should().Contain("aes128gcm");
        handler.Payload.Should().NotBeEmpty();
        Encoding.UTF8.GetString(handler.Payload).Should().NotContain(id.ToString()).And.NotContain("مدارس الفلاح");
        if (status == 429) result.RetryAfter.Should().Be(TimeSpan.FromSeconds(70));
    }
    private sealed class Capture(int status) : HttpMessageHandler
    {
        public int Count; public string Authorization = ""; public int Ttl; public string[] Encoding = []; public byte[] Payload = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Count++; Authorization = request.Headers.Authorization?.ToString() ?? "";
            Ttl = int.Parse(request.Headers.GetValues("TTL").Single());
            Encoding = request.Content!.Headers.ContentEncoding.ToArray(); Payload = await request.Content.ReadAsByteArrayAsync(ct);
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("") };
            if (status == 429) response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(70));
            return response;
        }
    }
}
