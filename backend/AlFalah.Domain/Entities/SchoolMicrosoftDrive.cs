namespace AlFalah.Domain.Entities;

/// <summary>
/// The school-owned Microsoft 365 drive that contains all teacher evidence folders.
/// This does not replace individual <see cref="TeacherDriveFolder"/> mappings.
/// </summary>
public sealed class SchoolMicrosoftDrive
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string SchoolMicrosoftEmail { get; set; } = string.Empty;
    public string DriveId { get; set; } = string.Empty;
    public string RootItemId { get; set; } = string.Empty;
    public string RootFolderDisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset ConnectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public School School { get; set; } = null!;
}
