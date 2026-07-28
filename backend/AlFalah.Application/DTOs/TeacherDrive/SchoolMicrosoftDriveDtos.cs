namespace AlFalah.Application.DTOs.TeacherDrive;

public sealed record SchoolMicrosoftDriveSettingsDto(
    int SchoolId, bool IsConfigured, bool IsEnabled, string? TenantId,
    string? SchoolMicrosoftEmail, string? DriveId, string? RootItemId,
    string? RootFolderDisplayName, DateTimeOffset? ConnectedAtUtc);

public sealed record ConfigureSchoolMicrosoftDriveRequest(
    string TenantId, string SchoolMicrosoftEmail, string DriveId,
    string RootItemId, string RootFolderDisplayName, bool IsEnabled);
