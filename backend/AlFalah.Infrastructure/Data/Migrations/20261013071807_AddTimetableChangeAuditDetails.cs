using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetableChangeAuditDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangeDetailsJson",
                table: "SchoolTimetableVersions",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeSummary",
                table: "SchoolTimetableVersions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "Arabic_CI_AS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChangeDetailsJson",
                table: "SchoolTimetableVersions");

            migrationBuilder.DropColumn(
                name: "ChangeSummary",
                table: "SchoolTimetableVersions");
        }
    }
}
