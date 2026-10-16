using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeachingAssignmentStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatementCategory",
                table: "TeachingAssignmentMembers",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeachingAssignmentStatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TimetableSetupProfileId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RendererVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pdf = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeachingAssignmentStatements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeachingAssignmentStatements_TimetableSetupProfiles_SchoolId_TimetableSetupProfileId",
                        columns: x => new { x.SchoolId, x.TimetableSetupProfileId },
                        principalTable: "TimetableSetupProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignmentStatements_SchoolId_TimetableSetupProfileId_Revision_Scope_RendererVersion",
                table: "TeachingAssignmentStatements",
                columns: new[] { "SchoolId", "TimetableSetupProfileId", "Revision", "Scope", "RendererVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM TeachingAssignmentStatements) OR EXISTS (SELECT 1 FROM TeachingAssignmentMembers WHERE StatementCategory IS NOT NULL) THROW 51000, 'Retained assignment statements/categories prevent destructive rollback.', 1;");
            migrationBuilder.DropTable(
                name: "TeachingAssignmentStatements");

            migrationBuilder.DropColumn(
                name: "StatementCategory",
                table: "TeachingAssignmentMembers");
        }
    }
}
