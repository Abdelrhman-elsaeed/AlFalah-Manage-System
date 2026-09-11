using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class BellScheduleTemplateConfiguration : IEntityTypeConfiguration<BellScheduleTemplate>
{
    public void Configure(EntityTypeBuilder<BellScheduleTemplate> b)
    {
        b.ToTable("BellScheduleTemplates", t => {
            t.HasCheckConstraint("CK_BellScheduleTemplates_Revision", "[Revision] > 0");
            t.HasCheckConstraint("CK_BellScheduleTemplates_Semester", "[Semester] BETWEEN 1 AND 2");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.SchoolId, x.Id });
        b.Property(x => x.Name).HasMaxLength(120).UseCollation("Arabic_CI_AS").IsRequired();
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.CreatedByUserId).HasMaxLength(450);
        b.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        b.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.Semester, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class BellScheduleRevisionConfiguration : IEntityTypeConfiguration<BellScheduleRevision>
{
    public void Configure(EntityTypeBuilder<BellScheduleRevision> b)
    {
        b.ToTable("BellScheduleRevisions");
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.SchoolId, x.Id });
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.SchoolTimeZoneId).HasMaxLength(100).IsRequired();
        b.Property(x => x.CreatedByUserId).HasMaxLength(450);
        b.HasIndex(x => new { x.BellScheduleTemplateId, x.Revision }).IsUnique();
        b.HasOne(x => x.Template).WithMany(x => x.Revisions)
            .HasForeignKey(x => new { x.SchoolId, x.BellScheduleTemplateId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BellScheduleDayConfiguration : IEntityTypeConfiguration<BellScheduleDay>
{
    public void Configure(EntityTypeBuilder<BellScheduleDay> b)
    {
        b.ToTable("BellScheduleDays", t => t.HasCheckConstraint("CK_BellScheduleDays_Day", "[Day] BETWEEN 0 AND 7"));
        b.Property(x => x.UsesDefaultBreaks).HasDefaultValue(true);
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.BellScheduleRevisionId, x.Day }).IsUnique();
        b.HasOne(x => x.Revision).WithMany(x => x.Days).HasForeignKey(x => x.BellScheduleRevisionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BellPeriodConfiguration : IEntityTypeConfiguration<BellPeriod>
{
    public void Configure(EntityTypeBuilder<BellPeriod> b)
    {
        b.ToTable("BellPeriods", t => {
            t.HasCheckConstraint("CK_BellPeriods_Time", "[StartLocalTime] < [EndLocalTime]");
            t.HasCheckConstraint("CK_BellPeriods_Sequence", "[Sequence] > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.DisplayLabel).HasMaxLength(100);
        b.Property(x => x.StartLocalTime).HasColumnType("time");
        b.Property(x => x.EndLocalTime).HasColumnType("time");
        b.HasIndex(x => new { x.BellScheduleDayId, x.Sequence }).IsUnique();
        b.HasOne(x => x.Day).WithMany(x => x.Periods).HasForeignKey(x => x.BellScheduleDayId).OnDelete(DeleteBehavior.Restrict);
    }
}
