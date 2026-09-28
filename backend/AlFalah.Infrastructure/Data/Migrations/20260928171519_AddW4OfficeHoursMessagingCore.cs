using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddW4OfficeHoursMessagingCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BellScheduleRevisionId",
                table: "TeacherOfficeHours",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ConflictReason",
                table: "TeacherOfficeHours",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                collation: "Arabic_CI_AS");

            migrationBuilder.AddColumn<bool>(
                name: "IsConflicted",
                table: "TeacherOfficeHours",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SchoolTimetableId",
                table: "TeacherOfficeHours",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StableSlotKey",
                table: "TeacherOfficeHours",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TeacherOfficeHourConfigurationId",
                table: "TeacherOfficeHours",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimetableRevision",
                table: "TeacherOfficeHours",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "ConversationMessages",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyPayloadHash",
                table: "ConversationMessages",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextEligibleSendAt",
                table: "ConversationMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReleaseOutboxEventId",
                table: "ConversationMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReleasedAt",
                table: "ConversationMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeacherOfficeHourConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    InstructorProfileId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    SchoolTimetableId = table.Column<int>(type: "int", nullable: false),
                    TimetableRevision = table.Column<int>(type: "int", nullable: false),
                    BellScheduleRevisionId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    OverrideReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, collation: "Arabic_CI_AS"),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherOfficeHourConfigurations", x => x.Id);
                    table.UniqueConstraint("AK_TeacherOfficeHourConfigurations_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_TeacherOfficeHourConfigurations_Source", "[Source] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHourConfigurations_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHourConfigurations_InstructorProfiles_SchoolId_InstructorProfileId",
                        columns: x => new { x.SchoolId, x.InstructorProfileId },
                        principalTable: "InstructorProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHourConfigurations_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHourConfigurations_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHourConfigurations_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHourConfigurations_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherOfficeHourAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TeacherOfficeHourConfigurationId = table.Column<int>(type: "int", nullable: false),
                    InstructorProfileId = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, collation: "Arabic_CI_AS"),
                    BeforeSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherOfficeHourAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHourAudits_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHourAudits_TeacherOfficeHourConfigurations_SchoolId_TeacherOfficeHourConfigurationId",
                        columns: x => new { x.SchoolId, x.TeacherOfficeHourConfigurationId },
                        principalTable: "TeacherOfficeHourConfigurations",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHours_SchoolId_TeacherOfficeHourConfigurationId",
                table: "TeacherOfficeHours",
                columns: new[] { "SchoolId", "TeacherOfficeHourConfigurationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHours_TeacherOfficeHourConfigurationId_StableSlotKey",
                table: "TeacherOfficeHours",
                columns: new[] { "TeacherOfficeHourConfigurationId", "StableSlotKey" },
                unique: true,
                filter: "[TeacherOfficeHourConfigurationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_OfficeHoursDisposition_NextEligibleSendAt",
                table: "ConversationMessages",
                columns: new[] { "OfficeHoursDisposition", "NextEligibleSendAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_SchoolId_SenderUserId_IdempotencyKey",
                table: "ConversationMessages",
                columns: new[] { "SchoolId", "SenderUserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHourAudits_SchoolId_InstructorProfileId_OccurredAt",
                table: "TeacherOfficeHourAudits",
                columns: new[] { "SchoolId", "InstructorProfileId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHourAudits_SchoolId_TeacherOfficeHourConfigurationId",
                table: "TeacherOfficeHourAudits",
                columns: new[] { "SchoolId", "TeacherOfficeHourConfigurationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHourConfigurations_CreatedByUserId",
                table: "TeacherOfficeHourConfigurations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHourConfigurations_DeletedByUserId",
                table: "TeacherOfficeHourConfigurations",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHourConfigurations_SchoolId_AcademicTermId",
                table: "TeacherOfficeHourConfigurations",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHourConfigurations_SchoolId_InstructorProfileId_IsCurrent",
                table: "TeacherOfficeHourConfigurations",
                columns: new[] { "SchoolId", "InstructorProfileId", "IsCurrent" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHourConfigurations_UpdatedByUserId",
                table: "TeacherOfficeHourConfigurations",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherOfficeHours_TeacherOfficeHourConfigurations_SchoolId_TeacherOfficeHourConfigurationId",
                table: "TeacherOfficeHours",
                columns: new[] { "SchoolId", "TeacherOfficeHourConfigurationId" },
                principalTable: "TeacherOfficeHourConfigurations",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeacherOfficeHours_TeacherOfficeHourConfigurations_SchoolId_TeacherOfficeHourConfigurationId",
                table: "TeacherOfficeHours");

            migrationBuilder.DropTable(
                name: "TeacherOfficeHourAudits");

            migrationBuilder.DropTable(
                name: "TeacherOfficeHourConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_TeacherOfficeHours_SchoolId_TeacherOfficeHourConfigurationId",
                table: "TeacherOfficeHours");

            migrationBuilder.DropIndex(
                name: "IX_TeacherOfficeHours_TeacherOfficeHourConfigurationId_StableSlotKey",
                table: "TeacherOfficeHours");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_OfficeHoursDisposition_NextEligibleSendAt",
                table: "ConversationMessages");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_SchoolId_SenderUserId_IdempotencyKey",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "BellScheduleRevisionId",
                table: "TeacherOfficeHours");

            migrationBuilder.DropColumn(
                name: "ConflictReason",
                table: "TeacherOfficeHours");

            migrationBuilder.DropColumn(
                name: "IsConflicted",
                table: "TeacherOfficeHours");

            migrationBuilder.DropColumn(
                name: "SchoolTimetableId",
                table: "TeacherOfficeHours");

            migrationBuilder.DropColumn(
                name: "StableSlotKey",
                table: "TeacherOfficeHours");

            migrationBuilder.DropColumn(
                name: "TeacherOfficeHourConfigurationId",
                table: "TeacherOfficeHours");

            migrationBuilder.DropColumn(
                name: "TimetableRevision",
                table: "TeacherOfficeHours");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "IdempotencyPayloadHash",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "NextEligibleSendAt",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "ReleaseOutboxEventId",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "ConversationMessages");
        }
    }
}
