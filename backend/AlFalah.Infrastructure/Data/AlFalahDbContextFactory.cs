using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlFalah.Infrastructure.Data;

/// <summary>Allows EF tooling to create migrations without starting the API host.</summary>
public sealed class AlFalahDbContextFactory : IDesignTimeDbContextFactory<AlFalahDbContext>
{
    public AlFalahDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ALFALAH_MIGRATIONS_CONNECTION")
            ?? "Server=(localdb)\\mssqllocaldb;Database=AlFalahMigrations;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new AlFalahDbContext(options);
    }
}
