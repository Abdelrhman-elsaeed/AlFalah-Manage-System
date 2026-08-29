using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class TeacherDriveFolderConfiguration : IEntityTypeConfiguration<TeacherDriveFolder>
{
    public void Configure(EntityTypeBuilder<TeacherDriveFolder> builder)
    {
        builder.ToTable("TeacherDriveFolders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DriveId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.RootItemId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.FolderDisplayName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.RootWebUrl).HasMaxLength(2048);
        builder.HasIndex(x => x.TeacherId).IsUnique();
        // A revoked grant remains as history for its teacher, but no longer reserves the folder.
        builder.HasIndex(x => new { x.DriveId, x.RootItemId }).IsUnique().HasFilter("[IsActive] = 1");
        builder.HasOne(x => x.Teacher).WithOne().HasForeignKey<TeacherDriveFolder>(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
