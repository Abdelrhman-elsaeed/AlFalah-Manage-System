using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations;

/// <summary>
/// This migration was hand-written without the generated `.Designer.cs`
/// companion, so it carried no <see cref="MigrationAttribute"/>. EF only
/// discovers migrations that are annotated with their id — without it the
/// migration was silently skipped by `Database.MigrateAsync()` and
/// `SchoolMicrosoftDrives` was never created, which made
/// GET /api/v1/school-microsoft-drive fail with "Invalid object name".
/// The attributes below are exactly what the designer file would have carried.
/// </summary>
[DbContext(typeof(AlFalahDbContext))]
[Migration("20260720193000_AddSchoolMicrosoftDrives")]
public partial class AddSchoolMicrosoftDrives : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SchoolMicrosoftDrives",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                SchoolId = table.Column<int>(type: "int", nullable: false),
                TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                SchoolMicrosoftEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                DriveId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                RootItemId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                RootFolderDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                ConnectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SchoolMicrosoftDrives", x => x.Id);
                table.ForeignKey("FK_SchoolMicrosoftDrives_Schools_SchoolId", x => x.SchoolId, "Schools", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_SchoolMicrosoftDrives_SchoolId", table: "SchoolMicrosoftDrives", column: "SchoolId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_SchoolMicrosoftDrives_TenantId", table: "SchoolMicrosoftDrives", column: "TenantId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "SchoolMicrosoftDrives");
}
