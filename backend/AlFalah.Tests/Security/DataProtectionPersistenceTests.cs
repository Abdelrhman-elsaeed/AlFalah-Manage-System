using AlFalah.Infrastructure;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AlFalah.Tests.Security;

public sealed class DataProtectionPersistenceTests
{
    [Fact]
    public void GoogleDriveCredential_Survives_A_Fresh_ServiceProvider_When_The_Key_Path_Is_Stable()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"alfalah-data-protection-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = "keys",
                ["DataProtection:ApplicationName"] = "AlFalah.Tests"
            })
            .Build();

        try
        {
            var ciphertext = ProtectWithFreshProvider(configuration, contentRoot, "drive-refresh-token");
            var plaintext = UnprotectWithFreshProvider(configuration, contentRoot, ciphertext);

            plaintext.Should().Be("drive-refresh-token");
            Directory.GetFiles(Path.Combine(contentRoot, "keys"), "*.xml").Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(contentRoot)) Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void KeyEncryption_None_Does_Not_Write_A_Dpapi_Encrypted_Key()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"alfalah-data-protection-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = "keys",
                ["DataProtection:ApplicationName"] = "AlFalah.Tests",
                ["DataProtection:KeyEncryption"] = "None"
            })
            .Build();

        try
        {
            _ = ProtectWithFreshProvider(configuration, contentRoot, "drive-refresh-token");

            var keyFile = Directory.GetFiles(Path.Combine(contentRoot, "keys"), "*.xml").Single();
            File.ReadAllText(keyFile).Should().NotContain("DpapiXmlDecryptor");
        }
        finally
        {
            if (Directory.Exists(contentRoot)) Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static string ProtectWithFreshProvider(IConfiguration configuration, string contentRoot, string plaintext)
    {
        using var provider = BuildProvider(configuration, contentRoot);
        return new GoogleDriveCredentialProtector(provider.GetRequiredService<IDataProtectionProvider>())
            .Protect(plaintext);
    }

    private static string UnprotectWithFreshProvider(IConfiguration configuration, string contentRoot, string ciphertext)
    {
        using var provider = BuildProvider(configuration, contentRoot);
        return new GoogleDriveCredentialProtector(provider.GetRequiredService<IDataProtectionProvider>())
            .Unprotect(ciphertext);
    }

    private static ServiceProvider BuildProvider(IConfiguration configuration, string contentRoot)
    {
        var services = new ServiceCollection();
        services.AddAlFalahDataProtection(configuration, contentRoot);
        return services.BuildServiceProvider();
    }
}
