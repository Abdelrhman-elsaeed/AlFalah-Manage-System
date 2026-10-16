using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyStandbyAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StandbySettingsJson",
                table: "SchoolTimetables",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<bool>(
                name: "IsStandbyLocked",
                table: "SchoolTimetableEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StandbySource",
                table: "SchoolTimetableEntries",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StandbySettingsJson",
                table: "SchoolTimetables");

            migrationBuilder.DropColumn(
                name: "IsStandbyLocked",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropColumn(
                name: "StandbySource",
                table: "SchoolTimetableEntries");
        }
    }
}
