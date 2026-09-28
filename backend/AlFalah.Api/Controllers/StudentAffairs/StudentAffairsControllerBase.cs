using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[ApiController]
[Authorize]
public abstract class StudentAffairsControllerBase : ControllerBase
{
    protected StudentAffairsControllerBase(IMediator mediator, ICurrentUserService currentUser)
    {
        Mediator = mediator;
        CurrentUser = currentUser;
    }

    protected IMediator Mediator { get; }
    protected ICurrentUserService CurrentUser { get; }

    protected bool HasAnyPermission(params string[] permissions) =>
        permissions.Any(CurrentUser.HasPermission);

    protected IActionResult PermissionDenied() =>
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("You do not have permission to perform this action."));

    protected IActionResult FeatureNotImplemented(string workstream) =>
        StatusCode(
            StatusCodes.Status501NotImplemented,
            ApiResponse.Fail($"This contract is explicitly deferred to {workstream} and is not executable in W1."));

    protected IActionResult FromResponse<T>(ApiResponse<T> response, int successStatus = StatusCodes.Status200OK)
    {
        if (response.IsSuccess) return StatusCode(successStatus, response);

        var error = response.Errors.FirstOrDefault() ?? response.Message;
        var normalized = error.ToLowerInvariant();
        var status = normalized.Contains("not found", StringComparison.Ordinal)
            ? StatusCodes.Status404NotFound
            : normalized.Contains("modified", StringComparison.Ordinal)
                || normalized.Contains("concurrency", StringComparison.Ordinal)
                || normalized.Contains("stale", StringComparison.Ordinal)
                || normalized.Contains("idempotency", StringComparison.Ordinal)
                ? StatusCodes.Status409Conflict
                : normalized.Contains("authenticated", StringComparison.Ordinal)
                    || normalized.Contains("authentication", StringComparison.Ordinal)
                    ? StatusCodes.Status401Unauthorized
                    : normalized.Contains("permission", StringComparison.Ordinal)
                        || normalized.Contains("authorized", StringComparison.Ordinal)
                        || normalized.Contains("outside the caller", StringComparison.Ordinal)
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status400BadRequest;
        return StatusCode(status, response);
    }
}
