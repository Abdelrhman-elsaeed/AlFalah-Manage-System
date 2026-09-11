using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class SchoolTimetableEntryConfiguration : IEntityTypeConfiguration<SchoolTimetableEntry>
{
    public void Configure(EntityTypeBuilder<SchoolTimetableEntry> builder)
    {
        builder.ToTable("SchoolTimetableEntries", table =>
        {
            table.HasCheckConstraint("CK_SchoolTimetableEntries_Period", "[Period] > 0");
            table.HasCheckConstraint("CK_SchoolTimetableEntries_Day", "[Day] BETWEEN 1 AND 7");
            table.HasCheckConstraint(
                "CK_SchoolTimetableEntries_Content",
                "([EntryType] = 1 AND [ClassLabel] IS NOT NULL AND [Subject] IS NOT NULL) OR ([EntryType] = 2 AND [ClassLabel] IS NULL AND [Subject] IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.ToTable("SchoolTimetableEntries", table => table.HasCheckConstraint("CK_TimetableEntry_SubjectSource",
            "[ClassSubjectRequirementId] IS NULL OR ([ClassroomId] IS NOT NULL AND [SubjectId] IS NOT NULL AND [EntryType] = 1)"));
        builder.HasOne(x => x.SubjectDefinition).WithMany().HasForeignKey(x => new { x.SchoolId, x.SubjectId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClassSubjectRequirement>().WithMany().HasForeignKey(x => new { x.SchoolId, x.ClassSubjectRequirementId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TimetableRoom>().WithMany().HasForeignKey(x => new { x.SchoolId, x.RoomId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        // Co-teachers share a room/class occurrence. The transactional validator checks logical collisions.
        builder.HasIndex(x => new { x.SchoolTimetableId, x.Day, x.Period, x.RoomId })
            .HasFilter("[IsDeleted] = 0 AND [RoomId] IS NOT NULL");
        builder.HasAlternateKey(x => new { x.SchoolId, x.Id });
        builder.Property(x => x.ClassLabel).HasMaxLength(50).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.Subject).HasMaxLength(200).IsUnicode(true).UseCollation("Arabic_CI_AS");

        builder.HasIndex(x => new { x.SchoolTimetableId, x.InstructorProfileId, x.Day, x.Period })
            .HasFilter("[IsDeleted] = 0")
            .IsUnique();
        builder.HasIndex(x => new { x.SchoolTimetableId, x.Day, x.Period, x.ClassLabel })
            .HasFilter($"[IsDeleted] = 0 AND [EntryType] = {(int)TimetableEntryType.Lesson}");

        builder.HasOne(x => x.School).WithMany()
            .HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SchoolTimetable).WithMany(x => x.Entries)
            .HasForeignKey(x => new { x.SchoolId, x.SchoolTimetableId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InstructorProfile).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.InstructorProfileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classroom).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.ClassroomId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
