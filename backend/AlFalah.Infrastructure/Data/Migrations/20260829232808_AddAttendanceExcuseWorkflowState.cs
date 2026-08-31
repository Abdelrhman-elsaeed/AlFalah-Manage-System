using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceExcuseWorkflowState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExcuseStatus",
                table: "DailyStudentAttendances",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudentAttendances_SchoolId_StudentId_AcademicTermId_ExcuseStatus",
                table: "DailyStudentAttendances",
                columns: new[] { "SchoolId", "StudentId", "AcademicTermId", "ExcuseStatus" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_DailyStudentAttendances_ExcuseStatus",
                table: "DailyStudentAttendances",
                sql: "[ExcuseStatus] BETWEEN 1 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DailyStudentAttendances_ExcuseStatus",
                table: "DailyStudentAttendances");

            migrationBuilder.DropIndex(
                name: "IX_DailyStudentAttendances_SchoolId_StudentId_AcademicTermId_ExcuseStatus",
                table: "DailyStudentAttendances");

            migrationBuilder.DropColumn(
                name: "ExcuseStatus",
                table: "DailyStudentAttendances");
        }
    }
}
