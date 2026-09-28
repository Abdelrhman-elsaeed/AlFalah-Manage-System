using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddW5OfficerReferralIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "StudentReferrals",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyPayloadHash",
                table: "StudentReferrals",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferrals_SchoolId_CreatedByUserId_IdempotencyKey",
                table: "StudentReferrals",
                columns: new[] { "SchoolId", "CreatedByUserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentReferrals_SchoolId_CreatedByUserId_IdempotencyKey",
                table: "StudentReferrals");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "StudentReferrals");

            migrationBuilder.DropColumn(
                name: "IdempotencyPayloadHash",
                table: "StudentReferrals");
        }
    }
}
