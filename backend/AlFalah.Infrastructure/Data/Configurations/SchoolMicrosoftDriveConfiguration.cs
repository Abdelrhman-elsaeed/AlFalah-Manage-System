using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class SchoolMicrosoftDriveConfiguration : IEntityTypeConfiguration<SchoolMicrosoftDrive>
{
    public void Configure(EntityTypeBuilder<SchoolMicrosoftDrive> builder)
    {
        builder.ToTable("SchoolMicrosoftDrives");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(x => x.SchoolMicrosoftEmail).HasMaxLength(320).IsUnicode(false).IsRequired();
        builder.Property(x => x.DriveId).HasMaxLength(256).IsUnicode(false).IsRequired();
        builder.Property(x => x.RootItemId).HasMaxLength(256).IsUnicode(false).IsRequired();
        builder.Property(x => x.RootFolderDisplayName).HasMaxLength(256).IsUnicode(true).IsRequired();
        builder.HasIndex(x => x.SchoolId).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasOne(x => x.School).WithOne(x => x.MicrosoftDrive)
            .HasForeignKey<SchoolMicrosoftDrive>(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
