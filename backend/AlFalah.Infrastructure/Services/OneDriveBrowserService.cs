using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AlFalah.Application.Common.Exceptions;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Services;

/// <summary>Graph REST adapter. Every operation resolves the teacher folder from the DB before contacting Graph.</summary>
public sealed class OneDriveBrowserService : IOneDriveBrowserService
{
    private readonly ITeacherMicrosoftAccountService _accounts;
    private readonly ITeacherDriveMappingService _mappings;
    private readonly IMicrosoftGraphTokenService _tokens;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AlFalahDbContext _context;
    private readonly AuditLogWriter _audit;

    public OneDriveBrowserService(ITeacherMicrosoftAccountService accounts, ITeacherDriveMappingService mappings,
        IMicrosoftGraphTokenService tokens, IHttpClientFactory httpClientFactory, AlFalahDbContext context, AuditLogWriter audit)
    { _accounts = accounts; _mappings = mappings; _tokens = tokens; _httpClientFactory = httpClientFactory; _context = context; _audit = audit; }

    public async Task<DriveItemsPageDto> ListAsync(ClaimsPrincipal principal, DriveQuery query, CancellationToken cancellationToken = default)
    {
        var (teacherId, schoolId, _) = await _accounts.ResolveLinkedTeacherAsync(principal, cancellationToken);
        var mapping = await _mappings.GetForTeacherAsync(teacherId, cancellationToken);
        var parentId = string.IsNullOrWhiteSpace(query.ParentItemId) ? mapping.RootItemId : query.ParentItemId!;
        await EnsureDescendantAsync(principal, mapping, parentId, cancellationToken);
        var path = $"drives/{Uri.EscapeDataString(mapping.DriveId)}/items/{Uri.EscapeDataString(parentId)}/children?$select=id,name,size,lastModifiedDateTime,lastModifiedBy,webUrl,eTag,file,folder,parentReference";
        if (!string.IsNullOrWhiteSpace(query.Search)) path += $"&$filter=contains(name,'{query.Search.Replace("'", "''")}')";
        path += $"&$orderby={OrderBy(query.SortBy)} {Direction(query.SortDirection)}&$top=50";
        if (!string.IsNullOrWhiteSpace(query.PageToken)) path += $"&$skiptoken={Uri.EscapeDataString(query.PageToken)}";
        var json = await SendGraphAsync(principal, HttpMethod.Get, path, null, cancellationToken);
        var items = json["value"]?.AsArray().Where(x => x is not null).Select(x => ParseItem(x!)).ToList() ?? [];
        var ids = items.Where(x => !x.IsFolder).Select(x => x.ItemId).ToArray();
        var statuses = await _context.TeacherEvidenceSubmissions.AsNoTracking()
            .Where(x => x.TeacherId == teacherId && ids.Contains(x.DriveItemId))
            .ToDictionaryAsync(x => x.DriveItemId, x => x.ReviewStatus.ToString(), cancellationToken);
        items = items.Select(x => statuses.TryGetValue(x.ItemId, out var status) ? x with { SubmissionStatus = status } : x).ToList();
        _audit.Write(schoolId, null, "TeacherDrive.FolderOpened", "TeacherDriveFolder", parentId, null, new { teacherId });
        await _context.SaveChangesAsync(cancellationToken);
        return new(items, ExtractSkipToken(json["@odata.nextLink"]?.GetValue<string>()), items.Count);
    }

    public async Task<DriveItemDto> GetItemAsync(ClaimsPrincipal principal, string itemId, CancellationToken cancellationToken = default)
    {
        var (teacherId, _, _) = await _accounts.ResolveLinkedTeacherAsync(principal, cancellationToken);
        var mapping = await _mappings.GetForTeacherAsync(teacherId, cancellationToken);
        await EnsureDescendantAsync(principal, mapping, itemId, cancellationToken);
        var json = await SendGraphAsync(principal, HttpMethod.Get, $"drives/{Uri.EscapeDataString(mapping.DriveId)}/items/{Uri.EscapeDataString(itemId)}?$select=id,name,size,lastModifiedDateTime,lastModifiedBy,webUrl,eTag,file,folder,parentReference", null, cancellationToken);
        return ParseItem(json);
    }

    public async Task<IReadOnlyList<DriveBreadcrumbDto>> GetBreadcrumbAsync(ClaimsPrincipal principal, string? itemId, CancellationToken cancellationToken = default)
    {
        var (teacherId, _, _) = await _accounts.ResolveLinkedTeacherAsync(principal, cancellationToken);
        var mapping = await _mappings.GetForTeacherAsync(teacherId, cancellationToken);
        var current = string.IsNullOrWhiteSpace(itemId) ? mapping.RootItemId : itemId!;
        await EnsureDescendantAsync(principal, mapping, current, cancellationToken);
        var result = new List<DriveBreadcrumbDto>();
        while (true)
        {
            var json = await GetItemMetadataAsync(principal, mapping, current, cancellationToken);
            result.Add(new(current, json["name"]?.GetValue<string>() ?? mapping.FolderDisplayName));
            if (current == mapping.RootItemId) break;
            current = json["parentReference"]?["id"]?.GetValue<string>() ?? throw new TeacherDriveAccessDeniedException();
        }
        result.Reverse();
        return result;
    }

    public async Task<FilePreviewDto> GetPreviewAsync(ClaimsPrincipal principal, string itemId, CancellationToken cancellationToken = default)
    {
        var (teacherId, schoolId, _) = await _accounts.ResolveLinkedTeacherAsync(principal, cancellationToken);
        var mapping = await _mappings.GetForTeacherAsync(teacherId, cancellationToken);
        await EnsureDescendantAsync(principal, mapping, itemId, cancellationToken);
        var item = await GetItemAsync(principal, itemId, cancellationToken);
        if (item.IsFolder) throw new ArgumentException("لا يمكن معاينة مجلد.");
        var json = await SendGraphAsync(principal, HttpMethod.Post, $"drives/{Uri.EscapeDataString(mapping.DriveId)}/items/{Uri.EscapeDataString(itemId)}/preview", new StringContent("{}", Encoding.UTF8, "application/json"), cancellationToken);
        var url = json["getUrl"]?.GetValue<string>() ?? throw new InvalidOperationException("تعذر إنشاء رابط المعاينة.");
        _audit.Write(schoolId, null, "TeacherDrive.FilePreviewed", "DriveItem", itemId, null, new { teacherId });
        await _context.SaveChangesAsync(cancellationToken);
        return new(url, item.WebUrl ?? string.Empty, item.Name, item.MimeType);
    }

    public async Task<IReadOnlyList<RecentFileDto>> GetRecentAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var (teacherId, _, _) = await _accounts.ResolveLinkedTeacherAsync(principal, cancellationToken);
        return await _context.TeacherEvidenceSubmissions.AsNoTracking().Where(x => x.TeacherId == teacherId && x.UploadStatus == AlFalah.Domain.Enums.EvidenceUploadStatus.Completed && !x.IsDeleted)
            .OrderByDescending(x => x.UploadedAtUtc).Take(4)
            .Select(x => new RecentFileDto(x.DriveItemId, x.FileName, x.FileExtension, x.SizeInBytes, x.UploadedAtUtc, x.WebUrl))
            .ToListAsync(cancellationToken);
    }

    internal async Task EnsureDescendantAsync(ClaimsPrincipal principal, DriveFolderMappingDto mapping, string itemId, CancellationToken cancellationToken)
    {
        var current = itemId;
        for (var hops = 0; hops < 50; hops++)
        {
            if (current == mapping.RootItemId) return;
            var item = await GetItemMetadataAsync(principal, mapping, current, cancellationToken);
            var parent = item["parentReference"]?["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(parent)) break;
            current = parent;
        }
        throw new TeacherDriveAccessDeniedException();
    }

    internal Task<JsonNode> GetItemMetadataAsync(ClaimsPrincipal principal, DriveFolderMappingDto mapping, string itemId, CancellationToken cancellationToken) =>
        SendGraphAsync(principal, HttpMethod.Get, $"drives/{Uri.EscapeDataString(mapping.DriveId)}/items/{Uri.EscapeDataString(itemId)}?$select=id,name,parentReference,file,folder,webUrl,size,lastModifiedDateTime,lastModifiedBy,eTag", null, cancellationToken);

    internal async Task<JsonNode> SendGraphAsync(ClaimsPrincipal principal, HttpMethod method, string relativePath, HttpContent? content, CancellationToken cancellationToken)
    {
        var token = await _tokens.GetForUserAsync(principal, cancellationToken);
        using var request = new HttpRequestMessage(method, relativePath) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _httpClientFactory.CreateClient("MicrosoftGraph").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            throw new TeacherDriveAccessDeniedException(response.StatusCode == HttpStatusCode.NotFound ? "لم يعد الملف أو المجلد موجوداً." : "ليس لديك صلاحية للوصول إلى هذا المجلد.");
        if (response.StatusCode == (HttpStatusCode)429)
            throw new InvalidOperationException("خدمة الملفات مشغولة حالياً. سنعيد المحاولة تلقائياً.");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("تعذر الاتصال بخدمة الملفات. يرجى المحاولة مرة أخرى.");
        return JsonNode.Parse(body) ?? throw new InvalidOperationException("استجابة خدمة الملفات غير صالحة.");
    }

    internal static DriveItemDto ParseItem(JsonNode node)
    {
        var name = node["name"]?.GetValue<string>() ?? string.Empty;
        var isFolder = node["folder"] is not null;
        var extension = isFolder ? null : Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        var modifiedBy = node["lastModifiedBy"]?["user"]?["displayName"]?.GetValue<string>();
        DateTimeOffset? modified = DateTimeOffset.TryParse(node["lastModifiedDateTime"]?.GetValue<string>(), out var date) ? date : null;
        return new(node["id"]?.GetValue<string>() ?? string.Empty, name, isFolder,
            node["folder"]?["childCount"]?.GetValue<int?>(), extension, node["file"]?["mimeType"]?.GetValue<string>(),
            node["size"]?.GetValue<long?>(), modified, modifiedBy, node["webUrl"]?.GetValue<string>(), node["eTag"]?.GetValue<string>(), null);
    }

    internal async Task DeleteGraphItemAsync(ClaimsPrincipal principal, string driveId, string itemId, CancellationToken cancellationToken)
    {
        var token = await _tokens.GetForUserAsync(principal, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"drives/{Uri.EscapeDataString(driveId)}/items/{Uri.EscapeDataString(itemId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _httpClientFactory.CreateClient("MicrosoftGraph")
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        // The desired remote state is absence. A OneDrive 404 therefore still
        // lets the local deletion clear the matrix checkmark safely.
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound) return;
        if (response.StatusCode == HttpStatusCode.Forbidden) throw new TeacherDriveAccessDeniedException();
        throw new InvalidOperationException("تعذر حذف الملف من OneDrive. يرجى المحاولة مرة أخرى.");
    }

    private static string OrderBy(string? value) => value?.ToLowerInvariant() switch { "modified" => "lastModifiedDateTime", "size" => "size", "type" => "name", _ => "name" };
    private static string Direction(string? value) => string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
    private static string? ExtractSkipToken(string? nextLink)
    {
        if (string.IsNullOrWhiteSpace(nextLink) || !Uri.TryCreate(nextLink, UriKind.Absolute, out var uri)) return null;
        var token = uri.Query.TrimStart('?').Split('&').FirstOrDefault(x => x.StartsWith("$skiptoken=", StringComparison.OrdinalIgnoreCase));
        return token is null ? null : Uri.UnescapeDataString(token[11..]);
    }
}
