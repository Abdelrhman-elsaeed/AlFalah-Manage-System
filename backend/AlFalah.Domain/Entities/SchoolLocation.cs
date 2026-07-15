namespace AlFalah.Domain.Entities;

/// <summary>
/// A reusable, map-ready Saudi location selected when a school is created or edited.
/// One location can be shared by many schools.
/// </summary>
public class SchoolLocation
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string RegionNameAr { get; set; } = string.Empty;
    public string? RegionNameEn { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public ICollection<School> Schools { get; set; } = new List<School>();
}
