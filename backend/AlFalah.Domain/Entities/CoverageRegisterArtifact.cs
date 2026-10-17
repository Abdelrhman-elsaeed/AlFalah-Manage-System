namespace AlFalah.Domain.Entities;

/// <summary>A frozen, dated QF71-B-2-1 issue. Corrections append a new artifact.</summary>
public sealed class CoverageRegisterArtifact
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public DateOnly LocalDate { get; set; }
    public int Revision { get; set; }
    public string SourceRevision { get; set; } = "";
    public string RendererVersion { get; set; } = "";
    public bool IsComplete { get; set; }
    public string SnapshotJson { get; set; } = "";
    public byte[] Pdf { get; set; } = [];
    public string Sha256 { get; set; } = "";
    public string FileName { get; set; } = "";
    public string CreatedByUserId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
