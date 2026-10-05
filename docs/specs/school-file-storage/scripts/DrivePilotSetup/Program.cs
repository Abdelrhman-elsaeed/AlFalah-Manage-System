using System.Text.Json;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Explicit, local-only preparation. No API host, migration, seeding, network or key generation.
try
{
    string Required(string key)
    {
        var index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : throw new ArgumentException();
    }
    var root = Path.GetFullPath(Required("--repository"));
    var schoolId = int.Parse(Required("--school-id"));
    var file = Path.GetFullPath(Required("--client-file"));
    var email = Required("--email");
    var apply = args.Contains("--apply", StringComparer.Ordinal);
    if (!System.Net.Mail.MailAddress.TryCreate(email, out _) || new FileInfo(file).Length > 65536) throw new ArgumentException();
    using var json = JsonDocument.Parse(await File.ReadAllTextAsync(file), new JsonDocumentOptions { MaxDepth = 8 });
    var client = json.RootElement.GetProperty("web");
    var clientId = client.GetProperty("client_id").GetString()!;
    var secret = client.GetProperty("client_secret").GetString()!;
    const string redirect = "http://localhost:5264/api/v1/school-google-drive/callback";
    if (clientId.Length > 512 || !clientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal) ||
        string.IsNullOrWhiteSpace(secret) || secret.Length > 2048 ||
        !client.GetProperty("redirect_uris").EnumerateArray().Any(x => x.GetString() == redirect)) throw new ArgumentException();
    var apiRoot = Path.Combine(root, "backend/AlFalah.Api");
    var configuration = new ConfigurationBuilder().SetBasePath(apiRoot).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json").Build();
    var connection = configuration.GetConnectionString("DefaultConnection")!;
    if (!new SqlConnectionStringBuilder(connection).DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException();
    await using var db = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection).Options);
    var school = await db.Schools.SingleAsync(x => x.Id == schoolId && x.Name == "Al-Falah E2E Test School" && x.IsActive);
    if (school.ManagerUserId is null || !await db.UserSchoolRoles.AnyAsync(x => x.SchoolId == schoolId && x.UserId == school.ManagerUserId && x.IsActive && x.Role.Name == RoleNames.SchoolManager)) throw new ArgumentException();
    var existing = await db.SchoolGoogleDrives.SingleOrDefaultAsync(x => x.SchoolId == schoolId);
    if (existing is not null && (existing.IsEnabled || existing.ProtectedCredential != "" || existing.RootFolderId != ""))
        throw new InvalidOperationException(); // Do not replace an active account or retained roots.
    if (apply)
    {
        var keys = configuration["DataProtection:KeysPath"] ?? "App_Data/DataProtectionKeys";
        keys = Path.GetFullPath(Path.IsPathRooted(keys) ? keys : Path.Combine(apiRoot, keys));
        if (!Directory.Exists(keys)) throw new InvalidOperationException();
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName(configuration["DataProtection:ApplicationName"] ?? "AlFalah.ManageSystem")
            .PersistKeysToFileSystem(new DirectoryInfo(keys)).DisableAutomaticKeyGeneration();
        using var provider = services.BuildServiceProvider();
        var protector = new GoogleDriveCredentialProtector(provider.GetRequiredService<IDataProtectionProvider>());
        existing ??= new SchoolGoogleDrive { SchoolId = schoolId };
        if (existing.Id == 0) db.SchoolGoogleDrives.Add(existing);
        existing.CredentialType = GoogleDriveCredentialType.OAuthRefreshToken;
        existing.SchoolGoogleEmail = email;
        existing.OAuthClientId = clientId;
        existing.ProtectedOAuthClientSecret = protector.Protect(secret);
        existing.ProtectedCredential = ""; existing.IsEnabled = false;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new() { SchoolId = schoolId, UserId = school.ManagerUserId, Action = "SchoolGoogleDrive.PilotPrepared",
            EntityName = "SchoolGoogleDrive", NewValues = "{\"credentialType\":\"OAuthRefreshToken\",\"isEnabled\":false,\"consentPending\":true}" });
        await db.SaveChangesAsync();
    }
    Console.WriteLine(JsonSerializer.Serialize(new { schoolId, applied = apply, oauthClientValidated = true,
        credentialsEncrypted = apply, consentPending = true, rootSelectionPending = true, enabled = false,
        migrationsExecuted = false, googleRequests = 0, existingKeysChanged = false }));
    return 0;
}
catch
{
    Console.Error.WriteLine("Pilot preparation failed. Check the local test school, OAuth Web JSON, existing keys and inactive connection. No secrets are logged.");
    return 1;
}
