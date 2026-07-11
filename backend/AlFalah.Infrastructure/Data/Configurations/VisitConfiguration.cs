using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InstructorId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);

        // D-31: explicit Unicode + Arabic_CI_AS collation on every Arabic-text column.
        builder.Property(x => x.Subject).HasMaxLength(200).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.GradeClass).HasMaxLength(100).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.Notes).HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");

        // Phase 5: approval / reopen workflow — Arabic-text columns.
        builder.Property(x => x.ApprovedByUserId).HasMaxLength(450);
        builder.Property(x => x.ReopenedByUserId).HasMaxLength(450);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.ReopenReason).HasMaxLength(1000).IsUnicode(true).UseCollation("Arabic_CI_AS");

        // FK: School
        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: Instructor (the evaluated teacher)
        builder.HasOne(x => x.Instructor)
            .WithMany()
            .HasForeignKey(x => x.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: CreatedBy (Moderator / School Manager)
        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DeletedByUser)
            .WithMany()
            .HasForeignKey(x => x.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Phase 5: ApprovedBy / ReopenedBy — nullable, Restrict so historical approvers
        // can never be cascade-deleted out from under the audit trail.
        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(x => x.ReopenedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReopenedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // FK: RubricVersion (snapshot at creation)
        builder.HasOne(x => x.RubricVersion)
            .WithMany()
            .HasForeignKey(x => x.RubricVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Children
        builder.HasMany(x => x.Scores)
            .WithOne(s => s.Visit)
            .HasForeignKey(s => s.VisitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Analysis)
            .WithOne(a => a.Visit)
            .HasForeignKey<VisitAnalysis>(a => a.VisitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ViewLogs)
            .WithOne(v => v.Visit)
            .HasForeignKey(v => v.VisitId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.InstructorId);
        builder.HasIndex(x => x.RubricVersionId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.VisitDate);
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => new { x.SchoolId, x.Status });
        builder.HasIndex(x => new { x.SchoolId, x.InstructorId });
        builder.HasIndex(x => new { x.SchoolId, x.VisitDate });

        // Phase 5: index on ApprovedByUserId so manager-dashboard filters by approver stay fast.
        builder.HasIndex(x => x.ApprovedByUserId);
    }
}