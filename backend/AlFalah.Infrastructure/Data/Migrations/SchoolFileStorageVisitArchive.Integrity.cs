using Microsoft.EntityFrameworkCore.Migrations;

namespace AlFalah.Infrastructure.Data.Migrations;

public partial class SchoolFileStorageVisitArchive
{
    private static void AddIntegrity(MigrationBuilder migrationBuilder)
    {
        static void Trigger(MigrationBuilder migration, string sql) => migration.Sql("EXEC(N'" + sql.Replace("'", "''") + "');");
        Trigger(migrationBuilder, """
            CREATE OR ALTER TRIGGER dbo.TR_Visits_ApprovalRevision ON dbo.Visits AFTER UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.Id=d.Id WHERE i.ApprovalRevision<d.ApprovalRevision)
                THROW 51201, 'Approval revision cannot decrease.', 1;
            END
            """);
        Trigger(migrationBuilder, """
            CREATE OR ALTER TRIGGER dbo.TR_VisitArchiveArtifacts_History ON dbo.VisitArchiveArtifacts AFTER UPDATE,DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON d.Id=i.Id WHERE i.Id IS NULL OR
                i.SchoolId<>d.SchoolId OR i.VisitId<>d.VisitId OR i.ApprovalRevision<>d.ApprovalRevision OR i.OperationId<>d.OperationId OR
                (d.OriginalStoredFileVersionId IS NOT NULL AND (i.OriginalStoredFileVersionId IS NULL OR i.OriginalStoredFileVersionId<>d.OriginalStoredFileVersionId)))
                THROW 51204, 'Archive identity and original history are immutable.', 1;
              IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.Id=d.Id
                JOIN StoredFileVersions oldVersion ON oldVersion.Id=d.StoredFileVersionId
                JOIN StoredFileVersions newVersion ON newVersion.Id=i.StoredFileVersionId
                WHERE oldVersion.StoredFileId<>newVersion.StoredFileId)
                THROW 51205, 'Recreation must retain the original file identity.', 1;
            END
            """);
        Trigger(migrationBuilder, """
            CREATE OR ALTER TRIGGER dbo.TR_VisitArchiveOperations_Snapshot ON dbo.VisitArchiveOperations AFTER UPDATE,DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON d.Id=i.Id WHERE d.SnapshotJson IS NOT NULL AND
                (i.Id IS NULL OR i.SnapshotJson IS NULL OR CONVERT(varbinary(max),i.SnapshotJson)<>CONVERT(varbinary(max),d.SnapshotJson) OR
                 i.SnapshotSHA256 IS NULL OR CONVERT(varbinary(max),i.SnapshotSHA256)<>CONVERT(varbinary(max),d.SnapshotSHA256) OR i.VisitId<>d.VisitId OR i.SchoolId<>d.SchoolId OR
                 i.ApprovalRevision<>d.ApprovalRevision OR i.ApprovedAtUtc<>d.ApprovedAtUtc OR i.ApprovalSource IS NULL OR i.ApprovalSource<>d.ApprovalSource))
                THROW 51202, 'Approval snapshots are immutable.', 1;
              IF EXISTS (SELECT 1 FROM deleted d JOIN inserted i ON d.Id=i.Id WHERE d.PdfBytes IS NOT NULL AND
                (i.PdfBytes IS NULL OR i.PdfBytes<>d.PdfBytes OR i.PdfSHA256 IS NULL OR i.PdfSHA256<>d.PdfSHA256))
                THROW 51203, 'Prepared report bytes are immutable.', 1;
            END
            """);
    }
}
