namespace AlFalah.Domain.Entities;

/// <summary>Administrator-owned OneDrive root mapping. It is never supplied by a teacher client.</summary>
public class TeacherDriveFolder
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public int SchoolId { get; set; }
    public string DriveId { get; set; } = string.Empty;
    public string RootItemId { get; set; } = string.Empty;
    public string FolderDisplayName { get; set; } = string.Empty;
    public string? RootWebUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public InstructorProfile Teacher { get; set; } = null!;
    public School School { get; set; } = null!;
}
