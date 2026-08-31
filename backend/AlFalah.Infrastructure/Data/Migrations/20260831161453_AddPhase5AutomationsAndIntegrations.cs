using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase5AutomationsAndIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentReferrals_SchoolId_RuleTriggerId",
                table: "StudentReferrals");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedAt_NextAttemptAt",
                table: "OutboxMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_DeliveryStatus",
                table: "Notifications");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeadLetteredAt",
                table: "OutboxMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAt",
                table: "OutboxMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwner",
                table: "OutboxMessages",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuppressed",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Notifications",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SuppressedAt",
                table: "Notifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuppressedByUserId",
                table: "Notifications",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuppressionReason",
                table: "Notifications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                collation: "Arabic_CI_AS");

            migrationBuilder.CreateTable(
                name: "NoorAbsenceCorrectionBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    WeekStartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    WeekEndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Sha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    ExportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExportedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoorAbsenceCorrectionBatches", x => x.Id);
                    table.UniqueConstraint("AK_NoorAbsenceCorrectionBatches_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_NoorAbsenceCorrectionBatches_Status", "[Status] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_NoorAbsenceCorrectionBatches_Week", "[WeekEndsOn] >= [WeekStartsOn] AND [RowCount] >= 0");
                    table.ForeignKey(
                        name: "FK_NoorAbsenceCorrectionBatches_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoorAbsenceCorrectionBatches_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoorAbsenceCorrectionBatches_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoorAbsenceCorrectionBatches_Users_ExportedByUserId",
                        column: x => x.ExportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoorAbsenceCorrectionBatches_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoorAbsenceCorrectionBatchItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    DailyStudentAttendanceId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    StudentNameSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false, collation: "Arabic_CI_AS"),
                    NationalIdSnapshot = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExcuseStatusSnapshot = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoorAbsenceCorrectionBatchItems", x => x.Id);
                    table.CheckConstraint("CK_NoorAbsenceCorrectionBatchItems_ExcuseStatusSnapshot", "[ExcuseStatusSnapshot] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_NoorAbsenceCorrectionBatchItems_DailyStudentAttendances_SchoolId_DailyStudentAttendanceId",
                        columns: x => new { x.SchoolId, x.DailyStudentAttendanceId },
                        principalTable: "DailyStudentAttendances",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoorAbsenceCorrectionBatchItems_NoorAbsenceCorrectionBatches_SchoolId_BatchId",
                        columns: x => new { x.SchoolId, x.BatchId },
                        principalTable: "NoorAbsenceCorrectionBatches",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoorAbsenceCorrectionBatchItems_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoorAbsenceCorrectionBatchItems_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferrals_SchoolId_RuleTriggerId",
                table: "StudentReferrals",
                columns: new[] { "SchoolId", "RuleTriggerId" },
                unique: true,
                filter: "[RuleTriggerId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_DeadLetteredAt_NextAttemptAt_LeaseExpiresAt",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "DeadLetteredAt", "NextAttemptAt", "LeaseExpiresAt" },
                filter: "[ProcessedAt] IS NULL AND [DeadLetteredAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SchoolId_RequiresApproval_IsSuppressed_CreatedAt",
                table: "Notifications",
                columns: new[] { "SchoolId", "RequiresApproval", "IsSuppressed", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SuppressedByUserId",
                table: "Notifications",
                column: "SuppressedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_DeliveryStatus",
                table: "Notifications",
                sql: "[DeliveryStatus] BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_NoorAbsenceCorrectionBatches_CreatedByUserId",
                table: "NoorAbsenceCorrectionBatches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoorAbsenceCorrectionBatches_DeletedByUserId",
                table: "NoorAbsenceCorrectionBatches",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoorAbsenceCorrectionBatches_ExportedByUserId",
                table: "NoorAbsenceCorrectionBatches",
                column: "ExportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoorAbsenceCorrectionBatches_SchoolId_IdempotencyKey",
                table: "NoorAbsenceCorrectionBatches",
                columns: new[] { "SchoolId", "IdempotencyKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NoorAbsenceCorrectionBatches_SchoolId_WeekStartsOn_Status",
                table: "NoorAbsenceCorrectionBatches",
                columns: new[] { "SchoolId", "WeekStartsOn", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_NoorAbsenceCorrectionBatches_UpdatedByUserId",
                table: "NoorAbsenceCorrectionBatches",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoorAbsenceCorrectionBatchItems_SchoolId_BatchId_DailyStudentAttendanceId",
                table: "NoorAbsenceCorrectionBatchItems",
                columns: new[] { "SchoolId", "BatchId", "DailyStudentAttendanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoorAbsenceCorrectionBatchItems_SchoolId_DailyStudentAttendanceId",
                table: "NoorAbsenceCorrectionBatchItems",
                columns: new[] { "SchoolId", "DailyStudentAttendanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_NoorAbsenceCorrectionBatchItems_SchoolId_StudentId",
                table: "NoorAbsenceCorrectionBatchItems",
                columns: new[] { "SchoolId", "StudentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_SuppressedByUserId",
                table: "Notifications",
                column: "SuppressedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_SuppressedByUserId",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "NoorAbsenceCorrectionBatchItems");

            migrationBuilder.DropTable(
                name: "NoorAbsenceCorrectionBatches");

            migrationBuilder.DropIndex(
                name: "IX_StudentReferrals_SchoolId_RuleTriggerId",
                table: "StudentReferrals");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedAt_DeadLetteredAt_NextAttemptAt_LeaseExpiresAt",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_SchoolId_RequiresApproval_IsSuppressed_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_SuppressedByUserId",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_DeliveryStatus",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LeaseOwner",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "IsSuppressed",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SuppressedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SuppressedByUserId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SuppressionReason",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferrals_SchoolId_RuleTriggerId",
                table: "StudentReferrals",
                columns: new[] { "SchoolId", "RuleTriggerId" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_NextAttemptAt",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "NextAttemptAt" },
                filter: "[ProcessedAt] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_DeliveryStatus",
                table: "Notifications",
                sql: "[DeliveryStatus] BETWEEN 1 AND 4");

        }
    }
}
