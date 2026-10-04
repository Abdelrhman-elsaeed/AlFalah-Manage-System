using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchoolFileStorageReviewIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_EvidenceReviewDecisions_RejectReason",
                table: "EvidenceReviewDecisions",
                sql: "[Decision] <> 4 OR [IsLegacyImported] = 1 OR ([Note] IS NOT NULL AND LTRIM(RTRIM([Note])) <> '')");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceRequirements_SchoolId_AcademicYearId_TemplateVersion_OriginalTaskId",
                table: "EvidenceRequirements",
                columns: new[] { "SchoolId", "AcademicYearId", "TemplateVersion", "OriginalTaskId" },
                unique: true,
                filter: "[SchoolId] IS NOT NULL AND [AcademicYearId] IS NOT NULL AND [OriginalTaskId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException("Retain review constraints and history; rollback closes feature flags.");
        }
    }
}
