namespace AlFalah.Domain.Entities;

/// <summary>Immutable PDF and display snapshot; separate from timetable/signature/Drive artifacts.</summary>
public sealed class TeachingAssignmentStatement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SchoolId { get; set; }
    public int TimetableSetupProfileId { get; set; }
    public int Revision { get; set; }
    public string Scope { get; set; } = "all";
    public string RendererVersion { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public byte[] Pdf { get; set; } = [];
    public string Sha256 { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public TimetableSetupProfile Setup { get; set; } = null!;
}
