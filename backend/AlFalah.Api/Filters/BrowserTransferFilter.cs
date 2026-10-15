using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AlFalah.Api.Filters;

// A binary application response is not a browser attachment. Keep the original
// bytes/stream (including bounded storage streams); the client restores metadata
// only after receipt. Direct downloads retain their normal attachment contract.
public sealed class BrowserTransferFilter : IResultFilter
{
    public const string MediaType = "application/vnd.alfalah.transfer";
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not FileResult file ||
            !context.HttpContext.Request.Headers.Accept.Any(value =>
                value?.Split(',').Any(part => part.Trim().Equals(MediaType, StringComparison.OrdinalIgnoreCase)) == true)) return;
        FileResult? transfer = file switch
        {
            FileContentResult content => new FileContentResult(content.FileContents, MediaType),
            FileStreamResult stream => new FileStreamResult(stream.FileStream, MediaType),
            PhysicalFileResult physical => new PhysicalFileResult(physical.FileName, MediaType),
            VirtualFileResult virtualFile => new VirtualFileResult(virtualFile.FileName, MediaType) { FileProvider = virtualFile.FileProvider },
            _ => null
        };
        if (transfer is null) return;
        transfer.EnableRangeProcessing = file.EnableRangeProcessing;
        transfer.LastModified = file.LastModified;
        transfer.EntityTag = file.EntityTag;
        var response = context.HttpContext.Response;
        response.Headers["X-AlFalah-Content-Type"] = file.ContentType;
        if (!string.IsNullOrEmpty(file.FileDownloadName))
            response.Headers["X-AlFalah-File-Name"] = Uri.EscapeDataString(file.FileDownloadName);
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers.CacheControl = "private, no-store";
        response.Headers.Remove("Content-Disposition");
        context.Result = transfer;
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
}
