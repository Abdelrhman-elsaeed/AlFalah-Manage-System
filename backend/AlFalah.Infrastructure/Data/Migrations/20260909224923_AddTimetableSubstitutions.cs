using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetableSubstitutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetableEntries_SchoolTimetableId_Day_Period_ClassLabel",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetableEntries_SchoolTimetableId_Day_Period_RoomId",
                table: "SchoolTimetableEntries");

            migrationBuilder.CreateTable(
                name: "TimetableSubstitutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    SchoolTimetableId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposalId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BeforeRevision = table.Column<int>(type: "int", nullable: false),
                    AfterRevision = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OverrideReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SchoolTimetableVersionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableSubstitutions", x => x.Id);
                    table.UniqueConstraint("AK_TimetableSubstitutions_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_TimetableSubstitutions_SchoolTimetableVersions_SchoolTimetableVersionId",
                        column: x => x.SchoolTimetableVersionId,
                        principalTable: "SchoolTimetableVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableSubstitutions_SchoolTimetables_SchoolId_SchoolTimetableId",
                        columns: x => new { x.SchoolId, x.SchoolTimetableId },
                        principalTable: "SchoolTimetables",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableSubstitutions_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableSubstitutions_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimetableSubstitutionMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TimetableSubstitutionId = table.Column<int>(type: "int", nullable: false),
                    SchoolTimetableEntryId = table.Column<int>(type: "int", nullable: false),
                    FromTeacherId = table.Column<int>(type: "int", nullable: false),
                    ToTeacherId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    FromPeriod = table.Column<int>(type: "int", nullable: false),
                    ToPeriod = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableSubstitutionMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimetableSubstitutionMovements_InstructorProfiles_SchoolId_FromTeacherId",
                        columns: x => new { x.SchoolId, x.FromTeacherId },
                        principalTable: "InstructorProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableSubstitutionMovements_InstructorProfiles_SchoolId_ToTeacherId",
                        columns: x => new { x.SchoolId, x.ToTeacherId },
                        principalTable: "InstructorProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableSubstitutionMovements_SchoolTimetableEntries_SchoolId_SchoolTimetableEntryId",
                        columns: x => new { x.SchoolId, x.SchoolTimetableEntryId },
                        principalTable: "SchoolTimetableEntries",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableSubstitutionMovements_TimetableSubstitutions_SchoolId_TimetableSubstitutionId",
                        columns: x => new { x.SchoolId, x.TimetableSubstitutionId },
                        principalTable: "TimetableSubstitutions",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_SchoolTimetableId_Day_Period_ClassLabel",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolTimetableId", "Day", "Period", "ClassLabel" },
                filter: "[IsDeleted] = 0 AND [EntryType] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_SchoolTimetableId_Day_Period_RoomId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolTimetableId", "Day", "Period", "RoomId" },
                filter: "[IsDeleted] = 0 AND [RoomId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSubstitutionMovements_SchoolId_FromTeacherId",
                table: "TimetableSubstitutionMovements",
                columns: new[] { "SchoolId", "FromTeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSubstitutionMovements_SchoolId_SchoolTimetableEntryId",
                table: "TimetableSubstitutionMovements",
                columns: new[] { "SchoolId", "SchoolTimetableEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSubstitutionMovements_SchoolId_TimetableSubstitutionId",
                table: "TimetableSubstitutionMovements",
                columns: new[] { "SchoolId", "TimetableSubstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSubstitutionMovements_SchoolId_ToTeacherId",
                table: "TimetableSubstitutionMovements",
                columns: new[] { "SchoolId", "ToTeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSubstitutionMovements_TimetableSubstitutionId_SchoolTimetableEntryId",
                table: "TimetableSubstitutionMovements",
                columns: new[] { "TimetableSubstitutionId", "SchoolTimetableEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSubstitutions_ApprovedByUserId",
                table: "TimetableSubstitutions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSubstitutions_RequestedByUserId",
                table: "TimetableSubstitutions",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSubstitutions_SchoolId_RequestId",
                table: "TimetableSubstitutions",
                columns: new[] { "SchoolId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSubstitutions_SchoolId_SchoolTimetableId",
                table: "TimetableSubstitutions",
                columns: new[] { "SchoolId", "SchoolTimetableId" });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSubstitutions_SchoolTimetableId_LocalDate",
                table: "TimetableSubstitutions",
                columns: new[] { "SchoolTimetableId", "LocalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSubstitutions_SchoolTimetableVersionId",
                table: "TimetableSubstitutions",
                column: "SchoolTimetableVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimetableSubstitutionMovements");

            migrationBuilder.DropTable(
                name: "TimetableSubstitutions");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetableEntries_SchoolTimetableId_Day_Period_ClassLabel",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetableEntries_SchoolTimetableId_Day_Period_RoomId",
                table: "SchoolTimetableEntries");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_SchoolTimetableId_Day_Period_ClassLabel",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolTimetableId", "Day", "Period", "ClassLabel" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [EntryType] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_SchoolTimetableId_Day_Period_RoomId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolTimetableId", "Day", "Period", "RoomId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [RoomId] IS NOT NULL");
        }
    }
}
