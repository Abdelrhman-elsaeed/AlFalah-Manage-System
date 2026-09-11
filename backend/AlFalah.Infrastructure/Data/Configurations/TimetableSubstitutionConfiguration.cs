using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class TimetableSubstitutionConfiguration : IEntityTypeConfiguration<TimetableSubstitution>, IEntityTypeConfiguration<TimetableSubstitutionMovement>
{
    public void Configure(EntityTypeBuilder<TimetableSubstitution> b)
    {
        b.ToTable("TimetableSubstitutions"); b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.SchoolId, x.Id });
        b.HasIndex(x => new { x.SchoolId, x.RequestId }).IsUnique();
        b.HasIndex(x => new { x.SchoolTimetableId, x.LocalDate });
        b.Property(x => x.ProposalId).HasMaxLength(64);
        b.Property(x => x.Kind).HasMaxLength(20);
        b.Property(x => x.OverrideReason).HasMaxLength(1000);
        b.HasOne(x => x.Timetable).WithMany().HasForeignKey(x => new { x.SchoolId, x.SchoolTimetableId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Version).WithMany().HasForeignKey(x => x.SchoolTimetableVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<TimetableSubstitutionMovement> b)
    {
        b.ToTable("TimetableSubstitutionMovements"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.TimetableSubstitutionId, x.SchoolTimetableEntryId }).IsUnique();
        b.HasOne(x => x.Substitution).WithMany(x => x.Movements).HasForeignKey(x => new { x.SchoolId, x.TimetableSubstitutionId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SchoolTimetableEntry>().WithMany().HasForeignKey(x => new { x.SchoolId, x.SchoolTimetableEntryId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => new { x.SchoolId, x.FromTeacherId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => new { x.SchoolId, x.ToTeacherId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
