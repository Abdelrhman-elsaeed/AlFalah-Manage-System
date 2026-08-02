using AlFalah.Application.Common.Exceptions;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// The access-control boundary for teacher evidence files.
///
/// Under OneDrive, Graph itself refused anything outside the teacher's own storage, so this
/// check was only a second line of defence. Google Drive is reached with ONE school-wide
/// credential that can see every teacher's folder, so this class is now the ONLY thing
/// standing between teacher A and teacher B's files. It must therefore fail closed: any
/// ambiguity — missing item, trashed item, unreadable parent chain, cycle, excessive depth —
/// denies access rather than allowing it.
/// </summary>
public sealed class TeacherDriveFolderGuard
{
    /// <summary>
    /// Depth ceiling for the ancestor walk. Deep enough for any realistic evidence tree,
    /// shallow enough that a hostile or corrupt parent graph cannot turn one request into
    /// hundreds of Drive calls.
    /// </summary>
    private const int MaxAncestorsInspected = 64;

    private readonly IGoogleDriveClient _drive;

    public TeacherDriveFolderGuard(IGoogleDriveClient drive) => _drive = drive;

    /// <summary>
    /// Proves <paramref name="itemId"/> is the granted root itself or a live descendant of
    /// it, and returns the item's metadata so callers need no second fetch.
    /// </summary>
    public async Task<GoogleDriveFile> EnsureWithinGrantAsync(
        DriveFolderMappingDto mapping, string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId)) throw new TeacherDriveAccessDeniedException();

        var item = await _drive.GetFileAsync(mapping.SchoolId, itemId, cancellationToken)
            ?? throw new TeacherDriveAccessDeniedException("لم يعد الملف أو المجلد موجوداً.");
        // A trashed item is still readable by id, so without this a deleted file would stay
        // reachable through a direct request.
        if (item.Trashed) throw new TeacherDriveAccessDeniedException("لم يعد الملف أو المجلد موجوداً.");
        if (string.Equals(item.Id, mapping.RootItemId, StringComparison.Ordinal)) return item;

        // Breadth-first over `parents` rather than following parents[0]: Drive permits an
        // item to have several parents, and following only the first would deny access to a
        // file that genuinely does sit inside the granted folder.
        var visited = new HashSet<string>(StringComparer.Ordinal) { item.Id };
        var queue = new Queue<string>(item.Parents);
        var inspected = 0;

        while (queue.Count > 0 && inspected < MaxAncestorsInspected)
        {
            var parentId = queue.Dequeue();
            if (string.IsNullOrWhiteSpace(parentId) || !visited.Add(parentId)) continue;
            if (string.Equals(parentId, mapping.RootItemId, StringComparison.Ordinal)) return item;

            inspected++;
            var parent = await _drive.GetFileAsync(mapping.SchoolId, parentId, cancellationToken);
            // Stop climbing a branch we cannot read: it can never prove containment, and
            // beyond the granted root the school credential's view is irrelevant anyway.
            if (parent is null || parent.Trashed) continue;
            foreach (var grandParent in parent.Parents) queue.Enqueue(grandParent);
        }

        throw new TeacherDriveAccessDeniedException();
    }

    /// <summary>Same guarantee as <see cref="EnsureWithinGrantAsync"/>, and also that the target is a folder.</summary>
    public async Task<GoogleDriveFile> EnsureFolderWithinGrantAsync(
        DriveFolderMappingDto mapping, string folderId, CancellationToken cancellationToken = default)
    {
        var item = await EnsureWithinGrantAsync(mapping, folderId, cancellationToken);
        if (!item.IsFolder) throw new ArgumentException("العنصر المحدد ليس مجلداً.");
        return item;
    }

    /// <summary>
    /// Non-throwing containment test used when granting a folder to a teacher: it answers
    /// "is <paramref name="candidateFolderId"/> inside <paramref name="rootFolderId"/>?"
    /// against an arbitrary root rather than an existing grant.
    /// </summary>
    public async Task<bool> IsWithinAsync(
        int schoolId, string rootFolderId, string candidateFolderId, CancellationToken cancellationToken = default)
    {
        var probe = new DriveFolderMappingDto(0, schoolId, string.Empty, rootFolderId, string.Empty, null, true);
        try
        {
            await EnsureWithinGrantAsync(probe, candidateFolderId, cancellationToken);
            return true;
        }
        catch (TeacherDriveAccessDeniedException)
        {
            return false;
        }
    }
}
