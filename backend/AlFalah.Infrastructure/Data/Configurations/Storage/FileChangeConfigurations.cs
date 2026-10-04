using AlFalah.Domain.Entities.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations.Storage;

public sealed class FileChangeRequestConfiguration : IEntityTypeConfiguration<FileChangeRequest>
{
    public void Configure(EntityTypeBuilder<FileChangeRequest> b)
    {
        StorageConfiguration.Record(b, "FileChangeRequests");
        StorageConfiguration.User(b, nameof(FileChangeRequest.RequestedByUserId));
        b.Property(x => x.Kind).HasMaxLength(16);
        b.Property(x => x.Status).HasMaxLength(16);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.HasIndex(x => new { x.SchoolId, x.StoredFileId }).IsUnique().HasFilter("[Status] = 'Pending'");
        b.HasOne<StoredFile>().WithMany().HasForeignKey(x => new { x.SchoolId, x.StoredFileId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StoredFileVersion>().WithMany().HasForeignKey(x => new { x.SchoolId, x.StoredFileId, x.OriginalVersionId }).HasPrincipalKey(x => new { x.SchoolId, x.StoredFileId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StoredFileVersion>().WithMany().HasForeignKey(x => new { x.SchoolId, x.StoredFileId, x.CandidateVersionId }).HasPrincipalKey(x => new { x.SchoolId, x.StoredFileId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.ToTable("FileChangeRequests", t => {
            t.HasCheckConstraint("CK_FileChangeRequest_State", "[Kind] IN ('Replace','Delete') AND [Status] IN ('Pending','Approved','Rejected') AND LTRIM(RTRIM([Reason])) <> ''");
            t.HasTrigger("TR_FileChangeRequests_History");
        });
    }
}
public sealed class FileChangeDecisionConfiguration : IEntityTypeConfiguration<FileChangeDecision>
{
    public void Configure(EntityTypeBuilder<FileChangeDecision> b)
    {
        StorageConfiguration.Record(b, "FileChangeDecisions");
        StorageConfiguration.User(b, nameof(FileChangeDecision.ReviewedByUserId));
        b.Property(x => x.Decision).HasMaxLength(16); b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.ReviewerName).HasMaxLength(300);
        b.HasIndex(x => x.FileChangeRequestId).IsUnique();
        b.HasOne<FileChangeRequest>().WithMany().HasForeignKey(x => new { x.SchoolId, x.FileChangeRequestId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.ToTable("FileChangeDecisions", t => {
            t.HasCheckConstraint("CK_FileChangeDecision_State", "[Decision] IN ('Approved','Rejected') AND ([Decision] <> 'Rejected' OR LTRIM(RTRIM([Note])) <> '' AND [Note] IS NOT NULL)");
            t.HasTrigger("TR_FileChangeDecisions_AppendOnly");
        });
    }
}
