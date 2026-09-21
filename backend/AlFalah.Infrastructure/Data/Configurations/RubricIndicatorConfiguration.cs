using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class RubricIndicatorConfiguration : IEntityTypeConfiguration<RubricIndicator>
{
    public void Configure(EntityTypeBuilder<RubricIndicator> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(30);
        builder.Property(x => x.TextAr).IsRequired().HasMaxLength(500).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);

        // FK: indicator → standard (configured also from RubricStandard side)
        builder.HasOne(x => x.Standard)
            .WithMany(s => s.Indicators)
            .HasForeignKey(x => x.RubricStandardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.RubricStandardId);
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => new { x.RubricStandardId, x.SortOrder });
        builder.HasIndex(x => new { x.RubricStandardId, x.Code })
            .IsUnique()
            .HasDatabaseName("UX_RubricIndicator_Standard_Code");
    }
}
