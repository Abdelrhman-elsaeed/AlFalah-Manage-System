using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchoolFileStoragePreApprovalReplacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReplaceBeforeReview",
                table: "FileChangeRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);
            var historyTrigger = """
                ALTER TRIGGER TR_FileChangeRequests_History ON FileChangeRequests AFTER UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
                        THROW 51102, 'File change requests cannot be removed.', 1;
                    IF EXISTS(SELECT Id, SchoolId, StoredFileId, OriginalVersionId, Kind, Reason, RequestedByUserId, CreatedAtUtc, ReplaceBeforeReview FROM deleted
                        EXCEPT SELECT Id, SchoolId, StoredFileId, OriginalVersionId, Kind, Reason, RequestedByUserId, CreatedAtUtc, ReplaceBeforeReview FROM inserted)
                        THROW 51103, 'File change request provenance is immutable.', 1;
                    IF EXISTS(SELECT 1 FROM deleted d JOIN inserted i ON i.Id=d.Id WHERE d.Status <> 'Pending' AND
                        (i.Status <> d.Status OR ISNULL(i.CandidateVersionId,0) <> ISNULL(d.CandidateVersionId,0)))
                        THROW 51104, 'Decided file changes are immutable.', 1;
                    IF EXISTS(SELECT 1 FROM deleted d JOIN inserted i ON i.Id=d.Id WHERE d.CandidateVersionId IS NOT NULL AND
                        ISNULL(i.CandidateVersionId,0) <> d.CandidateVersionId)
                        THROW 51105, 'Candidate version cannot be replaced.', 1;
                END
                """;
            migrationBuilder.Sql("EXEC(N'" + historyTrigger.Replace("'", "''") + "');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Preserve file change policy history. Roll back application flags, not storage records.");
        }
    }
}
