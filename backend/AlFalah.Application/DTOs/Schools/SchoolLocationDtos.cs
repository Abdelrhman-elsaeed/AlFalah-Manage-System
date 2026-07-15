namespace AlFalah.Application.DTOs.Schools;

public sealed record SchoolLocationDto(
    int Id,
    string NameAr,
    string? NameEn,
    string RegionNameAr,
    string? RegionNameEn,
    decimal Latitude,
    decimal Longitude);

public sealed class SchoolLocationCreateRequestDto
{
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string RegionNameAr { get; set; } = string.Empty;
    public string? RegionNameEn { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}
