using System.Security.Cryptography;
using System.Text.Json;
using AlFalah.Application.Storage;
using Microsoft.AspNetCore.DataProtection;

namespace AlFalah.Infrastructure.Services;

public sealed class StorageDriveHandles(IDataProtectionProvider provider) : IStorageDriveHandles
{
    private readonly IDataProtector handles = provider.CreateProtector("AlFalah.Storage.Browse.v1");
    private readonly IDataProtector plans = provider.CreateProtector("AlFalah.Storage.Mutation.v1");
    public string ProtectHandle(DriveBrowserHandle value) => handles.Protect(JsonSerializer.Serialize(value));
    public string ProtectPlan(DriveMutationPlan value) => plans.Protect(JsonSerializer.Serialize(value));
    public DriveBrowserHandle ReadHandle(string value) => Read<DriveBrowserHandle>(handles,value);
    public DriveMutationPlan ReadPlan(string value) => Read<DriveMutationPlan>(plans,value);
    private static T Read<T>(IDataProtector protector,string value)
    {
        if(string.IsNullOrWhiteSpace(value)||value.Length>100000)throw new ArgumentException("رابط الملف غير صالح؛ حدّث الصفحة.");
        try { return JsonSerializer.Deserialize<T>(protector.Unprotect(value))??throw new CryptographicException(); }
        catch(Exception e) when(e is CryptographicException or JsonException or FormatException)
        { throw new ArgumentException("انتهى الرابط أو تغيّر؛ حدّث الصفحة."); }
    }
}
