using AlFalah.Api.Controllers;
using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Controllers;

public sealed class LegacyVisitWriteCutoverTests
{
    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("submit")]
    [InlineData("delete")]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("reopen")]
    public async Task Every_legacy_write_endpoint_returns_410(string action)
    {
        var controller = new VisitsController(
            visitService: null!,
            currentUser: new CurrentUser(),
            pdfReportService: null!,
            bulkExportService: null!,
            logger: NullLogger<VisitsController>.Instance);

        var result = action switch
        {
            "create" => await controller.Create(new CreateVisitRequestDto(), default),
            "update" => await controller.Update(17, new UpdateVisitRequestDto(), default),
            "submit" => await controller.Submit(17, default),
            "delete" => await controller.SoftDelete(17, default),
            "approve" => await controller.Approve(17, default),
            "reject" => await controller.Reject(17, new RejectVisitRequestDto { Reason = "reason" }, default),
            "reopen" => await controller.Reopen(17, new ReopenVisitRequestDto { Reason = "reason" }, default),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(410);
    }

    private sealed class CurrentUser : ICurrentUserService
    {
        public string? UserId => "manager";
        public string? Username => "manager";
        public int? ActiveSchoolId => 7;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => false;
        public bool HasPermission(string permissionName) => true;
        public IEnumerable<string> GetRoles() => Array.Empty<string>();
        public IEnumerable<string> GetPermissions() => Array.Empty<string>();
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }
}
