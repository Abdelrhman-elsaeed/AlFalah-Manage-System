using AlFalah.Shared.Models;

namespace AlFalah.Application.DTOs.Schools;

/// <summary>
/// Filters for the schools list endpoint.
/// </summary>
public class SchoolListQuery : PagedQuery
{
    public string? Search { get; set; }
    public string? City { get; set; }
    public string? Stage { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// School list row used by p-table on the schools page.
/// </summary>
public class SchoolListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public int? SchoolLocationId { get; set; }
    public string? SchoolLocationName { get; set; }
    public string? RegionName { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? LocationDetails { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; }
    public string? ManagerUserId { get; set; }
    public string? ManagerFullName { get; set; }
    public int ActiveUserCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Detailed school view returned by GET /{id}.
/// </summary>
public class SchoolDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public int? SchoolLocationId { get; set; }
    public string? SchoolLocationName { get; set; }
    public string? RegionName { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? LocationDetails { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; }
    public string? ManagerUserId { get; set; }
    public string? ManagerFullName { get; set; }
    public string? ManagerUsername { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int ActiveUserCount { get; set; }
}

/// <summary>
/// Create-school request body.
/// </summary>
public class SchoolCreateRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Stage { get; set; } = "Primary";
    public int SchoolLocationId { get; set; }
    public string? LocationDetails { get; set; }
    public string? LogoUrl { get; set; }
    /// <summary>Manager is OPTIONAL at create time. Activation is blocked until assigned.</summary>
    public string? ManagerUserId { get; set; }
    public bool IsActive { get; set; } = false;
}

/// <summary>
/// Update-school request body.
/// </summary>
public class SchoolUpdateRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Stage { get; set; } = "Primary";
    public int SchoolLocationId { get; set; }
    public string? LocationDetails { get; set; }
    public string? LogoUrl { get; set; }
    public string? ManagerUserId { get; set; }
}

/// <summary>
/// Assign (or replace) the School Manager request body.
/// </summary>
public class AssignSchoolManagerRequestDto
{
    public string UserId { get; set; } = string.Empty;
}
