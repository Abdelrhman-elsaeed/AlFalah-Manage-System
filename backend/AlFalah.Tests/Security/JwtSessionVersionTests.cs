using System.IdentityModel.Tokens.Jwt;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AlFalah.Tests.Security;

public sealed class JwtSessionVersionTests
{
    [Fact]
    public void Access_token_carries_the_current_identity_security_stamp()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "w9-test-signing-key-that-is-at-least-thirty-two-bytes",
            ["Jwt:Issuer"] = "AlFalah.Tests",
            ["Jwt:Audience"] = "AlFalah.Tests",
            ["Jwt:AccessTokenExpiryMinutes"] = "5"
        }).Build();
        var token = new JwtService(configuration).GenerateAccessToken(
            "user-1", "manager", ["SchoolManager"], ["GatePass.ViewAudit"], 42, "ar", "stamp-v2");

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        parsed.Claims.Single(claim => claim.Type == "security_stamp").Value.Should().Be("stamp-v2");
    }
}
