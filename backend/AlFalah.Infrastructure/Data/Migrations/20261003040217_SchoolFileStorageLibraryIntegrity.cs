using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchoolFileStorageLibraryIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_SchoolId_OwnerTeacherId_FolderId_DisplayName_Id",
                table: "StoredFiles",
                columns: new[] { "SchoolId", "OwnerTeacherId", "FolderId", "DisplayName", "Id" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_SchoolId_StoredFileId_VersionId",
                table: "StorageOperations",
                columns: new[] { "SchoolId", "StoredFileId", "VersionId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StorageOperations_Completed",
                table: "StorageOperations",
                sql: "[Status] <> 'Completed' OR [Action] <> 'Upload' OR ([StoredFileId] IS NOT NULL AND [VersionId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_StorageFolders_SchoolId_Kind",
                table: "StorageFolders",
                columns: new[] { "SchoolId", "Kind" },
                unique: true,
                filter: "[IsActive] = 1 AND [OwnerTeacherId] IS NULL AND [ParentFolderId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StorageFolders_SchoolId_OwnerTeacherId_ParentFolderId_DisplayName_Id",
                table: "StorageFolders",
                columns: new[] { "SchoolId", "OwnerTeacherId", "ParentFolderId", "DisplayName", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_StorageOperations_StoredFileVersions_SchoolId_StoredFileId_VersionId",
                table: "StorageOperations",
                columns: new[] { "SchoolId", "StoredFileId", "VersionId" },
                principalTable: "StoredFileVersions",
                principalColumns: new[] { "SchoolId", "StoredFileId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StorageOperations_StoredFiles_SchoolId_StoredFileId",
                table: "StorageOperations",
                columns: new[] { "SchoolId", "StoredFileId" },
                principalTable: "StoredFiles",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Storage integrity and operation history must survive rollback. Disable the workspace flags.");
        }
    }
}
