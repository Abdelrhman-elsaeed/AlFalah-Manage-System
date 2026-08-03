using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// A persisted AI analysis. The exact selected grant/deduction values are kept
/// as JSON so the original report can be reconstructed without reparsing a file.
/// </summary>
public sealed class StudentAnalyzerReport
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int SourceFileId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public decimal GrantTotal { get; set; }
    public decimal DeductionTotal { get; set; }
    public string SelectedDataJson { get; set; } = string.Empty;
    public string AnalysisText { get; set; } = string.Empty;
    public StudentAnalyzerProvider Provider { get; set; }
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = "student-analyzer-v3.0-verbatim";
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public StudentAnalyzerSourceFile SourceFile { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? DeletedByUser { get; set; }
}
