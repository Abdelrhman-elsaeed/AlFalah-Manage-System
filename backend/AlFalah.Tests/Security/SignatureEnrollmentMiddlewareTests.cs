using System.Security.Claims;
using AlFalah.Api.Middlewares;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AlFalah.Tests.Security;

public sealed class SignatureEnrollmentMiddlewareTests
{
    [Theory]
    [InlineData("/api/v1/users", false, "Instructor", true)]
    [InlineData("/api/v1/dashboard/moderator", false, "Moderator", true)]
    [InlineData("/api/v1/account/teaching", false, "Secretary", true)]
    [InlineData("/api/v1/users", true, "Instructor", false)]
    [InlineData("/api/v1/users", false, "SchoolManager", false)]
    [InlineData("/api/v1/auth/me", false, "Instructor", false)]
    [InlineData("/api/v1/auth/change-password", false, "Instructor", false)]
    [InlineData("/api/v1/account/signature", false, "Instructor", false)]
    [InlineData("/api/v1/account/signature/requirement", false, "Instructor", false)]
    public async Task Enrollment_blocks_direct_api_access_until_signature_is_saved(
        string path, bool signed, string role, bool blocked)
    {
        var reachedEndpoint = false;
        var middleware = new SignatureEnrollmentMiddleware(_ => { reachedEndpoint = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"))
        };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context, new Access(signed));
        Assert.Equal(!blocked, reachedEndpoint);
        Assert.Equal(blocked ? 403 : 200, context.Response.StatusCode);
        Assert.Equal(blocked ? "true" : "", context.Response.Headers["X-Signature-Required"].ToString());
    }

    private sealed class Access(bool signed) : IAccountAccessService
    {
        public Task<SignatureRequirementDto> GetSignatureRequirementAsync(CancellationToken ct = default) =>
            Task.FromResult(new SignatureRequirementDto(!signed, signed));
        public Task<PasswordRevealDto> RevealPasswordAsync(string userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
