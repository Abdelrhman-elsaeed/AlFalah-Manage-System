using System.Net.Http.Headers;
using System.Reflection;
using AlFalah.Api.Controllers;
using AlFalah.Application.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AlFalah.Tests.Storage;

public sealed class StorageUploadMultipartTests
{
    [Fact]
    public async Task Browser_style_multipart_request_reaches_upload_service_with_complete_file()
    {
        var bytes = new byte[1_167_000];
        Random.Shared.NextBytes(bytes);
        using var form = new MultipartFormDataContent("----WebKitFormBoundaryG24HAtWGMYyMp6pB");
        form.Add(new StringContent("7"), "parentFolderId");
        form.Add(new StringContent(bytes.Length.ToString()), "length");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        form.Add(file, "file", "recording.mp4");
        await using var body = new MemoryStream();
        await form.CopyToAsync(body);
        body.Position = 0;

        var service = DispatchProxy.Create<IStorageLibraryService, UploadServiceProxy>();
        var context = new DefaultHttpContext();
        context.Request.ContentType = form.Headers.ContentType!.ToString();
        context.Request.Body = body;
        context.Request.ContentLength = body.Length;
        context.Request.Headers["Idempotency-Key"] = "test-key";
        var controller = new StorageLibraryController(service) { ControllerContext = new ControllerContext { HttpContext = context } };

        var response = await controller.Upload(CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        var proxy = (UploadServiceProxy)service;
        proxy.UploadRequest.Should().NotBeNull();
        proxy.UploadRequest!.ParentFolderId.Should().Be(7);
        proxy.UploadRequest.Length.Should().Be(bytes.Length);
        proxy.UploadRequest.FileName.Should().Be("recording.mp4");
        proxy.UploadedBytes.Should().Equal(bytes);
    }

    [Fact]
    public async Task Raw_upload_bypasses_multipart_reader_and_preserves_file_bytes()
    {
        var bytes = new byte[1_167_000];
        Random.Shared.NextBytes(bytes);
        var service = DispatchProxy.Create<IStorageLibraryService, UploadServiceProxy>();
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/octet-stream";
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        context.Request.QueryString = new QueryString($"?parentFolderId=7&length={bytes.Length}&fileName=recording.mp4");
        context.Request.Headers["Idempotency-Key"] = "test-key";
        var controller = new StorageLibraryController(service) { ControllerContext = new ControllerContext { HttpContext = context } };

        var response = await controller.Upload(CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        var proxy = (UploadServiceProxy)service;
        proxy.UploadRequest.Should().NotBeNull();
        proxy.UploadRequest!.ParentFolderId.Should().Be(7);
        proxy.UploadRequest.Length.Should().Be(bytes.Length);
        proxy.UploadRequest.FileName.Should().Be("recording.mp4");
        proxy.UploadedBytes.Should().Equal(bytes);
    }

    public class UploadServiceProxy : DispatchProxy
    {
        public StorageUploadRequest? UploadRequest { get; private set; }
        public byte[]? UploadedBytes { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IStorageLibraryService.UploadAsync)) throw new NotSupportedException(targetMethod?.Name);
            UploadRequest = (StorageUploadRequest)args![0]!;
            return HandleUploadAsync(UploadRequest);
        }

        private async Task<StorageUploadDto> HandleUploadAsync(StorageUploadRequest request)
        {
            await using var copy = new MemoryStream();
            await request.Content.CopyToAsync(copy);
            UploadedBytes = copy.ToArray();
            return new(1, 1, 1, "Completed", "recording.mp4", request.Length,
                "video/mp4", DateTimeOffset.UtcNow, null);
        }
    }
}
