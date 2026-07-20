using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class EvidenceTaskConfiguration : IEntityTypeConfiguration<EvidenceTask>
{
    public void Configure(EntityTypeBuilder<EvidenceTask> builder)
    {
        builder.ToTable("EvidenceTasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(256).IsUnicode(true).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(128).IsUnicode(true).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.CategorySortOrder, x.SortOrder });
        builder.HasData(Tasks());
    }

    private static EvidenceTask[] Tasks() =>
    [
        Task(1, "CV-01", "البيانات الأساسية", EvidenceTaskCategories.CurriculumVitae, 1, 1),
        Task(2, "CV-02", "الإنجازات الشخصية", EvidenceTaskCategories.CurriculumVitae, 1, 2),
        Task(3, "CV-03", "التكريمات", EvidenceTaskCategories.CurriculumVitae, 1, 3),
        Task(4, "CV-04", "الخبرات", EvidenceTaskCategories.CurriculumVitae, 1, 4),
        Task(5, "CV-05", "الدورات التدريبية", EvidenceTaskCategories.CurriculumVitae, 1, 5),
        Task(6, "CV-06", "الرخصة المهنية", EvidenceTaskCategories.CurriculumVitae, 1, 6),
        Task(7, "CV-07", "الشهادات التدريبية", EvidenceTaskCategories.CurriculumVitae, 1, 7),
        Task(8, "CV-08", "ملف إنجاز المعلم", EvidenceTaskCategories.CurriculumVitae, 1, 8),

        Task(9, "PC-01", "تبادل الزيارات", EvidenceTaskCategories.ProfessionalCommunities, 2, 1),
        Task(10, "PC-02", "الخبرات المهنية", EvidenceTaskCategories.ProfessionalCommunities, 2, 2),
        Task(11, "PC-03", "مبادرات المعلم", EvidenceTaskCategories.ProfessionalCommunities, 2, 3),

        Task(12, "EN-01", "الأنشطة الصفية", EvidenceTaskCategories.Enrichment, 3, 1),
        Task(13, "EN-02", "تصميمات ورسومات", EvidenceTaskCategories.Enrichment, 3, 2),
        Task(14, "EN-03", "عروض تقديمية", EvidenceTaskCategories.Enrichment, 3, 3),
        Task(15, "EN-04", "مواقع ومنصات", EvidenceTaskCategories.Enrichment, 3, 4),
        Task(16, "EN-05", "أوراق العمل", EvidenceTaskCategories.Enrichment, 3, 5),

        Task(17, "RP-01", "الفاقد التعليمي", EvidenceTaskCategories.RemedialPlans, 4, 1),
        Task(18, "RP-02", "خطة الطلاب المتعثرين والضعاف", EvidenceTaskCategories.RemedialPlans, 4, 2),
        Task(19, "RP-03", "خطة الطلاب المتفوقين والموهوبين", EvidenceTaskCategories.RemedialPlans, 4, 3),

        Task(20, "AS-01", "اختبارات قصيرة لكل وحدة", EvidenceTaskCategories.Assessment, 5, 1),
        Task(21, "AS-02", "بحوث ومشاريع للطلاب", EvidenceTaskCategories.Assessment, 5, 2),
        Task(22, "AS-03", "تحليل النتائج", EvidenceTaskCategories.Assessment, 5, 3),

        Task(23, "SP-01", "تحفيز الطلاب وتشجيعهم", EvidenceTaskCategories.StudentPack, 6, 1),
        Task(24, "SP-02", "سجل المتابعة للطلاب", EvidenceTaskCategories.StudentPack, 6, 2),
        Task(25, "SP-03", "شواهد من التواصل الأسري", EvidenceTaskCategories.StudentPack, 6, 3),
        Task(26, "SP-04", "عينة من أنشطة الطالب", EvidenceTaskCategories.StudentPack, 6, 4),
        Task(27, "SP-05", "كشوف رصد الدرجات", EvidenceTaskCategories.StudentPack, 6, 5),
        Task(28, "SP-06", "ملفات إنجاز الطالب", EvidenceTaskCategories.StudentPack, 6, 6),

        Task(29, "CP-01", "استراتيجيات التعلم النشط وشواهد", EvidenceTaskCategories.CurriculumPack, 7, 1),
        Task(30, "CP-02", "الخطة الأسبوعية لكل فصل دراسي", EvidenceTaskCategories.CurriculumPack, 7, 2),
        Task(31, "CP-03", "توزيع المنهج", EvidenceTaskCategories.CurriculumPack, 7, 3)
    ];

    private static EvidenceTask Task(int id, string code, string nameAr, string category, int categorySortOrder, int sortOrder) => new()
    {
        Id = id,
        Code = code,
        NameAr = nameAr,
        Category = category,
        CategorySortOrder = categorySortOrder,
        SortOrder = sortOrder,
        IsActive = true
    };
}
