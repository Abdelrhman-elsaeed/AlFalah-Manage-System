namespace AlFalah.Domain.Entities.Storage;

public sealed class StorageDelegation : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string GranteeUserId { get; set; } = string.Empty;
    public string GrantedByManagerUserId { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedByManagerUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? RevocationReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
