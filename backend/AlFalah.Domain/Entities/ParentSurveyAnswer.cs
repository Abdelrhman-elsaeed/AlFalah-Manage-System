using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

public class ParentSurveyAnswer
{
    public int Id { get; set; }
    public int ParentSurveySubmissionId { get; set; }
    public int ParentSurveyItemId { get; set; }
    public string ItemTextSnapshot { get; set; } = string.Empty;
    public ParentSurveyRating SubmittedRating { get; set; }
    public ParentSurveyRating EffectiveRating { get; set; }
    public string? WeakReason { get; set; }
    public bool WasAutoAdjusted { get; set; }

    public ParentSurveySubmission Submission { get; set; } = null!;
    public ParentSurveyItem Item { get; set; } = null!;
}
