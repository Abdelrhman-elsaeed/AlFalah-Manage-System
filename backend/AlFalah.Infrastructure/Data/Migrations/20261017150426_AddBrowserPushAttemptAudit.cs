using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBrowserPushAttemptAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PushDisposition",
                table: "PersonalNotifications",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Disabled");

            migrationBuilder.CreateTable(
                name: "BrowserPushAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetId = table.Column<long>(type: "bigint", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    State = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    HttpStatus = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserPushAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrowserPushAttempts_BrowserPushTargets_TargetId",
                        column: x => x.TargetId,
                        principalTable: "BrowserPushTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserPushAttempts_TargetId_LeaseId",
                table: "BrowserPushAttempts",
                columns: new[] { "TargetId", "LeaseId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrowserPushAttempts");

            migrationBuilder.DropColumn(
                name: "PushDisposition",
                table: "PersonalNotifications");
        }
    }
}
