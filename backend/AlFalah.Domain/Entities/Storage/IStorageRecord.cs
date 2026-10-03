namespace AlFalah.Domain.Entities.Storage;

public interface IStorageRecord
{
    int Id { get; set; }
    int SchoolId { get; set; }
    DateTimeOffset CreatedAtUtc { get; set; }
    DateTimeOffset UpdatedAtUtc { get; set; }
    byte[] RowVersion { get; set; }
}
