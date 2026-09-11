using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class TeacherTimetableProfileConfiguration : IEntityTypeConfiguration<TeacherTimetableProfile>
{
    public void Configure(EntityTypeBuilder<TeacherTimetableProfile> b)
    {
        b.ToTable("TeacherTimetableProfiles", t => {
            t.HasCheckConstraint("CK_TeacherTimetableProfiles_Load", "[MaximumWeeklyPeriods] >= 0");
            t.HasCheckConstraint("CK_TeacherTimetableProfiles_Revision", "[Revision] > 0");
        });
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.TimetableSetupProfileId, x.InstructorProfileId }).IsUnique();
        b.Property(x => x.ShortDisplayName).HasMaxLength(60).IsUnicode().IsRequired();
        b.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.UpdatedByUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasOne(x => x.Setup).WithMany().HasForeignKey(x => new { x.SchoolId, x.TimetableSetupProfileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Instructor).WithMany().HasForeignKey(x => new { x.SchoolId, x.InstructorProfileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.BellScheduleRevision).WithMany().HasForeignKey(x => new { x.SchoolId, x.BellScheduleRevisionId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TeacherAvailabilitySlotConfiguration : IEntityTypeConfiguration<TeacherAvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<TeacherAvailabilitySlot> b)
    {
        b.ToTable("TeacherAvailabilitySlots", t => t.HasCheckConstraint("CK_TeacherAvailabilitySlots_Day", "[Day] BETWEEN 1 AND 7"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.TeacherTimetableProfileId, x.Day, x.BellPeriodId }).IsUnique();
        b.HasOne(x => x.Profile).WithMany(x => x.Slots).HasForeignKey(x => x.TeacherTimetableProfileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Period).WithMany().HasForeignKey(x => x.BellPeriodId).OnDelete(DeleteBehavior.Restrict);
    }
}
