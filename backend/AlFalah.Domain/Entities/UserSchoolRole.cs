using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// Assigns a user to a school with a specific role.
/// A moderator can be assigned to multiple schools (multiple rows).
/// A school manager is assigned to exactly one school.
/// </summary>
public class UserSchoolRole
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int SchoolId { get; set; }
    public string RoleId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedByUserId { get; set; }

    // Soft delete (Phase 2)
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public School School { get; set; } = null!;
    public ApplicationRole Role { get; set; } = null!;
    public ApplicationUser? CreatedByUser { get; set; }
    public ApplicationUser? UpdatedByUser { get; set; }
    public ApplicationUser? DeletedByUser { get; set; }
}
