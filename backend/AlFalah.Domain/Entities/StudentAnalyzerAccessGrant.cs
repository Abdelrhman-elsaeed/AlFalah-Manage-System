namespace AlFalah.Domain.Entities;

/// <summary>
/// A school-manager-owned delegation that gives an active school user full
/// operational access to the student analyzer. Delegates can never grant access.
/// </summary>
public sealed class StudentAnalyzerAccessGrant
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string GrantedByUserId { get; set; } = string.Empty;
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public ApplicationUser GrantedByUser { get; set; } = null!;
    public ApplicationUser? DeletedByUser { get; set; }
}
