using AlFalah.Domain.Entities.Storage;

namespace AlFalah.Application.Storage;

public sealed record StorageMapNode(int Id, int? ParentId, string Name, int Files, string State);
public sealed record StorageMapSpace(string Key, string Name, string State, bool CanOpen, int? FolderId, string? BrowseHandle = null);
public sealed record StorageMapDto(string SchoolName, string? Year, DateTimeOffset CheckedAt,
    IReadOnlyList<StorageMapSpace> Spaces, IReadOnlyList<StorageMapNode> Nodes, int RegisteredFiles);
public sealed record StorageMapRow(StorageFolder Folder, int Files);
public interface IStorageMapRepository
{
    Task<IReadOnlyList<StorageMapRow>> RowsAsync(int school, CancellationToken ct);
}
public interface IStorageMapService
{
    Task<StorageMapDto> ReadAsync(CancellationToken ct);
}
