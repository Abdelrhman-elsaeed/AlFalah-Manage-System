namespace AlFalah.Domain.Entities;

/// <summary>
/// Represents a versioned snapshot of the Al-Falah evaluation rubric.
/// Only ONE version may be active (IsActive=true) at a time.
/// A filtered unique index in the DB enforces this constraint at the database level.
/// Copy-on-write: editing creates a new version; existing visits keep their original RubricVersionId.
/// </summary>
public class RubricVersion
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    public bool IsActive { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>FK to the ApplicationUser who created this version. Nullable for seed data.</summary>
    public string? CreatedByUserId { get; set; }

    /// <summary>Optional notes describing what changed in this version.</summary>
    public string? Notes { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public ApplicationUser? CreatedByUser { get; set; }
    public ICollection<RubricDomain> Domains { get; set; } = new List<RubricDomain>();
}
