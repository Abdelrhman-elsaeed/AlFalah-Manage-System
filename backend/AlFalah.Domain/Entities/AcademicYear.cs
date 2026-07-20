namespace AlFalah.Domain.Entities;

/// <summary>A reportable school year. Evidence is always isolated by this key.</summary>
public class AcademicYear
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public bool IsActive { get; set; }
}
