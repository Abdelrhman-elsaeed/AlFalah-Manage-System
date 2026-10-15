using AlFalah.Api.Filters;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace AlFalah.Tests.Controllers;

public sealed class BrowserTransferFilterTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Binary_transport_preserves_bytes_or_stream_and_restores_only_metadata(bool stream)
    {
        var bytes = "%PDF-1.7 synthetic"u8.ToArray();
        FileResult file = stream ? new FileStreamResult(new MemoryStream(bytes), "application/pdf") : new FileContentResult(bytes, "application/pdf");
        file.FileDownloadName = "تقرير المدرسة.pdf";
        var context = Context(file, BrowserTransferFilter.MediaType);
        new BrowserTransferFilter().OnResultExecuting(context);
        file = (FileResult)context.Result;
        file.ContentType.Should().Be(BrowserTransferFilter.MediaType);
        file.FileDownloadName.Should().BeEmpty();
        context.HttpContext.Response.Headers.Should().NotContainKey("Content-Disposition");
        Uri.UnescapeDataString(context.HttpContext.Response.Headers["X-AlFalah-File-Name"]!).Should().Be("تقرير المدرسة.pdf");
        context.HttpContext.Response.Headers["X-AlFalah-Content-Type"].ToString().Should().Be("application/pdf");
        if(file is FileContentResult content) content.FileContents.Should().Equal(bytes);
        else ((MemoryStream)((FileStreamResult)file).FileStream).ToArray().Should().Equal(bytes);
    }
    [Fact] public void Direct_downloads_and_error_envelopes_keep_their_original_contracts()
    {
        var file = new FileContentResult([1,2,3], "application/pdf") { FileDownloadName = "direct.pdf" };
        new BrowserTransferFilter().OnResultExecuting(Context(file,"application/pdf"));
        file.FileDownloadName.Should().Be("direct.pdf"); file.ContentType.Should().Be("application/pdf");
        var denied=Context(new ObjectResult(new { message="Denied" }) { StatusCode=403 },BrowserTransferFilter.MediaType);
        new BrowserTransferFilter().OnResultExecuting(denied);
        denied.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        denied.HttpContext.Response.Headers.Should().NotContainKey("X-AlFalah-Content-Type");
    }
    private static ResultExecutingContext Context(IActionResult result,string accept)
    {
        var http=new DefaultHttpContext();http.Request.Headers.Accept=accept;
        return new(new ActionContext(http,new RouteData(),new ActionDescriptor()),[],result,new object());
    }
}
