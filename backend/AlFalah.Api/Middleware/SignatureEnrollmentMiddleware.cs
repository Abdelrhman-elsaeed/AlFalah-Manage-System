using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;

namespace AlFalah.Api.Middlewares;

public sealed class SignatureEnrollmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAccountAccessService access)
    {
        var path = context.Request.Path;
        if (context.User.Identity?.IsAuthenticated != true || context.User.IsInRole(RoleNames.SchoolManager)
            || !path.StartsWithSegments("/api") || path.StartsWithSegments("/api/v1/auth")
            || path.StartsWithSegments("/api/v1/account/signature")
            || path.StartsWithSegments("/api/v1/school-duty")
            || path.StartsWithSegments("/api/v1/timetable-coverage")
            || context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(context);
            return;
        }
        if ((await access.GetSignatureRequirementAsync(context.RequestAborted)).Required)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers["X-Signature-Required"] = "true";
            await context.Response.WriteAsJsonAsync(ApiResponse.Fail("احفظ توقيعك أولًا لاستكمال استخدام المنصة."), context.RequestAborted);
            return;
        }
        await next(context);
    }
}
