using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

public sealed class TimetableAnalysisFinding
{
    public int Id { get; set; }
    public int AnalysisRunId { get; set; }
    public ViolationRuleCode RuleCode { get; set; }
    public ViolationSeverity Severity { get; set; }
    public string MessageAr { get; set; } = string.Empty;
    public int? ClassroomId { get; set; }
    public int? InstructorProfileId { get; set; }
    public int? SubjectId { get; set; }
    public TimetableDay? Day { get; set; }
    public int? Period { get; set; }
    public string? EvidenceJson { get; set; }
    public string? SuggestedRepairJson { get; set; }
    public bool IsOverridden { get; set; }
    public string? OverriddenByUserId { get; set; }
    public DateTimeOffset? OverriddenAt { get; set; }
    public string? OverrideReason { get; set; }
    public TimetableAnalysisRun AnalysisRun { get; set; } = null!;
    public Classroom? Classroom { get; set; }
    public InstructorProfile? InstructorProfile { get; set; }
    public SubjectDefinition? Subject { get; set; }
    public ApplicationUser? OverriddenByUser { get; set; }
}
