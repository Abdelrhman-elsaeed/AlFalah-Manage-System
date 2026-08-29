using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlFalah.Infrastructure;

public static class DataProtectionServiceCollectionExtensions
{
    public const string DefaultApplicationName = "AlFalah.ManageSystem";
    public static readonly string DefaultRelativeKeysPath = Path.Combine("App_Data", "DataProtectionKeys");

    public static IServiceCollection AddAlFalahDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var configuredPath = configuration["DataProtection:KeysPath"];
        var keysPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(contentRootPath, DefaultRelativeKeysPath)
            : Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(contentRootPath, configuredPath);

        keysPath = Path.GetFullPath(keysPath);
        Directory.CreateDirectory(keysPath);

        var applicationName = configuration["DataProtection:ApplicationName"];
        if (string.IsNullOrWhiteSpace(applicationName)) applicationName = DefaultApplicationName;

        var dataProtection = services.AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        var keyEncryption = configuration["DataProtection:KeyEncryption"]?.Trim();
        switch (keyEncryption?.ToUpperInvariant())
        {
            case null:
            case "":
            case "NONE":
                // Shared IIS hosts frequently run without a loadable user profile, so
                // current-user DPAPI cannot be assumed. The key directory is outside
                // wwwroot and access is restricted by the hosting account's filesystem ACL.
                break;
            case "DPAPICURRENTUSER":
                if (!OperatingSystem.IsWindows())
                    throw UnsupportedPlatform(keyEncryption);
                dataProtection.ProtectKeysWithDpapi();
                break;
            case "DPAPILOCALMACHINE":
                if (!OperatingSystem.IsWindows())
                    throw UnsupportedPlatform(keyEncryption);
                dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported DataProtection:KeyEncryption value '{keyEncryption}'. " +
                    "Use None, DpapiCurrentUser, or DpapiLocalMachine.");
        }

        return services;
    }

    private static InvalidOperationException UnsupportedPlatform(string keyEncryption) =>
        new($"DataProtection key encryption '{keyEncryption}' is only supported on Windows.");
}
