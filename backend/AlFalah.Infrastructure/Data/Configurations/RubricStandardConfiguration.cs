using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class RubricStandardConfiguration : IEntityTypeConfiguration<RubricStandard>
{
    public void Configure(EntityTypeBuilder<RubricStandard> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.TextAr).IsRequired().HasMaxLength(1000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.SortOrder).IsRequired();

        // FK: standard → domain (configured in RubricDomainConfiguration via HasMany)

        // Indexes
        builder.HasIndex(x => x.RubricDomainId);
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => new { x.RubricDomainId, x.SortOrder });
        builder.HasIndex(x => new { x.RubricDomainId, x.Code });
    }
}
