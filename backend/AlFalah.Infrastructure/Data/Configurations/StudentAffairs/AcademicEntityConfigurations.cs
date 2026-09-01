using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums.StudentAffairs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations.StudentAffairs;

internal sealed class StudentConfiguration : StudentAffairsMutableEntityConfiguration<Student>
{
    protected override string TableName => "Students";

    protected override void ConfigureEntity(EntityTypeBuilder<Student> builder)
    {
        builder.Property(x => x.StudentNumber).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.IdentityNumber).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.NationalId).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.FirstName).IsArabicText(100);
        builder.Property(x => x.MiddleName).IsOptionalArabicText(100);
        builder.Property(x => x.LastName).IsArabicText(100);
        builder.Property(x => x.ProfilePhotoStorageKey).HasMaxLength(1000).IsUnicode(false);
        builder.HasIndex(x => new { x.SchoolId, x.StudentNumber })
            .HasFilter("[IsDeleted] = 0").IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.IdentityNumber })
            .HasFilter("[IsDeleted] = 0").IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.NationalId })
            .HasFilter("[NationalId] IS NOT NULL AND [IsDeleted] = 0").IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.IsActive, x.StudentNumber });
    }
}

internal sealed class GuardianProfileConfiguration : StudentAffairsMutableEntityConfiguration<GuardianProfile>
{
    protected override string TableName => "GuardianProfiles";

