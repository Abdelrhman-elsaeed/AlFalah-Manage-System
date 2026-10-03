using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchoolFileStorageLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StorageOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FolderId = table.Column<int>(type: "int", nullable: true),
                    StoredFileId = table.Column<int>(type: "int", nullable: true),
                    VersionId = table.Column<int>(type: "int", nullable: true),
                    OwnerTeacherId = table.Column<int>(type: "int", nullable: true),
                    DriveId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProviderItemId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ParentItemId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    SHA256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LegacyTaskId = table.Column<int>(type: "int", nullable: true),
                    LegacyOperationId = table.Column<long>(type: "bigint", nullable: true),
                    LegacySubmissionId = table.Column<long>(type: "bigint", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageOperations", x => x.Id);
                    table.UniqueConstraint("AK_StorageOperations_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_StorageOperations_Status", "[Status] IN ('Pending','Completed','Failed','NeedsAttention')");
                    table.ForeignKey(
                        name: "FK_StorageOperations_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageOperations_StorageFolders_SchoolId_FolderId",
                        columns: x => new { x.SchoolId, x.FolderId },
                        principalTable: "StorageFolders",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageOperations_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_ActorUserId",
                table: "StorageOperations",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_SchoolId_ActorUserId_RequestKey",
                table: "StorageOperations",
                columns: new[] { "SchoolId", "ActorUserId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_SchoolId_FolderId",
                table: "StorageOperations",
                columns: new[] { "SchoolId", "FolderId" });

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_SchoolId_ProviderItemId",
                table: "StorageOperations",
                columns: new[] { "SchoolId", "ProviderItemId" },
                unique: true,
                filter: "[Action] IN ('Upload','CreateFolder')");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_SchoolId_RequestKey",
                table: "StorageOperations",
                columns: new[] { "SchoolId", "RequestKey" },
                unique: true,
                filter: "[Action] = 'CreateFolder'");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_SchoolId_Status_CreatedAtUtc",
                table: "StorageOperations",
                columns: new[] { "SchoolId", "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Storage operation history must be retained. Disable the workspace flags to roll back.");
        }
    }
}
