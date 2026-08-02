using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class SchoolGoogleDriveConfiguration : IEntityTypeConfiguration<SchoolGoogleDrive>
{
    public void Configure(EntityTypeBuilder<SchoolGoogleDrive> builder)
    {
        builder.ToTable("SchoolGoogleDrives");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CredentialType).HasConversion<int>().IsRequired();
        builder.Property(x => x.SchoolGoogleEmail).HasMaxLength(320).IsUnicode(false).IsRequired();
        // Data-Protection ciphertext. A service-account JSON key protects to a few
        // kilobytes of base64url, so this column must not be length-capped tightly.
        builder.Property(x => x.ProtectedCredential).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.OAuthClientId).HasMaxLength(512).IsUnicode(false);
        builder.Property(x => x.ProtectedOAuthClientSecret).HasMaxLength(2048).IsUnicode(false);
        builder.Property(x => x.ImpersonatedUserEmail).HasMaxLength(320).IsUnicode(false);
        builder.Property(x => x.SharedDriveId).HasMaxLength(256).IsUnicode(false);
        builder.Property(x => x.RootFolderId).HasMaxLength(256).IsUnicode(false).IsRequired();
        builder.Property(x => x.RootFolderDisplayName).HasMaxLength(256).IsUnicode(true).IsRequired();
        builder.HasIndex(x => x.SchoolId).IsUnique();
        builder.HasOne(x => x.School).WithOne(x => x.GoogleDrive)
            .HasForeignKey<SchoolGoogleDrive>(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
