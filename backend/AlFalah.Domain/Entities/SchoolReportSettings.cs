namespace AlFalah.Domain.Entities;

/// <summary>
/// Stores report branding settings per school.
/// </summary>
public class SchoolReportSettings
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string? ReportHeaderText { get; set; }
    public string? ReportFooterText { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public bool ShowModeratorSignature { get; set; } = true;
    public bool ShowManagerSignature { get; set; } = true;
    public bool ShowQrCode { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public School School { get; set; } = null!;
}
