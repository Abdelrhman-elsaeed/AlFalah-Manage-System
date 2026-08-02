using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolTimetables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchoolTimetables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Semester = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false, collation: "Arabic_CI_AS"),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolTimetables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolTimetables_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolTimetables_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolTimetables_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolTimetables_Users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolTimetables_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimetableEditorGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ModeratorUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GrantedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableEditorGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimetableEditorGrants_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableEditorGrants_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableEditorGrants_Users_ModeratorUserId",
                        column: x => x.ModeratorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchoolTimetableEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolTimetableId = table.Column<int>(type: "int", nullable: false),
                    InstructorProfileId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<byte>(type: "tinyint", nullable: false),
                    EntryType = table.Column<int>(type: "int", nullable: false),
                    ClassLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, collation: "Arabic_CI_AS"),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, collation: "Arabic_CI_AS"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolTimetableEntries", x => x.Id);
                    table.CheckConstraint("CK_SchoolTimetableEntries_Content", "([EntryType] = 1 AND [ClassLabel] IS NOT NULL AND [Subject] IS NOT NULL) OR ([EntryType] = 2 AND [ClassLabel] IS NULL AND [Subject] IS NULL)");
                    table.CheckConstraint("CK_SchoolTimetableEntries_Day", "[Day] BETWEEN 1 AND 6");
                    table.CheckConstraint("CK_SchoolTimetableEntries_Period", "[Period] BETWEEN 1 AND 8");
                    table.ForeignKey(
                        name: "FK_SchoolTimetableEntries_InstructorProfiles_InstructorProfileId",
                        column: x => x.InstructorProfileId,
                        principalTable: "InstructorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolTimetableEntries_SchoolTimetables_SchoolTimetableId",
                        column: x => x.SchoolTimetableId,
                        principalTable: "SchoolTimetables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchoolTimetableVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolTimetableId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    ChangeKind = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false, collation: "Arabic_CI_AS"),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RestoredFromVersionNumber = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolTimetableVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolTimetableVersions_SchoolTimetables_SchoolTimetableId",
                        column: x => x.SchoolTimetableId,
                        principalTable: "SchoolTimetables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SchoolTimetableVersions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_InstructorProfileId",
                table: "SchoolTimetableEntries",
                column: "InstructorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_SchoolTimetableId_Day_Period_ClassLabel",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolTimetableId", "Day", "Period", "ClassLabel" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [EntryType] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_SchoolTimetableId_InstructorProfileId_Day_Period",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolTimetableId", "InstructorProfileId", "Day", "Period" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetables_AcademicYearId",
                table: "SchoolTimetables",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetables_CreatedByUserId",
                table: "SchoolTimetables",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetables_PublishedByUserId",
                table: "SchoolTimetables",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetables_SchoolId_AcademicYearId_Semester",
                table: "SchoolTimetables",
                columns: new[] { "SchoolId", "AcademicYearId", "Semester" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetables_UpdatedByUserId",
                table: "SchoolTimetables",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableVersions_CreatedByUserId",
                table: "SchoolTimetableVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableVersions_SchoolTimetableId_VersionNumber",
                table: "SchoolTimetableVersions",
                columns: new[] { "SchoolTimetableId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimetableEditorGrants_GrantedByUserId",
                table: "TimetableEditorGrants",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableEditorGrants_ModeratorUserId",
                table: "TimetableEditorGrants",
                column: "ModeratorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableEditorGrants_SchoolId_ModeratorUserId",
                table: "TimetableEditorGrants",
                columns: new[] { "SchoolId", "ModeratorUserId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolTimetableEntries");

            migrationBuilder.DropTable(
                name: "SchoolTimetableVersions");

            migrationBuilder.DropTable(
                name: "TimetableEditorGrants");

            migrationBuilder.DropTable(
                name: "SchoolTimetables");
        }
    }
}
