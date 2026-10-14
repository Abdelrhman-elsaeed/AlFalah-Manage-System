namespace AlFalah.Domain.Entities.Duty;

/// <summary>Write-once rendered bytes, separate from D2's write-once signing evidence.</summary>
public sealed class DutyPdfArtifact
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int? DocumentId { get; set; }
    public int? PlanId { get; set; }
    public int PlanRevision { get; set; }
    public string ProvenanceJson { get; set; } = "";
    public string ProvenanceHash { get; set; } = "";
    public string RendererVersion { get; set; } = "";
    public string FileName { get; set; } = "";
    public byte[] Bytes { get; set; } = [];
    public string Hash { get; set; } = "";
    public DateTimeOffset PreparedAt { get; set; }
}
