using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations;

[DbContext(typeof(AlFalahDbContext))]
[Migration("20260916200000_AddCrossDaySwapDestination")]
public partial class AddCrossDaySwapDestination : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ToDay",
            table: "TimetableSubstitutionMovements",
            type: "int",
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE TimetableSubstitutionMovements SET ToDay = Day WHERE ToDay IS NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ToDay",
            table: "TimetableSubstitutionMovements");
    }
}
