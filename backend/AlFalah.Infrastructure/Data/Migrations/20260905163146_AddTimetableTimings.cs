using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetableTimings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server requires dependent check constraints to be removed while widening Period.
            migrationBuilder.DropCheckConstraint("CK_TeacherOfficeHours_TimeShape", "TeacherOfficeHours");
            migrationBuilder.DropCheckConstraint(
                name: "CK_TeacherOfficeHours_Day",
                table: "TeacherOfficeHours");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TeacherOfficeHours_Period",
                table: "TeacherOfficeHours");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SessionDelays_Period",
                table: "SessionDelays");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SchoolTimetableEntries_Day",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SchoolTimetableEntries_Period",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GatePasses_Period",
                table: "GatePasses");

            migrationBuilder.AlterColumn<int>(
                name: "Period",
                table: "TeacherOfficeHours",
                type: "int",
                nullable: true,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Period",
                table: "SessionDelays",
                type: "int",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AddColumn<int>(
                name: "BellScheduleRevisionId",
                table: "SchoolTimetables",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TimingsRequireRevalidation",
                table: "SchoolTimetables",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "Period",
                table: "SchoolTimetableEntries",
                type: "int",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AlterColumn<int>(
                name: "CurrentPeriod",
                table: "GatePasses",
                type: "int",
                nullable: true,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "BellScheduleTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Semester = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, collation: "Arabic_CI_AS"),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BellScheduleTemplates", x => x.Id);
                    table.UniqueConstraint("AK_BellScheduleTemplates_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_BellScheduleTemplates_Revision", "[Revision] > 0");
                    table.CheckConstraint("CK_BellScheduleTemplates_Semester", "[Semester] BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_BellScheduleTemplates_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BellScheduleTemplates_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BellScheduleRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BellScheduleTemplateId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SchoolTimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BellScheduleRevisions", x => x.Id);
                    table.UniqueConstraint("AK_BellScheduleRevisions_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_BellScheduleRevisions_BellScheduleTemplates_SchoolId_BellScheduleTemplateId",
                        columns: x => new { x.SchoolId, x.BellScheduleTemplateId },
                        principalTable: "BellScheduleTemplates",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BellScheduleDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BellScheduleRevisionId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    IsStudyDay = table.Column<bool>(type: "bit", nullable: false),
                    UsesDefaultSchedule = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BellScheduleDays", x => x.Id);
                    table.CheckConstraint("CK_BellScheduleDays_Day", "[Day] BETWEEN 0 AND 7");
                    table.ForeignKey(
                        name: "FK_BellScheduleDays_BellScheduleRevisions_BellScheduleRevisionId",
                        column: x => x.BellScheduleRevisionId,
                        principalTable: "BellScheduleRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BellPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BellScheduleDayId = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    DisplayLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartLocalTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndLocalTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BellPeriods", x => x.Id);
                    table.CheckConstraint("CK_BellPeriods_Sequence", "[Sequence] > 0");
                    table.CheckConstraint("CK_BellPeriods_Time", "[StartLocalTime] < [EndLocalTime]");
                    table.ForeignKey(
                        name: "FK_BellPeriods_BellScheduleDays_BellScheduleDayId",
                        column: x => x.BellScheduleDayId,
                        principalTable: "BellScheduleDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSetupProfiles_SchoolId_BellScheduleTemplateId",
                table: "TimetableSetupProfiles",
                columns: new[] { "SchoolId", "BellScheduleTemplateId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TeacherOfficeHours_Day",
                table: "TeacherOfficeHours",
                sql: "[Day] BETWEEN 1 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TeacherOfficeHours_Period",
                table: "TeacherOfficeHours",
                sql: "[Period] IS NULL OR [Period] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SessionDelays_Period",
                table: "SessionDelays",
                sql: "[Period] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetables_SchoolId_BellScheduleRevisionId",
                table: "SchoolTimetables",
                columns: new[] { "SchoolId", "BellScheduleRevisionId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchoolTimetableEntries_Day",
                table: "SchoolTimetableEntries",
                sql: "[Day] BETWEEN 1 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchoolTimetableEntries_Period",
                table: "SchoolTimetableEntries",
                sql: "[Period] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GatePasses_Period",
                table: "GatePasses",
                sql: "[CurrentPeriod] IS NULL OR [CurrentPeriod] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_BellPeriods_BellScheduleDayId_Sequence",
                table: "BellPeriods",
                columns: new[] { "BellScheduleDayId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BellScheduleDays_BellScheduleRevisionId_Day",
                table: "BellScheduleDays",
                columns: new[] { "BellScheduleRevisionId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BellScheduleRevisions_BellScheduleTemplateId_Revision",
                table: "BellScheduleRevisions",
                columns: new[] { "BellScheduleTemplateId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BellScheduleRevisions_SchoolId_BellScheduleTemplateId",
                table: "BellScheduleRevisions",
                columns: new[] { "SchoolId", "BellScheduleTemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_BellScheduleTemplates_AcademicYearId",
                table: "BellScheduleTemplates",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_BellScheduleTemplates_SchoolId_AcademicYearId_Semester_Name",
                table: "BellScheduleTemplates",
                columns: new[] { "SchoolId", "AcademicYearId", "Semester", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolTimetables_BellScheduleRevisions_SchoolId_BellScheduleRevisionId",
                table: "SchoolTimetables",
                columns: new[] { "SchoolId", "BellScheduleRevisionId" },
                principalTable: "BellScheduleRevisions",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TimetableSetupProfiles_BellScheduleTemplates_SchoolId_BellScheduleTemplateId",
                table: "TimetableSetupProfiles",
                columns: new[] { "SchoolId", "BellScheduleTemplateId" },
                principalTable: "BellScheduleTemplates",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);
            AddOfficeHourTimeShape(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint("CK_TeacherOfficeHours_TimeShape", "TeacherOfficeHours");
            migrationBuilder.DropForeignKey(
                name: "FK_SchoolTimetables_BellScheduleRevisions_SchoolId_BellScheduleRevisionId",
                table: "SchoolTimetables");

            migrationBuilder.DropForeignKey(
                name: "FK_TimetableSetupProfiles_BellScheduleTemplates_SchoolId_BellScheduleTemplateId",
                table: "TimetableSetupProfiles");

            migrationBuilder.DropTable(
                name: "BellPeriods");

            migrationBuilder.DropTable(
                name: "BellScheduleDays");

            migrationBuilder.DropTable(
                name: "BellScheduleRevisions");

            migrationBuilder.DropTable(
                name: "BellScheduleTemplates");

            migrationBuilder.DropIndex(
                name: "IX_TimetableSetupProfiles_SchoolId_BellScheduleTemplateId",
                table: "TimetableSetupProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TeacherOfficeHours_Day",
                table: "TeacherOfficeHours");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TeacherOfficeHours_Period",
                table: "TeacherOfficeHours");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SessionDelays_Period",
                table: "SessionDelays");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetables_SchoolId_BellScheduleRevisionId",
                table: "SchoolTimetables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SchoolTimetableEntries_Day",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SchoolTimetableEntries_Period",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GatePasses_Period",
                table: "GatePasses");

            migrationBuilder.DropColumn(
                name: "BellScheduleRevisionId",
                table: "SchoolTimetables");

            migrationBuilder.DropColumn(
                name: "TimingsRequireRevalidation",
                table: "SchoolTimetables");

            migrationBuilder.AlterColumn<byte>(
                name: "Period",
                table: "TeacherOfficeHours",
                type: "tinyint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte>(
                name: "Period",
                table: "SessionDelays",
                type: "tinyint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<byte>(
                name: "Period",
                table: "SchoolTimetableEntries",
                type: "tinyint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<byte>(
                name: "CurrentPeriod",
                table: "GatePasses",
                type: "tinyint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TeacherOfficeHours_Day",
                table: "TeacherOfficeHours",
                sql: "[Day] BETWEEN 1 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TeacherOfficeHours_Period",
                table: "TeacherOfficeHours",
                sql: "[Period] IS NULL OR [Period] BETWEEN 1 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SessionDelays_Period",
                table: "SessionDelays",
                sql: "[Period] BETWEEN 1 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchoolTimetableEntries_Day",
                table: "SchoolTimetableEntries",
                sql: "[Day] BETWEEN 1 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchoolTimetableEntries_Period",
                table: "SchoolTimetableEntries",
                sql: "[Period] BETWEEN 1 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GatePasses_Period",
                table: "GatePasses",
                sql: "[CurrentPeriod] IS NULL OR [CurrentPeriod] BETWEEN 1 AND 8");
            AddOfficeHourTimeShape(migrationBuilder);
        }

        private static void AddOfficeHourTimeShape(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AddCheckConstraint("CK_TeacherOfficeHours_TimeShape", "TeacherOfficeHours",
                "([Period] IS NOT NULL AND [LocalStartTime] IS NULL AND [LocalEndTime] IS NULL) OR " +
                "([Period] IS NULL AND [LocalStartTime] IS NOT NULL AND [LocalEndTime] IS NOT NULL AND [LocalEndTime] > [LocalStartTime])");
    }
}
