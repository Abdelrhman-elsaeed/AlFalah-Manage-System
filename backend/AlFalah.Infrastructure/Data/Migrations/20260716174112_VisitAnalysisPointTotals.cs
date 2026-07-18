using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class VisitAnalysisPointTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaximumScore",
                table: "VisitAnalyses",
                type: "decimal(8,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalScore",
                table: "VisitAnalyses",
                type: "decimal(8,3)",
                nullable: false,
                defaultValue: 0m);

            // Backfill existing immutable analysis snapshots from their
            // snapshotted standard scores. The initial rubric has 25 standards,
            // so a fully scored visit receives a maximum of 100 points.
            migrationBuilder.Sql(@"
                UPDATE analysis
                SET
                    analysis.TotalScore = totals.TotalScore,
                    analysis.MaximumScore = totals.MaximumScore
                FROM VisitAnalyses AS analysis
                INNER JOIN (
                    SELECT
                        VisitId,
                        SUM(CAST(Score AS decimal(8,3))) AS TotalScore,
                        COUNT(*) * 4 AS MaximumScore
                    FROM VisitScores
                    WHERE Score IS NOT NULL AND IsDeleted = 0
                    GROUP BY VisitId
                ) AS totals ON totals.VisitId = analysis.VisitId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaximumScore",
                table: "VisitAnalyses");

            migrationBuilder.DropColumn(
                name: "TotalScore",
                table: "VisitAnalyses");
        }
    }
}
