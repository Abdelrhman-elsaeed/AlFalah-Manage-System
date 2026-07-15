using System.Security.Claims;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Schools;

public sealed class SchoolLocationServiceTests
{
    [Fact]
    public async Task MainManager_Can_Create_Location_That_Is_Returned_By_Lookup()
    {
        await using var context = CreateContext();
        var repository = new SchoolLocationRepository(context);
        var service = new SchoolLocationService(repository, MainManager());

        var created = await service.CreateAsync(new()
        {
            NameAr = "الخرج",
            NameEn = "Al Kharj",
            RegionNameAr = "منطقة الرياض",
            RegionNameEn = "Riyadh Region",
            Latitude = 24.1556m,
            Longitude = 47.3120m
        });

        created.Id.Should().BeGreaterThan(0);
        var locations = await service.GetActiveAsync();
        locations.Should().ContainSingle(x => x.NameAr == "الخرج" && x.Latitude == 24.1556m);
    }

    private static AlFalahDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"school-locations-{Guid.NewGuid()}")
            .Options;
        return new AlFalahDbContext(options);
    }

    private static ICurrentUserService MainManager()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "MAIN-MANAGER-LOCATION-TEST"),
            new Claim(ClaimTypes.Role, RoleNames.MainManager)
        }, "integration-test"));
        return new CurrentUserService(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        });
    }
}
