using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities.Storage;

public sealed class StorageFolder : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int? ParentFolderId { get; set; }
    public int? OwnerTeacherId { get; set; }
    public StorageFolderKind Kind { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string DriveId { get; set; } = string.Empty;
    public string DriveItemId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public StorageFolder? ParentFolder { get; set; }
}
