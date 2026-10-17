using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class CoverageRegisterConfiguration : IEntityTypeConfiguration<CoverageRegisterArtifact>
{
    public void Configure(EntityTypeBuilder<CoverageRegisterArtifact> b)
    {
        b.ToTable("CoverageRegisterArtifacts"); b.HasKey(x=>x.Id);
        b.HasIndex(x=>new{x.SchoolId,x.LocalDate,x.Revision}).IsUnique();
        b.HasIndex(x=>new{x.SchoolId,x.LocalDate,x.SourceRevision,x.RendererVersion}).IsUnique();
        b.Property(x=>x.SourceRevision).HasMaxLength(64); b.Property(x=>x.RendererVersion).HasMaxLength(80);
        b.Property(x=>x.Sha256).HasMaxLength(64); b.Property(x=>x.FileName).HasMaxLength(240); b.Property(x=>x.CreatedByUserId).HasMaxLength(450);
        b.HasOne<School>().WithMany().HasForeignKey(x=>x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t=>t.HasCheckConstraint("CK_CoverageRegister_Revision","[Revision] > 0"));
    }
}
