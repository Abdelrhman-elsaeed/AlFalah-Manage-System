using AlFalah.Application.Common.Exceptions;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Read side of a teacher's evidence folder. Every entry point re-resolves the teacher and
/// their grant from the database, then proves the requested item lies inside that grant —
/// nothing is ever trusted from the request itself.
/// </summary>
public sealed class GoogleDriveBrowserService : IGoogleDriveBrowserService
{
    private const int PageSize = 50;

    private readonly ITeacherDriveIdentityService _identity;
    private readonly ITeacherDriveMappingService _mappings;
    private readonly IGoogleDriveClient _drive;
    private readonly TeacherDriveFolderGuard _guard;
    private readonly AlFalahDbContext _context;
    private readonly AuditLogWriter _audit;

    public GoogleDriveBrowserService(
        ITeacherDriveIdentityService identity,
        ITeacherDriveMappingService mappings,
        IGoogleDriveClient drive,
        TeacherDriveFolderGuard guard,
        AlFalahDbContext context,
        AuditLogWriter audit)
    {
        _identity = identity;
        _mappings = mappings;
        _drive = drive;
        _guard = guard;
        _context = context;
        _audit = audit;
    }

    public async Task<DriveItemsPageDto> ListAsync(DriveQuery query, CancellationToken cancellationToken = default)
    {
        var (teacher, mapping) = await ResolveAsync(cancellationToken);
        var parentId = string.IsNullOrWhiteSpace(query.ParentItemId) ? mapping.RootItemId : query.ParentItemId!;
        await _guard.EnsureFolderWithinGrantAsync(mapping, parentId, cancellationToken);

        var page = await _drive.ListChildrenAsync(teacher.SchoolId, new GoogleDriveListRequest(
            parentId,
            query.Search,
            OrderBy(query.SortBy, query.SortDirection),
            PageSize,
            query.PageToken,
            string.IsNullOrWhiteSpace(mapping.DriveId) ? null : mapping.DriveId), cancellationToken);

        var items = page.Files.Select(ToDto).ToList();

        // Overlay the review state so the teacher sees which of their files are still
        // pending, approved or rejected without leaving the page.
        var fileIds = items.Where(x => !x.IsFolder).Select(x => x.ItemId).ToArray();
        if (fileIds.Length > 0)
        {
            var statuses = await _context.TeacherEvidenceSubmissions.AsNoTracking()
                .Where(x => x.TeacherId == teacher.TeacherId && !x.IsDeleted && fileIds.Contains(x.DriveItemId))
                .ToDictionaryAsync(x => x.DriveItemId, x => x.ReviewStatus.ToString(), cancellationToken);
            items = items
                .Select(x => statuses.TryGetValue(x.ItemId, out var status) ? x with { SubmissionStatus = status } : x)
                .ToList();
        }

        _audit.Write(teacher.SchoolId, null, "TeacherDrive.FolderOpened", "TeacherDriveFolder", parentId, null,
            new { teacher.TeacherId });
        await _context.SaveChangesAsync(cancellationToken);
        return new(items, page.NextPageToken, items.Count);
    }

    public async Task<DriveItemDto> GetItemAsync(string itemId, CancellationToken cancellationToken = default)
    {
        var (_, mapping) = await ResolveAsync(cancellationToken);
        return ToDto(await _guard.EnsureWithinGrantAsync(mapping, itemId, cancellationToken));
    }

