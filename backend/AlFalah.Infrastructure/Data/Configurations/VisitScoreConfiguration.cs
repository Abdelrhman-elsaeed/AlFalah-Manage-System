using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class VisitScoreConfiguration : IEntityTypeConfiguration<VisitScore>
{
    public void Configure(EntityTypeBuilder<VisitScore> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EvidenceNote).HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);

        // FK relationships already declared on Visit (HasMany). Just map Score/EvidenceNote.

        // Indexes
        builder.HasIndex(x => x.VisitId);
        builder.HasIndex(x => x.RubricStandardId);
        builder.HasIndex(x => x.IsDeleted);

        // One VisitScore per (VisitId, RubricStandardId)
        builder.HasIndex(x => new { x.VisitId, x.RubricStandardId })
            .IsUnique()
            .HasDatabaseName("UX_VisitScore_Visit_Standard");
    }
}