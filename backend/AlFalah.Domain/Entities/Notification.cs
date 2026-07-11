namespace AlFalah.Domain.Entities;

/// <summary>
/// In-app notification sent to a specific user.
/// </summary>
public class Notification
{
    public int Id { get; set; }
    public int? SchoolId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public School? School { get; set; }
}
