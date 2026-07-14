using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// Instructor (teacher) profile linked to an ApplicationUser and a specific school.
/// </summary>
public class InstructorProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int SchoolId { get; set; }

    /// <summary>The subject the teacher teaches (e.g. الرياضيات). Arabic_CI_AS on DB.</summary>
    public string? SubjectSpecialization { get; set; }

    /// <summary>
    /// D-74 — School stage as set on the teacher. Stored explicitly on the
    /// profile (rather than only on the school) so a single teacher may
    /// surface their stage even if a future per-teacher-stage is needed.
    /// Defaults to the school's stage on insert if null.
    /// </summary>
    public SchoolStage? Stage { get; set; }

    public string? QualificationAr { get; set; }
    public string? QualificationEn { get; set; }

    /// <summary>Employee number / الرقم الوظيفي. NVARCHAR(50).</summary>
    public string? EmployeeNumber { get; set; }

    public DateOnly? HireDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public School School { get; set; } = null!;
    public ICollection<InstructorClass> Classes { get; set; } = new List<InstructorClass>();
}
