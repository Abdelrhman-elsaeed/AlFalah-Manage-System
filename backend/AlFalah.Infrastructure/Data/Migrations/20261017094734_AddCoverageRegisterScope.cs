using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoverageRegisterScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CoverageRegisterArtifacts_SchoolId_LocalDate_Revision",
                table: "CoverageRegisterArtifacts");

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "CoverageRegisterArtifacts",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "Day");

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRegisterArtifacts_SchoolId_LocalDate_Scope_Revision",
                table: "CoverageRegisterArtifacts",
                columns: new[] { "SchoolId", "LocalDate", "Scope", "Revision" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CoverageRegister_Scope",
                table: "CoverageRegisterArtifacts",
                sql: "[Scope] IN ('Day', 'Week')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [CoverageRegisterArtifacts] WHERE [Scope] = 'Week') THROW 51000, 'Retained weekly registers prevent removing their scope. Preserve these artifacts before rollback.', 1;");
            migrationBuilder.DropIndex(
                name: "IX_CoverageRegisterArtifacts_SchoolId_LocalDate_Scope_Revision",
                table: "CoverageRegisterArtifacts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CoverageRegister_Scope",
                table: "CoverageRegisterArtifacts");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "CoverageRegisterArtifacts");

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRegisterArtifacts_SchoolId_LocalDate_Revision",
                table: "CoverageRegisterArtifacts",
                columns: new[] { "SchoolId", "LocalDate", "Revision" },
                unique: true);
        }
    }
}
