using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class VisitDomainAverageConfiguration : IEntityTypeConfiguration<VisitDomainAverage>
{
    public void Configure(EntityTypeBuilder<VisitDomainAverage> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DomainCode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.DomainNameAr).IsRequired().HasMaxLength(300).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.AverageScore).HasColumnType("decimal(6,3)");
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);

        // FK relationships already declared on VisitAnalysis (HasMany).

        // Indexes
        builder.HasIndex(x => x.VisitAnalysisId);
        builder.HasIndex(x => x.RubricDomainId);
        builder.HasIndex(x => x.IsDeleted);
    }
}