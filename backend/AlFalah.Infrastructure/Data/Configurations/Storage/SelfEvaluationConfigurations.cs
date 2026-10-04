using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations.Storage;

public sealed class RequirementReadinessConfiguration : IEntityTypeConfiguration<EvidenceRequirement>
{
    public void Configure(EntityTypeBuilder<EvidenceRequirement> b)
    {
        b.Property(x => x.SourceKey).HasMaxLength(100);
        b.Property(x => x.SourceSHA256).HasMaxLength(64);
        b.Property(x => x.ReferencePath).HasMaxLength(2048);
        b.Property(x => x.CompletionAction).HasMaxLength(1500);
        b.Property(x => x.ImportanceReason).HasMaxLength(1000);
        b.Property(x => x.FollowUpNote).HasMaxLength(1000);
        b.Property(x => x.FollowUpStatus).HasMaxLength(30);
        b.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.TemplateVersion, x.IsActive, x.IsMandatory, x.StandardCode });
        b.ToTable("EvidenceRequirements", t => {
            t.HasTrigger("TR_EvidenceRequirements_S4History");
            t.HasCheckConstraint("CK_Requirements_FollowUp", "[FollowUpStatus] IN ('NotStarted','InProgress','ReadyForReview')");
        });
    }
}
public sealed class SelfEvaluationTemplateConfiguration : IEntityTypeConfiguration<SelfEvaluationTemplate>
{
    public void Configure(EntityTypeBuilder<SelfEvaluationTemplate> b)
    {
        b.ToTable("SelfEvaluationTemplates", t => t.HasTrigger("TR_SelfEvaluationTemplates_Immutable"));
        b.HasKey(x => x.Version); b.Property(x => x.Version).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(300); b.Property(x => x.SHA256).HasMaxLength(64);
        var template = AlFalah.Application.Storage.ReadinessService.BuiltInTemplate();
        b.HasData(new SelfEvaluationTemplate { Version = 1, Name = template.Name,
            SHA256 = AlFalah.Application.Storage.ReadinessService.TemplateHash(template),
            SnapshotJson = System.Text.Json.JsonSerializer.Serialize(template, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            CreatedAtUtc = DateTimeOffset.Parse("2026-10-04T00:00:00Z") });
    }
}
public sealed class SchoolEvaluationScopeConfiguration : IEntityTypeConfiguration<SchoolEvaluationScope>
{
    public void Configure(EntityTypeBuilder<SchoolEvaluationScope> b)
    {
        b.ToTable("SchoolEvaluationScopes", t => t.HasTrigger("TR_SchoolEvaluationScopes_Immutable"));
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.TemplateVersion }).IsUnique();
        b.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SelfEvaluationTemplate>().WithMany().HasForeignKey(x => x.TemplateVersion).OnDelete(DeleteBehavior.Restrict);
        StorageConfiguration.User(b, nameof(SchoolEvaluationScope.CreatedByUserId));
    }
}
public sealed class ManualEvaluationConfiguration : IEntityTypeConfiguration<ManualEvaluation>
{
    public void Configure(EntityTypeBuilder<ManualEvaluation> b)
    {
        StorageConfiguration.Record(b, "ManualEvaluations");
        b.Property(x => x.ScopeCode).HasMaxLength(20); b.Property(x => x.Judgment).HasMaxLength(300);
        b.Property(x => x.Reason).HasMaxLength(1000); b.Property(x => x.EvaluatorName).HasMaxLength(300);
        b.Property(x => x.Value).HasPrecision(9, 2);
        StorageConfiguration.User(b, nameof(ManualEvaluation.EvaluatorUserId));
        b.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SelfEvaluationTemplate>().WithMany().HasForeignKey(x => x.TemplateVersion).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.TemplateVersion, x.ScopeCode }).IsUnique();
        b.ToTable("ManualEvaluations", t => t.HasTrigger("TR_ManualEvaluations_History"));
    }
}
public sealed class ManualEvaluationRevisionConfiguration : IEntityTypeConfiguration<ManualEvaluationRevision>
{
    public void Configure(EntityTypeBuilder<ManualEvaluationRevision> b)
    {
        b.ToTable("ManualEvaluationRevisions", t => t.HasTrigger("TR_ManualEvaluationRevisions_AppendOnly"));
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.ManualEvaluationId, x.Revision }).IsUnique();
        b.HasOne<ManualEvaluation>().WithMany().HasForeignKey(x => new { x.SchoolId, x.ManualEvaluationId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class RequirementFollowUpRevisionConfiguration : IEntityTypeConfiguration<RequirementFollowUpRevision>
{
    public void Configure(EntityTypeBuilder<RequirementFollowUpRevision> b)
    {
        b.ToTable("RequirementFollowUpRevisions", t => t.HasTrigger("TR_RequirementFollowUpRevisions_AppendOnly"));
        b.HasKey(x => x.Id); b.Property(x => x.Reason).HasMaxLength(1000);
        b.HasOne<EvidenceRequirement>().WithMany().HasForeignKey(x => x.RequirementId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        StorageConfiguration.User(b, nameof(RequirementFollowUpRevision.ActorUserId));
        b.HasIndex(x => new { x.SchoolId, x.RequirementId, x.Id });
    }
}
