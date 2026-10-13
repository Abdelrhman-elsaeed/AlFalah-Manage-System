using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AlFalah.Api.Filters;

/// <summary>Keep binding failures in the standard envelope before ApiController short-circuits.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApiResponseValidationAttribute : ActionFilterAttribute
{
    public ApiResponseValidationAttribute() { Order = -3000; }
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid) return;
        var response = ApiResponse.Fail("راجع صيغة الحقول المطلوبة. / Check the required fields.");
        response.Message = response.Errors[0]; response.ErrorCode = "InvalidRequest";
        context.Result = new BadRequestObjectResult(response);
    }
}
