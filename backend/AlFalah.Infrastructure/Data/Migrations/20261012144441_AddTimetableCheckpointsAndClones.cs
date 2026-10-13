using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetableCheckpointsAndClones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckpointName",
                table: "SchoolTimetableVersions",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true,
                collation: "Arabic_CI_AS");

            migrationBuilder.AddColumn<string>(
                name: "CheckpointNote",
                table: "SchoolTimetableVersions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                collation: "Arabic_CI_AS");

            migrationBuilder.AddColumn<int>(
                name: "SourceTimetableId",
                table: "SchoolTimetableVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceVersionNumber",
                table: "SchoolTimetableVersions",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckpointName",
                table: "SchoolTimetableVersions");

            migrationBuilder.DropColumn(
                name: "CheckpointNote",
                table: "SchoolTimetableVersions");

            migrationBuilder.DropColumn(
                name: "SourceTimetableId",
                table: "SchoolTimetableVersions");

            migrationBuilder.DropColumn(
                name: "SourceVersionNumber",
                table: "SchoolTimetableVersions");
        }
    }
}
