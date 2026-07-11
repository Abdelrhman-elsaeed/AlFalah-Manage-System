using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fix_ArabicUnicodeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Schools",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "LocationDetails",
                table: "Schools",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Schools",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Schools",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "LocationDetails",
                table: "Schools",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Schools",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldCollation: "Arabic_CI_AS");
        }
    }
}
