using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// Represents a school in the Al-Falah organization.
/// Each school is independent and scoped by SchoolId in all related data.
/// </summary>
public class School
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SchoolStage Stage { get; set; }
    public string City { get; set; } = string.Empty;
    public string? LocationDetails { get; set; }
    public int? SchoolLocationId { get; set; }

    /// <summary>
    /// FK to ApplicationUser. Nullable at DB level, required by business before activation.
    /// </summary>
    public string? ManagerUserId { get; set; }

    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Soft delete (Phase 2)
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public ApplicationUser? Manager { get; set; }
    public ApplicationUser? DeletedByUser { get; set; }
    public SchoolReportSettings? ReportSettings { get; set; }
    public SchoolGoogleDrive? GoogleDrive { get; set; }
    public SchoolLocation? Location { get; set; }
    public ICollection<UserSchoolRole> UserSchoolRoles { get; set; } = new List<UserSchoolRole>();
}
