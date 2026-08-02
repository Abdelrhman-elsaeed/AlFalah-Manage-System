namespace AlFalah.Domain.Entities;

/// <summary>
/// The per-teacher permission grant: exactly one Google Drive folder a teacher may browse
/// and upload into. Administrator-owned — a teacher client never supplies or receives
/// <see cref="DriveId"/> or <see cref="RootItemId"/>.
///
/// Because the application reaches Drive with the school credential rather than each
/// teacher's own account, this row IS the access-control boundary. Every request
/// re-resolves it and proves the target item is the root itself or a descendant of it.
/// </summary>
public class TeacherDriveFolder
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public int SchoolId { get; set; }

    /// <summary>Google shared-drive id, or empty when the tree lives in the credential owner's My Drive.</summary>
    public string DriveId { get; set; } = string.Empty;

    /// <summary>Google folder id granted to this teacher. Always the school root or a descendant of it.</summary>
    public string RootItemId { get; set; } = string.Empty;

    public string FolderDisplayName { get; set; } = string.Empty;
    public string? RootWebUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public InstructorProfile Teacher { get; set; } = null!;
    public School School { get; set; } = null!;
}
