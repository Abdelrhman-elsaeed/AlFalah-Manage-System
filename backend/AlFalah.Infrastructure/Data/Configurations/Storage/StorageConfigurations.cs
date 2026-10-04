using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations.Storage;

internal static class StorageConfiguration
{
    internal static void Record<T>(EntityTypeBuilder<T> b, string table) where T : class, IStorageRecord
    {
        b.ToTable(table);
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.SchoolId, x.Id });
        b.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.RowVersion).IsRowVersion();
    }

    internal static void User<T>(EntityTypeBuilder<T> b, string property) where T : class
    {
        b.Property<string>(property).HasMaxLength(450);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(property).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StorageFolderConfiguration : IEntityTypeConfiguration<StorageFolder>
{
    public void Configure(EntityTypeBuilder<StorageFolder> b)
    {
        StorageConfiguration.Record(b, "StorageFolders");
        b.Property(x => x.DisplayName).HasMaxLength(512).IsRequired();
        b.Property(x => x.DriveId).HasMaxLength(256).IsRequired();
        b.Property(x => x.DriveItemId).HasMaxLength(256).IsRequired();
        b.HasIndex(x => new { x.SchoolId, x.DriveId, x.DriveItemId }).IsUnique();
        b.HasIndex(x => new { x.SchoolId, x.DriveItemId });
        b.HasIndex(x => new { x.SchoolId, x.OwnerTeacherId });
        b.HasOne(x => x.ParentFolder).WithMany().HasForeignKey(x => new { x.SchoolId, x.ParentFolderId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => x.OwnerTeacherId).OnDelete(DeleteBehavior.Restrict);
        StorageConfiguration.User(b, nameof(StorageFolder.CreatedByUserId));
        StorageConfiguration.User(b, nameof(StorageFolder.UpdatedByUserId));
        b.ToTable("StorageFolders", t =>
        {
            t.HasCheckConstraint("CK_StorageFolders_Parent", "[ParentFolderId] IS NULL OR [ParentFolderId] <> [Id]");
            t.HasCheckConstraint("CK_StorageFolders_Kind", "[Kind] BETWEEN 1 AND 3");
            t.HasTrigger("TR_StorageFolders_NoCycles");
        });
    }
}

public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> b)
    {
        StorageConfiguration.Record(b, "StoredFiles");
        // Id participates in the current-version FK; explicitly retain identity generation.
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.DisplayName).HasMaxLength(512).IsRequired();
        b.Property(x => x.LegacyFingerprint).HasMaxLength(64);
        b.Property(x => x.SharedWriterFingerprint).HasMaxLength(64);
        b.HasIndex(x => x.LegacySubmissionId).IsUnique().HasFilter("[LegacySubmissionId] IS NOT NULL");
        b.HasIndex(x => new { x.SchoolId, x.OwnerTeacherId });
        b.HasOne(x => x.Folder).WithMany().HasForeignKey(x => new { x.SchoolId, x.FolderId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        // The cyclic current-version FK is added explicitly in the migration. EF models
        // the scalar pointer; SQL enforces school + file + version without a key-inference cycle.
        // Teacher identity may later move school: keep the historical SchoolId on the file.
        b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => x.OwnerTeacherId).OnDelete(DeleteBehavior.Restrict);
        StorageConfiguration.User(b, nameof(StoredFile.DeletedByUserId));
        b.HasOne<TeacherEvidenceSubmission>().WithMany().HasForeignKey(x => x.LegacySubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
        b.ToTable("StoredFiles", t =>
        {
            t.HasCheckConstraint("CK_StoredFiles_SourceKind", "[SourceKind] BETWEEN 1 AND 4");
            t.HasTrigger("TR_StoredFiles_Provenance");
            t.HasTrigger("TR_StoredFiles_SharedProvenance");
        });
    }
}

public sealed class StoredFileVersionConfiguration : IEntityTypeConfiguration<StoredFileVersion>
{
    public void Configure(EntityTypeBuilder<StoredFileVersion> b)
    {
        StorageConfiguration.Record(b, "StoredFileVersions");
        b.HasAlternateKey(x => new { x.SchoolId, x.StoredFileId, x.Id });
        b.Property(x => x.DriveId).HasMaxLength(256).IsRequired();
        b.Property(x => x.DriveItemId).HasMaxLength(256).IsRequired();
        b.Property(x => x.DriveFileName).HasMaxLength(512).IsRequired();
        b.Property(x => x.FileExtension).HasMaxLength(32);
        b.Property(x => x.MimeType).HasMaxLength(256);
        b.Property(x => x.SHA256).HasMaxLength(64);
        b.HasIndex(x => new { x.SchoolId, x.StoredFileId, x.VersionNumber }).IsUnique();
        b.HasIndex(x => new { x.SchoolId, x.DriveId, x.DriveItemId }).IsUnique();
        b.HasIndex(x => new { x.SchoolId, x.DriveItemId });
        b.HasOne(x => x.StoredFile).WithMany().HasForeignKey(x => new { x.SchoolId, x.StoredFileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        StorageConfiguration.User(b, nameof(StoredFileVersion.UploadedByUserId));
        b.ToTable("StoredFileVersions", t =>
        {
            t.HasCheckConstraint("CK_StoredFileVersions_Size", "[SizeInBytes] >= 0 AND [VersionNumber] > 0");
            t.HasCheckConstraint("CK_StoredFileVersions_Availability", "[Availability] BETWEEN 1 AND 5");
            t.HasTrigger("TR_StoredFileVersions_Immutable");
        });
    }
}

public sealed class EvidenceRequirementConfiguration : IEntityTypeConfiguration<EvidenceRequirement>
{
    public void Configure(EntityTypeBuilder<EvidenceRequirement> b)
    {
        b.ToTable("EvidenceRequirements");
        b.HasKey(x => x.Id);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.Code).HasMaxLength(100).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(512).IsRequired();
        b.Property(x => x.DomainCode).HasMaxLength(100);
        b.Property(x => x.StandardCode).HasMaxLength(100);
        b.Property(x => x.ResponsibleRole).HasMaxLength(100);
        b.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.TemplateVersion, x.OriginalTaskId }).IsUnique()
            .HasFilter("[SchoolId] IS NOT NULL AND [AcademicYearId] IS NOT NULL AND [OriginalTaskId] IS NOT NULL");
        // Four NULL shapes: NULL scope means an actual template, never an implicit wildcard.
        b.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.TemplateVersion, x.Code }, "UX_Requirements_SchoolYear")
            .IsUnique().HasFilter("[SchoolId] IS NOT NULL AND [AcademicYearId] IS NOT NULL");
        b.HasIndex(x => new { x.SchoolId, x.TemplateVersion, x.Code }, "UX_Requirements_SchoolTemplate")
            .IsUnique().HasFilter("[SchoolId] IS NOT NULL AND [AcademicYearId] IS NULL");
        b.HasIndex(x => new { x.AcademicYearId, x.TemplateVersion, x.Code }, "UX_Requirements_GlobalYear")
            .IsUnique().HasFilter("[SchoolId] IS NULL AND [AcademicYearId] IS NOT NULL");
        b.HasIndex(x => new { x.TemplateVersion, x.Code }, "UX_Requirements_GlobalTemplate")
            .IsUnique().HasFilter("[SchoolId] IS NULL AND [AcademicYearId] IS NULL");
        b.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EvidenceTask>().WithMany().HasForeignKey(x => x.OriginalTaskId).OnDelete(DeleteBehavior.Restrict);
        StorageConfiguration.User(b, nameof(EvidenceRequirement.ResponsibleUserId));
        b.ToTable("EvidenceRequirements", t =>
        {
            t.HasCheckConstraint("CK_EvidenceRequirements_Policy", "[MinimumApprovedLinks] > 0 AND [TemplateVersion] > 0 AND [Importance] BETWEEN 1 AND 3 AND [FulfillmentPolicy] BETWEEN 1 AND 2");
            t.HasTrigger("TR_EvidenceRequirements_Scope");
        });
    }
}

