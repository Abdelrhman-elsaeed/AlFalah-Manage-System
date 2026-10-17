using System.Security.Cryptography;
using System.Text.Json;
using AlFalah.Application.Notifications;
using Microsoft.AspNetCore.DataProtection;

namespace AlFalah.Infrastructure.Notifications;

public sealed class InboxCursorCodec(IDataProtectionProvider provider) : IInboxCursorCodec
{
    private readonly IDataProtector protector = provider.CreateProtector("AlFalah.Inbox.Cursor.v1");
    public string Encode(InboxCursor cursor) => protector.Protect(JsonSerializer.Serialize(cursor));
    public InboxCursor? Decode(string cursor)
    {
        try { return JsonSerializer.Deserialize<InboxCursor>(protector.Unprotect(cursor)); }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { return null; }
    }
}
