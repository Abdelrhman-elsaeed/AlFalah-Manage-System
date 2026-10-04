namespace AlFalah.Domain.Entities.Storage;

public sealed class SelfEvaluationTemplate
{
    public int Version { get; set; }
    public string Name { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public string SHA256 { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SchoolEvaluationScope
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AcademicYearId { get; set; }
    public int TemplateVersion { get; set; }
    public string CreatedByUserId { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ManualEvaluation : IStorageRecord
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AcademicYearId { get; set; }
    public int TemplateVersion { get; set; }
    public string ScopeCode { get; set; } = "school";
    public string Judgment { get; set; } = "";
    public decimal? Value { get; set; }
    public string Reason { get; set; } = "";
    public string EvaluatorUserId { get; set; } = "";
    public string EvaluatorName { get; set; } = "";
    public DateTimeOffset EvaluatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Revision { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}

public sealed class ManualEvaluationRevision
{
    public int Id { get; set; }
    public int ManualEvaluationId { get; set; }
    public int SchoolId { get; set; }
    public int Revision { get; set; }
    public string SnapshotJson { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RequirementFollowUpRevision
{
    public int Id { get; set; }
    public int RequirementId { get; set; }
    public int SchoolId { get; set; }
    public string ActorUserId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string OldValuesJson { get; set; } = "";
    public string NewValuesJson { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
