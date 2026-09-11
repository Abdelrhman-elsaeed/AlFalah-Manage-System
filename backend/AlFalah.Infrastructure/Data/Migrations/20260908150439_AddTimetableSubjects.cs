using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetableSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClassSubjectRequirementId",
                table: "SchoolTimetableEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "SchoolTimetableEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubjectId",
                table: "SchoolTimetableEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubjectDefinition",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Arabic_CI_AS"),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectDefinition", x => x.Id);
                    table.UniqueConstraint("AK_SubjectDefinition_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_SubjectDefinition_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimetableRoom",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Arabic_CI_AS"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableRoom", x => x.Id);
                    table.UniqueConstraint("AK_TimetableRoom_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_TimetableRoom_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassSubjectRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TimetableSetupProfileId = table.Column<int>(type: "int", nullable: false),
                    ClassroomId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    IndividualPeriodCount = table.Column<int>(type: "int", nullable: false),
                    PairedBlockCount = table.Column<int>(type: "int", nullable: false),
                    TimePreference = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EarliestPeriodSequence = table.Column<int>(type: "int", nullable: true),
                    LatestPreferredPeriodSequence = table.Column<int>(type: "int", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSubjectRequirements", x => x.Id);
                    table.UniqueConstraint("AK_ClassSubjectRequirements_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_SubjectRequirements_Counts", "[IndividualPeriodCount] BETWEEN 0 AND 100 AND [PairedBlockCount] BETWEEN 0 AND 50 AND [IndividualPeriodCount] + 2 * [PairedBlockCount] > 0");
                    table.ForeignKey(
                        name: "FK_ClassSubjectRequirements_Classrooms_SchoolId_ClassroomId",
                        columns: x => new { x.SchoolId, x.ClassroomId },
                        principalTable: "Classrooms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSubjectRequirements_SubjectDefinition_SchoolId_SubjectId",
                        columns: x => new { x.SchoolId, x.SubjectId },
                        principalTable: "SubjectDefinition",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSubjectRequirements_TimetableSetupProfiles_SchoolId_TimetableSetupProfileId",
                        columns: x => new { x.SchoolId, x.TimetableSetupProfileId },
                        principalTable: "TimetableSetupProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassSubjectAllowedDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassSubjectRequirementId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSubjectAllowedDays", x => x.Id);
                    table.CheckConstraint("CK_SubjectAllowedDay", "[Day] BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_ClassSubjectAllowedDays_ClassSubjectRequirements_ClassSubjectRequirementId",
                        column: x => x.ClassSubjectRequirementId,
                        principalTable: "ClassSubjectRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassSubjectFixedSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassSubjectRequirementId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSubjectFixedSlots", x => x.Id);
                    table.CheckConstraint("CK_SubjectFixedSlot", "[Day] BETWEEN 1 AND 7 AND [Period] > 0");
                    table.ForeignKey(
                        name: "FK_ClassSubjectFixedSlots_ClassSubjectRequirements_ClassSubjectRequirementId",
                        column: x => x.ClassSubjectRequirementId,
                        principalTable: "ClassSubjectRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubjectRoomRequirement",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ClassSubjectRequirementId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    IsPreferred = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectRoomRequirement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubjectRoomRequirement_ClassSubjectRequirements_SchoolId_ClassSubjectRequirementId",
                        columns: x => new { x.SchoolId, x.ClassSubjectRequirementId },
                        principalTable: "ClassSubjectRequirements",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubjectRoomRequirement_TimetableRoom_SchoolId_RoomId",
                        columns: x => new { x.SchoolId, x.RoomId },
                        principalTable: "TimetableRoom",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_SchoolId_ClassSubjectRequirementId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolId", "ClassSubjectRequirementId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_SchoolId_RoomId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolId", "RoomId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_SchoolId_SubjectId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_SchoolTimetableId_Day_Period_RoomId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolTimetableId", "Day", "Period", "RoomId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [RoomId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TimetableEntry_SubjectSource",
                table: "SchoolTimetableEntries",
                sql: "[ClassSubjectRequirementId] IS NULL OR ([ClassroomId] IS NOT NULL AND [SubjectId] IS NOT NULL AND [EntryType] = 1)");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectAllowedDays_ClassSubjectRequirementId_Day",
                table: "ClassSubjectAllowedDays",
                columns: new[] { "ClassSubjectRequirementId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectFixedSlots_ClassSubjectRequirementId_Day_Period",
                table: "ClassSubjectFixedSlots",
                columns: new[] { "ClassSubjectRequirementId", "Day", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectRequirements_SchoolId_ClassroomId",
                table: "ClassSubjectRequirements",
                columns: new[] { "SchoolId", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectRequirements_SchoolId_SubjectId",
                table: "ClassSubjectRequirements",
                columns: new[] { "SchoolId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectRequirements_SchoolId_TimetableSetupProfileId",
                table: "ClassSubjectRequirements",
                columns: new[] { "SchoolId", "TimetableSetupProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectRequirements_TimetableSetupProfileId_ClassroomId_SubjectId",
                table: "ClassSubjectRequirements",
                columns: new[] { "TimetableSetupProfileId", "ClassroomId", "SubjectId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectDefinition_SchoolId_Name",
                table: "SubjectDefinition",
                columns: new[] { "SchoolId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectRoomRequirement_ClassSubjectRequirementId_RoomId",
                table: "SubjectRoomRequirement",
                columns: new[] { "ClassSubjectRequirementId", "RoomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubjectRoomRequirement_SchoolId_ClassSubjectRequirementId",
                table: "SubjectRoomRequirement",
                columns: new[] { "SchoolId", "ClassSubjectRequirementId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubjectRoomRequirement_SchoolId_RoomId",
                table: "SubjectRoomRequirement",
                columns: new[] { "SchoolId", "RoomId" });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableRoom_SchoolId_Name",
                table: "TimetableRoom",
                columns: new[] { "SchoolId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolTimetableEntries_ClassSubjectRequirements_SchoolId_ClassSubjectRequirementId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolId", "ClassSubjectRequirementId" },
                principalTable: "ClassSubjectRequirements",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolTimetableEntries_SubjectDefinition_SchoolId_SubjectId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolId", "SubjectId" },
                principalTable: "SubjectDefinition",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolTimetableEntries_TimetableRoom_SchoolId_RoomId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolId", "RoomId" },
                principalTable: "TimetableRoom",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SchoolTimetableEntries_ClassSubjectRequirements_SchoolId_ClassSubjectRequirementId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolTimetableEntries_SubjectDefinition_SchoolId_SubjectId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolTimetableEntries_TimetableRoom_SchoolId_RoomId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropTable(
                name: "ClassSubjectAllowedDays");

            migrationBuilder.DropTable(
                name: "ClassSubjectFixedSlots");

            migrationBuilder.DropTable(
                name: "SubjectRoomRequirement");

            migrationBuilder.DropTable(
                name: "ClassSubjectRequirements");

            migrationBuilder.DropTable(
                name: "TimetableRoom");

            migrationBuilder.DropTable(
                name: "SubjectDefinition");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetableEntries_SchoolId_ClassSubjectRequirementId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetableEntries_SchoolId_RoomId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetableEntries_SchoolId_SubjectId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetableEntries_SchoolTimetableId_Day_Period_RoomId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TimetableEntry_SubjectSource",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropColumn(
                name: "ClassSubjectRequirementId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "SchoolTimetableEntries");
        }
    }
}
