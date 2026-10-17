using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyStandbyCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoverageReconciliationSignals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaseOwner = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageReconciliationSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoverageReconciliationSignals_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyCoverageNeeds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    SchoolTimetableId = table.Column<int>(type: "int", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceEntryId = table.Column<int>(type: "int", nullable: false),
                    OriginalTeacherId = table.Column<int>(type: "int", nullable: false),
                    OriginalTeacherName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Classroom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntryIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PeriodsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    State = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    SubstituteWasAbsent = table.Column<bool>(type: "bit", nullable: false),
                    ReviewReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActiveSubstitutionId = table.Column<int>(type: "int", nullable: true),
                    AssignedTeacherName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AssignedUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Narrative = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCoverageNeeds", x => x.Id);
                    table.UniqueConstraint("AK_DailyCoverageNeeds_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_CoverageNeed_State", "[State] IN ('Needed','Assigned','Acknowledged','Executed','Unable','ReviewRequired','Withdrawn')");
                    table.CheckConstraint("CK_CoverageNeed_Window", "[StartsAt] < [EndsAt] AND [Revision] > 0");
                    table.ForeignKey(
                        name: "FK_DailyCoverageNeeds_InstructorProfiles_SchoolId_OriginalTeacherId",
                        columns: x => new { x.SchoolId, x.OriginalTeacherId },
                        principalTable: "InstructorProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCoverageNeeds_SchoolTimetableEntries_SchoolId_SourceEntryId",
                        columns: x => new { x.SchoolId, x.SourceEntryId },
                        principalTable: "SchoolTimetableEntries",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCoverageNeeds_SchoolTimetables_SchoolId_SchoolTimetableId",
                        columns: x => new { x.SchoolId, x.SchoolTimetableId },
                        principalTable: "SchoolTimetables",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCoverageNeeds_TimetableSubstitutions_SchoolId_ActiveSubstitutionId",
                        columns: x => new { x.SchoolId, x.ActiveSubstitutionId },
                        principalTable: "TimetableSubstitutions",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CoverageRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DailyCoverageNeedId = table.Column<int>(type: "int", nullable: false),
                    SubstitutionId = table.Column<int>(type: "int", nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Narrative = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SignatureBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    SignatureHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SignatureUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageRecords", x => x.Id);
                    table.CheckConstraint("CK_CoverageRecord_Signature", "([Kind] NOT IN ('Acknowledgement','Execution')) OR ([SignatureBytes] IS NOT NULL AND [SignatureHash] IS NOT NULL AND [SignatureUpdatedAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CoverageRecords_DailyCoverageNeeds_SchoolId_DailyCoverageNeedId",
                        columns: x => new { x.SchoolId, x.DailyCoverageNeedId },
                        principalTable: "DailyCoverageNeeds",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CoverageRecords_TimetableSubstitutions_SchoolId_SubstitutionId",
                        columns: x => new { x.SchoolId, x.SubstitutionId },
                        principalTable: "TimetableSubstitutions",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CoverageRecords_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageReconciliationSignals_ProcessedAt_NextAttemptAt_LeaseUntil",
                table: "CoverageReconciliationSignals",
                columns: new[] { "ProcessedAt", "NextAttemptAt", "LeaseUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageReconciliationSignals_SchoolId",
                table: "CoverageReconciliationSignals",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRecords_ActorUserId",
                table: "CoverageRecords",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRecords_SchoolId_DailyCoverageNeedId",
                table: "CoverageRecords",
                columns: new[] { "SchoolId", "DailyCoverageNeedId" });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRecords_SchoolId_RequestId",
                table: "CoverageRecords",
                columns: new[] { "SchoolId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRecords_SchoolId_SubstitutionId",
                table: "CoverageRecords",
                columns: new[] { "SchoolId", "SubstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCoverageNeeds_SchoolId_ActiveSubstitutionId",
                table: "DailyCoverageNeeds",
                columns: new[] { "SchoolId", "ActiveSubstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCoverageNeeds_SchoolId_LocalDate_SchoolTimetableId_SourceEntryId",
                table: "DailyCoverageNeeds",
                columns: new[] { "SchoolId", "LocalDate", "SchoolTimetableId", "SourceEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyCoverageNeeds_SchoolId_OriginalTeacherId",
                table: "DailyCoverageNeeds",
                columns: new[] { "SchoolId", "OriginalTeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCoverageNeeds_SchoolId_SchoolTimetableId",
                table: "DailyCoverageNeeds",
                columns: new[] { "SchoolId", "SchoolTimetableId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCoverageNeeds_SchoolId_SourceEntryId",
                table: "DailyCoverageNeeds",
                columns: new[] { "SchoolId", "SourceEntryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoverageReconciliationSignals");

            migrationBuilder.DropTable(
                name: "CoverageRecords");

            migrationBuilder.DropTable(
                name: "DailyCoverageNeeds");
        }
    }
}
