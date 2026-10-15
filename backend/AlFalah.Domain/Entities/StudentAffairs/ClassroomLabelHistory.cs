namespace AlFalah.Domain.Entities.StudentAffairs;

/// <summary>A retained school/year name binding used only to resolve legacy references.</summary>
public sealed class ClassroomLabelHistory
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AcademicYearId { get; set; }
    public int ClassroomId { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Classroom Classroom { get; set; } = null!;
}
