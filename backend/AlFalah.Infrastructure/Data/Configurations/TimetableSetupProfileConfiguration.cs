using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class TimetableSetupProfileConfiguration : IEntityTypeConfiguration<TimetableSetupProfile>
{
    public void Configure(EntityTypeBuilder<TimetableSetupProfile> builder)
    {
        builder.ToTable("TimetableSetupProfiles", table =>
        {
            table.HasCheckConstraint("CK_TimetableSetupProfiles_Semester", "[Semester] BETWEEN 1 AND 2");
            table.HasCheckConstraint("CK_TimetableSetupProfiles_Status", $"[Status] BETWEEN {(int)TimetableSetupStatus.Draft} AND {(int)TimetableSetupStatus.Archived}");
            table.HasCheckConstraint("CK_TimetableSetupProfiles_Revision", "[Revision] > 0");
        });

        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.SchoolId, x.Id });
        builder.Property(x => x.Name)
            .HasMaxLength(120)
            .IsUnicode(true)
            .UseCollation("Arabic_CI_AS")
            .IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);
        builder.Property(x => x.Revision).IsConcurrencyToken();

        builder.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.Semester, x.Name })
            .HasFilter("[IsDeleted] = 0")
            .IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.Semester, x.Status });

        builder.HasOne(x => x.BellScheduleTemplate).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.BellScheduleTemplateId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.School).WithMany()
            .HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicYear).WithMany()
            .HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany()
            .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany()
            .HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DeletedByUser).WithMany()
            .HasForeignKey(x => x.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
