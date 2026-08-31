using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums.StudentAffairs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations.StudentAffairs;

internal sealed class ClassroomEntryPermitConfiguration
    : StudentAffairsMutableEntityConfiguration<ClassroomEntryPermit>
{
    protected override string TableName => "ClassroomEntryPermits";

    protected override void ConfigureEntity(EntityTypeBuilder<ClassroomEntryPermit> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(
            "CK_ClassroomEntryPermits_Validity", "[ValidUntil] > [ValidFrom]"));
        builder.Property(x => x.Reason).IsArabicText(1000);
        builder.Property(x => x.RevocationReason).IsOptionalArabicText(1000);
        builder.Property(x => x.IssuedByStudentAffairsUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.AcknowledgedByTeacherUserId).HasMaxLength(450);
        builder.Property(x => x.RevokedByUserId).HasMaxLength(450);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.AcademicTermId, x.IssuedAt });
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classroom).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.ClassroomId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.IssuedByStudentAffairsUser).WithMany()
            .HasForeignKey(x => x.IssuedByStudentAffairsUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SchoolTimetable).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.SchoolTimetableId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SchoolTimetableEntry).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.SchoolTimetableEntryId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TargetInstructorProfile).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.TargetInstructorProfileId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AcknowledgedByTeacherUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GatePassConfiguration : StudentAffairsMutableEntityConfiguration<GatePass>
{
    protected override string TableName => "GatePasses";

    protected override void ConfigureEntity(EntityTypeBuilder<GatePass> builder)
    {
        builder.ToTable(TableName, table =>
        {
            table.HasCheckConstraint("CK_GatePasses_Period", "[CurrentPeriod] IS NULL OR [CurrentPeriod] BETWEEN 1 AND 8");
            table.HasCheckConstraint("CK_GatePasses_Review",
                $"([Status] = {(int)GatePassStatus.Requested} AND [ReviewedAt] IS NULL) OR " +
                $"([Status] <> {(int)GatePassStatus.Requested})");
            table.HasCheckConstraint("CK_GatePasses_Exit", "[ExitedAt] IS NULL OR [ExitRecordedByUserId] IS NOT NULL");
            table.HasCheckConstraint("CK_GatePasses_ApprovedWindow",
                "([ApprovedWindowStartsAt] IS NULL AND [ApprovedWindowEndsAt] IS NULL) OR " +
                "([ApprovedWindowStartsAt] IS NOT NULL AND [ApprovedWindowEndsAt] > [ApprovedWindowStartsAt])");
        });
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200).IsUnicode(false).IsRequired();
        builder.Property(x => x.Reason).IsArabicText(1000);
        builder.Property(x => x.PickupPersonName).IsArabicText(200);
        builder.Property(x => x.PickupRelationship).IsOptionalArabicText(100);
        builder.Property(x => x.PickupIdentityHint).IsOptionalArabicText(500);
        builder.Property(x => x.ApprovalNote).IsOptionalArabicText(1000);
        builder.Property(x => x.RejectionReason).IsOptionalArabicText(1000);
        builder.Property(x => x.PickupVerificationNote).IsOptionalArabicText(1000);
        builder.Property(x => x.ExitGateNote).IsOptionalArabicText(1000);
        builder.Property(x => x.CancellationReason).IsOptionalArabicText(1000);
        ConfigureUserIds(builder);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.SchoolId, x.Status, x.RequestedExitAt });
        builder.HasIndex(x => new { x.SchoolId, x.RequestedByGuardianProfileId, x.IdempotencyKey })
            .HasFilter("[IsDeleted] = 0").IsUnique();
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RequestedByGuardianProfile).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.RequestedByGuardianProfileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CurrentClassroom).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.CurrentClassroomId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SchoolTimetable).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.SchoolTimetableId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SchoolTimetableEntry).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.SchoolTimetableEntryId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CurrentInstructorProfile).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.CurrentInstructorProfileId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureUserIds(EntityTypeBuilder<GatePass> builder)
    {
        var ids = new[]
        {
            nameof(GatePass.ReviewedByUserId), nameof(GatePass.SecurityAcknowledgedByUserId),
            nameof(GatePass.ExitRecordedByUserId), nameof(GatePass.CancelledByUserId)
        };
        foreach (var id in ids) builder.Property<string?>(id).HasMaxLength(450);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SecurityAcknowledgedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ExitRecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GatePassTransitionConfiguration : IEntityTypeConfiguration<GatePassTransition>
{
    public void Configure(EntityTypeBuilder<GatePassTransition> builder)
    {
        builder.ToTable("GatePassTransitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.ActorRole).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.Reason).IsOptionalArabicText(1000);
        builder.Property(x => x.PickupVerificationNote).IsOptionalArabicText(1000);
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)");
        builder.HasEnumCheckConstraints("GatePassTransitions");
        builder.HasIndex(x => new { x.SchoolId, x.GatePassId, x.OccurredAt });
        builder.HasIndex(x => x.CorrelationId);
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GatePass).WithMany(x => x.Transitions)
            .HasForeignKey(x => new { x.SchoolId, x.GatePassId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StudentReferralConfiguration : StudentAffairsMutableEntityConfiguration<StudentReferral>
{
    protected override string TableName => "StudentReferrals";

    protected override void ConfigureEntity(EntityTypeBuilder<StudentReferral> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(
            "CK_StudentReferrals_Snapshots", "[CountSnapshot] IS NULL OR [CountSnapshot] >= 0"));
        builder.Property(x => x.AssignedSocialWorkerUserId).HasMaxLength(450);
        builder.Property(x => x.RecommendedActions).IsOptionalArabicText(3000);
        builder.Property(x => x.ResolutionNotes).IsOptionalArabicText(3000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.SchoolId, x.AssignedSocialWorkerUserId, x.Status, x.Priority });
        builder.HasIndex(x => new { x.SchoolId, x.RuleTriggerId })
            .HasFilter("[RuleTriggerId] IS NOT NULL AND [IsDeleted] = 0").IsUnique();
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedSocialWorkerUser).WithMany()
            .HasForeignKey(x => x.AssignedSocialWorkerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RuleTrigger).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.RuleTriggerId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GuardianSummonConfiguration : StudentAffairsMutableEntityConfiguration<GuardianSummon>
{
    protected override string TableName => "GuardianSummons";

    protected override void ConfigureEntity(EntityTypeBuilder<GuardianSummon> builder)
    {
        builder.Property(x => x.CreatedReason).IsArabicText(1000);
        builder.Property(x => x.Location).IsOptionalArabicText(250);
        builder.Property(x => x.Instructions).IsOptionalArabicText(2000);
        builder.Property(x => x.OfficerReviewReason).IsOptionalArabicText(1000);
        builder.Property(x => x.AttendanceNotes).IsOptionalArabicText(2000);
        builder.Property(x => x.ObservationNotes).IsOptionalArabicText(3000);
        builder.Property(x => x.ImprovementNotes).IsOptionalArabicText(3000);
        builder.Property(x => x.ScheduledBySocialWorkerUserId).HasMaxLength(450);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.SchoolId, x.Status, x.Priority });
        builder.HasIndex(x => new { x.SchoolId, x.RequiresOfficerReview });
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StudentReferral).WithMany(x => x.GuardianSummons)
            .HasForeignKey(x => new { x.SchoolId, x.StudentReferralId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GuardianProfile).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.GuardianProfileId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ScheduledBySocialWorkerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GuardianSummonStatusHistoryConfiguration
    : IEntityTypeConfiguration<GuardianSummonStatusHistory>
{
    public void Configure(EntityTypeBuilder<GuardianSummonStatusHistory> builder)
    {
        builder.ToTable("GuardianSummonStatusHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Notes).IsOptionalArabicText(2000);
        builder.HasEnumCheckConstraints("GuardianSummonStatusHistory");
        builder.HasIndex(x => new { x.SchoolId, x.GuardianSummonId, x.OccurredAt });
        builder.HasIndex(x => x.CorrelationId);
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GuardianSummon).WithMany(x => x.StatusHistory)
            .HasForeignKey(x => new { x.SchoolId, x.GuardianSummonId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StudentCaseActionConfiguration : StudentAffairsMutableEntityConfiguration<StudentCaseAction>
{
    protected override string TableName => "StudentCaseActions";

    protected override void ConfigureEntity(EntityTypeBuilder<StudentCaseAction> builder)
    {
        builder.Property(x => x.Description).IsArabicText(3000);
        builder.Property(x => x.Result).IsOptionalArabicText(3000);
        builder.Property(x => x.AttachmentStorageKey).HasMaxLength(1000).IsUnicode(false);
        builder.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(x => new { x.SchoolId, x.StudentReferralId, x.ActionAt });
        builder.HasOne(x => x.StudentReferral).WithMany(x => x.Actions)
            .HasForeignKey(x => new { x.SchoolId, x.StudentReferralId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
