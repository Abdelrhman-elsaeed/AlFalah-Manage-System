using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetableCloneIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetables_SchoolId_AcademicYearId_Semester_TimetableSetupProfileId",
                table: "SchoolTimetables");

            migrationBuilder.AddColumn<bool>(
                name: "IsClone",
                table: "SchoolTimetables",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetables_SchoolId_AcademicYearId_Semester_TimetableSetupProfileId",
                table: "SchoolTimetables",
                columns: new[] { "SchoolId", "AcademicYearId", "Semester", "TimetableSetupProfileId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsClone] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetables_SchoolId_AcademicYearId_Semester_TimetableSetupProfileId",
                table: "SchoolTimetables");

            migrationBuilder.DropColumn(
                name: "IsClone",
                table: "SchoolTimetables");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetables_SchoolId_AcademicYearId_Semester_TimetableSetupProfileId",
                table: "SchoolTimetables",
                columns: new[] { "SchoolId", "AcademicYearId", "Semester", "TimetableSetupProfileId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
