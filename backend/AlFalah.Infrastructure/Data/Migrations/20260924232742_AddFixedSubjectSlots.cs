using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFixedSubjectSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClassroomId",
                table: "ClassSubjectFixedSlots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "ClassSubjectFixedSlots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubjectId",
                table: "ClassSubjectFixedSlots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimetableSetupProfileId",
                table: "ClassSubjectFixedSlots",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE fixedSlot
                SET fixedSlot.[SchoolId] = requirement.[SchoolId],
                    fixedSlot.[TimetableSetupProfileId] = requirement.[TimetableSetupProfileId],
                    fixedSlot.[ClassroomId] = requirement.[ClassroomId],
                    fixedSlot.[SubjectId] = requirement.[SubjectId]
                FROM [ClassSubjectFixedSlots] AS fixedSlot
                INNER JOIN [ClassSubjectRequirements] AS requirement
                    ON requirement.[Id] = fixedSlot.[ClassSubjectRequirementId];
                """);

            migrationBuilder.AlterColumn<int>(name: "ClassroomId", table: "ClassSubjectFixedSlots",
                type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "SchoolId", table: "ClassSubjectFixedSlots",
                type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "SubjectId", table: "ClassSubjectFixedSlots",
                type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "TimetableSetupProfileId", table: "ClassSubjectFixedSlots",
                type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectFixedSlots_SchoolId_ClassroomId",
                table: "ClassSubjectFixedSlots",
                columns: new[] { "SchoolId", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectFixedSlots_SchoolId_SubjectId",
                table: "ClassSubjectFixedSlots",
                columns: new[] { "SchoolId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectFixedSlots_SchoolId_TimetableSetupProfileId_ClassroomId_Day_Period",
                table: "ClassSubjectFixedSlots",
                columns: new[] { "SchoolId", "TimetableSetupProfileId", "ClassroomId", "Day", "Period" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ClassSubjectFixedSlots_Classrooms_SchoolId_ClassroomId",
                table: "ClassSubjectFixedSlots",
                columns: new[] { "SchoolId", "ClassroomId" },
                principalTable: "Classrooms",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClassSubjectFixedSlots_SubjectDefinition_SchoolId_SubjectId",
                table: "ClassSubjectFixedSlots",
                columns: new[] { "SchoolId", "SubjectId" },
                principalTable: "SubjectDefinition",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClassSubjectFixedSlots_TimetableSetupProfiles_SchoolId_TimetableSetupProfileId",
                table: "ClassSubjectFixedSlots",
                columns: new[] { "SchoolId", "TimetableSetupProfileId" },
                principalTable: "TimetableSetupProfiles",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassSubjectFixedSlots_Classrooms_SchoolId_ClassroomId",
                table: "ClassSubjectFixedSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_ClassSubjectFixedSlots_SubjectDefinition_SchoolId_SubjectId",
                table: "ClassSubjectFixedSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_ClassSubjectFixedSlots_TimetableSetupProfiles_SchoolId_TimetableSetupProfileId",
                table: "ClassSubjectFixedSlots");

            migrationBuilder.DropIndex(
                name: "IX_ClassSubjectFixedSlots_SchoolId_ClassroomId",
                table: "ClassSubjectFixedSlots");

            migrationBuilder.DropIndex(
                name: "IX_ClassSubjectFixedSlots_SchoolId_SubjectId",
                table: "ClassSubjectFixedSlots");

            migrationBuilder.DropIndex(
                name: "IX_ClassSubjectFixedSlots_SchoolId_TimetableSetupProfileId_ClassroomId_Day_Period",
                table: "ClassSubjectFixedSlots");

            migrationBuilder.DropColumn(
                name: "ClassroomId",
                table: "ClassSubjectFixedSlots");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "ClassSubjectFixedSlots");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "ClassSubjectFixedSlots");

            migrationBuilder.DropColumn(
                name: "TimetableSetupProfileId",
                table: "ClassSubjectFixedSlots");
        }
    }
}
