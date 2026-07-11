using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class PlanFollowUpConfiguration : IEntityTypeConfiguration<PlanFollowUp>
{
    public void Configure(EntityTypeBuilder<PlanFollowUp> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);

        // D-31: Arabic Unicode and collation.
        builder.Property(x => x.ProgressNote).IsRequired().HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.EvidenceNote).HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS").IsRequired(false);

        // FK: CreatedByUser
        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: UpdatedByUser
        builder.HasOne(x => x.UpdatedByUser)
            .WithMany()
            .HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // FK: DeletedByUser
        builder.HasOne(x => x.DeletedByUser)
            .WithMany()
            .HasForeignKey(x => x.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Indexes
        builder.HasIndex(x => x.ImprovementPlanId);
        builder.HasIndex(x => x.FollowDate);
        builder.HasIndex(x => x.IsDeleted);
    }
}
