using System.Text.Json;
using AlFalah.Api.Middlewares;
using AlFalah.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Infrastructure;

public sealed class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task Unexpected_exception_is_logged_but_internal_details_are_not_returned()
    {
        const string technicalDetail = "The LINQ expression DbSet<VisitDomainAverage> could not be translated";
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new InvalidOperationException(technicalDetail),
            NullLogger<GlobalExceptionMiddleware>.Instance);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var body = await ReadBodyAsync(context);
        body.Should().NotContain(technicalDetail);
        body.Should().NotContain("DbSet");
        body.Should().Contain("Unable to complete the request");
    }

    [Fact]
    public async Task Explicit_business_rule_message_remains_user_visible()
    {
        const string message = "لا يمكن تعديل الزيارة في حالتها الحالية.";
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new BusinessRuleException(message),
            NullLogger<GlobalExceptionMiddleware>.Instance);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var body = await ReadBodyAsync(context);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("message").GetString().Should().Be(message);
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }
}
