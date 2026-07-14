using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherProfileClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "InstructorProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InstructorClasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstructorProfileId = table.Column<int>(type: "int", nullable: false),
                    ClassLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Arabic_CI_AS"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructorClasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstructorClasses_InstructorProfiles_InstructorProfileId",
                        column: x => x.InstructorProfileId,
                        principalTable: "InstructorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstructorClasses_InstructorProfileId",
                table: "InstructorClasses",
                column: "InstructorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorClasses_InstructorProfileId_ClassLabel",
                table: "InstructorClasses",
                columns: new[] { "InstructorProfileId", "ClassLabel" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorClasses_IsDeleted",
                table: "InstructorClasses",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstructorClasses");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "InstructorProfiles");
        }
    }
}
