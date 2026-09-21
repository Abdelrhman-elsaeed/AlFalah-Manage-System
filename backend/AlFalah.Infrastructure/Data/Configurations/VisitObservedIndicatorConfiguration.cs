using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class VisitObservedIndicatorConfiguration : IEntityTypeConfiguration<VisitObservedIndicator>
{
    public void Configure(EntityTypeBuilder<VisitObservedIndicator> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IndicatorTextArSnapshot)
            .IsRequired().HasMaxLength(500).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);

        // FK: observation → visit score (cascade from visit → scores → observations)
        builder.HasOne(x => x.VisitScore)
            .WithMany(s => s.ObservedIndicators)
            .HasForeignKey(x => x.VisitScoreId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: observation → rubric indicator (restrict — preserve rubric reference integrity)
        builder.HasOne(x => x.RubricIndicator)
            .WithMany()
            .HasForeignKey(x => x.RubricIndicatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // One observation per (VisitScoreId, RubricIndicatorId)
        builder.HasIndex(x => new { x.VisitScoreId, x.RubricIndicatorId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_VisitObservedIndicator_Score_Indicator");

        // Indexes
        builder.HasIndex(x => x.VisitScoreId);
        builder.HasIndex(x => x.RubricIndicatorId);
        builder.HasIndex(x => x.IsDeleted);
    }
}
