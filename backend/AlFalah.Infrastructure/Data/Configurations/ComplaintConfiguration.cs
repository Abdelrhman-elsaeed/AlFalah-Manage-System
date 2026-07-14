using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

/// <summary>Phase 8 — Complaint EF configuration (Arabic collation, FKs, indexes).</summary>
public class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InstructorUserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.ModeratorUserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.HandledByUserId).HasMaxLength(450);
        builder.Property(x => x.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);

        // D-30/D-31 carry-over: Arabic Unicode + collation on Arabic-text columns.
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(200).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.Body).IsRequired().HasMaxLength(4000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.ResolutionNote).HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.VisitReopenReason).HasMaxLength(1000).IsUnicode(true).UseCollation("Arabic_CI_AS");

        builder.Property(x => x.Status).HasConversion<int>();

        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Visit)
            .WithMany()
            .HasForeignKey(x => x.VisitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Instructor)
            .WithMany()
            .HasForeignKey(x => x.InstructorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Moderator)
            .WithMany()
            .HasForeignKey(x => x.ModeratorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HandledByUser)
            .WithMany()
            .HasForeignKey(x => x.HandledByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Indexes per PHASE-08 spec.
        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.VisitId);
        builder.HasIndex(x => x.InstructorUserId);
        builder.HasIndex(x => x.ModeratorUserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => new { x.SchoolId, x.Status });
    }
}
