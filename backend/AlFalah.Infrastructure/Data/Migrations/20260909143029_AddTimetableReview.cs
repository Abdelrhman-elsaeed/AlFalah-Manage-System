using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetableReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TimetableAnalysisRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    SchoolTimetableId = table.Column<int>(type: "int", nullable: false),
                    TimetableRevision = table.Column<int>(type: "int", nullable: false),
                    SetupRevision = table.Column<int>(type: "int", nullable: true),
                    BellScheduleRevisionId = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    HardViolationCount = table.Column<int>(type: "int", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    AnalyzerVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableAnalysisRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimetableAnalysisRuns_BellScheduleRevisions_BellScheduleRevisionId",
                        column: x => x.BellScheduleRevisionId,
                        principalTable: "BellScheduleRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableAnalysisRuns_SchoolTimetables_SchoolTimetableId",
                        column: x => x.SchoolTimetableId,
                        principalTable: "SchoolTimetables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TimetableAnalysisRuns_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableAnalysisRuns_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimetableAnalysisFindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnalysisRunId = table.Column<int>(type: "int", nullable: false),
                    RuleCode = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    MessageAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ClassroomId = table.Column<int>(type: "int", nullable: true),
                    InstructorProfileId = table.Column<int>(type: "int", nullable: true),
                    SubjectId = table.Column<int>(type: "int", nullable: true),
                    Day = table.Column<int>(type: "int", nullable: true),
                    Period = table.Column<int>(type: "int", nullable: true),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuggestedRepairJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsOverridden = table.Column<bool>(type: "bit", nullable: false),
                    OverriddenByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    OverriddenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OverrideReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableAnalysisFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimetableAnalysisFindings_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableAnalysisFindings_InstructorProfiles_InstructorProfileId",
                        column: x => x.InstructorProfileId,
                        principalTable: "InstructorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableAnalysisFindings_SubjectDefinition_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "SubjectDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableAnalysisFindings_TimetableAnalysisRuns_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "TimetableAnalysisRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TimetableAnalysisFindings_Users_OverriddenByUserId",
                        column: x => x.OverriddenByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableAnalysisFindings_AnalysisRunId",
                table: "TimetableAnalysisFindings",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableAnalysisFindings_ClassroomId",
                table: "TimetableAnalysisFindings",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableAnalysisFindings_InstructorProfileId",
                table: "TimetableAnalysisFindings",
                column: "InstructorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableAnalysisFindings_OverriddenByUserId",
                table: "TimetableAnalysisFindings",
                column: "OverriddenByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableAnalysisFindings_SubjectId",
                table: "TimetableAnalysisFindings",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableAnalysisRuns_BellScheduleRevisionId",
                table: "TimetableAnalysisRuns",
                column: "BellScheduleRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableAnalysisRuns_RequestedByUserId",
                table: "TimetableAnalysisRuns",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableAnalysisRuns_SchoolId",
                table: "TimetableAnalysisRuns",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableAnalysisRuns_SchoolTimetableId_TimetableRevision",
                table: "TimetableAnalysisRuns",
                columns: new[] { "SchoolTimetableId", "TimetableRevision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimetableAnalysisFindings");

            migrationBuilder.DropTable(
                name: "TimetableAnalysisRuns");
        }
    }
}
