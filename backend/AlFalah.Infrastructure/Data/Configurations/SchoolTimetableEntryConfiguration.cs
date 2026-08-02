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
            table.HasCheckConstraint("CK_SchoolTimetableEntries_Period", "[Period] BETWEEN 1 AND 8");
            table.HasCheckConstraint("CK_SchoolTimetableEntries_Day", "[Day] BETWEEN 1 AND 6");
            table.HasCheckConstraint(
                "CK_SchoolTimetableEntries_Content",
                "([EntryType] = 1 AND [ClassLabel] IS NOT NULL AND [Subject] IS NOT NULL) OR ([EntryType] = 2 AND [ClassLabel] IS NULL AND [Subject] IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClassLabel).HasMaxLength(50).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.Subject).HasMaxLength(200).IsUnicode(true).UseCollation("Arabic_CI_AS");

        builder.HasIndex(x => new { x.SchoolTimetableId, x.InstructorProfileId, x.Day, x.Period })
            .HasFilter("[IsDeleted] = 0")
            .IsUnique();
        builder.HasIndex(x => new { x.SchoolTimetableId, x.Day, x.Period, x.ClassLabel })
            .HasFilter($"[IsDeleted] = 0 AND [EntryType] = {(int)TimetableEntryType.Lesson}")
            .IsUnique();

        builder.HasOne(x => x.SchoolTimetable).WithMany(x => x.Entries)
            .HasForeignKey(x => x.SchoolTimetableId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.InstructorProfile).WithMany()
            .HasForeignKey(x => x.InstructorProfileId).OnDelete(DeleteBehavior.Restrict);
    }
}
