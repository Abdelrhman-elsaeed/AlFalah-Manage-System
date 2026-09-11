using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetableSettingsPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetables_SchoolId_AcademicYearId_Semester",
                table: "SchoolTimetables");

            migrationBuilder.AddColumn<int>(
                name: "SetupRevision",
                table: "SchoolTimetables",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimetableSetupProfileId",
                table: "SchoolTimetables",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalLocation",
                table: "Classrooms",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                collation: "Arabic_CI_AS");

            migrationBuilder.CreateTable(
                name: "TimetableSetupProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Semester = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, collation: "Arabic_CI_AS"),
                    BellScheduleTemplateId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableSetupProfiles", x => x.Id);
                    table.UniqueConstraint("AK_TimetableSetupProfiles_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_TimetableSetupProfiles_Revision", "[Revision] > 0");
                    table.CheckConstraint("CK_TimetableSetupProfiles_Semester", "[Semester] BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_TimetableSetupProfiles_Status", "[Status] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_TimetableSetupProfiles_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableSetupProfiles_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableSetupProfiles_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableSetupProfiles_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableSetupProfiles_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetables_SchoolId_AcademicYearId_Semester_TimetableSetupProfileId",
                table: "SchoolTimetables",
                columns: new[] { "SchoolId", "AcademicYearId", "Semester", "TimetableSetupProfileId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetables_SchoolId_TimetableSetupProfileId",
                table: "SchoolTimetables",
                columns: new[] { "SchoolId", "TimetableSetupProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSetupProfiles_AcademicYearId",
                table: "TimetableSetupProfiles",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSetupProfiles_CreatedByUserId",
                table: "TimetableSetupProfiles",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSetupProfiles_DeletedByUserId",
                table: "TimetableSetupProfiles",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSetupProfiles_SchoolId_AcademicYearId_Semester_Name",
                table: "TimetableSetupProfiles",
                columns: new[] { "SchoolId", "AcademicYearId", "Semester", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSetupProfiles_SchoolId_AcademicYearId_Semester_Status",
                table: "TimetableSetupProfiles",
                columns: new[] { "SchoolId", "AcademicYearId", "Semester", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSetupProfiles_UpdatedByUserId",
                table: "TimetableSetupProfiles",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolTimetables_TimetableSetupProfiles_SchoolId_TimetableSetupProfileId",
                table: "SchoolTimetables",
                columns: new[] { "SchoolId", "TimetableSetupProfileId" },
                principalTable: "TimetableSetupProfiles",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SchoolTimetables_TimetableSetupProfiles_SchoolId_TimetableSetupProfileId",
                table: "SchoolTimetables");

            migrationBuilder.DropTable(
                name: "TimetableSetupProfiles");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetables_SchoolId_AcademicYearId_Semester_TimetableSetupProfileId",
                table: "SchoolTimetables");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetables_SchoolId_TimetableSetupProfileId",
                table: "SchoolTimetables");

            migrationBuilder.DropColumn(
                name: "SetupRevision",
                table: "SchoolTimetables");

            migrationBuilder.DropColumn(
                name: "TimetableSetupProfileId",
                table: "SchoolTimetables");

            migrationBuilder.DropColumn(
                name: "PhysicalLocation",
                table: "Classrooms");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetables_SchoolId_AcademicYearId_Semester",
                table: "SchoolTimetables",
                columns: new[] { "SchoolId", "AcademicYearId", "Semester" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
