namespace AlFalah.Domain.Entities;

/// <summary>
/// Links a Role to a Permission (many-to-many).
/// </summary>
public class RolePermission
{
    public int Id { get; set; }
    public string RoleId { get; set; } = string.Empty;
    public int PermissionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ApplicationRole Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
