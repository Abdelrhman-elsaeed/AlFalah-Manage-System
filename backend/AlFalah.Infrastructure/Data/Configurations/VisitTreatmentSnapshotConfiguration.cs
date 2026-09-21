using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class VisitTreatmentSnapshotConfiguration : IEntityTypeConfiguration<VisitTreatmentSnapshot>
{
    public void Configure(EntityTypeBuilder<VisitTreatmentSnapshot> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DomainNameArSnapshot)
            .IsRequired().HasMaxLength(300).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.Goal)
            .IsRequired().HasMaxLength(1000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.Actions)
            .IsRequired().HasMaxLength(4000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.SuccessIndicators)
            .IsRequired().HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);

        // FK: treatment → visit (cascade from visit deletion)
        builder.HasOne(x => x.Visit)
            .WithMany(v => v.TreatmentSnapshots)
            .HasForeignKey(x => x.VisitId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: treatment → rubric domain (nullable, restrict to preserve rubric integrity)
        builder.HasOne(x => x.RubricDomain)
            .WithMany()
            .HasForeignKey(x => x.RubricDomainId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Indexes
        builder.HasIndex(x => x.VisitId);
        builder.HasIndex(x => new { x.VisitId, x.SortOrder });
        builder.HasIndex(x => x.IsDeleted);
    }
}
