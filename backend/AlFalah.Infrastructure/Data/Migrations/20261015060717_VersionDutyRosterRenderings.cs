using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class VersionDutyRosterRenderings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DutyPdfArtifacts_SchoolId_PlanId_PlanRevision",
                table: "DutyPdfArtifacts");

            migrationBuilder.CreateIndex(
                name: "IX_DutyPdfArtifacts_SchoolId_PlanId_PlanRevision_RendererVersion",
                table: "DutyPdfArtifacts",
                columns: new[] { "SchoolId", "PlanId", "PlanRevision", "RendererVersion" },
                unique: true,
                filter: "[PlanId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException("Retained roster template versions must not be removed. Roll back application behavior while retaining this additive index and every artifact.");
        }
    }
}
