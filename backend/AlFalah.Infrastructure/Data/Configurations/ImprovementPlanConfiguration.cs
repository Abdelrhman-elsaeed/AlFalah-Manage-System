using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class ImprovementPlanConfiguration : IEntityTypeConfiguration<ImprovementPlan>
{
    public void Configure(EntityTypeBuilder<ImprovementPlan> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InstructorId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);

        // D-31: Arabic Unicode and collation.
        builder.Property(x => x.Goal).IsRequired().HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.Actions).IsRequired().HasMaxLength(4000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.SuccessIndicators).IsRequired().HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");

        builder.Property(x => x.Status).HasConversion<int>();

        // FK: School
        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: Instructor
        builder.HasOne(x => x.Instructor)
            .WithMany()
            .HasForeignKey(x => x.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: Visit
        builder.HasOne(x => x.Visit)
            .WithMany()
            .HasForeignKey(x => x.VisitId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: Domain (nullable)
        builder.HasOne(x => x.Domain)
            .WithMany()
            .HasForeignKey(x => x.DomainId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

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

        // Children: follow-ups are cascade deleted ONLY if the plan is hard deleted (e.g. physically).
        // For soft delete, the application manually soft deletes follow-ups.
        builder.HasMany(x => x.FollowUps)
            .WithOne(f => f.ImprovementPlan)
            .HasForeignKey(f => f.ImprovementPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.InstructorId);
        builder.HasIndex(x => x.VisitId);
        builder.HasIndex(x => x.DomainId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => new { x.SchoolId, x.Status });
    }
}
