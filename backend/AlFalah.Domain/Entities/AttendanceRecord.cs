using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// One staff member's attendance for one school workday. The unique school,
/// user, and date tuple makes an edit replace the day's result rather than
/// creating duplicate attendance rows.
/// </summary>
public class AttendanceRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateOnly AttendanceDate { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public School School { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public ApplicationUser RecordedByUser { get; set; } = null!;
}
