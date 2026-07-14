using System.ComponentModel.DataAnnotations;

namespace AlFalah.Domain.Entities;

/// <summary>
/// D-74 — One class label taught by an Instructor.
/// Bridge rows for the many-to-many between InstructorProfile (the teacher's
/// profile row, 1 per school) and the labels they teach (e.g. "3/1", "3/2").
///
/// Stored as a small dedicated table rather than a CSV column on
/// InstructorProfile so:
///  - the auto-fill dropdown on the visit form can fetch them with one indexed
///    query (no CSV parse),
///  - future ordering / soft-delete / per-class metadata stays cheap to add,
///  - the table mirrors the pattern used by ImprovementPlan / PlanFollowUp
///    for soft-deletable child rows.
/// </summary>
public class InstructorClass
{
    public int Id { get; set; }

    /// <summary>The owning InstructorProfile (1 row per school).</summary>
    public int InstructorProfileId { get; set; }

    /// <summary>
    /// Free-form class label (e.g. "3/1", "3/2", "1/أ"). Length-bounded; trimmed
    /// and Arabic_CI_AS on the DB side (see migration).
    /// </summary>
    [MaxLength(50)]
    public string ClassLabel { get; set; } = string.Empty;

    /// <summary>Display order within the teacher's list (preserves the order entered).</summary>
    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public InstructorProfile InstructorProfile { get; set; } = null!;
}
