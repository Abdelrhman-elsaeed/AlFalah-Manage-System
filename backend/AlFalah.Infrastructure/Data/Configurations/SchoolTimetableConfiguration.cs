using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class SchoolTimetableConfiguration : IEntityTypeConfiguration<SchoolTimetable>
{
    public void Configure(EntityTypeBuilder<SchoolTimetable> builder)
    {
        builder.ToTable("SchoolTimetables");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.SchoolId, x.Id });
        builder.Property(x => x.Title).HasMaxLength(250).IsUnicode(true).UseCollation("Arabic_CI_AS").IsRequired();
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.Semester })
            .HasFilter("[IsDeleted] = 0")
            .IsUnique();

        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PublishedByUser).WithMany().HasForeignKey(x => x.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