    public async Task<IReadOnlyList<DriveBreadcrumbDto>> GetBreadcrumbAsync(string? itemId, CancellationToken cancellationToken = default)
    {
        var (teacher, mapping) = await ResolveAsync(cancellationToken);
        var currentId = string.IsNullOrWhiteSpace(itemId) ? mapping.RootItemId : itemId!;
        var current = await _guard.EnsureWithinGrantAsync(mapping, currentId, cancellationToken);

        // Build the trail from the item up to the granted root and stop there: the teacher
        // must not learn the names of folders above their own.
        var trail = new List<DriveBreadcrumbDto>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var cursor = current;
        while (cursor is not null && visited.Add(cursor.Id))
        {
            trail.Add(new(cursor.Id, cursor.Name));
            if (string.Equals(cursor.Id, mapping.RootItemId, StringComparison.Ordinal)) break;
            var parentId = cursor.Parents.FirstOrDefault();
            cursor = string.IsNullOrWhiteSpace(parentId)
                ? null
                : await _drive.GetFileAsync(teacher.SchoolId, parentId!, cancellationToken);
        }

        // The guard already proved containment, so a trail that fails to reach the root means
        // the parent chain shifted mid-walk. Show the grant root rather than a partial path.
        if (trail.Count == 0 || !string.Equals(trail[^1].ItemId, mapping.RootItemId, StringComparison.Ordinal))
            return [new DriveBreadcrumbDto(mapping.RootItemId, mapping.FolderDisplayName)];

        trail.Reverse();
        return trail;
    }

    public async Task<DriveFileContentDto> DownloadAsync(string itemId, CancellationToken cancellationToken = default)
    {
        var (teacher, mapping) = await ResolveAsync(cancellationToken);
        var item = await _guard.EnsureWithinGrantAsync(mapping, itemId, cancellationToken);
        if (item.IsFolder) throw new ArgumentException("لا يمكن تنزيل مجلد.");

        _audit.Write(teacher.SchoolId, null, "TeacherDrive.FileOpened", "DriveItem", itemId, null,
            new { teacher.TeacherId, item.Name });
        await _context.SaveChangesAsync(cancellationToken);
        return await _drive.DownloadAsync(teacher.SchoolId, itemId, cancellationToken);
    }

    public async Task<IReadOnlyList<RecentFileDto>> GetRecentAsync(CancellationToken cancellationToken = default)
    {
        var teacher = await _identity.ResolveCurrentTeacherAsync(cancellationToken);
        return await _context.TeacherEvidenceSubmissions.AsNoTracking()
            .Where(x => x.TeacherId == teacher.TeacherId
                && x.UploadStatus == AlFalah.Domain.Enums.EvidenceUploadStatus.Completed
                && !x.IsDeleted)
            .OrderByDescending(x => x.UploadedAtUtc)
            .Take(4)
            .Select(x => new RecentFileDto(x.DriveItemId, x.FileName, x.FileExtension, x.SizeInBytes, x.UploadedAtUtc, x.WebUrl))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Resolves the caller's teacher identity and their active folder grant together.</summary>
    internal async Task<(TeacherDriveIdentity Teacher, DriveFolderMappingDto Mapping)> ResolveAsync(CancellationToken cancellationToken)
    {
        var teacher = await _identity.ResolveCurrentTeacherAsync(cancellationToken);
        var mapping = await _mappings.GetForTeacherAsync(teacher.TeacherId, cancellationToken);
        // Defence in depth: a grant row that names another school would let a transferred
        // teacher reach their old school's drive credential.
        if (mapping.SchoolId != teacher.SchoolId) throw new TeacherDriveAccessDeniedException();
        return (teacher, mapping);
    }

    internal static DriveItemDto ToDto(GoogleDriveFile file)
    {
        var extension = file.IsFolder ? null : Path.GetExtension(file.Name).TrimStart('.').ToLowerInvariant();
        return new(
            file.Id,
            file.Name,
            file.IsFolder,
            null,
            string.IsNullOrEmpty(extension) ? null : extension,
            file.MimeType,
            file.Size,
            file.ModifiedTime,
            file.LastModifiedBy,
            file.WebViewLink,
            file.Version,
            null);
    }

    /// <summary>
    /// Drive sorts on a comma-separated key list, and <c>folder</c> is a pseudo-key that
    /// groups directories first — which is what users expect from a file browser.
    /// </summary>
    private static string OrderBy(string? sortBy, string? sortDirection)
    {
        var key = sortBy?.ToLowerInvariant() switch
        {
            "modified" => "modifiedTime",
            "size" => "quotaBytesUsed",
            _ => "name"
        };
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return descending ? $"folder,{key} desc" : $"folder,{key}";
    }
}
