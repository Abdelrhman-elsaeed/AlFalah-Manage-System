using Microsoft.AspNetCore.Identity;

namespace AlFalah.Domain.Entities;

/// <summary>
/// Extends ASP.NET Core IdentityUser with Al-Falah specific fields.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Computed or stored display name. Typically FirstName + " " + LastName.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Preferred language: "ar" or "en". Defaults to Arabic.
    /// </summary>
    public string PreferredLanguage { get; set; } = "ar";

    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Soft delete (Phase 2)
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public ApplicationUser? DeletedByUser { get; set; }
    public ICollection<UserSchoolRole> UserSchoolRoles { get; set; } = new List<UserSchoolRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
