using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddW8SocialWorkerCaseWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorRole",
                table: "GuardianSummonStatusHistory",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "GuardianSummons",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyPayloadHash",
                table: "GuardianSummons",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImprovementVerificationDetails",
                table: "GuardianSummons",
                type: "nvarchar(3000)",
                maxLength: 3000,
                nullable: true,
                collation: "Arabic_CI_AS");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ObservationEndDate",
                table: "GuardianSummons",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservationGoals",
                table: "GuardianSummons",
                type: "nvarchar(3000)",
                maxLength: 3000,
                nullable: true,
                collation: "Arabic_CI_AS");

            migrationBuilder.AddColumn<string>(
                name: "ObservationIndicatorsJson",
                table: "GuardianSummons",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservationResponsibleStaffUserId",
                table: "GuardianSummons",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ObservationReviewDate",
                table: "GuardianSummons",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ObservationStartDate",
                table: "GuardianSummons",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentReferralId",
                table: "ConversationThreads",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GuardianSummonAppointmentHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    GuardianSummonId = table.Column<int>(type: "int", nullable: false),
                    GuardianProfileId = table.Column<int>(type: "int", nullable: false),
                    AppointmentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false, collation: "Arabic_CI_AS"),
                    Instructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, collation: "Arabic_CI_AS"),
                    Action = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActorRole = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, collation: "Arabic_CI_AS"),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuardianSummonAppointmentHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuardianSummonAppointmentHistory_GuardianProfiles_SchoolId_GuardianProfileId",
                        columns: x => new { x.SchoolId, x.GuardianProfileId },
                        principalTable: "GuardianProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianSummonAppointmentHistory_GuardianSummons_SchoolId_GuardianSummonId",
                        columns: x => new { x.SchoolId, x.GuardianSummonId },
                        principalTable: "GuardianSummons",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianSummonAppointmentHistory_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianSummonAppointmentHistory_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentReferralTransitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentReferralId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActorRole = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true, collation: "Arabic_CI_AS"),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentReferralTransitions", x => x.Id);
                    table.CheckConstraint("CK_StudentReferralTransitions_FromStatus", "[FromStatus] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_StudentReferralTransitions_ToStatus", "[ToStatus] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_StudentReferralTransitions_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentReferralTransitions_StudentReferrals_SchoolId_StudentReferralId",
                        columns: x => new { x.SchoolId, x.StudentReferralId },
                        principalTable: "StudentReferrals",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentReferralTransitions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummons_ObservationResponsibleStaffUserId",
                table: "GuardianSummons",
                column: "ObservationResponsibleStaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummons_SchoolId_CreatedByUserId_IdempotencyKey",
                table: "GuardianSummons",
                columns: new[] { "SchoolId", "CreatedByUserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationThreads_SchoolId_StudentReferralId_Status",
                table: "ConversationThreads",
                columns: new[] { "SchoolId", "StudentReferralId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummonAppointmentHistory_ActorUserId",
                table: "GuardianSummonAppointmentHistory",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummonAppointmentHistory_CorrelationId",
                table: "GuardianSummonAppointmentHistory",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummonAppointmentHistory_SchoolId_GuardianProfileId",
                table: "GuardianSummonAppointmentHistory",
                columns: new[] { "SchoolId", "GuardianProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummonAppointmentHistory_SchoolId_GuardianSummonId_OccurredAt",
                table: "GuardianSummonAppointmentHistory",
                columns: new[] { "SchoolId", "GuardianSummonId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferralTransitions_ActorUserId",
                table: "StudentReferralTransitions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferralTransitions_CorrelationId",
                table: "StudentReferralTransitions",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferralTransitions_SchoolId_StudentReferralId_OccurredAt",
                table: "StudentReferralTransitions",
                columns: new[] { "SchoolId", "StudentReferralId", "OccurredAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationThreads_StudentReferrals_SchoolId_StudentReferralId",
                table: "ConversationThreads",
                columns: new[] { "SchoolId", "StudentReferralId" },
                principalTable: "StudentReferrals",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GuardianSummons_Users_ObservationResponsibleStaffUserId",
                table: "GuardianSummons",
                column: "ObservationResponsibleStaffUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationThreads_StudentReferrals_SchoolId_StudentReferralId",
                table: "ConversationThreads");

            migrationBuilder.DropForeignKey(
                name: "FK_GuardianSummons_Users_ObservationResponsibleStaffUserId",
                table: "GuardianSummons");

            migrationBuilder.DropTable(
                name: "GuardianSummonAppointmentHistory");

            migrationBuilder.DropTable(
                name: "StudentReferralTransitions");

            migrationBuilder.DropIndex(
                name: "IX_GuardianSummons_ObservationResponsibleStaffUserId",
                table: "GuardianSummons");

            migrationBuilder.DropIndex(
                name: "IX_GuardianSummons_SchoolId_CreatedByUserId_IdempotencyKey",
                table: "GuardianSummons");

            migrationBuilder.DropIndex(
                name: "IX_ConversationThreads_SchoolId_StudentReferralId_Status",
                table: "ConversationThreads");

            migrationBuilder.DropColumn(
                name: "ActorRole",
                table: "GuardianSummonStatusHistory");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "GuardianSummons");

            migrationBuilder.DropColumn(
                name: "IdempotencyPayloadHash",
                table: "GuardianSummons");

            migrationBuilder.DropColumn(
                name: "ImprovementVerificationDetails",
                table: "GuardianSummons");

            migrationBuilder.DropColumn(
                name: "ObservationEndDate",
                table: "GuardianSummons");

            migrationBuilder.DropColumn(
                name: "ObservationGoals",
                table: "GuardianSummons");

            migrationBuilder.DropColumn(
                name: "ObservationIndicatorsJson",
                table: "GuardianSummons");

            migrationBuilder.DropColumn(
                name: "ObservationResponsibleStaffUserId",
                table: "GuardianSummons");

            migrationBuilder.DropColumn(
                name: "ObservationReviewDate",
                table: "GuardianSummons");

            migrationBuilder.DropColumn(
                name: "ObservationStartDate",
                table: "GuardianSummons");

            migrationBuilder.DropColumn(
                name: "StudentReferralId",
                table: "ConversationThreads");
        }
    }
}
