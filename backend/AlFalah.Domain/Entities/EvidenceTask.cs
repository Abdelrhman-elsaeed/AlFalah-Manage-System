namespace AlFalah.Domain.Entities;

/// <summary>
/// A fixed column in the teacher evidence matrix. IDs are intentionally stable
/// and are seeded from the approved manual report template.
/// </summary>
public class EvidenceTask
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int CategorySortOrder { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class EvidenceTaskCategories
{
    public const string CurriculumPack = "حقيبة المنهج";
    public const string StudentPack = "حقيبة الطالب";
    public const string Assessment = "التقويم";
    public const string RemedialPlans = "الخطط العلاجية";
    public const string Enrichment = "المواد الإثرائية";
    public const string ProfessionalCommunities = "المجتمعات المهنية";
    public const string CurriculumVitae = "السيرة الذاتية";
}
