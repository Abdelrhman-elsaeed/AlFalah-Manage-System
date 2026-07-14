using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitLessonTitleAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AbsentCount",
                table: "Visits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LessonTitle",
                table: "Visits",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                collation: "Arabic_CI_AS");

            migrationBuilder.AddColumn<int>(
                name: "PresentCount",
                table: "Visits",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbsentCount",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "LessonTitle",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "PresentCount",
                table: "Visits");
        }
    }
}
