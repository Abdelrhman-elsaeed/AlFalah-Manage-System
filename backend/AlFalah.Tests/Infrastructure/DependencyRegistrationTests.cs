using AlFalah.Application.Interfaces;
using AlFalah.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using AlFalah.Application.Storage;
using Xunit;

namespace AlFalah.Tests.Infrastructure;

public class DependencyRegistrationTests
{
    [Fact]
    public void ControllerFacingServices_Resolve()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=AlFalahDb;Trusted_Connection=True"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITeacherService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IComplaintService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IDashboardService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IStorageDelegationService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IStorageAuthorizationService>().Should().NotBeNull();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "AlFalah.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
