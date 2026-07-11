using System;
using System.Collections.Generic;

namespace AlFalah.Application.DTOs.ImprovementPlans;

public class ImprovementPlanDto
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string InstructorId { get; set; } = string.Empty;
    public string InstructorFullName { get; set; } = string.Empty;
    public int VisitId { get; set; }
    public int? DomainId { get; set; }
    public string? DomainNameAr { get; set; }
    public string Goal { get; set; } = string.Empty;
    public string Actions { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string SuccessIndicators { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByFullName { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsReadOnly { get; set; }
    
    public List<PlanFollowUpDto> FollowUps { get; set; } = new();
}

public class CreatePlanRequestDto
{
    public string InstructorId { get; set; } = string.Empty;
    public int VisitId { get; set; }
    public int? DomainId { get; set; }
    public string Goal { get; set; } = string.Empty;
    public string Actions { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string SuccessIndicators { get; set; } = string.Empty;
}

public class UpdatePlanRequestDto
{
    public string Goal { get; set; } = string.Empty;
    public string Actions { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string SuccessIndicators { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

public class PlanFollowUpDto
{
    public int Id { get; set; }
    public int ImprovementPlanId { get; set; }
    public DateTimeOffset FollowDate { get; set; }
    public string ProgressNote { get; set; } = string.Empty;
    public string? EvidenceNote { get; set; }
    public int? ProgressScore { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByFullName { get; set; } = string.Empty;
}

public class CreateFollowUpRequestDto
{
    public DateTimeOffset FollowDate { get; set; }
    public string ProgressNote { get; set; } = string.Empty;
    public string? EvidenceNote { get; set; }
    public int? ProgressScore { get; set; }
}

public class UpdateFollowUpRequestDto
{
    public DateTimeOffset FollowDate { get; set; }
    public string ProgressNote { get; set; } = string.Empty;
    public string? EvidenceNote { get; set; }
    public int? ProgressScore { get; set; }
}

public class ChartPointDto
{
    public DateTimeOffset FollowDate { get; set; }
    public int ProgressScore { get; set; }
}

public class PlanProgressDto
{
    public int? LatestProgressScore { get; set; }
    public string? LatestProgressColor { get; set; } // "success", "warning", "danger"
    public List<ChartPointDto> ChartData { get; set; } = new();
}

public class WeakDomainSuggestionDto
{
    public int DomainId { get; set; }
    public string DomainCode { get; set; } = string.Empty;
    public string DomainNameAr { get; set; } = string.Empty;
    public decimal AverageScore { get; set; }
    public string PrefilledGoal { get; set; } = string.Empty;
    public string PrefilledActions { get; set; } = string.Empty;
    public string PrefilledSuccessIndicators { get; set; } = string.Empty;
}
