using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

public class ParentSurvey
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsTemplate { get; set; }
    public ParentSurveyStatus Status { get; set; } = ParentSurveyStatus.Draft;
    public string? PublicToken { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ICollection<ParentSurveyItem> Items { get; set; } = new List<ParentSurveyItem>();
    public ICollection<ParentSurveySubmission> Submissions { get; set; } = new List<ParentSurveySubmission>();
}
