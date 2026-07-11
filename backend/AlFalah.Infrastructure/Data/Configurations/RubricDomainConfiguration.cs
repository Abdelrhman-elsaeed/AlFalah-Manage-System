using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class RubricDomainConfiguration : IEntityTypeConfiguration<RubricDomain>
{
    public void Configure(EntityTypeBuilder<RubricDomain> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(300).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.SortOrder).IsRequired();

        // FK: domain → version (configured in RubricVersionConfiguration via HasMany)

        // Indexes
        builder.HasIndex(x => x.RubricVersionId);
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => new { x.RubricVersionId, x.SortOrder });
        builder.HasIndex(x => new { x.RubricVersionId, x.Code });

        // One-to-many: domain → standards
        builder.HasMany(x => x.Standards)
            .WithOne(s => s.Domain)
            .HasForeignKey(s => s.RubricDomainId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
