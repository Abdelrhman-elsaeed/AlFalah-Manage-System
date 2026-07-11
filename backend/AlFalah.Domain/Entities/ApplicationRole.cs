using Microsoft.AspNetCore.Identity;

namespace AlFalah.Domain.Entities;

/// <summary>
/// Extends ASP.NET Core IdentityRole.
/// All roles are database-driven; role names are defined in RoleNames constants.
/// </summary>
public class ApplicationRole : IdentityRole
{
    public string? DescriptionAr { get; set; }
    public string? DescriptionEn { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
