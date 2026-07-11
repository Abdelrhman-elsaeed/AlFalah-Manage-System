namespace AlFalah.Domain.Entities;

/// <summary>
/// Instructor (teacher) profile linked to an ApplicationUser and a specific school.
/// </summary>
public class InstructorProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int SchoolId { get; set; }
    public string? SubjectSpecialization { get; set; }
    public string? QualificationAr { get; set; }
    public string? QualificationEn { get; set; }
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
}
