using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AcbXX3KgvqD7B8Y4WjCu6yNx1Prfu5cNHz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeacherTimetableProfiles_SchoolId_TimetableSetupProfileId",
                table: "TeacherTimetableProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ClassSubjectRequirements_SchoolId_TimetableSetupProfileId",
                table: "ClassSubjectRequirements");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TeacherTimetableProfiles_SchoolId_TimetableSetupProfileId_Id",
                table: "TeacherTimetableProfiles",
                columns: new[] { "SchoolId", "TimetableSetupProfileId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ClassSubjectRequirements_SchoolId_TimetableSetupProfileId_Id",
                table: "ClassSubjectRequirements",
                columns: new[] { "SchoolId", "TimetableSetupProfileId", "Id" });

            migrationBuilder.CreateTable(
                name: "TeachingAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TimetableSetupProfileId = table.Column<int>(type: "int", nullable: false),
                    ClassSubjectRequirementId = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeachingAssignments", x => x.Id);
                    table.UniqueConstraint("AK_TeachingAssignments_SchoolId_TimetableSetupProfileId_Id", x => new { x.SchoolId, x.TimetableSetupProfileId, x.Id });
                    table.CheckConstraint("CK_TeachingAssignment_Mode", "[Mode] IN ('SingleTeacher', 'CoTeaching', 'SplitQuota')");
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_ClassSubjectRequirements_SchoolId_TimetableSetupProfileId_ClassSubjectRequirementId",
                        columns: x => new { x.SchoolId, x.TimetableSetupProfileId, x.ClassSubjectRequirementId },
                        principalTable: "ClassSubjectRequirements",
                        principalColumns: new[] { "SchoolId", "TimetableSetupProfileId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeachingAssignmentMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TimetableSetupProfileId = table.Column<int>(type: "int", nullable: false),
                    TeachingAssignmentId = table.Column<int>(type: "int", nullable: false),
                    TeacherTimetableProfileId = table.Column<int>(type: "int", nullable: false),
                    AllocatedPeriodCount = table.Column<int>(type: "int", nullable: false),
                    AllocatedPairedBlockCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeachingAssignmentMembers", x => x.Id);
                    table.CheckConstraint("CK_TeachingAssignmentMember_Allocation", "[AllocatedPeriodCount] >= 0 AND [AllocatedPairedBlockCount] >= 0 AND 2 * [AllocatedPairedBlockCount] <= [AllocatedPeriodCount]");
                    table.ForeignKey(
                        name: "FK_TeachingAssignmentMembers_TeacherTimetableProfiles_SchoolId_TimetableSetupProfileId_TeacherTimetableProfileId",
                        columns: x => new { x.SchoolId, x.TimetableSetupProfileId, x.TeacherTimetableProfileId },
                        principalTable: "TeacherTimetableProfiles",
                        principalColumns: new[] { "SchoolId", "TimetableSetupProfileId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeachingAssignmentMembers_TeachingAssignments_SchoolId_TimetableSetupProfileId_TeachingAssignmentId",
                        columns: x => new { x.SchoolId, x.TimetableSetupProfileId, x.TeachingAssignmentId },
                        principalTable: "TeachingAssignments",
                        principalColumns: new[] { "SchoolId", "TimetableSetupProfileId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignmentMembers_SchoolId_TimetableSetupProfileId_TeacherTimetableProfileId",
                table: "TeachingAssignmentMembers",
                columns: new[] { "SchoolId", "TimetableSetupProfileId", "TeacherTimetableProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignmentMembers_SchoolId_TimetableSetupProfileId_TeachingAssignmentId",
                table: "TeachingAssignmentMembers",
                columns: new[] { "SchoolId", "TimetableSetupProfileId", "TeachingAssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignmentMembers_TeachingAssignmentId_TeacherTimetableProfileId",
                table: "TeachingAssignmentMembers",
                columns: new[] { "TeachingAssignmentId", "TeacherTimetableProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_ClassSubjectRequirementId",
                table: "TeachingAssignments",
                column: "ClassSubjectRequirementId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_SchoolId_TimetableSetupProfileId_ClassSubjectRequirementId",
                table: "TeachingAssignments",
                columns: new[] { "SchoolId", "TimetableSetupProfileId", "ClassSubjectRequirementId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeachingAssignmentMembers");

            migrationBuilder.DropTable(
                name: "TeachingAssignments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TeacherTimetableProfiles_SchoolId_TimetableSetupProfileId_Id",
                table: "TeacherTimetableProfiles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ClassSubjectRequirements_SchoolId_TimetableSetupProfileId_Id",
                table: "ClassSubjectRequirements");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherTimetableProfiles_SchoolId_TimetableSetupProfileId",
                table: "TeacherTimetableProfiles",
                columns: new[] { "SchoolId", "TimetableSetupProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectRequirements_SchoolId_TimetableSetupProfileId",
                table: "ClassSubjectRequirements",
                columns: new[] { "SchoolId", "TimetableSetupProfileId" });
        }
    }
}
