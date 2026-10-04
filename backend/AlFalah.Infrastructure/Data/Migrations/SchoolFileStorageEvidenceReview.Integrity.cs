using Microsoft.EntityFrameworkCore.Migrations;

namespace AlFalah.Infrastructure.Data.Migrations;

public partial class SchoolFileStorageEvidenceReview
{
    private static void SqlTrigger(MigrationBuilder builder, string sql) =>
        builder.Sql("EXEC(N'" + sql.Replace("'", "''") + "');");

    private static void AddEvidenceIntegrity(MigrationBuilder m)
    {
        SqlTrigger(m, """
            CREATE TRIGGER TR_FileChangeDecisions_AppendOnly ON FileChangeDecisions AFTER UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS(SELECT 1 FROM deleted) THROW 51101, 'File change decisions are append-only.', 1;
            END
            """);
        SqlTrigger(m, """
            CREATE TRIGGER TR_FileChangeRequests_History ON FileChangeRequests AFTER UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
                    THROW 51102, 'File change requests cannot be removed.', 1;
                IF EXISTS(SELECT Id, SchoolId, StoredFileId, OriginalVersionId, Kind, Reason, RequestedByUserId, CreatedAtUtc FROM deleted
                    EXCEPT SELECT Id, SchoolId, StoredFileId, OriginalVersionId, Kind, Reason, RequestedByUserId, CreatedAtUtc FROM inserted)
                    THROW 51103, 'File change request provenance is immutable.', 1;
                IF EXISTS(SELECT 1 FROM deleted d JOIN inserted i ON i.Id=d.Id WHERE d.Status <> 'Pending' AND
                    (i.Status <> d.Status OR ISNULL(i.CandidateVersionId,0) <> ISNULL(d.CandidateVersionId,0)))
                    THROW 51104, 'Decided file changes are immutable.', 1;
                IF EXISTS(SELECT 1 FROM deleted d JOIN inserted i ON i.Id=d.Id WHERE d.CandidateVersionId IS NOT NULL AND
                    ISNULL(i.CandidateVersionId,0) <> d.CandidateVersionId)
                    THROW 51105, 'Candidate version cannot be replaced.', 1;
            END
            """);
        SqlTrigger(m, """
            CREATE TRIGGER TR_StoredFiles_SharedProvenance ON StoredFiles AFTER UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS(SELECT Id, SharedWriterProvenanceJson, SharedWriterFingerprint FROM deleted WHERE SharedWriterFingerprint IS NOT NULL
                    EXCEPT SELECT Id, SharedWriterProvenanceJson, SharedWriterFingerprint FROM inserted)
                    THROW 51106, 'Shared writer provenance is immutable.', 1;
            END
            """);
        SqlTrigger(m, """
            CREATE TRIGGER TR_AuditLogs_StorageAppendOnly ON AuditLogs AFTER UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS(SELECT 1 FROM deleted WHERE Action LIKE 'Storage.%')
                    THROW 51107, 'Storage audit is append-only.', 1;
            END
            """);
        SqlTrigger(m, """
            CREATE TRIGGER TR_TeacherEvidenceSubmissions_ReviewAuthority ON TeacherEvidenceSubmissions AFTER UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS(SELECT d.Id, d.ReviewStatus, d.ReviewNote, d.ReviewedAtUtc, d.ReviewedByUserId FROM deleted d
                    WHERE EXISTS(SELECT 1 FROM StoredFiles f WHERE f.LegacySubmissionId=d.Id)
                    EXCEPT SELECT i.Id, i.ReviewStatus, i.ReviewNote, i.ReviewedAtUtc, i.ReviewedByUserId FROM inserted i)
                    THROW 51108, 'Mapped submission review is a compatibility baseline; use EvidenceLink decisions.', 1;
            END
            """);
    }
}
