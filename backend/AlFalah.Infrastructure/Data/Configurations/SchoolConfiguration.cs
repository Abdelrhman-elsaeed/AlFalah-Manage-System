using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> builder)
    {
        builder.HasKey(x => x.Id);
        // D-31: explicit Unicode on every Arabic-text column.
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.City).IsRequired().HasMaxLength(100).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.LocationDetails).HasMaxLength(500).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.LogoUrl).HasMaxLength(1000).IsUnicode(false);
        builder.Property(x => x.Stage).HasConversion<string>().HasMaxLength(50).IsUnicode(false);

        // FK to manager (nullable at DB level)
        builder.HasOne(x => x.Manager)
            .WithMany()
            .HasForeignKey(x => x.ManagerUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // Soft delete FK
        builder.HasOne(x => x.DeletedByUser)
            .WithMany()
            .HasForeignKey(x => x.DeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        // One-to-one with report settings
        builder.HasOne(x => x.ReportSettings)
            .WithOne(x => x.School)
            .HasForeignKey<SchoolReportSettings>(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.Stage);
        builder.HasIndex(x => x.City);
        builder.HasIndex(x => x.SchoolLocationId);
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => new { x.Name, x.City, x.LocationDetails });
    }
}
