using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClassroomConsumerReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClassroomId",
                table: "Visits",
                type: "int",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Classrooms_SchoolId_AcademicYearId_Id",
                table: "Classrooms",
                columns: new[] { "SchoolId", "AcademicYearId", "Id" });

            migrationBuilder.CreateTable(
                name: "ClassroomLabelHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    ClassroomId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Arabic_CI_AS"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassroomLabelHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassroomLabelHistory_Classrooms_SchoolId_AcademicYearId_ClassroomId",
                        columns: x => new { x.SchoolId, x.AcademicYearId, x.ClassroomId },
                        principalTable: "Classrooms",
                        principalColumns: new[] { "SchoolId", "AcademicYearId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_SchoolId_ClassroomId",
                table: "Visits",
                columns: new[] { "SchoolId", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomLabelHistory_SchoolId_AcademicYearId_ClassroomId",
                table: "ClassroomLabelHistory",
                columns: new[] { "SchoolId", "AcademicYearId", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomLabelHistory_SchoolId_AcademicYearId_Label",
                table: "ClassroomLabelHistory",
                columns: new[] { "SchoolId", "AcademicYearId", "Label" },
                unique: true);

            // Retain existing names as scoped bindings. No visit, timetable entry,
            // serialized checkpoint or signed artifact is rewritten by this migration.
            migrationBuilder.Sql("""
                INSERT INTO ClassroomLabelHistory (SchoolId,AcademicYearId,ClassroomId,Label,CreatedAt)
                SELECT SchoolId,AcademicYearId,Id,ClassLabel,SYSDATETIMEOFFSET()
                FROM Classrooms WHERE IsDeleted=0;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Visits_Classrooms_SchoolId_ClassroomId",
                table: "Visits",
                columns: new[] { "SchoolId", "ClassroomId" },
                principalTable: "Classrooms",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS(SELECT 1 FROM Visits WHERE ClassroomId IS NOT NULL)
                  OR EXISTS(SELECT 1 FROM ClassroomLabelHistory h JOIN Classrooms c ON c.Id=h.ClassroomId
                    WHERE h.Label<>c.ClassLabel)
                  THROW 51000, 'Review retained classroom references and names before rollback. No data was removed.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Visits_Classrooms_SchoolId_ClassroomId",
                table: "Visits");

            migrationBuilder.DropTable(
                name: "ClassroomLabelHistory");

            migrationBuilder.DropIndex(
                name: "IX_Visits_SchoolId_ClassroomId",
                table: "Visits");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Classrooms_SchoolId_AcademicYearId_Id",
                table: "Classrooms");

            migrationBuilder.DropColumn(
                name: "ClassroomId",
                table: "Visits");
        }
    }
}
