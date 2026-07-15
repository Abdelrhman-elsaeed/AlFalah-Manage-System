using System.Security.Claims;
using System.Text;
using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Infrastructure;
using Xunit;

namespace AlFalah.Tests.Reports;

public sealed class DashboardExportTests
{
    [Fact]
    public async Task MainManager_Exports_Contain_Production_Dashboard_Surfaces()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        await using var context = CreateContext();
        var currentUser = MainManager();
        var service = new DashboardService(
            context,
            currentUser,
            new SchoolScopeGuard(context, currentUser, NullLogger<SchoolScopeGuard>.Instance),
            NullLogger<DashboardService>.Instance);

        var excel = await service.ExportExcelAsync(DashboardRole.MainManager, new DashboardFilterDto());
        var pdf = await service.ExportPdfAsync(DashboardRole.MainManager, new DashboardFilterDto());

        excel.Bytes.Length.Should().BeGreaterThan(5_000);
        using var workbook = new XLWorkbook(new MemoryStream(excel.Bytes));
        workbook.Worksheet(1).Name.Should().Be("لوحة المؤشرات");
        workbook.Worksheet(1).Cell(1, 1).GetString().Should().Contain("لوحة");

        pdf.Bytes.Length.Should().BeGreaterThan(5_000);
        Encoding.ASCII.GetString(pdf.Bytes, 0, 4).Should().Be("%PDF");
    }

    private static AlFalahDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"dashboard-export-{Guid.NewGuid()}")
            .Options;
        return new AlFalahDbContext(options);
    }

    private static ICurrentUserService MainManager()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "MAIN-MANAGER-1"),
            new Claim(ClaimTypes.Role, RoleNames.MainManager),
            new Claim("permission", PermissionNames.DashboardMainManager)
        }, "integration-test"));
        return new CurrentUserService(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        });
    }
}
