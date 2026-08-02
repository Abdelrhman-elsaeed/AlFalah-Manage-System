namespace AlFalah.Domain.Entities;

/// <summary>School-manager delegation that lets one moderator fully manage timetables.</summary>
public class TimetableEditorGrant
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string ModeratorUserId { get; set; } = string.Empty;
    public string GrantedByUserId { get; set; } = string.Empty;
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public ApplicationUser ModeratorUser { get; set; } = null!;
    public ApplicationUser GrantedByUser { get; set; } = null!;
}
