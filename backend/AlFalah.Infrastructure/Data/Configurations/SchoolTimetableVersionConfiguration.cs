using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class SchoolTimetableVersionConfiguration : IEntityTypeConfiguration<SchoolTimetableVersion>
{
    public void Configure(EntityTypeBuilder<SchoolTimetableVersion> builder)
    {
        builder.ToTable("SchoolTimetableVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(250).IsUnicode(true).UseCollation("Arabic_CI_AS").IsRequired();
        builder.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.CheckpointName).HasMaxLength(150).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.CheckpointNote).HasMaxLength(1000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.ChangeSummary).HasMaxLength(500).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.ChangeDetailsJson).HasMaxLength(8000).IsUnicode(true);
        builder.HasIndex(x => new { x.SchoolTimetableId, x.VersionNumber }).IsUnique();
        builder.HasOne(x => x.SchoolTimetable).WithMany(x => x.Versions)
            .HasForeignKey(x => x.SchoolTimetableId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CreatedByUser).WithMany()
            .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
