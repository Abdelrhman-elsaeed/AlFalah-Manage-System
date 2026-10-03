using AlFalah.Domain.Entities.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations.Storage;

public sealed class StorageLibraryFileIndexConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> b) =>
        b.HasIndex(x => new { x.SchoolId, x.OwnerTeacherId, x.FolderId, x.DisplayName, x.Id })
            .HasFilter("[IsDeleted] = 0");
}
public sealed class StorageLibraryFolderIndexConfiguration : IEntityTypeConfiguration<StorageFolder>
{
    public void Configure(EntityTypeBuilder<StorageFolder> b)
    {
        b.HasIndex(x => new { x.SchoolId, x.OwnerTeacherId, x.ParentFolderId, x.DisplayName, x.Id });
        b.HasIndex(x => new { x.SchoolId, x.Kind }).IsUnique()
            .HasFilter("[IsActive] = 1 AND [OwnerTeacherId] IS NULL AND [ParentFolderId] IS NULL");
    }
}
