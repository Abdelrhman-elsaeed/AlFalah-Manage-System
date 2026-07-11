using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class VisitAnalysisConfiguration : IEntityTypeConfiguration<VisitAnalysis>
{
    public void Configure(EntityTypeBuilder<VisitAnalysis> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PerformanceLevelAr).IsRequired().HasMaxLength(100).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.StrengthsJson).IsRequired().HasColumnType("nvarchar(max)").IsUnicode(true);
        builder.Property(x => x.ImprovementAreasJson).IsRequired().HasColumnType("nvarchar(max)").IsUnicode(true);
        builder.Property(x => x.PriorityStandardsJson).IsRequired().HasColumnType("nvarchar(max)").IsUnicode(true);
        builder.Property(x => x.OverallScore).HasColumnType("decimal(6,3)");
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);

        // FK: VisitId is already mapped on VisitConfiguration (HasOne + WithOne).

        // One analysis per visit
        builder.HasIndex(x => x.VisitId)
            .IsUnique()
            .HasDatabaseName("UX_VisitAnalysis_Visit");

        // Indexes
        builder.HasIndex(x => x.IsDeleted);
    }
}