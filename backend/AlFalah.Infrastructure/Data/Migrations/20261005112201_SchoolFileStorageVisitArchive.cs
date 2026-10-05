using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchoolFileStorageVisitArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalRevision",
                table: "Visits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Visits",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalSource",
                table: "VisitArchiveOperations",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAtUtc",
                table: "VisitArchiveOperations",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "ArchiveFolderId",
                table: "VisitArchiveOperations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveFolderItemId",
                table: "VisitArchiveOperations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAtUtc",
                table: "VisitArchiveOperations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriveId",
                table: "VisitArchiveOperations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAtUtc",
                table: "VisitArchiveOperations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAtUtc",
                table: "VisitArchiveOperations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseToken",
                table: "VisitArchiveOperations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "PdfBytes",
                table: "VisitArchiveOperations",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfSHA256",
                table: "VisitArchiveOperations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderItemId",
                table: "VisitArchiveOperations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecoveryGeneration",
                table: "VisitArchiveOperations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SchoolRootItemId",
                table: "VisitArchiveOperations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotJson",
                table: "VisitArchiveOperations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotSHA256",
                table: "VisitArchiveOperations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UploadIdentity",
                table: "VisitArchiveOperations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UploadStarted",
                table: "VisitArchiveOperations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReconciledAtUtc",
                table: "VisitArchiveArtifacts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginalStoredFileVersionId",
                table: "VisitArchiveArtifacts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitArchiveOperations_SchoolId_ApprovedAtUtc_Id",
                table: "VisitArchiveOperations",
                columns: new[] { "SchoolId", "ApprovedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitArchiveArtifacts_SchoolId_OriginalStoredFileVersionId",
                table: "VisitArchiveArtifacts",
                columns: new[] { "SchoolId", "OriginalStoredFileVersionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_VisitArchiveArtifacts_StoredFileVersions_SchoolId_OriginalStoredFileVersionId",
                table: "VisitArchiveArtifacts",
                columns: new[] { "SchoolId", "OriginalStoredFileVersionId" },
                principalTable: "StoredFileVersions",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);
            AddIntegrity(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new InvalidOperationException("Archive rollback disables flags and preserves approvals, snapshots and file history.");
    }
}
