using AlFalah.Domain.Entities.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations.Storage;

public sealed class StorageOperationConfiguration : IEntityTypeConfiguration<StorageOperation>
{
    public void Configure(EntityTypeBuilder<StorageOperation> b)
    {
        StorageConfiguration.Record(b, "StorageOperations");
        StorageConfiguration.User(b, nameof(StorageOperation.ActorUserId));
        b.Property(x => x.RequestKey).HasMaxLength(128);
        b.Property(x => x.Fingerprint).HasMaxLength(64);
        b.Property(x => x.SHA256).HasMaxLength(64);
        b.Property(x => x.Status).HasMaxLength(32);
        b.Property(x => x.Action).HasMaxLength(32);
        b.Property(x => x.ErrorCode).HasMaxLength(64);
        b.Property(x => x.DisplayName).HasMaxLength(255);
        b.Property(x => x.MimeType).HasMaxLength(256);
        b.Property(x => x.DriveId).HasMaxLength(256);
        b.Property(x => x.ProviderItemId).HasMaxLength(256);
        b.Property(x => x.ParentItemId).HasMaxLength(256);
        b.HasIndex(x => new { x.SchoolId, x.ActorUserId, x.RequestKey }).IsUnique();
        b.HasIndex(x => new { x.SchoolId, x.RequestKey }).IsUnique().HasFilter("[Action] = 'CreateFolder'");
        b.HasIndex(x => new { x.SchoolId, x.Status, x.CreatedAtUtc });
        b.HasIndex(x => x.ChangeRequestId).IsUnique().HasFilter("[ChangeRequestId] IS NOT NULL");
        b.HasOne<FileChangeRequest>().WithMany().HasForeignKey(x => new { x.SchoolId, x.ChangeRequestId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SchoolId, x.ProviderItemId }).IsUnique().HasFilter("[Action] IN ('Upload','CreateFolder')");
        b.HasOne<StorageFolder>().WithMany().HasForeignKey(x => new { x.SchoolId, x.FolderId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StoredFile>().WithMany().HasForeignKey(x => new { x.SchoolId, x.StoredFileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StoredFileVersion>().WithMany().HasForeignKey(x => new { x.SchoolId, x.StoredFileId, x.VersionId })
            .HasPrincipalKey(x => new { x.SchoolId, x.StoredFileId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.ToTable("StorageOperations", t => t.HasCheckConstraint("CK_StorageOperations_Completed", "[Status] <> 'Completed' OR [Action] <> 'Upload' OR ([StoredFileId] IS NOT NULL AND [VersionId] IS NOT NULL)"));
        b.ToTable("StorageOperations", t => t.HasCheckConstraint("CK_StorageOperations_Status", "[Status] IN ('Pending','Completed','Failed','NeedsAttention')"));
    }
}
