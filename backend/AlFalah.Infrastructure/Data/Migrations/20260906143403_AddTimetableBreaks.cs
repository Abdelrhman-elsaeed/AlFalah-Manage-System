using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetableBreaks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UsesDefaultBreaks",
                table: "BellScheduleDays",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "ScheduleBreakDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BellScheduleDayId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleBreakDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleBreakDefinitions_BellScheduleDays_BellScheduleDayId",
                        column: x => x.BellScheduleDayId,
                        principalTable: "BellScheduleDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleBreakWindows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleBreakDefinitionId = table.Column<int>(type: "int", nullable: false),
                    StartLocalTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndLocalTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleBreakWindows", x => x.Id);
                    table.CheckConstraint("CK_ScheduleBreakWindows_Time", "[StartLocalTime] < [EndLocalTime]");
                    table.ForeignKey(
                        name: "FK_ScheduleBreakWindows_ScheduleBreakDefinitions_ScheduleBreakDefinitionId",
                        column: x => x.ScheduleBreakDefinitionId,
                        principalTable: "ScheduleBreakDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleBreakDefinitions_BellScheduleDayId",
                table: "ScheduleBreakDefinitions",
                column: "BellScheduleDayId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleBreakWindows_ScheduleBreakDefinitionId",
                table: "ScheduleBreakWindows",
                column: "ScheduleBreakDefinitionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleBreakWindows");

            migrationBuilder.DropTable(
                name: "ScheduleBreakDefinitions");

            migrationBuilder.DropColumn(
                name: "UsesDefaultBreaks",
                table: "BellScheduleDays");
        }
    }
}
