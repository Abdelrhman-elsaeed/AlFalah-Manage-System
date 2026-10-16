using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class TeachingAssignmentStatementConfiguration : IEntityTypeConfiguration<TeachingAssignmentStatement>
{
    public void Configure(EntityTypeBuilder<TeachingAssignmentStatement> b)
    {
        b.ToTable("TeachingAssignmentStatements"); b.HasKey(x => x.Id);
        b.Property(x => x.Scope).HasMaxLength(16);
        b.Property(x => x.RendererVersion).HasMaxLength(80);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.Property(x => x.FileName).HasMaxLength(240);
        b.Property(x => x.CreatedByUserId).HasMaxLength(450);
        b.HasIndex(x => new { x.SchoolId, x.TimetableSetupProfileId, x.Revision, x.Scope, x.RendererVersion }).IsUnique();
        b.HasOne(x => x.Setup).WithMany().HasForeignKey(x => new { x.SchoolId, x.TimetableSetupProfileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
