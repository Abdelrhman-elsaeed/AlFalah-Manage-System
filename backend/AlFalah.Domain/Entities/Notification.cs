using System.ComponentModel.DataAnnotations;
using AlFalah.Domain.Enums.StudentAffairs;

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
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public string? TemplateKey { get; set; }
    public Guid? CorrelationId { get; set; }
    public string? DeduplicationKey { get; set; }
    public int? StudentId { get; set; }
    public NotificationDeliveryStatus DeliveryStatus { get; set; } = NotificationDeliveryStatus.Pending;
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public bool RequiresApproval { get; set; }
    public bool IsSuppressed { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? SuppressedByUserId { get; set; }
    public DateTimeOffset? SuppressedAt { get; set; }
    public string? SuppressionReason { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public School? School { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
    public ApplicationUser? SuppressedByUser { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }
    public ApplicationUser? UpdatedByUser { get; set; }
    public ApplicationUser? DeletedByUser { get; set; }
}
