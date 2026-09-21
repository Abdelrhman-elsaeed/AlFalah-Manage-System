using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClassroomVisitsV2AdditiveSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClassroomPeriod",
                table: "Visits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluatorNameSnapshot",
                table: "Visits",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Arabic_CI_AS");

            migrationBuilder.AddColumn<string>(
                name: "EvaluatorRoleSnapshot",
                table: "Visits",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Arabic_CI_AS");

            migrationBuilder.AddColumn<int>(
                name: "ExperienceVersion",
                table: "Visits",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ScoringRuleSetVersion",
                table: "Visits",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PercentageScore",
                table: "VisitDomainAverages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OverallPercentage",
                table: "VisitAnalyses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RuleSetVersion",
                table: "VisitAnalyses",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "RubricIndicators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RubricStandardId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TextAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, collation: "Arabic_CI_AS"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RubricIndicators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RubricIndicators_RubricStandards_RubricStandardId",
                        column: x => x.RubricStandardId,
                        principalTable: "RubricStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VisitTreatmentSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitId = table.Column<int>(type: "int", nullable: false),
                    RubricDomainId = table.Column<int>(type: "int", nullable: true),
                    DomainNameArSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false, collation: "Arabic_CI_AS"),
                    Goal = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, collation: "Arabic_CI_AS"),
                    Actions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false, collation: "Arabic_CI_AS"),
                    SuccessIndicators = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, collation: "Arabic_CI_AS"),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitTreatmentSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitTreatmentSnapshots_RubricDomains_RubricDomainId",
                        column: x => x.RubricDomainId,
                        principalTable: "RubricDomains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitTreatmentSnapshots_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VisitObservedIndicators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitScoreId = table.Column<int>(type: "int", nullable: false),
                    RubricIndicatorId = table.Column<int>(type: "int", nullable: false),
                    IndicatorTextArSnapshot = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, collation: "Arabic_CI_AS"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitObservedIndicators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitObservedIndicators_RubricIndicators_RubricIndicatorId",
                        column: x => x.RubricIndicatorId,
                        principalTable: "RubricIndicators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitObservedIndicators_VisitScores_VisitScoreId",
                        column: x => x.VisitScoreId,
                        principalTable: "VisitScores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Visits_ClassroomPeriod",
                table: "Visits",
                sql: "[ClassroomPeriod] IS NULL OR ([ClassroomPeriod] >= 1 AND [ClassroomPeriod] <= 7)");

            migrationBuilder.CreateIndex(
                name: "IX_RubricIndicators_IsDeleted",
                table: "RubricIndicators",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_RubricIndicators_RubricStandardId",
                table: "RubricIndicators",
                column: "RubricStandardId");

            migrationBuilder.CreateIndex(
                name: "IX_RubricIndicators_RubricStandardId_SortOrder",
                table: "RubricIndicators",
                columns: new[] { "RubricStandardId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_RubricIndicator_Standard_Code",
                table: "RubricIndicators",
                columns: new[] { "RubricStandardId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitObservedIndicators_IsDeleted",
                table: "VisitObservedIndicators",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_VisitObservedIndicators_RubricIndicatorId",
                table: "VisitObservedIndicators",
                column: "RubricIndicatorId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitObservedIndicators_VisitScoreId",
                table: "VisitObservedIndicators",
                column: "VisitScoreId");

            migrationBuilder.CreateIndex(
                name: "UX_VisitObservedIndicator_Score_Indicator",
                table: "VisitObservedIndicators",
                columns: new[] { "VisitScoreId", "RubricIndicatorId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_VisitTreatmentSnapshots_IsDeleted",
                table: "VisitTreatmentSnapshots",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_VisitTreatmentSnapshots_RubricDomainId",
                table: "VisitTreatmentSnapshots",
                column: "RubricDomainId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitTreatmentSnapshots_VisitId",
                table: "VisitTreatmentSnapshots",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitTreatmentSnapshots_VisitId_SortOrder",
                table: "VisitTreatmentSnapshots",
                columns: new[] { "VisitId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisitObservedIndicators");

            migrationBuilder.DropTable(
                name: "VisitTreatmentSnapshots");

            migrationBuilder.DropTable(
                name: "RubricIndicators");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Visits_ClassroomPeriod",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "ClassroomPeriod",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "EvaluatorNameSnapshot",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "EvaluatorRoleSnapshot",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "ExperienceVersion",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "ScoringRuleSetVersion",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "PercentageScore",
                table: "VisitDomainAverages");

            migrationBuilder.DropColumn(
                name: "OverallPercentage",
                table: "VisitAnalyses");

            migrationBuilder.DropColumn(
                name: "RuleSetVersion",
                table: "VisitAnalyses");
        }
    }
}
