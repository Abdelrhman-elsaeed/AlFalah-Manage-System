namespace AlFalah.Domain.Entities;

public class ParentSurveySubmission
{
    public int Id { get; set; }
    public int ParentSurveyId { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    public ParentSurvey ParentSurvey { get; set; } = null!;
    public ICollection<ParentSurveyAnswer> Answers { get; set; } = new List<ParentSurveyAnswer>();
}
