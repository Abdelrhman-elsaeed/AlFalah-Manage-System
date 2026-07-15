using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace AlFalah.Infrastructure.Data.Migrations;

public partial class AddSchoolLocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SchoolLocationId",
            table: "Schools",
            type: "int",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "SchoolLocations",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                NameAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, collation: "Arabic_CI_AS"),
                NameEn = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true),
                RegionNameAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, collation: "Arabic_CI_AS"),
                RegionNameEn = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true),
                Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                DeletedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_SchoolLocations", x => x.Id));

        var createdAt = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);
        migrationBuilder.InsertData(
            table: "SchoolLocations",
            columns: ["Id", "CreatedAt", "DeletedAt", "DeletedByUserId", "IsActive", "IsDeleted", "Latitude", "Longitude", "NameAr", "NameEn", "RegionNameAr", "RegionNameEn", "UpdatedAt"],
            values: new object[,]
            {
                { 1, createdAt, null, null, true, false, 24.713552m, 46.675296m, "الرياض", "Riyadh", "منطقة الرياض", "Riyadh Region", createdAt },
                { 2, createdAt, null, null, true, false, 21.485811m, 39.192505m, "جدة", "Jeddah", "منطقة مكة المكرمة", "Makkah Region", createdAt },
                { 3, createdAt, null, null, true, false, 21.389082m, 39.857910m, "مكة المكرمة", "Makkah", "منطقة مكة المكرمة", "Makkah Region", createdAt },
                { 4, createdAt, null, null, true, false, 24.524654m, 39.569184m, "المدينة المنورة", "Madinah", "منطقة المدينة المنورة", "Madinah Region", createdAt },
                { 5, createdAt, null, null, true, false, 26.420683m, 50.088794m, "الدمام", "Dammam", "المنطقة الشرقية", "Eastern Region", createdAt },
                { 6, createdAt, null, null, true, false, 26.217190m, 50.197138m, "الخبر", "Al Khobar", "المنطقة الشرقية", "Eastern Region", createdAt },
                { 7, createdAt, null, null, true, false, 25.364572m, 49.587238m, "الهفوف", "Al Hofuf", "المنطقة الشرقية", "Eastern Region", createdAt },
                { 8, createdAt, null, null, true, false, 21.270283m, 40.415833m, "الطائف", "Taif", "منطقة مكة المكرمة", "Makkah Region", createdAt },
                { 9, createdAt, null, null, true, false, 26.359230m, 43.981812m, "بريدة", "Buraydah", "منطقة القصيم", "Al-Qassim Region", createdAt },
                { 10, createdAt, null, null, true, false, 18.216389m, 42.505280m, "أبها", "Abha", "منطقة عسير", "Asir Region", createdAt },
                { 11, createdAt, null, null, true, false, 28.383507m, 36.566190m, "تبوك", "Tabuk", "منطقة تبوك", "Tabuk Region", createdAt },
                { 12, createdAt, null, null, true, false, 27.511410m, 41.720825m, "حائل", "Hail", "منطقة حائل", "Hail Region", createdAt },
                { 13, createdAt, null, null, true, false, 30.975310m, 41.038082m, "عرعر", "Arar", "منطقة الحدود الشمالية", "Northern Borders Region", createdAt },
                { 14, createdAt, null, null, true, false, 16.889359m, 42.570568m, "جازان", "Jazan", "منطقة جازان", "Jazan Region", createdAt },
                { 15, createdAt, null, null, true, false, 17.565603m, 44.228944m, "نجران", "Najran", "منطقة نجران", "Najran Region", createdAt },
                { 16, createdAt, null, null, true, false, 20.012880m, 41.467670m, "الباحة", "Al Bahah", "منطقة الباحة", "Al-Bahah Region", createdAt },
                { 17, createdAt, null, null, true, false, 29.969740m, 40.206410m, "سكاكا", "Sakaka", "منطقة الجوف", "Al-Jawf Region", createdAt },
                { 18, createdAt, null, null, true, false, 27.017400m, 49.658300m, "الجبيل", "Jubail", "المنطقة الشرقية", "Eastern Region", createdAt }
            });

        migrationBuilder.Sql("""
            UPDATE s
            SET s.SchoolLocationId = l.Id
            FROM Schools AS s
            INNER JOIN SchoolLocations AS l
                ON LTRIM(RTRIM(s.City)) = l.NameAr
            WHERE s.SchoolLocationId IS NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Schools_SchoolLocationId",
            table: "Schools",
            column: "SchoolLocationId");
        migrationBuilder.CreateIndex(name: "IX_SchoolLocations_IsActive", table: "SchoolLocations", column: "IsActive");
        migrationBuilder.CreateIndex(name: "IX_SchoolLocations_IsDeleted", table: "SchoolLocations", column: "IsDeleted");
        migrationBuilder.CreateIndex(
            name: "IX_SchoolLocations_NameAr_RegionNameAr",
            table: "SchoolLocations",
            columns: ["NameAr", "RegionNameAr"],
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Schools_SchoolLocations_SchoolLocationId",
            table: "Schools",
            column: "SchoolLocationId",
            principalTable: "SchoolLocations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_Schools_SchoolLocations_SchoolLocationId", "Schools");
        migrationBuilder.DropTable("SchoolLocations");
        migrationBuilder.DropIndex("IX_Schools_SchoolLocationId", "Schools");
        migrationBuilder.DropColumn("SchoolLocationId", "Schools");
    }
}
