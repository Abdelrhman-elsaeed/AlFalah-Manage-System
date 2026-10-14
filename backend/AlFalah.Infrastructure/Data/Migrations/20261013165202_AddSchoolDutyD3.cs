using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolDutyD3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StoredFiles_SourceKind",
                table: "StoredFiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StorageFolders_Kind",
                table: "StorageFolders");

            migrationBuilder.AddColumn<string>(
                name: "DutyIdentity",
                table: "StoredFiles",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DutyIdentity",
                table: "StorageFolders",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DutyProvisioned",
                table: "StorageFolders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DutyArchiveEnabled",
                table: "SchoolGoogleDrives",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ArtifactId",
                table: "DutyArchiveIntents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "DutyArchiveIntents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DriveId",
                table: "DutyArchiveIntents",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                table: "DutyArchiveIntents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FolderId",
                table: "DutyArchiveIntents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FolderItemId",
                table: "DutyArchiveIntents",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAt",
                table: "DutyArchiveIntents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAt",
                table: "DutyArchiveIntents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseToken",
                table: "DutyArchiveIntents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "DutyArchiveIntents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationIdentity",
                table: "DutyArchiveIntents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderItemId",
                table: "DutyArchiveIntents",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DutyArchiveIntents",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "SchoolRootItemId",
                table: "DutyArchiveIntents",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StoredFileVersionId",
                table: "DutyArchiveIntents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerifiedAt",
                table: "DutyArchiveIntents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DutyPdfArtifacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: true),
                    PlanId = table.Column<int>(type: "int", nullable: true),
                    PlanRevision = table.Column<int>(type: "int", nullable: false),
                    ProvenanceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProvenanceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RendererVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Bytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PreparedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyPdfArtifacts", x => x.Id);
                    table.UniqueConstraint("AK_DutyPdfArtifacts_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_DutyPdfArtifacts_Identity", "([DocumentId] IS NOT NULL AND [PlanId] IS NULL) OR ([DocumentId] IS NULL AND [PlanId] IS NOT NULL AND [PlanRevision] > 0)");
                    table.ForeignKey(
                        name: "FK_DutyPdfArtifacts_DutyPlans_SchoolId_PlanId",
                        columns: x => new { x.SchoolId, x.PlanId },
                        principalTable: "DutyPlans",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_DutyPdfArtifacts_DutySignedDocuments_SchoolId_DocumentId",
                        columns: x => new { x.SchoolId, x.DocumentId },
                        principalTable: "DutySignedDocuments",
                        principalColumns: new[] { "SchoolId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_SchoolId_DutyIdentity",
                table: "StoredFiles",
                columns: new[] { "SchoolId", "DutyIdentity" },
                unique: true,
                filter: "[DutyIdentity] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StoredFiles_SourceKind",
                table: "StoredFiles",
                sql: "[SourceKind] BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_StorageFolders_SchoolId_DutyIdentity",
                table: "StorageFolders",
                columns: new[] { "SchoolId", "DutyIdentity" },
                unique: true,
                filter: "[DutyIdentity] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StorageFolders_Kind",
                table: "StorageFolders",
                sql: "[Kind] BETWEEN 1 AND 4");

            migrationBuilder.CreateIndex(
                name: "IX_DutyArchiveIntents_OperationIdentity",
                table: "DutyArchiveIntents",
                column: "OperationIdentity",
                unique: true,
                filter: "[OperationIdentity] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DutyArchiveIntents_SchoolId_ArtifactId",
                table: "DutyArchiveIntents",
                columns: new[] { "SchoolId", "ArtifactId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyArchiveIntents_SchoolId_FolderId",
                table: "DutyArchiveIntents",
                columns: new[] { "SchoolId", "FolderId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyArchiveIntents_SchoolId_StoredFileVersionId",
                table: "DutyArchiveIntents",
                columns: new[] { "SchoolId", "StoredFileVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyArchiveIntents_State_NextAttemptAt",
                table: "DutyArchiveIntents",
                columns: new[] { "State", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyPdfArtifacts_DocumentId",
                table: "DutyPdfArtifacts",
                column: "DocumentId",
                unique: true,
                filter: "[DocumentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DutyPdfArtifacts_SchoolId_DocumentId",
                table: "DutyPdfArtifacts",
                columns: new[] { "SchoolId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyPdfArtifacts_SchoolId_PlanId_PlanRevision",
                table: "DutyPdfArtifacts",
                columns: new[] { "SchoolId", "PlanId", "PlanRevision" },
                unique: true,
                filter: "[PlanId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_DutyArchiveIntents_DutyPdfArtifacts_SchoolId_ArtifactId",
                table: "DutyArchiveIntents",
                columns: new[] { "SchoolId", "ArtifactId" },
                principalTable: "DutyPdfArtifacts",
                principalColumns: new[] { "SchoolId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_DutyArchiveIntents_StorageFolders_SchoolId_FolderId",
                table: "DutyArchiveIntents",
                columns: new[] { "SchoolId", "FolderId" },
                principalTable: "StorageFolders",
                principalColumns: new[] { "SchoolId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_DutyArchiveIntents_StoredFileVersions_SchoolId_StoredFileVersionId",
                table: "DutyArchiveIntents",
                columns: new[] { "SchoolId", "StoredFileVersionId" },
                principalTable: "StoredFileVersions",
                principalColumns: new[] { "SchoolId", "Id" });
            AddIntegrity(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Disable new duty processing for rollback; signed evidence and artifacts must be retained.");
        }
    }
}
