using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities.Storage;

public sealed class StoredFileVersion : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StoredFileId { get; set; }
    public int VersionNumber { get; set; }
    public string DriveId { get; set; } = string.Empty;
    public string DriveItemId { get; set; } = string.Empty;
    public string DriveFileName { get; set; } = string.Empty;
    public string? FileExtension { get; set; }
    public string? SHA256 { get; set; }
    public long SizeInBytes { get; set; }
    public string? MimeType { get; set; }
    public string? UploadedByUserId { get; set; }
    public StoredFileAvailability Availability { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; }
    public DateTimeOffset? MissingFromDriveAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public StoredFile StoredFile { get; set; } = null!;
}
