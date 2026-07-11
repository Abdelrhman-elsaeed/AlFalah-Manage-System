using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Rubric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RubricVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RubricVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RubricVersions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RubricDomains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RubricVersionId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RubricDomains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RubricDomains_RubricVersions_RubricVersionId",
                        column: x => x.RubricVersionId,
                        principalTable: "RubricVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RubricStandards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RubricDomainId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TextAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RubricStandards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RubricStandards_RubricDomains_RubricDomainId",
                        column: x => x.RubricDomainId,
                        principalTable: "RubricDomains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RubricDomains_IsDeleted",
                table: "RubricDomains",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_RubricDomains_RubricVersionId",
                table: "RubricDomains",
                column: "RubricVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RubricDomains_RubricVersionId_Code",
                table: "RubricDomains",
                columns: new[] { "RubricVersionId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_RubricDomains_RubricVersionId_SortOrder",
                table: "RubricDomains",
                columns: new[] { "RubricVersionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RubricStandards_IsDeleted",
                table: "RubricStandards",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_RubricStandards_RubricDomainId",
                table: "RubricStandards",
                column: "RubricDomainId");

            migrationBuilder.CreateIndex(
                name: "IX_RubricStandards_RubricDomainId_Code",
                table: "RubricStandards",
                columns: new[] { "RubricDomainId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_RubricStandards_RubricDomainId_SortOrder",
                table: "RubricStandards",
                columns: new[] { "RubricDomainId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RubricVersions_CreatedByUserId",
                table: "RubricVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RubricVersions_IsDeleted",
                table: "RubricVersions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_RubricVersions_VersionNumber",
                table: "RubricVersions",
                column: "VersionNumber");

            migrationBuilder.CreateIndex(
                name: "UX_RubricVersion_Active",
                table: "RubricVersions",
                column: "IsActive",
                unique: true,
                filter: "[IsActive] = 1 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RubricStandards");

            migrationBuilder.DropTable(
                name: "RubricDomains");

            migrationBuilder.DropTable(
                name: "RubricVersions");
        }
    }
}
