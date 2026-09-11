using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class SubjectDefinitionConfiguration : IEntityTypeConfiguration<SubjectDefinition>
{
    public void Configure(EntityTypeBuilder<SubjectDefinition> b)
    {
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.SchoolId, x.Id });
        b.HasIndex(x => new { x.SchoolId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.Property(x => x.Name).HasMaxLength(200).UseCollation("Arabic_CI_AS");
        b.Property(x => x.Color).HasMaxLength(7);
        b.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
public sealed class TimetableRoomConfiguration : IEntityTypeConfiguration<TimetableRoom>
{
    public void Configure(EntityTypeBuilder<TimetableRoom> b)
    {
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.SchoolId, x.Id });
        b.Property(x => x.Name).HasMaxLength(100).UseCollation("Arabic_CI_AS");
        b.HasIndex(x => new { x.SchoolId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
public sealed class ClassSubjectRequirementConfiguration : IEntityTypeConfiguration<ClassSubjectRequirement>
{
    public void Configure(EntityTypeBuilder<ClassSubjectRequirement> b)
    {
        b.ToTable("ClassSubjectRequirements", t => t.HasCheckConstraint("CK_SubjectRequirements_Counts",
            "[IndividualPeriodCount] BETWEEN 0 AND 100 AND [PairedBlockCount] BETWEEN 0 AND 50 AND [IndividualPeriodCount] + 2 * [PairedBlockCount] > 0"));
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.SchoolId, x.Id });
        b.HasIndex(x => new { x.TimetableSetupProfileId, x.ClassroomId, x.SubjectId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.Ignore(x => x.TotalWeeklyPeriods);
        b.Property(x => x.TimePreference).HasMaxLength(10);
        b.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasOne(x => x.Setup).WithMany().HasForeignKey(x => new { x.SchoolId, x.TimetableSetupProfileId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Classroom).WithMany().HasForeignKey(x => new { x.SchoolId, x.ClassroomId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Subject).WithMany().HasForeignKey(x => new { x.SchoolId, x.SubjectId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.AllowedDays).WithOne().HasForeignKey(x => x.ClassSubjectRequirementId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.FixedSlots).WithOne().HasForeignKey(x => x.ClassSubjectRequirementId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Rooms).WithOne().HasForeignKey(x => new { x.SchoolId, x.ClassSubjectRequirementId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
public sealed class SubjectRoomRequirementConfiguration : IEntityTypeConfiguration<SubjectRoomRequirement>
{
    public void Configure(EntityTypeBuilder<SubjectRoomRequirement> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassSubjectRequirementId, x.RoomId }).IsUnique();
        b.HasOne(x => x.Room).WithMany().HasForeignKey(x => new { x.SchoolId, x.RoomId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ClassSubjectAllowedDayConfiguration : IEntityTypeConfiguration<ClassSubjectAllowedDay>
{
    public void Configure(EntityTypeBuilder<ClassSubjectAllowedDay> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassSubjectRequirementId, x.Day }).IsUnique();
        b.ToTable("ClassSubjectAllowedDays", t => t.HasCheckConstraint("CK_SubjectAllowedDay", "[Day] BETWEEN 1 AND 7"));
    }
}
public sealed class ClassSubjectFixedSlotConfiguration : IEntityTypeConfiguration<ClassSubjectFixedSlot>
{
    public void Configure(EntityTypeBuilder<ClassSubjectFixedSlot> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassSubjectRequirementId, x.Day, x.Period }).IsUnique();
        b.ToTable("ClassSubjectFixedSlots", t => t.HasCheckConstraint("CK_SubjectFixedSlot", "[Day] BETWEEN 1 AND 7 AND [Period] > 0"));
    }
}
