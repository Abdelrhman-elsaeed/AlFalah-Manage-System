using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeacherTimetableProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TimetableSetupProfileId = table.Column<int>(type: "int", nullable: false),
                    InstructorProfileId = table.Column<int>(type: "int", nullable: false),
                    BellScheduleRevisionId = table.Column<int>(type: "int", nullable: false),
                    ShortDisplayName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    MaximumWeeklyPeriods = table.Column<int>(type: "int", nullable: false),
                    IsVisiting = table.Column<bool>(type: "bit", nullable: false),
                    HideFromPrint = table.Column<bool>(type: "bit", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherTimetableProfiles", x => x.Id);
                    table.CheckConstraint("CK_TeacherTimetableProfiles_Load", "[MaximumWeeklyPeriods] >= 0");
                    table.CheckConstraint("CK_TeacherTimetableProfiles_Revision", "[Revision] > 0");
                    table.ForeignKey(
                        name: "FK_TeacherTimetableProfiles_BellScheduleRevisions_SchoolId_BellScheduleRevisionId",
                        columns: x => new { x.SchoolId, x.BellScheduleRevisionId },
                        principalTable: "BellScheduleRevisions",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherTimetableProfiles_InstructorProfiles_SchoolId_InstructorProfileId",
                        columns: x => new { x.SchoolId, x.InstructorProfileId },
                        principalTable: "InstructorProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherTimetableProfiles_TimetableSetupProfiles_SchoolId_TimetableSetupProfileId",
                        columns: x => new { x.SchoolId, x.TimetableSetupProfileId },
                        principalTable: "TimetableSetupProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherAvailabilitySlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherTimetableProfileId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    BellPeriodId = table.Column<int>(type: "int", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherAvailabilitySlots", x => x.Id);
                    table.CheckConstraint("CK_TeacherAvailabilitySlots_Day", "[Day] BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_TeacherAvailabilitySlots_BellPeriods_BellPeriodId",
                        column: x => x.BellPeriodId,
                        principalTable: "BellPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAvailabilitySlots_TeacherTimetableProfiles_TeacherTimetableProfileId",
                        column: x => x.TeacherTimetableProfileId,
                        principalTable: "TeacherTimetableProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAvailabilitySlots_BellPeriodId",
                table: "TeacherAvailabilitySlots",
                column: "BellPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAvailabilitySlots_TeacherTimetableProfileId_Day_BellPeriodId",
                table: "TeacherAvailabilitySlots",
                columns: new[] { "TeacherTimetableProfileId", "Day", "BellPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherTimetableProfiles_SchoolId_BellScheduleRevisionId",
                table: "TeacherTimetableProfiles",
                columns: new[] { "SchoolId", "BellScheduleRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherTimetableProfiles_SchoolId_InstructorProfileId",
                table: "TeacherTimetableProfiles",
                columns: new[] { "SchoolId", "InstructorProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherTimetableProfiles_SchoolId_TimetableSetupProfileId",
                table: "TeacherTimetableProfiles",
                columns: new[] { "SchoolId", "TimetableSetupProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherTimetableProfiles_TimetableSetupProfileId_InstructorProfileId",
                table: "TeacherTimetableProfiles",
                columns: new[] { "TimetableSetupProfileId", "InstructorProfileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeacherAvailabilitySlots");

            migrationBuilder.DropTable(
                name: "TeacherTimetableProfiles");
        }
    }
}
