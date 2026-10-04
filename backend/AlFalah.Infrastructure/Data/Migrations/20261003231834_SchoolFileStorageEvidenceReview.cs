using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchoolFileStorageEvidenceReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SharedWriterFingerprint",
                table: "StoredFiles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharedWriterProvenanceJson",
                table: "StoredFiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChangeRequestId",
                table: "StorageOperations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FileChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StoredFileId = table.Column<int>(type: "int", nullable: false),
                    OriginalVersionId = table.Column<int>(type: "int", nullable: false),
                    CandidateVersionId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileChangeRequests", x => x.Id);
                    table.UniqueConstraint("AK_FileChangeRequests_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_FileChangeRequest_State", "[Kind] IN ('Replace','Delete') AND [Status] IN ('Pending','Approved','Rejected') AND LTRIM(RTRIM([Reason])) <> ''");
                    table.ForeignKey(
                        name: "FK_FileChangeRequests_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileChangeRequests_StoredFileVersions_SchoolId_StoredFileId_CandidateVersionId",
                        columns: x => new { x.SchoolId, x.StoredFileId, x.CandidateVersionId },
                        principalTable: "StoredFileVersions",
                        principalColumns: new[] { "SchoolId", "StoredFileId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileChangeRequests_StoredFileVersions_SchoolId_StoredFileId_OriginalVersionId",
                        columns: x => new { x.SchoolId, x.StoredFileId, x.OriginalVersionId },
                        principalTable: "StoredFileVersions",
                        principalColumns: new[] { "SchoolId", "StoredFileId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileChangeRequests_StoredFiles_SchoolId_StoredFileId",
                        columns: x => new { x.SchoolId, x.StoredFileId },
                        principalTable: "StoredFiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileChangeRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FileChangeDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    FileChangeRequestId = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileChangeDecisions", x => x.Id);
                    table.UniqueConstraint("AK_FileChangeDecisions_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_FileChangeDecision_State", "[Decision] IN ('Approved','Rejected') AND ([Decision] <> 'Rejected' OR LTRIM(RTRIM([Note])) <> '' AND [Note] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_FileChangeDecisions_FileChangeRequests_SchoolId_FileChangeRequestId",
                        columns: x => new { x.SchoolId, x.FileChangeRequestId },
                        principalTable: "FileChangeRequests",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileChangeDecisions_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileChangeDecisions_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_ChangeRequestId",
                table: "StorageOperations",
                column: "ChangeRequestId",
                unique: true,
                filter: "[ChangeRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_SchoolId_ChangeRequestId",
                table: "StorageOperations",
                columns: new[] { "SchoolId", "ChangeRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_FileChangeDecisions_FileChangeRequestId",
                table: "FileChangeDecisions",
                column: "FileChangeRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileChangeDecisions_ReviewedByUserId",
                table: "FileChangeDecisions",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FileChangeDecisions_SchoolId_FileChangeRequestId",
                table: "FileChangeDecisions",
                columns: new[] { "SchoolId", "FileChangeRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_FileChangeRequests_RequestedByUserId",
                table: "FileChangeRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FileChangeRequests_SchoolId_StoredFileId",
                table: "FileChangeRequests",
                columns: new[] { "SchoolId", "StoredFileId" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_FileChangeRequests_SchoolId_StoredFileId_CandidateVersionId",
                table: "FileChangeRequests",
                columns: new[] { "SchoolId", "StoredFileId", "CandidateVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_FileChangeRequests_SchoolId_StoredFileId_OriginalVersionId",
                table: "FileChangeRequests",
                columns: new[] { "SchoolId", "StoredFileId", "OriginalVersionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_StorageOperations_FileChangeRequests_SchoolId_ChangeRequestId",
                table: "StorageOperations",
                columns: new[] { "SchoolId", "ChangeRequestId" },
                principalTable: "FileChangeRequests",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);
            AddEvidenceIntegrity(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("S3 rollback closes feature flags and retains versions and decisions.");
        }
    }
}
