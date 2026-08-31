using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums.StudentAffairs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations.StudentAffairs;

internal sealed class DailyStudentAttendanceConfiguration
    : StudentAffairsMutableEntityConfiguration<DailyStudentAttendance>
{
    protected override string TableName => "DailyStudentAttendances";

    protected override void ConfigureEntity(EntityTypeBuilder<DailyStudentAttendance> builder)
    {
        builder.Property(x => x.CorrectionReason).IsOptionalArabicText(1000);
        builder.Property(x => x.RecordedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.CorrectedByUserId).HasMaxLength(450);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.AcademicTermId, x.ExcuseStatus });
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.AttendanceDate })
            .HasFilter("[IsDeleted] = 0").IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.AttendanceDate, x.Status });
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.AcademicTermId });
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classroom).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.ClassroomId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecordedByUser).WithMany()
            .HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CorrectedByUser).WithMany()
            .HasForeignKey(x => x.CorrectedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AbsenceExcuseConfiguration : StudentAffairsMutableEntityConfiguration<AbsenceExcuse>
{
    protected override string TableName => "AbsenceExcuses";

    protected override void ConfigureEntity(EntityTypeBuilder<AbsenceExcuse> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(
            "CK_AbsenceExcuses_Review",
            $"([Status] = {(int)AbsenceExcuseStatus.Pending} AND [ReviewedAt] IS NULL AND [ReviewedByUserId] IS NULL) OR " +
            $"([Status] IN ({(int)AbsenceExcuseStatus.Accepted}, {(int)AbsenceExcuseStatus.Rejected}) AND [ReviewedAt] IS NOT NULL AND [ReviewedByUserId] IS NOT NULL)"));
        builder.Property(x => x.GuardianNotes).IsOptionalArabicText(2000);
        builder.Property(x => x.ReviewReason).IsOptionalArabicText(1000);
        builder.Property(x => x.ReviewedByUserId).HasMaxLength(450);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200).IsUnicode(false).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.SchoolId, x.GuardianProfileId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.DailyStudentAttendanceId, x.Status })
            .HasFilter($"[IsDeleted] = 0 AND [Status] = {(int)AbsenceExcuseStatus.Accepted}").IsUnique();
        builder.HasOne(x => x.DailyStudentAttendance).WithMany(x => x.Excuses)
            .HasForeignKey(x => new { x.SchoolId, x.DailyStudentAttendanceId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GuardianProfile).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.GuardianProfileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewedByUser).WithMany()
            .HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AbsenceExcuseAttachmentConfiguration
    : StudentAffairsMutableEntityConfiguration<AbsenceExcuseAttachment>
{
    protected override string TableName => "AbsenceExcuseAttachments";

    protected override void ConfigureEntity(EntityTypeBuilder<AbsenceExcuseAttachment> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(
            "CK_AbsenceExcuseAttachments_Size", "[SizeBytes] > 0"));
        builder.Property(x => x.OriginalFileName).IsArabicText(255);
        builder.Property(x => x.ContentType).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
        builder.Property(x => x.StorageProvider).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(1000).IsUnicode(false).IsRequired();
        builder.Property(x => x.UploadedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(x => new { x.SchoolId, x.AbsenceExcuseId });
        builder.HasOne(x => x.AbsenceExcuse).WithMany(x => x.Attachments)
            .HasForeignKey(x => new { x.SchoolId, x.AbsenceExcuseId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UploadedByUser).WithMany()
            .HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MorningArrivalDelayConfiguration
    : StudentAffairsMutableEntityConfiguration<MorningArrivalDelay>
{
    protected override string TableName => "MorningArrivalDelays";

    protected override void ConfigureEntity(EntityTypeBuilder<MorningArrivalDelay> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(
            "CK_MorningArrivalDelays_DelayMinutes", "[DelayMinutes] >= 0"));
        builder.Property(x => x.Reason).IsOptionalArabicText(1000);
        builder.Property(x => x.NotificationPolicySnapshot).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.SchoolLocalDate })
            .HasFilter("[IsDeleted] = 0").IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.AcademicTermId, x.ArrivalAt });
        ConfigureStudentAndTerm(builder);
    }

    private static void ConfigureStudentAndTerm(EntityTypeBuilder<MorningArrivalDelay> builder)
    {
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SessionDelayConfiguration : StudentAffairsMutableEntityConfiguration<SessionDelay>
{
    protected override string TableName => "SessionDelays";

    protected override void ConfigureEntity(EntityTypeBuilder<SessionDelay> builder)
    {
        builder.ToTable(TableName, table =>
        {
            table.HasCheckConstraint("CK_SessionDelays_Period", "[Period] BETWEEN 1 AND 8");
            table.HasCheckConstraint("CK_SessionDelays_DelayMinutes", "[DelayMinutes] IS NULL OR [DelayMinutes] >= 0");
        });
        builder.Property(x => x.Reason).IsOptionalArabicText(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.AcademicTermId, x.OccurredAt });
        builder.HasIndex(x => new { x.SchoolId, x.ClassroomId, x.OccurredAt });
        ConfigureStudentTermClassroom(builder);
        builder.HasOne(x => x.SchoolTimetable).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.SchoolTimetableId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SchoolTimetableEntry).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.SchoolTimetableEntryId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReportedByInstructorProfile).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.ReportedByInstructorProfileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureStudentTermClassroom(EntityTypeBuilder<SessionDelay> builder)
    {
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classroom).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.ClassroomId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AcademicConcernConfiguration : StudentAffairsMutableEntityConfiguration<AcademicConcern>
{
    protected override string TableName => "AcademicConcerns";

    protected override void ConfigureEntity(EntityTypeBuilder<AcademicConcern> builder)
    {
        builder.Property(x => x.Category).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.Description).IsArabicText(2000);
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.AcademicTermId, x.OccurredAt });
        builder.HasIndex(x => new { x.SchoolId, x.ClassroomId, x.OccurredAt });
        ConfigureCommonRelationships(builder);
        builder.HasOne(x => x.SchoolTimetableEntry).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.SchoolTimetableEntryId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCommonRelationships(EntityTypeBuilder<AcademicConcern> builder)
    {
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classroom).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.ClassroomId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReportedByInstructorProfile).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.ReportedByInstructorProfileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BehaviorIncidentConfiguration : StudentAffairsMutableEntityConfiguration<BehaviorIncident>
{
    protected override string TableName => "BehaviorIncidents";

    protected override void ConfigureEntity(EntityTypeBuilder<BehaviorIncident> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(
            "CK_BehaviorIncidents_Reporter",
            "([ReportedByInstructorProfileId] IS NOT NULL AND [ReportedByStaffUserId] IS NULL) OR " +
            "([ReportedByInstructorProfileId] IS NULL AND [ReportedByStaffUserId] IS NOT NULL)"));
        builder.Property(x => x.CategoryCode).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.Description).IsArabicText(2000);
        builder.Property(x => x.Location).IsOptionalArabicText(250);
        builder.Property(x => x.ImmediateActionTaken).IsOptionalArabicText(2000);
        builder.Property(x => x.ReportedByStaffUserId).HasMaxLength(450);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.AcademicTermId, x.OccurredAt });
        builder.HasIndex(x => new { x.SchoolId, x.ClassroomId, x.OccurredAt });
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classroom).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.ClassroomId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReportedByInstructorProfile).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.ReportedByInstructorProfileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReportedByStaffUser).WithMany()
            .HasForeignKey(x => x.ReportedByStaffUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StudentRecognitionConfiguration
    : StudentAffairsMutableEntityConfiguration<StudentRecognition>
{
    protected override string TableName => "StudentRecognitions";

    protected override void ConfigureEntity(EntityTypeBuilder<StudentRecognition> builder)
    {
        builder.Property(x => x.RecognitionType).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.Title).IsArabicText(250);
        builder.Property(x => x.Description).IsArabicText(2000);
        builder.HasIndex(x => new { x.SchoolId, x.AcademicTermId, x.RecognizedAt });
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.RecognizedAt });
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classroom).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.ClassroomId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReportedByInstructorProfile).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.ReportedByInstructorProfileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
