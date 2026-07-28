namespace AlFalah.Domain.Entities;

public class ParentSurveyItem
{
    public int Id { get; set; }
    public int ParentSurveyId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }

    public ParentSurvey ParentSurvey { get; set; } = null!;
    public ICollection<ParentSurveyAnswer> Answers { get; set; } = new List<ParentSurveyAnswer>();
}
