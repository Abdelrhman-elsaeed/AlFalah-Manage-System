using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDutyTeacherSchoolIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_InstructorProfiles_SchoolId_UserId",
                table: "InstructorProfiles",
                columns: new[] { "SchoolId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_SchoolId_EffectiveTeacherId",
                table: "DutyAssignments",
                columns: new[] { "SchoolId", "EffectiveTeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_SchoolId_OriginalTeacherId",
                table: "DutyAssignments",
                columns: new[] { "SchoolId", "OriginalTeacherId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DutyAssignments_InstructorProfiles_SchoolId_EffectiveTeacherId",
                table: "DutyAssignments",
                columns: new[] { "SchoolId", "EffectiveTeacherId" },
                principalTable: "InstructorProfiles",
                principalColumns: new[] { "SchoolId", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DutyAssignments_InstructorProfiles_SchoolId_OriginalTeacherId",
                table: "DutyAssignments",
                columns: new[] { "SchoolId", "OriginalTeacherId" },
                principalTable: "InstructorProfiles",
                principalColumns: new[] { "SchoolId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DutyAssignments_InstructorProfiles_SchoolId_EffectiveTeacherId",
                table: "DutyAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_DutyAssignments_InstructorProfiles_SchoolId_OriginalTeacherId",
                table: "DutyAssignments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InstructorProfiles_SchoolId_UserId",
                table: "InstructorProfiles");

            migrationBuilder.DropIndex(
                name: "IX_DutyAssignments_SchoolId_EffectiveTeacherId",
                table: "DutyAssignments");

            migrationBuilder.DropIndex(
                name: "IX_DutyAssignments_SchoolId_OriginalTeacherId",
                table: "DutyAssignments");
        }
    }
}
