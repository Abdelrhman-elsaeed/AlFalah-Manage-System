using System.Globalization;
using AlFalah.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class LocalFileStorageServiceTests
{
    [Fact]
    public async Task StoreAsync_UsesInvariantGregorianFolderUnderArabicCulture()
    {
        var root = Path.Combine(Path.GetTempPath(), $"alfalah-excuses-{Guid.NewGuid():N}");
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var storage = CreateStorage(root);
            await using var content = new MemoryStream("%PDF-test"u8.ToArray());

            var stored = await storage.StoreAsync(18, content, "report.pdf", "application/pdf", CancellationToken.None);

            Assert.StartsWith($"18/{DateTime.UtcNow.ToString("yyyy/MM", CultureInfo.InvariantCulture)}/", stored.StorageKey);
            Assert.Equal("%PDF-test"u8.ToArray(), await storage.ReadBytesAsync(stored.StorageKey, CancellationToken.None));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReadBytesAsync_RecoversLegacyDirectionMarkLostByVarcharColumn()
    {
        var root = Path.Combine(Path.GetTempPath(), $"alfalah-excuses-{Guid.NewGuid():N}");
        var legacyFolder = Path.Combine(root, "18", "1448\u200f", "04");
        try
        {
            Directory.CreateDirectory(legacyFolder);
            await File.WriteAllBytesAsync(Path.Combine(legacyFolder, "report.pdf"), "%PDF-legacy"u8.ToArray());

            var bytes = await CreateStorage(root).ReadBytesAsync("18/1448?/04/report.pdf", CancellationToken.None);

            Assert.Equal("%PDF-legacy"u8.ToArray(), bytes);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StoreAsync_DefaultRootStaysWithApplicationContentAcrossBuildConfigurations()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"alfalah-content-{Guid.NewGuid():N}");
        try
        {
            var storage = new LocalFileStorageService(new ConfigurationBuilder().Build(), new TestEnvironment(contentRoot));
            await using var content = new MemoryStream("%PDF-test"u8.ToArray());

            var stored = await storage.StoreAsync(18, content, "report.pdf", "application/pdf", CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(contentRoot, "App_Data", "absence-excuses",
                stored.StorageKey.Replace('/', Path.DirectorySeparatorChar))));
        }
        finally
        {
            if (Directory.Exists(contentRoot)) Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static LocalFileStorageService CreateStorage(string root) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["StudentAffairs:ExcuseStoragePath"] = root
        }).Build(), new TestEnvironment(root));

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "AlFalah.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
