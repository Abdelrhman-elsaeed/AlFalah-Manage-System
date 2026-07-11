namespace AlFalah.Application.DTOs.Schools;

/// <summary>
/// School lookup item used in the login school selection dropdown.
/// </summary>
public class SchoolLookupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
}
