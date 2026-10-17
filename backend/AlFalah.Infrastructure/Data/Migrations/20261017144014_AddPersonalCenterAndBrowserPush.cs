using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalCenterAndBrowserPush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceCompletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ClassroomId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    RosterCount = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceCompletions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrowserPushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    InstallationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndpointHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProtectedMaterial = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    VapidKeyId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BindingVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserPushSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonalNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: true),
                    Audience = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NotificationId = table.Column<int>(type: "int", nullable: true),
                    MessageId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Preview = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrowserPushTargets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BindingVersion = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LastHttpStatus = table.Column<int>(type: "int", nullable: true),
                    ProviderAcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserPushTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrowserPushTargets_BrowserPushSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "BrowserPushSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BrowserPushTargets_PersonalNotifications_EntryId",
                        column: x => x.EntryId,
                        principalTable: "PersonalNotifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCompletions_SchoolId_Date_ClassroomId",
                table: "AttendanceCompletions",
                columns: new[] { "SchoolId", "Date", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserPushSubscriptions_EndpointHash",
                table: "BrowserPushSubscriptions",
                column: "EndpointHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrowserPushSubscriptions_SchoolId_UserId_RevokedAtUtc",
                table: "BrowserPushSubscriptions",
                columns: new[] { "SchoolId", "UserId", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserPushTargets_EntryId_SubscriptionId",
                table: "BrowserPushTargets",
                columns: new[] { "EntryId", "SubscriptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrowserPushTargets_State_NextAttemptAtUtc_LeaseUntilUtc",
                table: "BrowserPushTargets",
                columns: new[] { "State", "NextAttemptAtUtc", "LeaseUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserPushTargets_SubscriptionId",
                table: "BrowserPushTargets",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalNotifications_SchoolId_UserId_CreatedAtUtc_Id",
                table: "PersonalNotifications",
                columns: new[] { "SchoolId", "UserId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalNotifications_SchoolId_UserId_ReadAtUtc",
                table: "PersonalNotifications",
                columns: new[] { "SchoolId", "UserId", "ReadAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalNotifications_SchoolId_UserId_SourceKey",
                table: "PersonalNotifications",
                columns: new[] { "SchoolId", "UserId", "SourceKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceCompletions");

            migrationBuilder.DropTable(
                name: "BrowserPushTargets");

            migrationBuilder.DropTable(
                name: "BrowserPushSubscriptions");

            migrationBuilder.DropTable(
                name: "PersonalNotifications");
        }
    }
}
