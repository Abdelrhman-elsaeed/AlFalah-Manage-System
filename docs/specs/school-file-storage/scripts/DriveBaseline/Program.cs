using System.Text.Json;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Development-only observer: no application host, seeding, migrations or SaveChanges.
var root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
var output = Path.Combine(root, "docs/specs/school-file-storage/baseline/drive-baseline.json");
var apiRoot = Path.Combine(root, "backend/AlFalah.Api");
var configuration = new ConfigurationBuilder().SetBasePath(apiRoot)
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json").Build();
var connection = configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Development connection required.");
var parsed = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);
if (!parsed.DataSource.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("This observer requires LocalDB.");
var keyPath = configuration["DataProtection:KeysPath"] ?? "App_Data/DataProtectionKeys";
keyPath = Path.GetFullPath(Path.IsPathRooted(keyPath) ? keyPath : Path.Combine(apiRoot, keyPath));
if (!Directory.Exists(keyPath)) throw new InvalidOperationException("Existing key directory required.");
var services = new ServiceCollection();
services.AddLogging(builder => builder.ClearProviders());
services.AddDataProtection().SetApplicationName(configuration["DataProtection:ApplicationName"] ?? "AlFalah.ManageSystem")
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath)).DisableAutomaticKeyGeneration();
using var provider = services.BuildServiceProvider();
using var database = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection).Options);
using var cache = new MemoryCache(new MemoryCacheOptions());
using var factory = new ReadOnlyFactory();
var tokens = new GoogleDriveTokenService(database, new GoogleDriveCredentialProtector(provider.GetRequiredService<IDataProtectionProvider>()),
    factory, cache, configuration, NullLogger<GoogleDriveTokenService>.Instance);
var drive = new GoogleDriveClient(tokens, factory, configuration, NullLogger<GoogleDriveClient>.Instance);
var results = new List<object>();
foreach (var school in await database.SchoolGoogleDrives.AsNoTracking().Where(x => x.IsEnabled).ToListAsync())
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
    var files = new HashSet<string>();
    var folders = new HashSet<string>();
    var parentOf = new Dictionary<string, string>();
    var status = "Complete";
    string? error = null;
    string stage = "AcquireAccessToken";
    string? failureReason = null;
    try
    {
        await tokens.GetAccessTokenAsync(school.SchoolId, timeout.Token);
        stage = "ReadRoot";
        var rootItem = await drive.GetFileAsync(school.SchoolId, school.RootFolderId, timeout.Token);
        if (rootItem is null || rootItem.Trashed || !rootItem.IsFolder) throw new InvalidOperationException("RootUnavailable");
        var queue = new Queue<string>();
        queue.Enqueue(school.RootFolderId);
        folders.Add(school.RootFolderId);
        stage = "EnumerateFolders";
        while (queue.TryDequeue(out var parent))
        {
            string? pageToken = null;
            do
            {
                var page = await drive.ListChildrenAsync(school.SchoolId,
                    new GoogleDriveListRequest(parent, null, "name", 1000, pageToken, school.SharedDriveId), timeout.Token);
                foreach (var item in page.Files)
                {
                    parentOf.TryAdd(item.Id, parent);
                    if (item.IsFolder) { if (folders.Add(item.Id)) queue.Enqueue(item.Id); }
                    else files.Add(item.Id);
                }
                pageToken = page.NextPageToken;
            } while (pageToken is not null);
        }
    }
    catch (Exception ex)
    {
        status = files.Count + folders.Count > 0 ? "Partial" : "Unavailable";
        error = ex.GetType().Name; // Never include exception messages, IDs, credential bodies or personal data.
        failureReason = ex.InnerException is System.Security.Cryptography.CryptographicException
            ? "StoredCredentialCannotBeDecryptedWithCurrentKeys"
            : "ObservationFailed";
    }
    var recorded = await database.TeacherEvidenceSubmissions.AsNoTracking()
        .Where(x => x.SchoolId == school.SchoolId && !x.IsDeleted).Select(x => x.DriveItemId).ToListAsync();
    var grants = await database.TeacherDriveFolders.AsNoTracking().Where(x => x.SchoolId == school.SchoolId && x.IsActive).ToListAsync();
    bool InGrant(string item, string grantedRoot)
    {
        var seen = new HashSet<string>();
        while (seen.Add(item))
        {
            if (item == grantedRoot) return true;
            if (!parentOf.TryGetValue(item, out var parent)) return false;
            item = parent;
        }
        return false;
    }
    results.Add(new {
        SchoolKey = school.SchoolId, Status = status, ErrorType = error,
        FailedStage = status == "Complete" ? null : stage, FailureReason = failureReason,
        Files = status == "Complete" ? (int?)files.Count : null,
        FoldersIncludingRoot = status == "Complete" ? (int?)folders.Count : null,
        UnindexedFiles = status == "Complete" ? (int?)files.Except(recorded).Count() : null,
        RecordedFilesObserved = status == "Complete" ? (int?)recorded.Distinct().Count(files.Contains) : null,
        TeacherGroups = grants.Select(x => new {
            TeacherKey = x.TeacherId,
            Files = status == "Complete" ? (int?)files.Count(id => InGrant(id, x.RootItemId)) : null,
            UnindexedFiles = status == "Complete" ? (int?)files.Except(recorded).Count(id => InGrant(id, x.RootItemId)) : null
        }).ToArray()
    });
}
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new {
    CapturedAtUtc = DateTimeOffset.UtcNow,
    Environment = "Development connection; actual Drive HTTP read attempt",
    Method = "Existing token/client adapters with HTTP method gate; no SQL or Drive mutations, no content downloads",
    MaxHttpRequests = 300, HttpRequests = factory.RequestCount, Schools = results,
    Privacy = "No names, raw Drive identifiers, URLs, tokens, response bodies or exception messages exported"
}, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine($"Drive observation written; schools={results.Count}; HTTP requests={factory.RequestCount}.");

sealed class ReadOnlyFactory : IHttpClientFactory, IDisposable
{
    private readonly GateHandler handler = new() { InnerHandler = new HttpClientHandler { AllowAutoRedirect = false } };
    public int RequestCount => handler.RequestCount;
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };
    public void Dispose() => handler.Dispose();
}

sealed class GateHandler : DelegatingHandler
{
    public int RequestCount { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var allowed = uri.Scheme == "https" && (
            (request.Method == HttpMethod.Get && uri.Host == "www.googleapis.com" && uri.AbsolutePath.StartsWith("/drive/v3/", StringComparison.Ordinal)) ||
            (request.Method == HttpMethod.Post && uri.Host == "oauth2.googleapis.com" && uri.AbsolutePath == "/token"));
        if (!allowed || ++RequestCount > 300) throw new InvalidOperationException("ReadOnlyRequestGate");
        return base.SendAsync(request, cancellationToken);
    }
}
