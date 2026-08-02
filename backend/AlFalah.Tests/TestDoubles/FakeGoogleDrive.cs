using System.Text;
using AlFalah.Application.Common.Exceptions;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;

namespace AlFalah.Tests.TestDoubles;

/// <summary>
/// An in-memory stand-in for Google Drive.
///
/// The point of the <see cref="IGoogleDriveClient"/> seam is that everything above it is pure
/// policy — who may see which folder, what gets written to the ledger, when a matrix cell
/// ticks — so the real services can be exercised end to end without a network or a Google
/// project. This fake therefore models only the Drive behaviour those rules depend on:
/// parent links, trashing, and the fact that Drive never overwrites on create.
/// </summary>
public sealed class FakeGoogleDrive : IGoogleDriveClient
{
    private readonly Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);
    private int _generatedIds;

    /// <summary>Every upload the code under test performed, in order.</summary>
    public List<UploadRecord> Uploads { get; } = [];

    /// <summary>File ids that were trashed, in order.</summary>
    public List<string> Trashed { get; } = [];

    /// <summary>Schools whose access token could not be obtained. Any call for them throws.</summary>
    public HashSet<int> UnreachableSchools { get; } = [];

    public FakeGoogleDrive AddFolder(string id, string name, string? parentId = null)
    {
        _nodes[id] = new Node(id, name, GoogleDriveFile.FolderMimeType, parentId, []);
        return this;
    }

    public FakeGoogleDrive AddFile(string id, string name, string parentId, string content = "evidence", string mimeType = "application/pdf")
    {
        _nodes[id] = new Node(id, name, mimeType, parentId, Encoding.UTF8.GetBytes(content));
        return this;
    }

    /// <summary>Simulates a file being trashed directly in Drive, outside the application.</summary>
    public void TrashExternally(string id)
    {
        if (_nodes.TryGetValue(id, out var node)) node.Trashed = true;
    }

    public void RestoreExternally(string id)
    {
        if (_nodes.TryGetValue(id, out var node)) node.Trashed = false;
    }

    /// <summary>Simulates a hard delete performed directly in Drive.</summary>
    public void RemoveExternally(string id) => _nodes.Remove(id);

    /// <summary>Re-parents an item, so tests can move a folder out from under a grant.</summary>
    public void MoveExternally(string id, string? newParentId)
    {
        if (_nodes.TryGetValue(id, out var node)) node.ParentId = newParentId;
    }

    public bool Exists(string id) => _nodes.ContainsKey(id);
    public IReadOnlyList<string> ChildIdsOf(string parentId) =>
        _nodes.Values.Where(x => x.ParentId == parentId && !x.Trashed).Select(x => x.Id).ToList();

    public Task<GoogleDriveFile?> GetFileAsync(int schoolId, string fileId, CancellationToken cancellationToken = default)
    {
        EnsureReachable(schoolId);
        return Task.FromResult(_nodes.TryGetValue(fileId, out var node) ? node.ToFile() : null);
    }

    public Task<GoogleDriveFileList> ListChildrenAsync(int schoolId, GoogleDriveListRequest request, CancellationToken cancellationToken = default)
    {
        EnsureReachable(schoolId);
        var descending = request.OrderBy.EndsWith(" desc", StringComparison.Ordinal);
        var children = _nodes.Values
            .Where(x => x.ParentId == request.ParentFolderId && !x.Trashed)
            .Where(x => string.IsNullOrWhiteSpace(request.NameContains)
                || x.Name.Contains(request.NameContains!, StringComparison.OrdinalIgnoreCase))
            // Mirrors the real `orderBy=folder,<key>` contract: folders first, then the key.
            .OrderBy(x => x.IsFolder ? 0 : 1)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
        if (descending) children = [.. children.OrderBy(x => x.IsFolder ? 0 : 1).ThenByDescending(x => x.Name, StringComparer.Ordinal)];

        var skip = string.IsNullOrWhiteSpace(request.PageToken) ? 0 : int.Parse(request.PageToken!);
        var page = children.Skip(skip).Take(request.PageSize).ToList();
        var next = skip + page.Count < children.Count ? (skip + page.Count).ToString() : null;
        return Task.FromResult(new GoogleDriveFileList(page.Select(x => x.ToFile()).ToList(), next));
    }

    public async Task<GoogleDriveFile> UploadAsync(int schoolId, GoogleDriveUploadRequest request, CancellationToken cancellationToken = default)
    {
        EnsureReachable(schoolId);
        // The real API rejects an upload into a folder the credential cannot see; without this
        // a test could "succeed" writing into a folder that does not exist.
        if (!_nodes.TryGetValue(request.ParentFolderId, out var parent) || !parent.IsFolder)
            throw new TeacherDriveAccessDeniedException("لم يعد المجلد موجوداً.");

        using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, cancellationToken);
        var id = $"uploaded-{++_generatedIds}";
        _nodes[id] = new Node(id, request.FileName, request.ContentType, request.ParentFolderId, buffer.ToArray());
        Uploads.Add(new(schoolId, id, request.FileName, request.ParentFolderId, request.SharedDriveId, buffer.Length));
        return _nodes[id].ToFile();
    }

    public Task<DriveFileContentDto> DownloadAsync(int schoolId, string fileId, CancellationToken cancellationToken = default)
    {
        EnsureReachable(schoolId);
        if (!_nodes.TryGetValue(fileId, out var node) || node.Trashed)
            throw new TeacherDriveAccessDeniedException("لم يعد الملف موجوداً.");
        if (node.IsFolder) throw new ArgumentException("لا يمكن تنزيل مجلد.");
        return Task.FromResult(new DriveFileContentDto(
            new MemoryStream(node.Content, writable: false), node.Name, node.MimeType, node.Content.Length));
    }

    public Task<bool> TrashAsync(int schoolId, string fileId, string? sharedDriveId, CancellationToken cancellationToken = default)
    {
        EnsureReachable(schoolId);
        Trashed.Add(fileId);
        if (!_nodes.TryGetValue(fileId, out var node)) return Task.FromResult(false);
        node.Trashed = true;
        return Task.FromResult(true);
    }

    private void EnsureReachable(int schoolId)
    {
        if (UnreachableSchools.Contains(schoolId))
            throw new InvalidOperationException("تعذر الاتصال بحساب Google Drive الخاص بالمدرسة. تحقق من بيانات الاعتماد.");
    }

    public sealed record UploadRecord(int SchoolId, string FileId, string FileName, string ParentFolderId, string? SharedDriveId, long Length);

    private sealed class Node
    {
        public Node(string id, string name, string mimeType, string? parentId, byte[] content)
        {
            Id = id;
            Name = name;
            MimeType = mimeType;
            ParentId = parentId;
            Content = content;
        }

        public string Id { get; }
        public string Name { get; }
        public string MimeType { get; }
        public string? ParentId { get; set; }
        public byte[] Content { get; }
        public bool Trashed { get; set; }
        public bool IsFolder => MimeType == GoogleDriveFile.FolderMimeType;

        public GoogleDriveFile ToFile() => new(
            Id, Name, MimeType, IsFolder ? null : Content.Length, new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero),
            "حساب المدرسة", $"https://drive.google.com/file/d/{Id}/view", "1",
            ParentId is null ? [] : [ParentId], Trashed);
    }
}
