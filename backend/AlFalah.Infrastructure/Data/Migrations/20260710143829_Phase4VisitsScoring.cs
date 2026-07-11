using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase4VisitsScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Visits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    InstructorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RubricVersionId = table.Column<int>(type: "int", nullable: false),
                    VisitCategory = table.Column<int>(type: "int", nullable: false),
                    VisitSequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VisitDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GradeClass = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Visits_RubricVersions_RubricVersionId",
                        column: x => x.RubricVersionId,
                        principalTable: "RubricVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_Users_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VisitAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitId = table.Column<int>(type: "int", nullable: false),
                    OverallScore = table.Column<decimal>(type: "decimal(6,3)", nullable: false),
                    PerformanceLevelAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StrengthsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImprovementAreasJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PriorityStandardsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitAnalyses_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VisitScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitId = table.Column<int>(type: "int", nullable: false),
                    RubricStandardId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true),
                    EvidenceNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitScores_RubricStandards_RubricStandardId",
                        column: x => x.RubricStandardId,
                        principalTable: "RubricStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VisitScores_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VisitDomainAverages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitAnalysisId = table.Column<int>(type: "int", nullable: false),
                    RubricDomainId = table.Column<int>(type: "int", nullable: false),
                    DomainCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DomainNameAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AverageScore = table.Column<decimal>(type: "decimal(6,3)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitDomainAverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitDomainAverages_RubricDomains_RubricDomainId",
                        column: x => x.RubricDomainId,
                        principalTable: "RubricDomains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VisitDomainAverages_VisitAnalyses_VisitAnalysisId",
                        column: x => x.VisitAnalysisId,
                        principalTable: "VisitAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisitAnalyses_IsDeleted",
                table: "VisitAnalyses",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "UX_VisitAnalysis_Visit",
                table: "VisitAnalyses",
                column: "VisitId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitDomainAverages_IsDeleted",
                table: "VisitDomainAverages",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_VisitDomainAverages_RubricDomainId",
                table: "VisitDomainAverages",
                column: "RubricDomainId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitDomainAverages_VisitAnalysisId",
                table: "VisitDomainAverages",
                column: "VisitAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_CreatedByUserId",
                table: "Visits",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_DeletedByUserId",
                table: "Visits",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_InstructorId",
                table: "Visits",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_IsDeleted",
                table: "Visits",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_RubricVersionId",
                table: "Visits",
                column: "RubricVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_SchoolId",
                table: "Visits",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_SchoolId_InstructorId",
                table: "Visits",
                columns: new[] { "SchoolId", "InstructorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_SchoolId_Status",
                table: "Visits",
                columns: new[] { "SchoolId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_SchoolId_VisitDate",
                table: "Visits",
                columns: new[] { "SchoolId", "VisitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_Status",
                table: "Visits",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_VisitDate",
                table: "Visits",
                column: "VisitDate");

            migrationBuilder.CreateIndex(
                name: "IX_VisitScores_IsDeleted",
                table: "VisitScores",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_VisitScores_RubricStandardId",
                table: "VisitScores",
                column: "RubricStandardId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitScores_VisitId",
                table: "VisitScores",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "UX_VisitScore_Visit_Standard",
                table: "VisitScores",
                columns: new[] { "VisitId", "RubricStandardId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisitDomainAverages");

            migrationBuilder.DropTable(
                name: "VisitScores");

            migrationBuilder.DropTable(
                name: "VisitAnalyses");

            migrationBuilder.DropTable(
                name: "Visits");
        }
    }
}
