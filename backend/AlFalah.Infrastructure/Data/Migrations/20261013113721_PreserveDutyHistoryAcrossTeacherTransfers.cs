using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreserveDutyHistoryAcrossTeacherTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DutyAssignments_InstructorProfiles_SchoolId_EffectiveTeacherId",
                table: "DutyAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_DutyAssignments_InstructorProfiles_SchoolId_OriginalTeacherId",
                table: "DutyAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_DutyParticipants_InstructorProfiles_SchoolId_InstructorProfileId_UserId",
                table: "DutyParticipants");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InstructorProfiles_SchoolId_Id_UserId",
                table: "InstructorProfiles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InstructorProfiles_SchoolId_UserId",
                table: "InstructorProfiles");

            migrationBuilder.DropIndex(
                name: "IX_DutyParticipants_SchoolId_InstructorProfileId_UserId",
                table: "DutyParticipants");

            migrationBuilder.DropIndex(
                name: "IX_DutyAssignments_SchoolId_EffectiveTeacherId",
                table: "DutyAssignments");

            migrationBuilder.DropIndex(
                name: "IX_DutyAssignments_SchoolId_OriginalTeacherId",
                table: "DutyAssignments");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_InstructorProfiles_Id_UserId",
                table: "InstructorProfiles",
                columns: new[] { "Id", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyParticipants_InstructorProfileId_UserId",
                table: "DutyParticipants",
                columns: new[] { "InstructorProfileId", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DutyParticipants_InstructorProfiles_InstructorProfileId_UserId",
                table: "DutyParticipants",
                columns: new[] { "InstructorProfileId", "UserId" },
                principalTable: "InstructorProfiles",
                principalColumns: new[] { "Id", "UserId" });
            ProtectTeacherScope(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RemoveScopeProtection(migrationBuilder);
            migrationBuilder.DropForeignKey(
                name: "FK_DutyParticipants_InstructorProfiles_InstructorProfileId_UserId",
                table: "DutyParticipants");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InstructorProfiles_Id_UserId",
                table: "InstructorProfiles");

            migrationBuilder.DropIndex(
                name: "IX_DutyParticipants_InstructorProfileId_UserId",
                table: "DutyParticipants");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_InstructorProfiles_SchoolId_Id_UserId",
                table: "InstructorProfiles",
                columns: new[] { "SchoolId", "Id", "UserId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_InstructorProfiles_SchoolId_UserId",
                table: "InstructorProfiles",
                columns: new[] { "SchoolId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyParticipants_SchoolId_InstructorProfileId_UserId",
                table: "DutyParticipants",
                columns: new[] { "SchoolId", "InstructorProfileId", "UserId" });

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

            migrationBuilder.AddForeignKey(
                name: "FK_DutyParticipants_InstructorProfiles_SchoolId_InstructorProfileId_UserId",
                table: "DutyParticipants",
                columns: new[] { "SchoolId", "InstructorProfileId", "UserId" },
                principalTable: "InstructorProfiles",
                principalColumns: new[] { "SchoolId", "Id", "UserId" });
        }
    }
}
