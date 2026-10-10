namespace AlFalah.Domain.Entities;

/// <summary>A school-owned reusable phrase. Visits copy its text at selection time.</summary>
public sealed class VisitFeedbackTemplate
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int Kind { get; set; } // 1 = strength, 2 = improvement
    public string Text { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public School School { get; set; } = null!;
}
