using System.Text.Json;
using AlFalah.Application.Common;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AlFalah.Api.Middlewares;

/// <summary>
/// Global exception middleware. Catches unhandled exceptions and returns
/// a consistent ApiResponse envelope with appropriate HTTP status codes.
/// No stack traces are exposed in production.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, exception.Message),
            UnauthorizedSchoolAccessException => (StatusCodes.Status403Forbidden, exception.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
            InvalidOperationException => (StatusCodes.Status400BadRequest, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى.")
        };

        context.Response.StatusCode = statusCode;

        var errorDetail = _env.IsDevelopment() ? exception.Message : message;

        var response = ApiResponse.Fail(
            error: errorDetail,
            message: message
        );

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
