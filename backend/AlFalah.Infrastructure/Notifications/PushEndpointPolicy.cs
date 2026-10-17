using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using AlFalah.Application.Notifications;
using Microsoft.Extensions.Options;

namespace AlFalah.Infrastructure.Notifications;

public sealed class PushEndpointPolicy(IOptions<WebPushOptions> options) : IPushEndpointPolicy
{
    public bool IsValid(PushMaterial material)
    {
        if (material?.Keys is null || !TryUri(material.Endpoint, out _)) return false;
        try
        {
            var publicKey = Decode(material.Keys.P256dh); var auth = Decode(material.Keys.Auth);
            if (publicKey.Length != 65 || publicKey[0] != 4 || auth.Length != 16) return false;
            using var ec = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..65] } });
            return true;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException) { return false; }
    }

    internal bool TryUri(string endpoint, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrEmpty(endpoint) || endpoint.Length > 2048 || !Uri.TryCreate(endpoint, UriKind.Absolute, out var parsed)
            || parsed.Scheme != "https" || parsed.Port != 443 || parsed.UserInfo.Length > 0 || parsed.Fragment.Length > 0
            || Uri.CheckHostName(parsed.Host) != UriHostNameType.Dns || parsed.Host.EndsWith('.')
            || !options.Value.AllowedProviderHosts.Contains(parsed.IdnHost, StringComparer.OrdinalIgnoreCase)) return false;
        uri = parsed; return true;
    }
    internal static byte[] Decode(string value)
    {
        if (value.Length > 100 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) throw new FormatException();
        var raw = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(raw.PadRight((raw.Length + 3) / 4 * 4, '='));
    }
    internal static bool PublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) return PublicAddress(address.MapToIPv4());
        if (IPAddress.IsLoopback(address)) return false;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
            return bytes[0] is not (0 or 10 or 127) && bytes[0] < 224 && !(bytes[0] == 169 && bytes[1] == 254)
                && !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) && !(bytes[0] == 192 && bytes[1] == 168)
                && !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) && !(bytes[0] == 198 && bytes[1] is 18 or 19);
        return address.AddressFamily == AddressFamily.InterNetworkV6 && (bytes[0] & 0xe0) == 0x20;
    }
    public static SocketsHttpHandler Handler() => new() {
        AllowAutoRedirect = false, UseProxy = false, PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = async (context, ct) => {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            if (addresses.Length == 0 || addresses.Any(x => !PublicAddress(x))) throw new HttpRequestException("Push provider address rejected");
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            try { await socket.ConnectAsync(new IPEndPoint(addresses[0], context.DnsEndPoint.Port), ct); return new NetworkStream(socket, true); }
            catch { socket.Dispose(); throw; }
        }
    };
}
