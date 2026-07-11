using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class RubricVersionConfiguration : IEntityTypeConfiguration<RubricVersion>
{
    public void Configure(EntityTypeBuilder<RubricVersion> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.VersionNumber).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired(false);

        // FK to creator (nullable — seed data has no creator)
        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // One-to-many: version → domains
        builder.HasMany(x => x.Domains)
            .WithOne(d => d.Version)
            .HasForeignKey(d => d.RubricVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Standard indexes
        builder.HasIndex(x => x.VersionNumber);
        builder.HasIndex(x => x.IsDeleted);

        // MOD-3: Filtered unique index — only one active (non-deleted) version allowed at DB level.
        // SQL Server filtered index: WHERE IsActive = 1 AND IsDeleted = 0
        builder.HasIndex(x => x.IsActive)
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0")
            .IsUnique()
            .HasDatabaseName("UX_RubricVersion_Active");
    }
}
