using System.Text;
using AlFalah.Application.Storage;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace AlFalah.Api.Controllers;

[ApiController, Authorize]
[Route("api/v1/storage")]
[EnableRateLimiting("teacher-drive")]
public sealed class StorageLibraryController(IStorageLibraryService service) : ControllerBase
{
    [HttpGet("context")]
    public async Task<IActionResult> Context([FromQuery] bool own, CancellationToken ct) =>
        Ok(ApiResponse<StorageContextDto>.Success(await service.ContextAsync(own, ct)));
    [HttpGet("folders")]
    public async Task<IActionResult> Folders([FromQuery] bool own, [FromQuery] int? parentFolderId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse<StoragePage<StorageFolderDto>>.Success(await service.FoldersAsync(own, parentFolderId, page, pageSize, ct)));
    [HttpPost("folders")]
    public async Task<IActionResult> CreateFolder(CreateStorageFolderRequest request, CancellationToken ct) =>
        Ok(ApiResponse<StorageFolderDto>.Success(await service.CreateFolderAsync(request, ct)));
    [HttpGet("folders/{id:int}/drive-items")]
    public async Task<IActionResult> Discover(int id, [FromQuery] bool own, [FromQuery] string? pageToken, CancellationToken ct) =>
        Ok(ApiResponse<StorageDiscoveryPageDto>.Success(await service.DiscoverAsync(own, id, pageToken, ct)));
    [HttpPatch("folders/{id:int}/parent")]
    public async Task<IActionResult> MoveFolder(int id, MoveStorageFolderRequest request, CancellationToken ct) =>
        Ok(ApiResponse<StorageFolderDto>.Success(await service.MoveFolderAsync(id, request, ct)));
    [HttpGet("files")]
    public async Task<IActionResult> Files([FromQuery] StorageListRequest request, CancellationToken ct) =>
        Ok(ApiResponse<StoragePage<StorageFileListDto>>.Success(await service.FilesAsync(false, request, ct)));
    [HttpGet("me/files")]
    public async Task<IActionResult> OwnFiles([FromQuery] StorageListRequest request, CancellationToken ct) =>
        Ok(ApiResponse<StoragePage<StorageFileListDto>>.Success(await service.FilesAsync(true, request, ct)));
    [HttpGet("files/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct) =>
        Ok(ApiResponse<StorageFileDetailsDto>.Success(await service.DetailsAsync(id, ct)));
    [HttpGet("files/{id:int}/content")]
    public async Task<IActionResult> Content(int id, [FromQuery] bool preview, CancellationToken ct)
    {
        var file = await service.ContentAsync(id, ct);
        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox";
        if (preview && ValidatedStorageUpload.CanPreview(file.ContentType))
            return File(file.Content, file.ContentType);
        return File(file.Content, file.ContentType, file.FileName);
    }
    [HttpPost("files")]
    [RequestSizeLimit(ValidatedStorageUpload.MaxRequestBytes)]
    public Task<IActionResult> Upload(CancellationToken ct) => ReadUploadAsync(false, ct);
    [HttpPost("me/files")]
    [RequestSizeLimit(ValidatedStorageUpload.MaxRequestBytes)]
    public Task<IActionResult> UploadOwn(CancellationToken ct) => ReadUploadAsync(true, ct);

    // No IFormFile/form binding: metadata precedes the file and the section stream passes
    // directly to bounded validation. Request multipart overhead is independent of file size.
    private async Task<IActionResult> ReadUploadAsync(bool own, CancellationToken ct)
    {
        if (!MediaTypeHeaderValue.TryParse(Request.ContentType, out var type) ||
            !type.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("طلب الرفع غير صالح.");
        var boundary = HeaderUtilities.RemoveQuotes(type.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > 128) throw new ArgumentException("حدود طلب الرفع غير صالحة.");
        var key = Request.Headers["Idempotency-Key"].ToString();
        var reader = new MultipartReader(boundary, Request.Body) { BodyLengthLimit = ValidatedStorageUpload.MaxFileBytes };
        int? folderId = null;
        long size = 0;
        var fields = new HashSet<string>();
        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(ct)) is not null)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)) throw new ArgumentException("محتوى طلب الرفع غير صالح.");
            var field = HeaderUtilities.RemoveQuotes(disposition.Name).Value ?? "";
            if (!fields.Add(field) || fields.Count > 3) throw new ArgumentException("حقول الرفع مكررة أو زائدة.");
            var name = HeaderUtilities.RemoveQuotes(disposition.FileNameStar.HasValue ? disposition.FileNameStar : disposition.FileName).Value;
            if (name is not null)
            {
                if (field != "file" || size <= 0) throw new ArgumentException("حجم الملف مطلوب قبل المحتوى.");
                var result = await service.UploadAsync(new(section.Body, name, size, folderId, key, own), ct);
                return result.Status == "Completed"
                    ? Ok(ApiResponse<StorageUploadDto>.Success(result, "تم رفع الملف."))
                    : Accepted(ApiResponse<StorageUploadDto>.Success(result, "عملية الرفع تنتظر المصالحة."));
            }
            var value = new byte[65];
            var length = 0;
            int read;
            while ((read = await section.Body.ReadAsync(value.AsMemory(length), ct)) > 0)
            {
                length += read;
                if (length == value.Length) throw new ArgumentException("حقل الرفع أطول من الحد المسموح.");
            }
            var text = Encoding.UTF8.GetString(value, 0, length);
            if (field == "length" && long.TryParse(text, out var parsed)) size = parsed;
            else if (field == "parentFolderId" && int.TryParse(text, out var id)) folderId = id;
            else throw new ArgumentException("حقل الرفع غير صالح.");
        }
        throw new ArgumentException("لم يتم اختيار ملف.");
    }
    [HttpPost("operations/{id:int}/reconcile")]
    public async Task<IActionResult> Reconcile(int id, CancellationToken ct) =>
        Ok(ApiResponse<StorageUploadDto>.Success(await service.ReconcileAsync(id, ct)));
    [HttpPatch("files/{id:int}/name")]
    public async Task<IActionResult> Rename(int id, RenameStorageFileRequest request, CancellationToken ct)
    {
        await service.RenameAsync(id, request, ct);
        return Ok(ApiResponse.Success("تم تعديل الاسم."));
    }
    [HttpDelete("files/{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromBody] DeleteStorageFileRequest request, CancellationToken ct)
    {
        await service.DeleteAsync(id, request, ct);
        return Ok(ApiResponse.Success("تم نقل الملف إلى المهملات."));
    }
}
