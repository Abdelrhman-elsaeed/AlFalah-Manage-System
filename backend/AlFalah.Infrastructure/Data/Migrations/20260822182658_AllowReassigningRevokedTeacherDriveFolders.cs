using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowReassigningRevokedTeacherDriveFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeacherDriveFolders_DriveId_RootItemId",
                table: "TeacherDriveFolders");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherDriveFolders_DriveId_RootItemId",
                table: "TeacherDriveFolders",
                columns: new[] { "DriveId", "RootItemId" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeacherDriveFolders_DriveId_RootItemId",
                table: "TeacherDriveFolders");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherDriveFolders_DriveId_RootItemId",
                table: "TeacherDriveFolders",
                columns: new[] { "DriveId", "RootItemId" },
                unique: true);
        }
    }
}
