using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAbsenceExcuseIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "AbsenceExcuses",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [AbsenceExcuses] SET [IdempotencyKey] = CONCAT('legacy-', [Id]) WHERE [IdempotencyKey] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                table: "AbsenceExcuses",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldUnicode: false,
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceExcuses_SchoolId_GuardianProfileId_IdempotencyKey",
                table: "AbsenceExcuses",
                columns: new[] { "SchoolId", "GuardianProfileId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AbsenceExcuses_SchoolId_GuardianProfileId_IdempotencyKey",
                table: "AbsenceExcuses");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "AbsenceExcuses");
        }
    }
}
