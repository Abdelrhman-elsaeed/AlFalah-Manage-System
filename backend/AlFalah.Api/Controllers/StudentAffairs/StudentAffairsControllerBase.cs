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
}
