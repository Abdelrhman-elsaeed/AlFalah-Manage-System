namespace AlFalah.Domain.Entities;

/// <summary>
/// Represents a named permission in the system.
/// Permission names are defined in PermissionNames constants.
/// </summary>
public class Permission
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;        // e.g. "Visit.Approve"
    public string? DescriptionAr { get; set; }
    public string? DescriptionEn { get; set; }
    public string? Group { get; set; }                      // e.g. "Visit", "School"
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