public sealed class EvidenceLinkConfiguration : IEntityTypeConfiguration<EvidenceLink>
{
    public void Configure(EntityTypeBuilder<EvidenceLink> b)
    {
        StorageConfiguration.Record(b, "EvidenceLinks");
        b.HasAlternateKey(x => new { x.SchoolId, x.StoredFileId, x.Id });
        b.HasIndex(x => new { x.StoredFileId, x.RequirementId, x.TeacherId, x.AcademicYearId }, "UX_EvidenceLinks_Teacher")
            .IsUnique().HasFilter("[IsActive] = 1 AND [TeacherId] IS NOT NULL");
        b.HasIndex(x => new { x.StoredFileId, x.RequirementId, x.AcademicYearId }, "UX_EvidenceLinks_NoTeacher")
            .IsUnique().HasFilter("[IsActive] = 1 AND [TeacherId] IS NULL");
        b.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.RequirementId });
        b.HasOne(x => x.StoredFile).WithMany().HasForeignKey(x => new { x.SchoolId, x.StoredFileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Version).WithMany().HasForeignKey(x => new { x.SchoolId, x.StoredFileId, x.VersionId })
            .HasPrincipalKey(x => new { x.SchoolId, x.StoredFileId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Requirement).WithMany().HasForeignKey(x => x.RequirementId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable("EvidenceLinks", t =>
        {
            t.HasCheckConstraint("CK_EvidenceLinks_Status", "[Status] BETWEEN 1 AND 5");
            t.HasTrigger("TR_EvidenceLinks_Scope");
        });
    }
}

public sealed class EvidenceReviewDecisionConfiguration : IEntityTypeConfiguration<EvidenceReviewDecision>
{
    public void Configure(EntityTypeBuilder<EvidenceReviewDecision> b)
    {
        StorageConfiguration.Record(b, "EvidenceReviewDecisions");
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.ReviewerName).HasMaxLength(300);
        b.HasOne(x => x.EvidenceLink).WithMany().HasForeignKey(x => new { x.SchoolId, x.StoredFileId, x.EvidenceLinkId })
            .HasPrincipalKey(x => new { x.SchoolId, x.StoredFileId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Version).WithMany().HasForeignKey(x => new { x.SchoolId, x.StoredFileId, x.VersionId })
            .HasPrincipalKey(x => new { x.SchoolId, x.StoredFileId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        StorageConfiguration.User(b, nameof(EvidenceReviewDecision.ReviewedByUserId));
        b.ToTable("EvidenceReviewDecisions", t =>
        {
            t.HasCheckConstraint("CK_EvidenceReviewDecisions_Decision", "[Decision] IN (3,4) AND ([IsLegacyImported] = 1 OR ([ReviewedAtUtc] IS NOT NULL AND [ReviewedByUserId] IS NOT NULL))");
            t.HasCheckConstraint("CK_EvidenceReviewDecisions_RejectReason", "[Decision] <> 4 OR [IsLegacyImported] = 1 OR ([Note] IS NOT NULL AND LTRIM(RTRIM([Note])) <> '')");
            t.HasTrigger("TR_EvidenceReviewDecisions_AppendOnly");
        });
    }
}

public sealed class StorageDelegationConfiguration : IEntityTypeConfiguration<StorageDelegation>
{
    public void Configure(EntityTypeBuilder<StorageDelegation> b)
    {
        StorageConfiguration.Record(b, "StorageDelegations");
        StorageConfiguration.User(b, nameof(StorageDelegation.GranteeUserId));
        StorageConfiguration.User(b, nameof(StorageDelegation.GrantedByManagerUserId));
        StorageConfiguration.User(b, nameof(StorageDelegation.RevokedByManagerUserId));
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.RevocationReason).HasMaxLength(1000);
        b.HasIndex(x => new { x.SchoolId, x.GranteeUserId, x.StartsAt }).IsUnique();
        b.ToTable("StorageDelegations", t => t.HasCheckConstraint("CK_StorageDelegations_Dates", "[ExpiresAt] IS NULL OR [ExpiresAt] > [StartsAt]"));
    }
}

public sealed class VisitArchiveOperationConfiguration : IEntityTypeConfiguration<VisitArchiveOperation>
{
    public void Configure(EntityTypeBuilder<VisitArchiveOperation> b)
    {
        StorageConfiguration.Record(b, "VisitArchiveOperations");
        b.HasAlternateKey(x => new { x.SchoolId, x.VisitId, x.ApprovalRevision, x.Id });
        b.HasIndex(x => new { x.VisitId, x.ApprovalRevision }).IsUnique();
        b.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        b.Property(x => x.LastErrorCode).HasMaxLength(100);
        b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable("VisitArchiveOperations", t =>
        {
            t.HasCheckConstraint("CK_VisitArchiveOperations_State", "[ApprovalRevision] > 0 AND [Attempts] >= 0 AND [Status] BETWEEN 1 AND 5");
            t.HasTrigger("TR_VisitArchiveOperations_Scope");
        });
    }
}

public sealed class VisitArchiveArtifactConfiguration : IEntityTypeConfiguration<VisitArchiveArtifact>
{
    public void Configure(EntityTypeBuilder<VisitArchiveArtifact> b)
    {
        StorageConfiguration.Record(b, "VisitArchiveArtifacts");
        b.HasIndex(x => new { x.VisitId, x.ApprovalRevision }).IsUnique();
        b.HasIndex(x => x.VisitId, "UX_VisitArchiveArtifacts_Current").IsUnique().HasFilter("[IsCurrent] = 1");
        b.HasOne<VisitArchiveOperation>().WithMany().HasForeignKey(x => new { x.SchoolId, x.VisitId, x.ApprovalRevision, x.OperationId })
            .HasPrincipalKey(x => new { x.SchoolId, x.VisitId, x.ApprovalRevision, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StoredFileVersion>().WithMany().HasForeignKey(x => new { x.SchoolId, x.StoredFileVersionId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PrototypeImportBatchConfiguration : IEntityTypeConfiguration<PrototypeImportBatch>
{
    public void Configure(EntityTypeBuilder<PrototypeImportBatch> b)
    {
        StorageConfiguration.Record(b, "PrototypeImportBatches");
        b.Property(x => x.SourceSHA256).HasMaxLength(64).IsRequired();
        b.Property(x => x.SourceName).HasMaxLength(512).IsRequired();
        b.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.SourceSHA256 }).IsUnique();
        b.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        StorageConfiguration.User(b, nameof(PrototypeImportBatch.CreatedByUserId));
        b.ToTable("PrototypeImportBatches", t => t.HasCheckConstraint("CK_PrototypeImportBatches_Status", "[Status] BETWEEN 1 AND 4"));
    }
}

public sealed class PrototypeImportRowConfiguration : IEntityTypeConfiguration<PrototypeImportRow>
{
    public void Configure(EntityTypeBuilder<PrototypeImportRow> b)
    {
        StorageConfiguration.Record(b, "PrototypeImportRows");
        b.Property(x => x.SourceRowSHA256).HasMaxLength(64).IsRequired();
        b.Property(x => x.ReferencePath).HasMaxLength(2048);
        b.Property(x => x.ExceptionNote).HasMaxLength(2000);
        b.HasIndex(x => new { x.BatchId, x.SourceOrdinal }).IsUnique();
        b.HasOne<PrototypeImportBatch>().WithMany().HasForeignKey(x => new { x.SchoolId, x.BatchId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.ToTable("PrototypeImportRows", t => t.HasCheckConstraint("CK_PrototypeImportRows_State", "[SourceOrdinal] >= 0 AND [Status] BETWEEN 1 AND 4"));
    }
}
