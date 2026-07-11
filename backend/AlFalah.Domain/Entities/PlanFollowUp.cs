namespace AlFalah.Domain.Entities;

/// <summary>
/// A progress follow-up (متابعة) for an improvement plan.
/// </summary>
public class PlanFollowUp
{
    public int Id { get; set; }
    public int ImprovementPlanId { get; set; }
    public DateTimeOffset FollowDate { get; set; } = DateTimeOffset.UtcNow;
    public string ProgressNote { get; set; } = string.Empty;
    public string? EvidenceNote { get; set; }
    public int? ProgressScore { get; set; } // 0..100

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedByUserId { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    // Navigation properties
    public ImprovementPlan ImprovementPlan { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? UpdatedByUser { get; set; }
    public ApplicationUser? DeletedByUser { get; set; }
}
