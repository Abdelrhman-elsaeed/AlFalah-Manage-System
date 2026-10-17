using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoverageRegisterArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoverageRegisterArtifacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SourceRevision = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RendererVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsComplete = table.Column<bool>(type: "bit", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pdf = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageRegisterArtifacts", x => x.Id);
                    table.CheckConstraint("CK_CoverageRegister_Revision", "[Revision] > 0");
                    table.ForeignKey(
                        name: "FK_CoverageRegisterArtifacts_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CoverageRegisterArtifacts_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRegisterArtifacts_CreatedByUserId",
                table: "CoverageRegisterArtifacts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRegisterArtifacts_SchoolId_LocalDate_Revision",
                table: "CoverageRegisterArtifacts",
                columns: new[] { "SchoolId", "LocalDate", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRegisterArtifacts_SchoolId_LocalDate_SourceRevision_RendererVersion",
                table: "CoverageRegisterArtifacts",
                columns: new[] { "SchoolId", "LocalDate", "SourceRevision", "RendererVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoverageRegisterArtifacts");
        }
    }
}