    protected override void ConfigureEntity(EntityTypeBuilder<GuardianProfile> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(
            "CK_GuardianProfiles_PreferredContactLanguage", "[PreferredContactLanguage] IN ('ar', 'en')"));
        builder.Property(x => x.ApplicationUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.NationalId).HasMaxLength(30).IsUnicode(false);
        builder.HasIndex(x => new { x.SchoolId, x.ApplicationUserId })
            .HasFilter("[IsDeleted] = 0 AND [IsActive] = 1").IsUnique();
        builder.Property(x => x.PreferredContactLanguage)
            .HasConversion(
                value => value == PreferredContactLanguage.Arabic ? "ar" : "en",
                value => value == "ar" ? PreferredContactLanguage.Arabic : PreferredContactLanguage.English)
            .HasMaxLength(2).IsUnicode(false);
        builder.HasOne(x => x.ApplicationUser).WithMany()
            .HasForeignKey(x => x.ApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StudentGuardianConfiguration : StudentAffairsMutableEntityConfiguration<StudentGuardian>
{
    protected override string TableName => "StudentGuardians";

    protected override void ConfigureEntity(EntityTypeBuilder<StudentGuardian> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(
            "CK_StudentGuardians_Validity", "[ValidTo] IS NULL OR [ValidTo] >= [ValidFrom]"));
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.GuardianProfileId })
            .HasFilter("[IsDeleted] = 0").IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.IsPrimary })
            .HasFilter("[IsDeleted] = 0 AND [IsPrimary] = 1 AND [ValidTo] IS NULL").IsUnique();
        builder.HasOne(x => x.Student).WithMany(x => x.Guardians)
            .HasForeignKey(x => new { x.SchoolId, x.StudentId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GuardianProfile).WithMany(x => x.Students)
            .HasForeignKey(x => new { x.SchoolId, x.GuardianProfileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AcademicTermConfiguration : StudentAffairsMutableEntityConfiguration<AcademicTerm>
{
    protected override string TableName => "AcademicTerms";

    protected override void ConfigureEntity(EntityTypeBuilder<AcademicTerm> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(
            "CK_AcademicTerms_DateRange", "[EndsOn] >= [StartsOn]"));
        builder.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.Semester })
            .HasFilter("[IsDeleted] = 0").IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.IsActive })
            .HasFilter("[IsDeleted] = 0 AND [IsActive] = 1").IsUnique();
        builder.HasOne(x => x.AcademicYear).WithMany()
            .HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SchoolStudentAffairsSettingsConfiguration
    : StudentAffairsMutableEntityConfiguration<SchoolStudentAffairsSettings>
{
    protected override string TableName => "SchoolStudentAffairsSettings";

    protected override void ConfigureEntity(EntityTypeBuilder<SchoolStudentAffairsSettings> builder)
    {
        builder.ToTable(TableName, table =>
        {
            table.HasCheckConstraint("CK_StudentAffairsSettings_Thresholds",
                "[MorningDelayThresholdPerTerm] > 0 AND [BehaviorIncidentMultiplePerTerm] > 0 " +
                "AND [AcademicConcernThresholdPerTerm] > 0 AND [ClassroomEntryPermitThresholdPerTerm] > 0 " +
                "AND [AbsenceVisualAlertThresholdPerTerm] > 0 AND [AbsenceReferralThresholdPerTerm] > 0 " +
                "AND [AbsenceChildRightsThresholdPerTerm] > 0");
            table.HasCheckConstraint("CK_StudentAffairsSettings_AbsenceOrder",
                "[AbsenceVisualAlertThresholdPerTerm] < [AbsenceReferralThresholdPerTerm] " +
                "AND [AbsenceReferralThresholdPerTerm] < [AbsenceChildRightsThresholdPerTerm]");
            table.HasCheckConstraint("CK_StudentAffairsSettings_ArrivalGrace", "[ArrivalGraceMinutes] >= 0");
            table.HasCheckConstraint("CK_StudentAffairsSettings_Version", "[Version] > 0");
        });
        builder.HasIndex(x => x.SchoolId).HasFilter("[IsDeleted] = 0").IsUnique();
        builder.Property(x => x.MorningDelayThresholdPerTerm).HasDefaultValue(10);
        builder.Property(x => x.BehaviorIncidentMultiplePerTerm).HasDefaultValue(10);
        builder.Property(x => x.AcademicConcernThresholdPerTerm).HasDefaultValue(3);
        builder.Property(x => x.ClassroomEntryPermitThresholdPerTerm).HasDefaultValue(5);
        builder.Property(x => x.AbsenceVisualAlertThresholdPerTerm).HasDefaultValue(3);
        builder.Property(x => x.AbsenceReferralThresholdPerTerm).HasDefaultValue(5);
        builder.Property(x => x.AbsenceChildRightsThresholdPerTerm).HasDefaultValue(10);
        builder.Property(x => x.BehaviorCountabilityPolicy).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

internal sealed class ClassroomConfiguration : StudentAffairsMutableEntityConfiguration<Classroom>
{
    protected override string TableName => "Classrooms";

    protected override void ConfigureEntity(EntityTypeBuilder<Classroom> builder)
    {
        builder.Property(x => x.Section).IsArabicText(50);
        builder.Property(x => x.ClassLabel).IsArabicText(50);
        builder.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.ClassLabel })
            .HasFilter("[IsDeleted] = 0").IsUnique();
        builder.HasOne(x => x.AcademicYear).WithMany()
            .HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StudentEnrollmentConfiguration : StudentAffairsMutableEntityConfiguration<StudentEnrollment>
{
    protected override string TableName => "StudentEnrollments";

    protected override void ConfigureEntity(EntityTypeBuilder<StudentEnrollment> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(
            "CK_StudentEnrollments_Dates", "[WithdrawnOn] IS NULL OR [WithdrawnOn] >= [EnrolledOn]"));
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.AcademicTermId })
            .HasFilter($"[IsDeleted] = 0 AND [Status] = {(int)StudentEnrollmentStatus.Active}").IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.AcademicTermId, x.ClassroomId, x.Status });
        builder.HasOne(x => x.Student).WithMany(x => x.Enrollments)
            .HasForeignKey(x => new { x.SchoolId, x.StudentId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classroom).WithMany(x => x.Enrollments)
            .HasForeignKey(x => new { x.SchoolId, x.ClassroomId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
