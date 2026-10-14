using AlFalah.Domain.Entities.Duty;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class DutyPdfArtifactConfiguration : IEntityTypeConfiguration<DutyPdfArtifact>
{
    public void Configure(EntityTypeBuilder<DutyPdfArtifact> b)
    {
        b.ToTable("DutyPdfArtifacts", t => { t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_DutyPdfArtifacts_Identity", "([DocumentId] IS NOT NULL AND [PlanId] IS NULL) OR ([DocumentId] IS NULL AND [PlanId] IS NOT NULL AND [PlanRevision] > 0)"); });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.SchoolId, x.Id });
        b.HasOne<DutySignedDocument>().WithMany().HasForeignKey(x => new { x.SchoolId, x.DocumentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<DutyPlan>().WithMany().HasForeignKey(x => new { x.SchoolId, x.PlanId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.DocumentId).IsUnique().HasFilter("[DocumentId] IS NOT NULL");
        b.HasIndex(x => new { x.SchoolId, x.PlanId, x.PlanRevision }).IsUnique().HasFilter("[PlanId] IS NOT NULL");
        b.Property(x => x.Hash).HasMaxLength(64); b.Property(x => x.ProvenanceHash).HasMaxLength(64);
        b.Property(x => x.RendererVersion).HasMaxLength(80); b.Property(x => x.FileName).HasMaxLength(512);
    }
}
