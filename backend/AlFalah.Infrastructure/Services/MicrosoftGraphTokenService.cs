using System.Security.Claims;
using AlFalah.Application.Interfaces;
using Microsoft.Identity.Web;
using Microsoft.Extensions.Configuration;

namespace AlFalah.Infrastructure.Services;

/// <summary>Acquires delegated Graph tokens through the official OBO cache; tokens never touch the database.</summary>
public sealed class MicrosoftGraphTokenService : IMicrosoftGraphTokenService
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly IConfiguration _configuration;

    public MicrosoftGraphTokenService(ITokenAcquisition tokenAcquisition, IConfiguration configuration)
    {
        _tokenAcquisition = tokenAcquisition;
        _configuration = configuration;
    }

    public Task<string> GetForUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var scopes = _configuration.GetSection("MicrosoftGraph:Scopes").Get<string[]>() ?? ["Files.ReadWrite"];
        return _tokenAcquisition.GetAccessTokenForUserAsync(scopes, user: principal);
    }
}

/// <summary>
/// Keeps the infrastructure container valid in tooling/unit-test hosts that do
/// not configure Microsoft Identity. The real API host always registers
/// <see cref="ITokenAcquisition"/> and therefore uses the delegated service.
/// </summary>
public sealed class UnavailableMicrosoftGraphTokenService : IMicrosoftGraphTokenService
{
    public Task<string> GetForUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default) =>
        Task.FromException<string>(new InvalidOperationException("Microsoft Graph token acquisition is not configured."));
}
