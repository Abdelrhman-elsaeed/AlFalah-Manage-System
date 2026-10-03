using System.Text.Json;
using AlFalah.Application.Storage;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// This offline adapter never starts the API, migrates, seeds, decrypts credentials or registers Drive.
try
{
    var apply = args.Contains("--apply", StringComparer.Ordinal);
    var dryRun = args.Contains("--dry-run", StringComparer.Ordinal);
    if (apply == dryRun) throw new ArgumentException("Specify exactly one of --dry-run or --apply.");
    string Required(string key)
    {
        var index = Array.IndexOf(args, key);
        if (index < 0 || index + 1 >= args.Length) throw new ArgumentException($"Missing {key}.");
        return args[index + 1];
    }
    var root = Path.GetFullPath(Required("--repository"));
    var database = Required("--database");
    var reportPath = Path.GetFullPath(Required("--report"));
    using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "backend/AlFalah.Api/appsettings.Development.json")));
    var connection = new SqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
    if (!connection.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(connection.DataSource, "localhost", StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("This adapter accepts the configured local Development server only.");
    connection.InitialCatalog = database;
    connection.ApplicationName = "SFS-S1-OfflineBackfill";
    await using var db = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(connection.ConnectionString).Options);
    var report = await new StorageBackfillService(new StorageBackfillRepository(db)).RunAsync(dryRun);
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    Console.WriteLine($"Source={report.SourceCount}; existing={report.ExistingCount}; planned/created files={report.CreatedFiles}; links={report.CreatedLinks}; decisions={report.CreatedDecisions}; issues={report.Issues.Count}; dryRun={report.DryRun}");
    return report.Issues.Count == 0 ? 0 : 2;
}
catch
{
    // SQL exception text may contain connection/provider details; the report is the diagnostic contract.
    Console.Error.WriteLine("Backfill failed. Check arguments, local connectivity, migration state and constraints. No credentials are logged.");
    return 1;
}
