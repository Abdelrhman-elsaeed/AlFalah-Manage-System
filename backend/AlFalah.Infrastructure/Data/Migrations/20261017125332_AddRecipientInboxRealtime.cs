using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipientInboxRealtime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxChanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxDeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IntentId = table.Column<long>(type: "bigint", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxDeliveryAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveryIntents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveryIntents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecipientInboxStreams",
                columns: table => new
                {
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CommittedSequence = table.Column<long>(type: "bigint", nullable: false),
                    MinimumRetainedSequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipientInboxStreams", x => new { x.SchoolId, x.UserId })
                        .Annotation("SqlServer:Clustered", false);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxChanges_OccurredAtUtc",
                table: "InboxChanges",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InboxChanges_SchoolId_UserId_Sequence",
                table: "InboxChanges",
                columns: new[] { "SchoolId", "UserId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxDeliveryAttempts_IntentId_LeaseId",
                table: "InboxDeliveryAttempts",
                columns: new[] { "IntentId", "LeaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryIntents_CompletedAtUtc_NextAttemptAtUtc_LeaseUntilUtc",
                table: "NotificationDeliveryIntents",
                columns: new[] { "CompletedAtUtc", "NextAttemptAtUtc", "LeaseUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryIntents_SchoolId_UserId_SourceKey_Channel",
                table: "NotificationDeliveryIntents",
                columns: new[] { "SchoolId", "UserId", "SourceKey", "Channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxChanges");

            migrationBuilder.DropTable(
                name: "InboxDeliveryAttempts");

            migrationBuilder.DropTable(
                name: "NotificationDeliveryIntents");

            migrationBuilder.DropTable(
                name: "RecipientInboxStreams");
        }
    }
}
