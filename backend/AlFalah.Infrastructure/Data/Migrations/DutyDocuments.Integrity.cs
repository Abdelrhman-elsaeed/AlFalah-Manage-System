using Microsoft.EntityFrameworkCore.Migrations;

namespace AlFalah.Infrastructure.Data.Migrations;

public partial class AddSchoolDutyD3
{
    // CREATE TRIGGER must be its own batch even inside EF's idempotent IF wrapper.
    private static void Trigger(MigrationBuilder migration, string sql) => migration.Sql("EXEC(N'" + sql.Replace("'", "''") + "');");
    private static void AddIntegrity(MigrationBuilder migration)
    {
        Trigger(migration,"""
            CREATE TRIGGER TR_DutyPdfArtifacts_Immutable ON DutyPdfArtifacts AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51301, 'Duty PDF evidence is immutable.', 1; END
            """);
        Trigger(migration,"""
            CREATE TRIGGER TR_DutyPdfArtifacts_Integrity ON DutyPdfArtifacts AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS(SELECT 1 FROM inserted WHERE ISJSON(ProvenanceJson)<>1 OR DATALENGTH(Bytes)<100
                OR SUBSTRING(Bytes,1,5)<>0x255044462D OR CONVERT(varchar(64),HASHBYTES('SHA2_256',Bytes),2)<>Hash
                OR CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),CONVERT(varchar(max),ProvenanceJson COLLATE Latin1_General_100_BIN2_UTF8))),2)<>ProvenanceHash)
                THROW 51302, 'Duty PDF content hash is invalid.', 1;
              IF EXISTS(SELECT 1 FROM inserted i JOIN DutySignedDocuments d ON d.Id=i.DocumentId
                WHERE ISNULL(JSON_VALUE(i.ProvenanceJson,'$.SnapshotHash'),'')<>d.SnapshotHash
                OR ISNULL(JSON_VALUE(i.ProvenanceJson,'$.SignatureHash'),'')<>d.SignatureHash
                OR ISNULL(TRY_CONVERT(int,JSON_VALUE(i.ProvenanceJson,'$.Id')),0)<>d.Id
                OR ISNULL(JSON_VALUE(i.ProvenanceJson,'$.RendererVersion'),'')<>i.RendererVersion)
                THROW 51303, 'Duty PDF provenance does not match its signed evidence.', 1;
            END
            """);
        Trigger(migration,"""
            CREATE TRIGGER TR_DutyArchiveIntents_Identity ON DutyArchiveIntents AFTER INSERT, UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL
                OR i.SchoolId<>d.SchoolId OR i.DocumentId<>d.DocumentId OR i.CreatedAt<>d.CreatedAt
                OR (d.ArtifactId IS NOT NULL AND ISNULL(i.ArtifactId,0)<>d.ArtifactId)
                OR (d.OperationIdentity IS NOT NULL AND ISNULL(i.OperationIdentity,'')<>d.OperationIdentity)
                OR (d.ProviderItemId IS NOT NULL AND ISNULL(i.ProviderItemId,'')<>d.ProviderItemId)
                OR (d.DriveId IS NOT NULL AND ISNULL(i.DriveId,'')<>d.DriveId)
                OR (d.SchoolRootItemId IS NOT NULL AND ISNULL(i.SchoolRootItemId,'')<>d.SchoolRootItemId)
                OR (d.FolderId IS NOT NULL AND ISNULL(i.FolderId,0)<>d.FolderId)
                OR (d.FolderItemId IS NOT NULL AND ISNULL(i.FolderItemId,'')<>d.FolderItemId)
                OR (d.StoredFileVersionId IS NOT NULL AND ISNULL(i.StoredFileVersionId,0)<>d.StoredFileVersionId)
                OR (d.State IN ('Archived','TestVerified') AND i.State<>d.State))
                THROW 51304, 'Duty archive operation identity and completed history are immutable.', 1;
              IF EXISTS(SELECT 1 FROM inserted i JOIN DutyPdfArtifacts a ON a.Id=i.ArtifactId
                WHERE a.DocumentId IS NULL OR a.DocumentId<>i.DocumentId)
                THROW 51305, 'Duty archive artifact belongs to another document.', 1;
              IF EXISTS(SELECT 1 FROM inserted i JOIN StorageFolders f ON f.Id=i.FolderId
                WHERE f.Kind<>4 OR f.DriveItemId<>i.FolderItemId)
                THROW 51306, 'Duty archive requires a protected duty folder.', 1;
              IF EXISTS(SELECT 1 FROM inserted i WHERE i.State IN ('Archived','TestVerified') AND
                (i.VerifiedAt IS NULL OR i.ArtifactId IS NULL OR i.StoredFileVersionId IS NULL OR NOT EXISTS(
                  SELECT 1 FROM StoredFileVersions v JOIN StoredFiles f ON f.Id=v.StoredFileId
                  JOIN DutyPdfArtifacts a ON a.Id=i.ArtifactId WHERE v.Id=i.StoredFileVersionId AND f.SourceKind=5
                  AND f.FolderId=i.FolderId AND v.DriveItemId=i.ProviderItemId AND v.SHA256=a.Hash AND v.SizeInBytes=DATALENGTH(a.Bytes))))
                THROW 51307, 'Duty completion requires committed verified ledger evidence.', 1;
            END
            """);
        Trigger(migration,"""
            CREATE TRIGGER TR_StorageFolders_DutyProtection ON StorageFolders AFTER INSERT, UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS(SELECT 1 FROM inserted WHERE (Kind=4 AND DutyIdentity IS NULL) OR (Kind<>4 AND (DutyIdentity IS NOT NULL OR DutyProvisioned=1)))
                THROW 51308, 'Duty folder identity requires the duty folder kind.', 1;
              IF EXISTS(SELECT 1 FROM inserted i JOIN StorageFolders p ON p.Id=i.ParentFolderId
                WHERE (p.Kind=4 AND i.Kind<>4) OR (i.Kind=4 AND p.Kind<>4))
                THROW 51309, 'Duty folder hierarchy is protected.', 1;
              IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE d.Kind=4 AND
                (i.Id IS NULL OR i.Kind<>4 OR i.SchoolId<>d.SchoolId OR ISNULL(i.ParentFolderId,0)<>ISNULL(d.ParentFolderId,0)
                OR i.DriveId<>d.DriveId OR i.DriveItemId<>d.DriveItemId OR i.DisplayName<>d.DisplayName
                OR ISNULL(i.DutyIdentity,'')<>d.DutyIdentity OR i.IsActive<>d.IsActive
                OR ISNULL(i.OwnerTeacherId,0)<>ISNULL(d.OwnerTeacherId,0) OR i.DutyProvisioned<d.DutyProvisioned))
                THROW 51310, 'Duty folders cannot be renamed, moved, rebound or removed.', 1;
            END
            """);
        Trigger(migration,"""
            CREATE TRIGGER TR_StoredFiles_DutyProtection ON StoredFiles AFTER INSERT, UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS(SELECT 1 FROM inserted i JOIN StorageFolders f ON f.Id=i.FolderId WHERE
                (i.SourceKind=5 AND (f.Kind<>4 OR i.DutyIdentity IS NULL OR i.OwnerTeacherId IS NOT NULL
                  OR i.NeedsLink=1 OR i.LegacySubmissionId IS NOT NULL)) OR (i.SourceKind<>5 AND (f.Kind=4 OR i.DutyIdentity IS NOT NULL)))
                THROW 51311, 'Duty files require independent protected provenance.', 1;
              IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE d.SourceKind=5 AND
                (i.Id IS NULL OR i.SourceKind<>5 OR i.SchoolId<>d.SchoolId OR i.FolderId<>d.FolderId
                OR i.DisplayName<>d.DisplayName OR ISNULL(i.DutyIdentity,'')<>d.DutyIdentity
                OR i.IsDeleted<>d.IsDeleted OR i.NeedsLink<>d.NeedsLink OR i.OwnerTeacherId IS NOT NULL))
                THROW 51312, 'Duty file identities and signed history are protected.', 1;
              IF EXISTS(SELECT 1 FROM inserted i JOIN StoredFileVersions v ON v.Id=i.CurrentVersionId
                WHERE i.SourceKind=5 AND (v.StoredFileId<>i.Id OR NOT EXISTS(SELECT 1 FROM DutyArchiveIntents a
                  WHERE a.ProviderItemId=v.DriveItemId AND a.SchoolId=i.SchoolId AND a.FolderId=i.FolderId)))
                THROW 51313, 'Duty current version requires reserved operation evidence.', 1;
            END
            """);
        Trigger(migration,"""
            CREATE TRIGGER TR_StoredFileVersions_DutyProvenance ON StoredFileVersions AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS(SELECT 1 FROM inserted i JOIN StoredFiles f ON f.Id=i.StoredFileId WHERE f.SourceKind=5 AND NOT EXISTS(
                SELECT 1 FROM DutyArchiveIntents o JOIN DutyPdfArtifacts a ON a.Id=o.ArtifactId
                JOIN DutySignedDocuments d ON d.Id=o.DocumentId WHERE o.SchoolId=i.SchoolId AND o.FolderId=f.FolderId
                AND o.ProviderItemId=i.DriveItemId AND o.DriveId=i.DriveId AND a.Hash=i.SHA256
                AND i.SizeInBytes=DATALENGTH(a.Bytes) AND i.UploadedByUserId=d.AuthorUserId AND i.MimeType='application/pdf'))
                THROW 51314, 'Duty version must match frozen bytes and the reserved identity.', 1;
            END
            """);
        Trigger(migration,"""
            CREATE TRIGGER TR_EvidenceLinks_NoDuty ON EvidenceLinks AFTER INSERT, UPDATE AS
            BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN StoredFiles f ON f.Id=i.StoredFileId WHERE f.SourceKind=5)
              THROW 51315, 'Duty documents cannot fulfill evidence requirements.', 1; END
            """);
        Trigger(migration,"""
            CREATE TRIGGER TR_FileChangeRequests_NoDuty ON FileChangeRequests AFTER INSERT, UPDATE AS
            BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN StoredFiles f ON f.Id=i.StoredFileId WHERE f.SourceKind=5)
              THROW 51316, 'Duty documents cannot enter general storage change workflows.', 1; END
            """);
    }
}
