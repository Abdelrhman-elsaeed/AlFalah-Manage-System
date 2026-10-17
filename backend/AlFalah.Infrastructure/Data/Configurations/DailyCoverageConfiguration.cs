using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class DailyCoverageConfiguration : IEntityTypeConfiguration<DailyCoverageNeed>,
    IEntityTypeConfiguration<CoverageRecord>, IEntityTypeConfiguration<CoverageReconciliationSignal>
{
    public void Configure(EntityTypeBuilder<DailyCoverageNeed> b)
    {
        b.ToTable("DailyCoverageNeeds"); b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.SchoolId, x.Id });
        b.HasIndex(x => new { x.SchoolId, x.LocalDate, x.SchoolTimetableId, x.SourceEntryId }).IsUnique();
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.OriginalTeacherName).HasMaxLength(400); b.Property(x => x.Classroom).HasMaxLength(100);
        b.Property(x => x.AssignedTeacherName).HasMaxLength(400); b.Property(x => x.AssignedUserId).HasMaxLength(450);
        b.Property(x => x.Subject).HasMaxLength(200); b.Property(x => x.TimeZoneId).HasMaxLength(100);
        b.Property(x => x.State).HasMaxLength(30); b.Property(x => x.ReviewReason).HasMaxLength(1000);
        b.Property(x => x.CorrectionReason).HasMaxLength(1000); b.Property(x => x.Narrative).HasMaxLength(10000); b.Property(x => x.Notes).HasMaxLength(2000);
        b.HasOne<SchoolTimetable>().WithMany().HasForeignKey(x => new { x.SchoolId, x.SchoolTimetableId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SchoolTimetableEntry>().WithMany().HasForeignKey(x => new { x.SchoolId, x.SourceEntryId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => new { x.SchoolId, x.OriginalTeacherId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ActiveSubstitution).WithMany().HasForeignKey(x => new { x.SchoolId, x.ActiveSubstitutionId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => { t.HasCheckConstraint("CK_CoverageNeed_State", "[State] IN ('Needed','Assigned','Acknowledged','Executed','Unable','ReviewRequired','Withdrawn')"); t.HasCheckConstraint("CK_CoverageNeed_Window", "[StartsAt] < [EndsAt] AND [Revision] > 0"); });
    }
    public void Configure(EntityTypeBuilder<CoverageRecord> b)
    {
        b.ToTable("CoverageRecords"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.SchoolId, x.RequestId }).IsUnique();
        b.Property(x => x.ActorUserId).HasMaxLength(450); b.Property(x => x.Kind).HasMaxLength(30);
        b.Property(x => x.Narrative).HasMaxLength(10000); b.Property(x => x.Notes).HasMaxLength(2000); b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.SnapshotHash).HasMaxLength(64); b.Property(x => x.SignatureHash).HasMaxLength(64);
        b.HasOne(x => x.Need).WithMany(x => x.Records).HasForeignKey(x => new { x.SchoolId, x.DailyCoverageNeedId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TimetableSubstitution>().WithMany().HasForeignKey(x => new { x.SchoolId, x.SubstitutionId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("CK_CoverageRecord_Signature", "([Kind] NOT IN ('Acknowledgement','Execution')) OR ([SignatureBytes] IS NOT NULL AND [SignatureHash] IS NOT NULL AND [SignatureUpdatedAt] IS NOT NULL)"));
    }
    public void Configure(EntityTypeBuilder<CoverageReconciliationSignal> b)
    {
        b.ToTable("CoverageReconciliationSignals"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt, x.LeaseUntil });
        b.Property(x => x.LeaseOwner).HasMaxLength(100); b.Property(x => x.LastError).HasMaxLength(2000);
        b.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
