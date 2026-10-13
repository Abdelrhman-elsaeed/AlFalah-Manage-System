using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.StudentAffairs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class DutyConfiguration : IEntityTypeConfiguration<DutyPlan>, IEntityTypeConfiguration<DutyParticipant>,
    IEntityTypeConfiguration<DutyClosedDate>, IEntityTypeConfiguration<DutyAssignment>,
    IEntityTypeConfiguration<DutyAssignmentChange>, IEntityTypeConfiguration<DutyReconciliationOperation>, IEntityTypeConfiguration<DutyRequestReceipt>
{
    public void Configure(EntityTypeBuilder<DutyPlan> b)
    {
        b.ToTable("DutyPlans"); b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.SchoolId, x.Id });
        b.HasIndex(x => new { x.SchoolId, x.AcademicTermId }).IsUnique();
        b.Property(x => x.Revision).IsConcurrencyToken(); b.Property(x => x.TimeZoneId).HasMaxLength(100);
        b.Property(x => x.PublishedByUserId).HasMaxLength(450);
        b.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<AcademicTerm>().WithMany().HasForeignKey(x => new { x.SchoolId, x.AcademicTermId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<BellScheduleRevision>().WithMany().HasForeignKey(x => x.BellScheduleRevisionId).OnDelete(DeleteBehavior.NoAction);
        b.HasMany(x => x.Participants).WithOne().HasForeignKey(x => new { x.SchoolId, x.DutyPlanId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasMany(x => x.Closures).WithOne().HasForeignKey(x => new { x.SchoolId, x.DutyPlanId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<DutyParticipant> b)
    {
        b.ToTable("DutyParticipants", t => t.UseSqlOutputClause(false)); b.HasKey(x => x.Id); b.Property(x => x.UserId).HasMaxLength(450);
        b.HasIndex(x => new { x.DutyPlanId, x.UserId }).IsUnique(); b.HasIndex(x => new { x.DutyPlanId, x.Position }).IsUnique();
        // Profile/user identity is stable. School membership is validated when inserting,
        // so a later school transfer cannot invalidate historical plans.
        b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => new { x.InstructorProfileId, x.UserId })
            .HasPrincipalKey(x => new { x.Id, x.UserId }).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<DutyClosedDate> b)
    {
        b.ToTable("DutyClosedDates", t => t.HasCheckConstraint("CK_DutyClosure_Range", "[From] <= [To]"));
        b.HasKey(x => x.Id); b.Property(x => x.Reason).HasMaxLength(1000);
    }
    public void Configure(EntityTypeBuilder<DutyAssignment> b)
    {
        b.ToTable("DutyAssignments", t => t.UseSqlOutputClause(false)); b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.SchoolId, x.Id });
        b.HasIndex(x => new { x.SchoolId, x.Date }).IsUnique(); b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.OriginalTeacherId).HasMaxLength(450); b.Property(x => x.EffectiveTeacherId).HasMaxLength(450);
        b.Property(x => x.ExceptionCode).HasMaxLength(80);
        b.HasOne<DutyPlan>().WithMany().HasForeignKey(x => new { x.SchoolId, x.DutyPlanId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OriginalTeacherId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.EffectiveTeacherId).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<DutyAssignmentChange> b)
    {
        b.ToTable("DutyAssignmentChanges", t => t.UseSqlOutputClause(false)); b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.SchoolId, x.Id });
        b.HasIndex(x => new { x.SchoolId, x.Date }); b.Property(x => x.Kind).HasMaxLength(80); b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ActorUserId).HasMaxLength(450);
        foreach (var p in new[] { "BeforeTeacherId", "AfterTeacherId", "OtherBeforeTeacherId", "OtherAfterTeacherId" }) b.Property(p).HasMaxLength(450);
        b.HasOne<DutyPlan>().WithMany().HasForeignKey(x => new { x.SchoolId, x.DutyPlanId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<DutyAssignment>().WithMany().HasForeignKey(x => new { x.SchoolId, x.AssignmentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<DutyAssignment>().WithMany().HasForeignKey(x => new { x.SchoolId, x.OtherAssignmentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<DutyAssignmentChange>().WithMany().HasForeignKey(x => new { x.SchoolId, x.RelatedChangeId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<DutyReconciliationOperation> b)
    {
        b.ToTable("DutyReconciliationOperations"); b.HasKey(x => x.Id); b.HasIndex(x => x.SignalId).IsUnique();
        b.HasIndex(x => new { x.CompletedAt, x.NextAttemptAt }); b.Property(x => x.ErrorCode).HasMaxLength(80);
        b.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<DutyRequestReceipt> b)
    {
        b.ToTable("DutyRequestReceipts"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.SchoolId, x.RequestId }).IsUnique();
        b.Property(x => x.Digest).HasMaxLength(64);
        b.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.NoAction);
    }
}
