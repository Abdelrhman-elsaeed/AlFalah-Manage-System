using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// An improvement plan (خطة تحسين) created for a school classroom visit.
/// Can be associated with a specific rubric domain.
/// </summary>
public class ImprovementPlan
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    
    /// <summary>The evaluated teacher (user Id).</summary>
    public string InstructorId { get; set; } = string.Empty;
    public int VisitId { get; set; }
    
    /// <summary>Optional reference to the Rubric Domain that needs improvement.</summary>
    public int? DomainId { get; set; }

    public string Goal { get; set; } = string.Empty;
    public string Actions { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string SuccessIndicators { get; set; } = string.Empty;
    
    public PlanStatus Status { get; set; } = PlanStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedByUserId { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation properties
    public School School { get; set; } = null!;
    public ApplicationUser Instructor { get; set; } = null!;
    public Visit Visit { get; set; } = null!;
    public RubricDomain? Domain { get; set; }
    
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? UpdatedByUser { get; set; }
    public ApplicationUser? DeletedByUser { get; set; }

    public ICollection<PlanFollowUp> FollowUps { get; set; } = new List<PlanFollowUp>();
}
