using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations.StudentAffairs;

internal sealed class AutomationRuleDefinitionConfiguration
    : IEntityTypeConfiguration<AutomationRuleDefinition>
{
    public void Configure(EntityTypeBuilder<AutomationRuleDefinition> builder)
    {
        builder.ToTable("AutomationRuleDefinitions", table => table.HasCheckConstraint(
            "CK_AutomationRuleDefinitions_Threshold", "[Threshold] > 0 AND [Version] > 0"));
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.SchoolId, x.Id });
        builder.Property(x => x.PolicySnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.CompiledByUserId).HasMaxLength(450).IsRequired();
        builder.HasEnumCheckConstraints("AutomationRuleDefinitions");
        builder.HasIndex(x => new { x.SchoolId, x.Version, x.MetricCode }).IsUnique();
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SchoolStudentAffairsSettings).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.SchoolStudentAffairsSettingsId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CompiledByUser).WithMany()
            .HasForeignKey(x => x.CompiledByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StudentTermMetricConfiguration
    : StudentAffairsMutableEntityConfiguration<StudentTermMetric>
{
    protected override string TableName => "StudentTermMetrics";

    protected override void ConfigureEntity(EntityTypeBuilder<StudentTermMetric> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(
            "CK_StudentTermMetrics_Count", "[Count] >= 0"));
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.AcademicTermId, x.MetricCode })
            .HasFilter("[IsDeleted] = 0").IsUnique();
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AutomationTriggerLedgerConfiguration
    : IEntityTypeConfiguration<AutomationTriggerLedger>
{
    public void Configure(EntityTypeBuilder<AutomationTriggerLedger> builder)
    {
        builder.ToTable("AutomationTriggerLedgers", table =>
        {
            table.HasCheckConstraint("CK_AutomationTriggerLedgers_Threshold", "[Threshold] > 0");
            table.HasCheckConstraint("CK_AutomationTriggerLedgers_Occurrence", "[OccurrenceNumber] > 0");
            table.HasCheckConstraint("CK_AutomationTriggerLedgers_Count", "[CountSnapshot] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.SchoolId, x.Id });
        builder.Property(x => x.ReviewNote).HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.HasEnumCheckConstraints("AutomationTriggerLedgers");
        builder.HasIndex(x => new
        {
            x.SchoolId, x.StudentId, x.AcademicTermId, x.RuleVersionId, x.Threshold, x.OccurrenceNumber
        }).IsUnique();
        builder.HasIndex(x => x.CorrelationId);
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RuleVersion).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.RuleVersionId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", table =>
        {
            table.HasCheckConstraint("CK_OutboxMessages_Attempts", "[AttemptCount] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(500).IsUnicode(false).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.LastError).HasColumnType("nvarchar(max)");
        builder.Property(x => x.LeaseOwner).HasMaxLength(200).IsUnicode(false);
        builder.HasIndex(x => x.EventId).IsUnique();
        builder.HasIndex(x => new { x.ProcessedAt, x.DeadLetteredAt, x.NextAttemptAt, x.LeaseExpiresAt })
            .HasFilter("[ProcessedAt] IS NULL AND [DeadLetteredAt] IS NULL");
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MessageType).HasMaxLength(500).IsUnicode(false).IsRequired();
        builder.Property(x => x.ProcessingError).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.SchoolId, x.MessageId }).IsUnique();
        builder.HasIndex(x => new { x.ProcessedAt, x.ReceivedAt });
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
