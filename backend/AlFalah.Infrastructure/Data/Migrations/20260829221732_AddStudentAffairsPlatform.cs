using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentAffairsPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Schools_SchoolId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolTimetableEntries_InstructorProfiles_InstructorProfileId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolTimetableEntries_SchoolTimetables_SchoolTimetableId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetableEntries_InstructorProfileId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_SchoolId",
                table: "Notifications");

            migrationBuilder.AddColumn<int>(
                name: "ClassroomId",
                table: "SchoolTimetableEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "SchoolTimetableEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE entry
                SET entry.SchoolId = timetable.SchoolId
                FROM SchoolTimetableEntries AS entry
                INNER JOIN SchoolTimetables AS timetable
                    ON timetable.Id = entry.SchoolTimetableId;
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "Notifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserId",
                table: "Notifications",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Notifications",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeduplicationKey",
                table: "Notifications",
                type: "varchar(500)",
                unicode: false,
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Notifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "Notifications",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAt",
                table: "Notifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryStatus",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FailedAt",
                table: "Notifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Notifications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                collation: "Arabic_CI_AS");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresApproval",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "Notifications",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Notifications",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByUserId",
                table: "Notifications",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SchoolTimetables_SchoolId_Id",
                table: "SchoolTimetables",
                columns: new[] { "SchoolId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SchoolTimetableEntries_SchoolId_Id",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_InstructorProfiles_SchoolId_Id",
                table: "InstructorProfiles",
                columns: new[] { "SchoolId", "Id" });

            migrationBuilder.CreateTable(
                name: "AcademicTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Semester = table.Column<int>(type: "int", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AcademicTerms", x => x.Id);
                    table.UniqueConstraint("AK_AcademicTerms_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_AcademicTerms_DateRange", "[EndsOn] >= [StartsOn]");
                    table.CheckConstraint("CK_AcademicTerms_Semester", "[Semester] BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_AcademicTerms_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicTerms_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicTerms_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicTerms_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicTerms_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Classrooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    GradeLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    Section = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Arabic_CI_AS"),
                    ClassLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Arabic_CI_AS"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_Classrooms", x => x.Id);
                    table.UniqueConstraint("AK_Classrooms_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_Classrooms_Stage", "[Stage] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_Classrooms_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Classrooms_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Classrooms_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Classrooms_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Classrooms_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GuardianProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    NationalId = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PreferredContactLanguage = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_GuardianProfiles", x => x.Id);
                    table.UniqueConstraint("AK_GuardianProfiles_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_GuardianProfiles_PreferredContactLanguage", "[PreferredContactLanguage] IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "FK_GuardianProfiles_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianProfiles_Users_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianProfiles_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianProfiles_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianProfiles_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageType = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProcessingError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboxMessages_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.CheckConstraint("CK_OutboxMessages_Attempts", "[AttemptCount] >= 0");
                    table.ForeignKey(
                        name: "FK_OutboxMessages_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchoolStudentAffairsSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MorningDelayThresholdPerTerm = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    BehaviorIncidentMultiplePerTerm = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    AcademicConcernThresholdPerTerm = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                    ClassroomEntryPermitThresholdPerTerm = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    AbsenceVisualAlertThresholdPerTerm = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                    AbsenceReferralThresholdPerTerm = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    AbsenceChildRightsThresholdPerTerm = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    BehaviorCountabilityPolicy = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    ArrivalCutoffLocalTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    ArrivalGraceMinutes = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
                    table.PrimaryKey("PK_SchoolStudentAffairsSettings", x => x.Id);
                    table.UniqueConstraint("AK_SchoolStudentAffairsSettings_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_StudentAffairsSettings_AbsenceOrder", "[AbsenceVisualAlertThresholdPerTerm] < [AbsenceReferralThresholdPerTerm] AND [AbsenceReferralThresholdPerTerm] < [AbsenceChildRightsThresholdPerTerm]");
                    table.CheckConstraint("CK_StudentAffairsSettings_ArrivalGrace", "[ArrivalGraceMinutes] >= 0");
                    table.CheckConstraint("CK_StudentAffairsSettings_Thresholds", "[MorningDelayThresholdPerTerm] > 0 AND [BehaviorIncidentMultiplePerTerm] > 0 AND [AcademicConcernThresholdPerTerm] > 0 AND [ClassroomEntryPermitThresholdPerTerm] > 0 AND [AbsenceVisualAlertThresholdPerTerm] > 0 AND [AbsenceReferralThresholdPerTerm] > 0 AND [AbsenceChildRightsThresholdPerTerm] > 0");
                    table.CheckConstraint("CK_StudentAffairsSettings_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_SchoolStudentAffairsSettings_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolStudentAffairsSettings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolStudentAffairsSettings_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolStudentAffairsSettings_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NationalId = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Arabic_CI_AS"),
                    MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, collation: "Arabic_CI_AS"),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Arabic_CI_AS"),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ProfilePhotoStorageKey = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.UniqueConstraint("AK_Students_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_Students_Gender", "[Gender] BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_Students_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Students_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Students_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Students_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherOfficeHours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    InstructorProfileId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<byte>(type: "tinyint", nullable: true),
                    LocalStartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    LocalEndTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TeacherOfficeHours", x => x.Id);
                    table.UniqueConstraint("AK_TeacherOfficeHours_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_TeacherOfficeHours_Day", "[Day] BETWEEN 1 AND 6");
                    table.CheckConstraint("CK_TeacherOfficeHours_EffectiveDates", "[EffectiveUntil] IS NULL OR [EffectiveUntil] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_TeacherOfficeHours_Period", "[Period] IS NULL OR [Period] BETWEEN 1 AND 8");
                    table.CheckConstraint("CK_TeacherOfficeHours_Source", "[Source] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_TeacherOfficeHours_TimeShape", "([Period] IS NOT NULL AND [LocalStartTime] IS NULL AND [LocalEndTime] IS NULL) OR ([Period] IS NULL AND [LocalStartTime] IS NOT NULL AND [LocalEndTime] IS NOT NULL AND [LocalEndTime] > [LocalStartTime])");
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHours_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHours_InstructorProfiles_SchoolId_InstructorProfileId",
                        columns: x => new { x.SchoolId, x.InstructorProfileId },
                        principalTable: "InstructorProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHours_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHours_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHours_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherOfficeHours_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutomationRuleDefinitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    SchoolStudentAffairsSettingsId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    MetricCode = table.Column<int>(type: "int", nullable: false),
                    Threshold = table.Column<int>(type: "int", nullable: false),
                    RepeatsAtMultiples = table.Column<bool>(type: "bit", nullable: false),
                    PolicySnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompiledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompiledByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRuleDefinitions", x => x.Id);
                    table.UniqueConstraint("AK_AutomationRuleDefinitions_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_AutomationRuleDefinitions_MetricCode", "[MetricCode] BETWEEN 1 AND 6");
                    table.CheckConstraint("CK_AutomationRuleDefinitions_Threshold", "[Threshold] > 0 AND [Version] > 0");
                    table.ForeignKey(
                        name: "FK_AutomationRuleDefinitions_SchoolStudentAffairsSettings_SchoolId_SchoolStudentAffairsSettingsId",
                        columns: x => new { x.SchoolId, x.SchoolStudentAffairsSettingsId },
                        principalTable: "SchoolStudentAffairsSettings",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationRuleDefinitions_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationRuleDefinitions_Users_CompiledByUserId",
                        column: x => x.CompiledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AcademicConcerns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    ClassroomId = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, collation: "Arabic_CI_AS"),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReportedByInstructorProfileId = table.Column<int>(type: "int", nullable: false),
                    SchoolTimetableEntryId = table.Column<int>(type: "int", nullable: true),
                    GuardianDispatchDecision = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AcademicConcerns", x => x.Id);
                    table.UniqueConstraint("AK_AcademicConcerns_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_AcademicConcerns_GuardianDispatchDecision", "[GuardianDispatchDecision] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_AcademicConcerns_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicConcerns_Classrooms_SchoolId_ClassroomId",
                        columns: x => new { x.SchoolId, x.ClassroomId },
                        principalTable: "Classrooms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicConcerns_InstructorProfiles_SchoolId_ReportedByInstructorProfileId",
                        columns: x => new { x.SchoolId, x.ReportedByInstructorProfileId },
                        principalTable: "InstructorProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicConcerns_SchoolTimetableEntries_SchoolId_SchoolTimetableEntryId",
                        columns: x => new { x.SchoolId, x.SchoolTimetableEntryId },
                        principalTable: "SchoolTimetableEntries",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicConcerns_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicConcerns_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicConcerns_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicConcerns_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicConcerns_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BehaviorIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    ClassroomId = table.Column<int>(type: "int", nullable: true),
                    CategoryCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, collation: "Arabic_CI_AS"),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true, collation: "Arabic_CI_AS"),
                    ReportedByInstructorProfileId = table.Column<int>(type: "int", nullable: true),
                    ReportedByStaffUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ImmediateActionTaken = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, collation: "Arabic_CI_AS"),
                    GuardianDispatchDecision = table.Column<int>(type: "int", nullable: false),
                    IsUpheld = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_BehaviorIncidents", x => x.Id);
                    table.UniqueConstraint("AK_BehaviorIncidents_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_BehaviorIncidents_GuardianDispatchDecision", "[GuardianDispatchDecision] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_BehaviorIncidents_Reporter", "([ReportedByInstructorProfileId] IS NOT NULL AND [ReportedByStaffUserId] IS NULL) OR ([ReportedByInstructorProfileId] IS NULL AND [ReportedByStaffUserId] IS NOT NULL)");
                    table.CheckConstraint("CK_BehaviorIncidents_Severity", "[Severity] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_BehaviorIncidents_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BehaviorIncidents_Classrooms_SchoolId_ClassroomId",
                        columns: x => new { x.SchoolId, x.ClassroomId },
                        principalTable: "Classrooms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BehaviorIncidents_InstructorProfiles_SchoolId_ReportedByInstructorProfileId",
                        columns: x => new { x.SchoolId, x.ReportedByInstructorProfileId },
                        principalTable: "InstructorProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BehaviorIncidents_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BehaviorIncidents_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BehaviorIncidents_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BehaviorIncidents_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BehaviorIncidents_Users_ReportedByStaffUserId",
                        column: x => x.ReportedByStaffUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BehaviorIncidents_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassroomEntryPermits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    ClassroomId = table.Column<int>(type: "int", nullable: false),
                    IssuedByStudentAffairsUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, collation: "Arabic_CI_AS"),
                    ValidFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SchoolTimetableId = table.Column<int>(type: "int", nullable: true),
                    SchoolTimetableEntryId = table.Column<int>(type: "int", nullable: true),
                    TargetInstructorProfileId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AcknowledgedByTeacherUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, collation: "Arabic_CI_AS"),
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
                    table.PrimaryKey("PK_ClassroomEntryPermits", x => x.Id);
                    table.UniqueConstraint("AK_ClassroomEntryPermits_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_ClassroomEntryPermits_Status", "[Status] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_ClassroomEntryPermits_Validity", "[ValidUntil] > [ValidFrom]");
                    table.ForeignKey(
                        name: "FK_ClassroomEntryPermits_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomEntryPermits_Classrooms_SchoolId_ClassroomId",
                        columns: x => new { x.SchoolId, x.ClassroomId },
                        principalTable: "Classrooms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomEntryPermits_InstructorProfiles_SchoolId_TargetInstructorProfileId",
                        columns: x => new { x.SchoolId, x.TargetInstructorProfileId },
                        principalTable: "InstructorProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomEntryPermits_SchoolTimetableEntries_SchoolId_SchoolTimetableEntryId",
                        columns: x => new { x.SchoolId, x.SchoolTimetableEntryId },
                        principalTable: "SchoolTimetableEntries",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomEntryPermits_SchoolTimetables_SchoolId_SchoolTimetableId",
                        columns: x => new { x.SchoolId, x.SchoolTimetableId },
                        principalTable: "SchoolTimetables",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomEntryPermits_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomEntryPermits_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomEntryPermits_Users_AcknowledgedByTeacherUserId",
                        column: x => x.AcknowledgedByTeacherUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomEntryPermits_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomEntryPermits_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomEntryPermits_Users_IssuedByStudentAffairsUserId",
                        column: x => x.IssuedByStudentAffairsUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomEntryPermits_Users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomEntryPermits_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationThreads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: true),
                    ThreadType = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false, collation: "Arabic_CI_AS"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationThreads", x => x.Id);
                    table.UniqueConstraint("AK_ConversationThreads_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_ConversationThreads_Status", "[Status] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_ConversationThreads_ThreadType", "[ThreadType] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_ConversationThreads_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationThreads_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationThreads_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationThreads_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationThreads_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyStudentAttendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    ClassroomId = table.Column<int>(type: "int", nullable: false),
                    AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ArrivedAfterAttendanceRecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, collation: "Arabic_CI_AS"),
                    CorrectedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CorrectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_DailyStudentAttendances", x => x.Id);
                    table.UniqueConstraint("AK_DailyStudentAttendances_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_DailyStudentAttendances_Source", "[Source] BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_DailyStudentAttendances_Status", "[Status] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_DailyStudentAttendances_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyStudentAttendances_Classrooms_SchoolId_ClassroomId",
                        columns: x => new { x.SchoolId, x.ClassroomId },
                        principalTable: "Classrooms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyStudentAttendances_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyStudentAttendances_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyStudentAttendances_Users_CorrectedByUserId",
                        column: x => x.CorrectedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyStudentAttendances_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyStudentAttendances_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyStudentAttendances_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyStudentAttendances_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GatePasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    RequestedByGuardianProfileId = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestedExitAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, collation: "Arabic_CI_AS"),
                    PickupPersonName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Arabic_CI_AS"),
                    PickupRelationship = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, collation: "Arabic_CI_AS"),
                    PickupIdentityHint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, collation: "Arabic_CI_AS"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovalNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, collation: "Arabic_CI_AS"),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, collation: "Arabic_CI_AS"),
                    SchoolTimetableId = table.Column<int>(type: "int", nullable: true),
                    SchoolTimetableEntryId = table.Column<int>(type: "int", nullable: true),
                    CurrentInstructorProfileId = table.Column<int>(type: "int", nullable: true),
                    CurrentPeriod = table.Column<byte>(type: "tinyint", nullable: true),
                    SecurityAcknowledgedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SecurityAcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PickupVerificationMethod = table.Column<int>(type: "int", nullable: true),
                    PickupVerificationNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, collation: "Arabic_CI_AS"),
                    ExitedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExitRecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, collation: "Arabic_CI_AS"),
                    ExpiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_GatePasses", x => x.Id);
                    table.UniqueConstraint("AK_GatePasses_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_GatePasses_Exit", "[ExitedAt] IS NULL OR [ExitRecordedByUserId] IS NOT NULL");
                    table.CheckConstraint("CK_GatePasses_Period", "[CurrentPeriod] IS NULL OR [CurrentPeriod] BETWEEN 1 AND 8");
                    table.CheckConstraint("CK_GatePasses_PickupVerificationMethod", "[PickupVerificationMethod] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_GatePasses_Review", "([Status] = 1 AND [ReviewedAt] IS NULL) OR ([Status] <> 1)");
                    table.CheckConstraint("CK_GatePasses_Status", "[Status] BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_GatePasses_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_GuardianProfiles_SchoolId_RequestedByGuardianProfileId",
                        columns: x => new { x.SchoolId, x.RequestedByGuardianProfileId },
                        principalTable: "GuardianProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_InstructorProfiles_SchoolId_CurrentInstructorProfileId",
                        columns: x => new { x.SchoolId, x.CurrentInstructorProfileId },
                        principalTable: "InstructorProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_SchoolTimetableEntries_SchoolId_SchoolTimetableEntryId",
                        columns: x => new { x.SchoolId, x.SchoolTimetableEntryId },
                        principalTable: "SchoolTimetableEntries",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_SchoolTimetables_SchoolId_SchoolTimetableId",
                        columns: x => new { x.SchoolId, x.SchoolTimetableId },
                        principalTable: "SchoolTimetables",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_Users_ExitRecordedByUserId",
                        column: x => x.ExitRecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_Users_SecurityAcknowledgedByUserId",
                        column: x => x.SecurityAcknowledgedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MorningArrivalDelays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    ArrivalAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SchoolLocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CutoffTimeSnapshot = table.Column<TimeOnly>(type: "time", nullable: false),
                    DelayMinutes = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, collation: "Arabic_CI_AS"),
                    ReasonProvidedByGuardianAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NotificationPolicySnapshot = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_MorningArrivalDelays", x => x.Id);
                    table.UniqueConstraint("AK_MorningArrivalDelays_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_MorningArrivalDelays_DelayMinutes", "[DelayMinutes] >= 0");
                    table.ForeignKey(
                        name: "FK_MorningArrivalDelays_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MorningArrivalDelays_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MorningArrivalDelays_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MorningArrivalDelays_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MorningArrivalDelays_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MorningArrivalDelays_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionDelays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    ClassroomId = table.Column<int>(type: "int", nullable: false),
                    SchoolTimetableId = table.Column<int>(type: "int", nullable: true),
                    SchoolTimetableEntryId = table.Column<int>(type: "int", nullable: true),
                    Period = table.Column<byte>(type: "tinyint", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DelayMinutes = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, collation: "Arabic_CI_AS"),
                    ReportedByInstructorProfileId = table.Column<int>(type: "int", nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuardianNotificationStatus = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_SessionDelays", x => x.Id);
                    table.UniqueConstraint("AK_SessionDelays_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_SessionDelays_DelayMinutes", "[DelayMinutes] IS NULL OR [DelayMinutes] >= 0");
                    table.CheckConstraint("CK_SessionDelays_GuardianNotificationStatus", "[GuardianNotificationStatus] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_SessionDelays_Period", "[Period] BETWEEN 1 AND 8");
                    table.ForeignKey(
                        name: "FK_SessionDelays_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionDelays_Classrooms_SchoolId_ClassroomId",
                        columns: x => new { x.SchoolId, x.ClassroomId },
                        principalTable: "Classrooms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionDelays_InstructorProfiles_SchoolId_ReportedByInstructorProfileId",
                        columns: x => new { x.SchoolId, x.ReportedByInstructorProfileId },
                        principalTable: "InstructorProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionDelays_SchoolTimetableEntries_SchoolId_SchoolTimetableEntryId",
                        columns: x => new { x.SchoolId, x.SchoolTimetableEntryId },
                        principalTable: "SchoolTimetableEntries",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionDelays_SchoolTimetables_SchoolId_SchoolTimetableId",
                        columns: x => new { x.SchoolId, x.SchoolTimetableId },
                        principalTable: "SchoolTimetables",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionDelays_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionDelays_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionDelays_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionDelays_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionDelays_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentEnrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ClassroomId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    RollNumber = table.Column<int>(type: "int", nullable: true),
                    EnrolledOn = table.Column<DateOnly>(type: "date", nullable: false),
                    WithdrawnOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_StudentEnrollments", x => x.Id);
                    table.UniqueConstraint("AK_StudentEnrollments_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_StudentEnrollments_Dates", "[WithdrawnOn] IS NULL OR [WithdrawnOn] >= [EnrolledOn]");
                    table.CheckConstraint("CK_StudentEnrollments_Status", "[Status] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_Classrooms_SchoolId_ClassroomId",
                        columns: x => new { x.SchoolId, x.ClassroomId },
                        principalTable: "Classrooms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentGuardians",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    GuardianProfileId = table.Column<int>(type: "int", nullable: false),
                    RelationshipType = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    ReceivesNotifications = table.Column<bool>(type: "bit", nullable: false),
                    CanSubmitExcuses = table.Column<bool>(type: "bit", nullable: false),
                    CanRequestGatePass = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_StudentGuardians", x => x.Id);
                    table.UniqueConstraint("AK_StudentGuardians_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_StudentGuardians_RelationshipType", "[RelationshipType] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_StudentGuardians_Validity", "[ValidTo] IS NULL OR [ValidTo] >= [ValidFrom]");
                    table.ForeignKey(
                        name: "FK_StudentGuardians_GuardianProfiles_SchoolId_GuardianProfileId",
                        columns: x => new { x.SchoolId, x.GuardianProfileId },
                        principalTable: "GuardianProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentGuardians_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentGuardians_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentGuardians_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentGuardians_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentGuardians_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentRecognitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    ClassroomId = table.Column<int>(type: "int", nullable: true),
                    RecognitionType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false, collation: "Arabic_CI_AS"),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, collation: "Arabic_CI_AS"),
                    RecognizedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReportedByInstructorProfileId = table.Column<int>(type: "int", nullable: false),
                    GuardianNotificationStatus = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_StudentRecognitions", x => x.Id);
                    table.UniqueConstraint("AK_StudentRecognitions_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_StudentRecognitions_GuardianNotificationStatus", "[GuardianNotificationStatus] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_StudentRecognitions_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentRecognitions_Classrooms_SchoolId_ClassroomId",
                        columns: x => new { x.SchoolId, x.ClassroomId },
                        principalTable: "Classrooms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentRecognitions_InstructorProfiles_SchoolId_ReportedByInstructorProfileId",
                        columns: x => new { x.SchoolId, x.ReportedByInstructorProfileId },
                        principalTable: "InstructorProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentRecognitions_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentRecognitions_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentRecognitions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentRecognitions_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentRecognitions_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentTermMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    MetricCode = table.Column<int>(type: "int", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    RecalculatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
                    table.PrimaryKey("PK_StudentTermMetrics", x => x.Id);
                    table.UniqueConstraint("AK_StudentTermMetrics_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_StudentTermMetrics_Count", "[Count] >= 0");
                    table.CheckConstraint("CK_StudentTermMetrics_MetricCode", "[MetricCode] BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_StudentTermMetrics_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentTermMetrics_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentTermMetrics_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentTermMetrics_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentTermMetrics_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentTermMetrics_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutomationTriggerLedgers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    RuleVersionId = table.Column<long>(type: "bigint", nullable: false),
                    Threshold = table.Column<int>(type: "int", nullable: false),
                    OccurrenceNumber = table.Column<int>(type: "int", nullable: false),
                    CountSnapshot = table.Column<int>(type: "int", nullable: false),
                    Validity = table.Column<int>(type: "int", nullable: false),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SourceInvalidatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, collation: "Arabic_CI_AS"),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationTriggerLedgers", x => x.Id);
                    table.UniqueConstraint("AK_AutomationTriggerLedgers_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_AutomationTriggerLedgers_Count", "[CountSnapshot] >= 0");
                    table.CheckConstraint("CK_AutomationTriggerLedgers_Occurrence", "[OccurrenceNumber] > 0");
                    table.CheckConstraint("CK_AutomationTriggerLedgers_Threshold", "[Threshold] > 0");
                    table.CheckConstraint("CK_AutomationTriggerLedgers_Validity", "[Validity] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_AutomationTriggerLedgers_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationTriggerLedgers_AutomationRuleDefinitions_SchoolId_RuleVersionId",
                        columns: x => new { x.SchoolId, x.RuleVersionId },
                        principalTable: "AutomationRuleDefinitions",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationTriggerLedgers_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationTriggerLedgers_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ConversationThreadId = table.Column<int>(type: "int", nullable: false),
                    SenderUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false, collation: "Arabic_CI_AS"),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    QueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OfficeHoursDisposition = table.Column<int>(type: "int", nullable: false),
                    ReplyToMessageId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                    table.UniqueConstraint("AK_ConversationMessages_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_ConversationMessages_OfficeHoursDisposition", "[OfficeHoursDisposition] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_ConversationMessages_ConversationMessages_SchoolId_ReplyToMessageId",
                        columns: x => new { x.SchoolId, x.ReplyToMessageId },
                        principalTable: "ConversationMessages",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_ConversationThreads_SchoolId_ConversationThreadId",
                        columns: x => new { x.SchoolId, x.ConversationThreadId },
                        principalTable: "ConversationThreads",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ConversationThreadId = table.Column<int>(type: "int", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ParticipantRoleSnapshot = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeftAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_ConversationParticipants", x => x.Id);
                    table.UniqueConstraint("AK_ConversationParticipants_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_ConversationThreads_SchoolId_ConversationThreadId",
                        columns: x => new { x.SchoolId, x.ConversationThreadId },
                        principalTable: "ConversationThreads",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_Users_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AbsenceExcuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DailyStudentAttendanceId = table.Column<int>(type: "int", nullable: false),
                    GuardianProfileId = table.Column<int>(type: "int", nullable: false),
                    ExcuseType = table.Column<int>(type: "int", nullable: false),
                    GuardianNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, collation: "Arabic_CI_AS"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, collation: "Arabic_CI_AS"),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
                    table.PrimaryKey("PK_AbsenceExcuses", x => x.Id);
                    table.UniqueConstraint("AK_AbsenceExcuses_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_AbsenceExcuses_ExcuseType", "[ExcuseType] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_AbsenceExcuses_Review", "([Status] = 1 AND [ReviewedAt] IS NULL AND [ReviewedByUserId] IS NULL) OR ([Status] IN (2, 3) AND [ReviewedAt] IS NOT NULL AND [ReviewedByUserId] IS NOT NULL)");
                    table.CheckConstraint("CK_AbsenceExcuses_Status", "[Status] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_AbsenceExcuses_DailyStudentAttendances_SchoolId_DailyStudentAttendanceId",
                        columns: x => new { x.SchoolId, x.DailyStudentAttendanceId },
                        principalTable: "DailyStudentAttendances",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceExcuses_GuardianProfiles_SchoolId_GuardianProfileId",
                        columns: x => new { x.SchoolId, x.GuardianProfileId },
                        principalTable: "GuardianProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceExcuses_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceExcuses_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceExcuses_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceExcuses_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceExcuses_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GatePassTransitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    GatePassId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActorRole = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, collation: "Arabic_CI_AS"),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PickupVerificationMethod = table.Column<int>(type: "int", nullable: true),
                    PickupVerificationNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, collation: "Arabic_CI_AS")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatePassTransitions", x => x.Id);
                    table.CheckConstraint("CK_GatePassTransitions_FromStatus", "[FromStatus] BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_GatePassTransitions_PickupVerificationMethod", "[PickupVerificationMethod] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_GatePassTransitions_ToStatus", "[ToStatus] BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_GatePassTransitions_GatePasses_SchoolId_GatePassId",
                        columns: x => new { x.SchoolId, x.GatePassId },
                        principalTable: "GatePasses",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePassTransitions_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePassTransitions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentReferrals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceEntityId = table.Column<int>(type: "int", nullable: true),
                    RuleTriggerId = table.Column<long>(type: "bigint", nullable: true),
                    CountSnapshot = table.Column<int>(type: "int", nullable: true),
                    ThresholdSnapshot = table.Column<int>(type: "int", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AssignedSocialWorkerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RecommendedActions = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true, collation: "Arabic_CI_AS"),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true, collation: "Arabic_CI_AS"),
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
                    table.PrimaryKey("PK_StudentReferrals", x => x.Id);
                    table.UniqueConstraint("AK_StudentReferrals_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_StudentReferrals_Priority", "[Priority] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_StudentReferrals_Snapshots", "[CountSnapshot] IS NULL OR [CountSnapshot] >= 0");
                    table.CheckConstraint("CK_StudentReferrals_SourceType", "[SourceType] BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_StudentReferrals_Status", "[Status] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_StudentReferrals_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentReferrals_AutomationTriggerLedgers_SchoolId_RuleTriggerId",
                        columns: x => new { x.SchoolId, x.RuleTriggerId },
                        principalTable: "AutomationTriggerLedgers",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentReferrals_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentReferrals_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentReferrals_Users_AssignedSocialWorkerUserId",
                        column: x => x.AssignedSocialWorkerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentReferrals_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentReferrals_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentReferrals_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessageReceipts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ConversationMessageId = table.Column<int>(type: "int", nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DeliveryState = table.Column<int>(type: "int", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, collation: "Arabic_CI_AS"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageReceipts", x => x.Id);
                    table.CheckConstraint("CK_MessageReceipts_DeliveryState", "[DeliveryState] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_MessageReceipts_ConversationMessages_SchoolId_ConversationMessageId",
                        columns: x => new { x.SchoolId, x.ConversationMessageId },
                        principalTable: "ConversationMessages",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageReceipts_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageReceipts_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AbsenceExcuseAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AbsenceExcuseId = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Arabic_CI_AS"),
                    ContentType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    StorageProvider = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
                    table.PrimaryKey("PK_AbsenceExcuseAttachments", x => x.Id);
                    table.UniqueConstraint("AK_AbsenceExcuseAttachments_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_AbsenceExcuseAttachments_Size", "[SizeBytes] > 0");
                    table.ForeignKey(
                        name: "FK_AbsenceExcuseAttachments_AbsenceExcuses_SchoolId_AbsenceExcuseId",
                        columns: x => new { x.SchoolId, x.AbsenceExcuseId },
                        principalTable: "AbsenceExcuses",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceExcuseAttachments_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceExcuseAttachments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceExcuseAttachments_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceExcuseAttachments_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceExcuseAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GuardianSummons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    StudentReferralId = table.Column<int>(type: "int", nullable: true),
                    CreatedReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, collation: "Arabic_CI_AS"),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    SourceCountSnapshot = table.Column<int>(type: "int", nullable: true),
                    ThresholdSnapshot = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScheduledBySocialWorkerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true, collation: "Arabic_CI_AS"),
                    Instructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, collation: "Arabic_CI_AS"),
                    GuardianProfileId = table.Column<int>(type: "int", nullable: false),
                    GuardianNotifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RequiresOfficerReview = table.Column<bool>(type: "bit", nullable: false),
                    OfficerReviewReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, collation: "Arabic_CI_AS"),
                    OfficerReviewFlaggedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OfficerReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OfficerReviewDecision = table.Column<int>(type: "int", nullable: true),
                    AttendedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AttendanceNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, collation: "Arabic_CI_AS"),
                    ObservationStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ObservationNotes = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true, collation: "Arabic_CI_AS"),
                    ImprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ImprovementNotes = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true, collation: "Arabic_CI_AS"),
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
                    table.PrimaryKey("PK_GuardianSummons", x => x.Id);
                    table.UniqueConstraint("AK_GuardianSummons_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_GuardianSummons_OfficerReviewDecision", "[OfficerReviewDecision] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_GuardianSummons_Priority", "[Priority] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_GuardianSummons_Status", "[Status] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_GuardianSummons_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianSummons_GuardianProfiles_SchoolId_GuardianProfileId",
                        columns: x => new { x.SchoolId, x.GuardianProfileId },
                        principalTable: "GuardianProfiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianSummons_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianSummons_StudentReferrals_SchoolId_StudentReferralId",
                        columns: x => new { x.SchoolId, x.StudentReferralId },
                        principalTable: "StudentReferrals",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianSummons_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianSummons_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianSummons_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianSummons_Users_ScheduledBySocialWorkerUserId",
                        column: x => x.ScheduledBySocialWorkerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianSummons_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentCaseActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentReferralId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false, collation: "Arabic_CI_AS"),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActionAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true, collation: "Arabic_CI_AS"),
                    AttachmentStorageKey = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_StudentCaseActions", x => x.Id);
                    table.UniqueConstraint("AK_StudentCaseActions_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_StudentCaseActions_ActionType", "[ActionType] BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_StudentCaseActions_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCaseActions_StudentReferrals_SchoolId_StudentReferralId",
                        columns: x => new { x.SchoolId, x.StudentReferralId },
                        principalTable: "StudentReferrals",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCaseActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCaseActions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCaseActions_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCaseActions_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GuardianSummonStatusHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    GuardianSummonId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, collation: "Arabic_CI_AS"),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuardianSummonStatusHistory", x => x.Id);
                    table.CheckConstraint("CK_GuardianSummonStatusHistory_FromStatus", "[FromStatus] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_GuardianSummonStatusHistory_ToStatus", "[ToStatus] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_GuardianSummonStatusHistory_GuardianSummons_SchoolId_GuardianSummonId",
                        columns: x => new { x.SchoolId, x.GuardianSummonId },
                        principalTable: "GuardianSummons",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianSummonStatusHistory_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianSummonStatusHistory_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_SchoolId_ClassroomId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolId", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_SchoolId_InstructorProfileId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolId", "InstructorProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_SchoolId_SchoolTimetableId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolId", "SchoolTimetableId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ApprovedByUserId",
                table: "Notifications",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedByUserId",
                table: "Notifications",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DeletedByUserId",
                table: "Notifications",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SchoolId_UserId_DeduplicationKey",
                table: "Notifications",
                columns: new[] { "SchoolId", "UserId", "DeduplicationKey" },
                unique: true,
                filter: "[DeduplicationKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SchoolId_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "SchoolId", "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UpdatedByUserId",
                table: "Notifications",
                column: "UpdatedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_DeliveryStatus",
                table: "Notifications",
                sql: "[DeliveryStatus] BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_Priority",
                table: "Notifications",
                sql: "[Priority] BETWEEN 1 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_RetryCount",
                table: "Notifications",
                sql: "[RetryCount] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceExcuseAttachments_CreatedByUserId",
                table: "AbsenceExcuseAttachments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceExcuseAttachments_DeletedByUserId",
                table: "AbsenceExcuseAttachments",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceExcuseAttachments_SchoolId_AbsenceExcuseId",
                table: "AbsenceExcuseAttachments",
                columns: new[] { "SchoolId", "AbsenceExcuseId" });

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceExcuseAttachments_UpdatedByUserId",
                table: "AbsenceExcuseAttachments",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceExcuseAttachments_UploadedByUserId",
                table: "AbsenceExcuseAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceExcuses_CreatedByUserId",
                table: "AbsenceExcuses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceExcuses_DeletedByUserId",
                table: "AbsenceExcuses",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceExcuses_ReviewedByUserId",
                table: "AbsenceExcuses",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceExcuses_SchoolId_DailyStudentAttendanceId_Status",
                table: "AbsenceExcuses",
                columns: new[] { "SchoolId", "DailyStudentAttendanceId", "Status" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceExcuses_SchoolId_GuardianProfileId",
                table: "AbsenceExcuses",
                columns: new[] { "SchoolId", "GuardianProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceExcuses_UpdatedByUserId",
                table: "AbsenceExcuses",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicConcerns_CreatedByUserId",
                table: "AcademicConcerns",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicConcerns_DeletedByUserId",
                table: "AcademicConcerns",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicConcerns_SchoolId_AcademicTermId",
                table: "AcademicConcerns",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicConcerns_SchoolId_ClassroomId_OccurredAt",
                table: "AcademicConcerns",
                columns: new[] { "SchoolId", "ClassroomId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicConcerns_SchoolId_ReportedByInstructorProfileId",
                table: "AcademicConcerns",
                columns: new[] { "SchoolId", "ReportedByInstructorProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicConcerns_SchoolId_SchoolTimetableEntryId",
                table: "AcademicConcerns",
                columns: new[] { "SchoolId", "SchoolTimetableEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicConcerns_SchoolId_StudentId_AcademicTermId_OccurredAt",
                table: "AcademicConcerns",
                columns: new[] { "SchoolId", "StudentId", "AcademicTermId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicConcerns_UpdatedByUserId",
                table: "AcademicConcerns",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_AcademicYearId",
                table: "AcademicTerms",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_CreatedByUserId",
                table: "AcademicTerms",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_DeletedByUserId",
                table: "AcademicTerms",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_SchoolId_AcademicYearId_Semester",
                table: "AcademicTerms",
                columns: new[] { "SchoolId", "AcademicYearId", "Semester" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_SchoolId_IsActive",
                table: "AcademicTerms",
                columns: new[] { "SchoolId", "IsActive" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_UpdatedByUserId",
                table: "AcademicTerms",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRuleDefinitions_CompiledByUserId",
                table: "AutomationRuleDefinitions",
                column: "CompiledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRuleDefinitions_SchoolId_SchoolStudentAffairsSettingsId",
                table: "AutomationRuleDefinitions",
                columns: new[] { "SchoolId", "SchoolStudentAffairsSettingsId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRuleDefinitions_SchoolId_Version_MetricCode",
                table: "AutomationRuleDefinitions",
                columns: new[] { "SchoolId", "Version", "MetricCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationTriggerLedgers_CorrelationId",
                table: "AutomationTriggerLedgers",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationTriggerLedgers_SchoolId_AcademicTermId",
                table: "AutomationTriggerLedgers",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationTriggerLedgers_SchoolId_RuleVersionId",
                table: "AutomationTriggerLedgers",
                columns: new[] { "SchoolId", "RuleVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationTriggerLedgers_SchoolId_StudentId_AcademicTermId_RuleVersionId_Threshold_OccurrenceNumber",
                table: "AutomationTriggerLedgers",
                columns: new[] { "SchoolId", "StudentId", "AcademicTermId", "RuleVersionId", "Threshold", "OccurrenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorIncidents_CreatedByUserId",
                table: "BehaviorIncidents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorIncidents_DeletedByUserId",
                table: "BehaviorIncidents",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorIncidents_ReportedByStaffUserId",
                table: "BehaviorIncidents",
                column: "ReportedByStaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorIncidents_SchoolId_AcademicTermId",
                table: "BehaviorIncidents",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorIncidents_SchoolId_ClassroomId_OccurredAt",
                table: "BehaviorIncidents",
                columns: new[] { "SchoolId", "ClassroomId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorIncidents_SchoolId_ReportedByInstructorProfileId",
                table: "BehaviorIncidents",
                columns: new[] { "SchoolId", "ReportedByInstructorProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorIncidents_SchoolId_StudentId_AcademicTermId_OccurredAt",
                table: "BehaviorIncidents",
                columns: new[] { "SchoolId", "StudentId", "AcademicTermId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorIncidents_UpdatedByUserId",
                table: "BehaviorIncidents",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomEntryPermits_AcknowledgedByTeacherUserId",
                table: "ClassroomEntryPermits",
                column: "AcknowledgedByTeacherUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomEntryPermits_CreatedByUserId",
                table: "ClassroomEntryPermits",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomEntryPermits_DeletedByUserId",
                table: "ClassroomEntryPermits",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomEntryPermits_IssuedByStudentAffairsUserId",
                table: "ClassroomEntryPermits",
                column: "IssuedByStudentAffairsUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomEntryPermits_RevokedByUserId",
                table: "ClassroomEntryPermits",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomEntryPermits_SchoolId_AcademicTermId",
                table: "ClassroomEntryPermits",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomEntryPermits_SchoolId_ClassroomId",
                table: "ClassroomEntryPermits",
                columns: new[] { "SchoolId", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomEntryPermits_SchoolId_SchoolTimetableEntryId",
                table: "ClassroomEntryPermits",
                columns: new[] { "SchoolId", "SchoolTimetableEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomEntryPermits_SchoolId_SchoolTimetableId",
                table: "ClassroomEntryPermits",
                columns: new[] { "SchoolId", "SchoolTimetableId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomEntryPermits_SchoolId_StudentId_AcademicTermId_IssuedAt",
                table: "ClassroomEntryPermits",
                columns: new[] { "SchoolId", "StudentId", "AcademicTermId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomEntryPermits_SchoolId_TargetInstructorProfileId",
                table: "ClassroomEntryPermits",
                columns: new[] { "SchoolId", "TargetInstructorProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomEntryPermits_UpdatedByUserId",
                table: "ClassroomEntryPermits",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_AcademicYearId",
                table: "Classrooms",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_CreatedByUserId",
                table: "Classrooms",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_DeletedByUserId",
                table: "Classrooms",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_SchoolId_AcademicYearId_ClassLabel",
                table: "Classrooms",
                columns: new[] { "SchoolId", "AcademicYearId", "ClassLabel" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_UpdatedByUserId",
                table: "Classrooms",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_CreatedByUserId",
                table: "ConversationMessages",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_DeletedByUserId",
                table: "ConversationMessages",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_SchoolId_ConversationThreadId_QueuedAt",
                table: "ConversationMessages",
                columns: new[] { "SchoolId", "ConversationThreadId", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_SchoolId_ReplyToMessageId",
                table: "ConversationMessages",
                columns: new[] { "SchoolId", "ReplyToMessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_SenderUserId",
                table: "ConversationMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_UpdatedByUserId",
                table: "ConversationMessages",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_ApplicationUserId",
                table: "ConversationParticipants",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_CreatedByUserId",
                table: "ConversationParticipants",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_DeletedByUserId",
                table: "ConversationParticipants",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_SchoolId_ConversationThreadId_ApplicationUserId",
                table: "ConversationParticipants",
                columns: new[] { "SchoolId", "ConversationThreadId", "ApplicationUserId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [LeftAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_UpdatedByUserId",
                table: "ConversationParticipants",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationThreads_CreatedByUserId",
                table: "ConversationThreads",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationThreads_DeletedByUserId",
                table: "ConversationThreads",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationThreads_SchoolId_StudentId_Status",
                table: "ConversationThreads",
                columns: new[] { "SchoolId", "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationThreads_UpdatedByUserId",
                table: "ConversationThreads",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudentAttendances_CorrectedByUserId",
                table: "DailyStudentAttendances",
                column: "CorrectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudentAttendances_CreatedByUserId",
                table: "DailyStudentAttendances",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudentAttendances_DeletedByUserId",
                table: "DailyStudentAttendances",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudentAttendances_RecordedByUserId",
                table: "DailyStudentAttendances",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudentAttendances_SchoolId_AcademicTermId",
                table: "DailyStudentAttendances",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudentAttendances_SchoolId_AttendanceDate_Status",
                table: "DailyStudentAttendances",
                columns: new[] { "SchoolId", "AttendanceDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudentAttendances_SchoolId_ClassroomId",
                table: "DailyStudentAttendances",
                columns: new[] { "SchoolId", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudentAttendances_SchoolId_StudentId_AcademicTermId",
                table: "DailyStudentAttendances",
                columns: new[] { "SchoolId", "StudentId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudentAttendances_SchoolId_StudentId_AttendanceDate",
                table: "DailyStudentAttendances",
                columns: new[] { "SchoolId", "StudentId", "AttendanceDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudentAttendances_UpdatedByUserId",
                table: "DailyStudentAttendances",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_CancelledByUserId",
                table: "GatePasses",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_CreatedByUserId",
                table: "GatePasses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_DeletedByUserId",
                table: "GatePasses",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_ExitRecordedByUserId",
                table: "GatePasses",
                column: "ExitRecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_ReviewedByUserId",
                table: "GatePasses",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_SchoolId_AcademicTermId",
                table: "GatePasses",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_SchoolId_CurrentInstructorProfileId",
                table: "GatePasses",
                columns: new[] { "SchoolId", "CurrentInstructorProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_SchoolId_RequestedByGuardianProfileId",
                table: "GatePasses",
                columns: new[] { "SchoolId", "RequestedByGuardianProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_SchoolId_SchoolTimetableEntryId",
                table: "GatePasses",
                columns: new[] { "SchoolId", "SchoolTimetableEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_SchoolId_SchoolTimetableId",
                table: "GatePasses",
                columns: new[] { "SchoolId", "SchoolTimetableId" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_SchoolId_Status_RequestedExitAt",
                table: "GatePasses",
                columns: new[] { "SchoolId", "Status", "RequestedExitAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_SchoolId_StudentId",
                table: "GatePasses",
                columns: new[] { "SchoolId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_SecurityAcknowledgedByUserId",
                table: "GatePasses",
                column: "SecurityAcknowledgedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_UpdatedByUserId",
                table: "GatePasses",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePassTransitions_ActorUserId",
                table: "GatePassTransitions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePassTransitions_CorrelationId",
                table: "GatePassTransitions",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePassTransitions_SchoolId_GatePassId_OccurredAt",
                table: "GatePassTransitions",
                columns: new[] { "SchoolId", "GatePassId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianProfiles_ApplicationUserId",
                table: "GuardianProfiles",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianProfiles_CreatedByUserId",
                table: "GuardianProfiles",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianProfiles_DeletedByUserId",
                table: "GuardianProfiles",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianProfiles_SchoolId_ApplicationUserId",
                table: "GuardianProfiles",
                columns: new[] { "SchoolId", "ApplicationUserId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianProfiles_UpdatedByUserId",
                table: "GuardianProfiles",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummons_CreatedByUserId",
                table: "GuardianSummons",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummons_DeletedByUserId",
                table: "GuardianSummons",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummons_ScheduledBySocialWorkerUserId",
                table: "GuardianSummons",
                column: "ScheduledBySocialWorkerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummons_SchoolId_AcademicTermId",
                table: "GuardianSummons",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummons_SchoolId_GuardianProfileId",
                table: "GuardianSummons",
                columns: new[] { "SchoolId", "GuardianProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummons_SchoolId_RequiresOfficerReview",
                table: "GuardianSummons",
                columns: new[] { "SchoolId", "RequiresOfficerReview" });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummons_SchoolId_Status_Priority",
                table: "GuardianSummons",
                columns: new[] { "SchoolId", "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummons_SchoolId_StudentId",
                table: "GuardianSummons",
                columns: new[] { "SchoolId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummons_SchoolId_StudentReferralId",
                table: "GuardianSummons",
                columns: new[] { "SchoolId", "StudentReferralId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummons_UpdatedByUserId",
                table: "GuardianSummons",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummonStatusHistory_ActorUserId",
                table: "GuardianSummonStatusHistory",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummonStatusHistory_CorrelationId",
                table: "GuardianSummonStatusHistory",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianSummonStatusHistory_SchoolId_GuardianSummonId_OccurredAt",
                table: "GuardianSummonStatusHistory",
                columns: new[] { "SchoolId", "GuardianSummonId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ProcessedAt_ReceivedAt",
                table: "InboxMessages",
                columns: new[] { "ProcessedAt", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_SchoolId_MessageId",
                table: "InboxMessages",
                columns: new[] { "SchoolId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageReceipts_RecipientUserId",
                table: "MessageReceipts",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReceipts_SchoolId_ConversationMessageId_RecipientUserId",
                table: "MessageReceipts",
                columns: new[] { "SchoolId", "ConversationMessageId", "RecipientUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageReceipts_SchoolId_RecipientUserId_ReadAt",
                table: "MessageReceipts",
                columns: new[] { "SchoolId", "RecipientUserId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MorningArrivalDelays_CreatedByUserId",
                table: "MorningArrivalDelays",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MorningArrivalDelays_DeletedByUserId",
                table: "MorningArrivalDelays",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MorningArrivalDelays_SchoolId_AcademicTermId",
                table: "MorningArrivalDelays",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_MorningArrivalDelays_SchoolId_StudentId_AcademicTermId_ArrivalAt",
                table: "MorningArrivalDelays",
                columns: new[] { "SchoolId", "StudentId", "AcademicTermId", "ArrivalAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MorningArrivalDelays_SchoolId_StudentId_SchoolLocalDate",
                table: "MorningArrivalDelays",
                columns: new[] { "SchoolId", "StudentId", "SchoolLocalDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MorningArrivalDelays_UpdatedByUserId",
                table: "MorningArrivalDelays",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_EventId",
                table: "OutboxMessages",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_NextAttemptAt",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "NextAttemptAt" },
                filter: "[ProcessedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_SchoolId",
                table: "OutboxMessages",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStudentAffairsSettings_CreatedByUserId",
                table: "SchoolStudentAffairsSettings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStudentAffairsSettings_DeletedByUserId",
                table: "SchoolStudentAffairsSettings",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStudentAffairsSettings_SchoolId",
                table: "SchoolStudentAffairsSettings",
                column: "SchoolId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStudentAffairsSettings_UpdatedByUserId",
                table: "SchoolStudentAffairsSettings",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionDelays_CreatedByUserId",
                table: "SessionDelays",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionDelays_DeletedByUserId",
                table: "SessionDelays",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionDelays_SchoolId_AcademicTermId",
                table: "SessionDelays",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionDelays_SchoolId_ClassroomId_OccurredAt",
                table: "SessionDelays",
                columns: new[] { "SchoolId", "ClassroomId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionDelays_SchoolId_ReportedByInstructorProfileId",
                table: "SessionDelays",
                columns: new[] { "SchoolId", "ReportedByInstructorProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionDelays_SchoolId_SchoolTimetableEntryId",
                table: "SessionDelays",
                columns: new[] { "SchoolId", "SchoolTimetableEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionDelays_SchoolId_SchoolTimetableId",
                table: "SessionDelays",
                columns: new[] { "SchoolId", "SchoolTimetableId" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionDelays_SchoolId_StudentId_AcademicTermId_OccurredAt",
                table: "SessionDelays",
                columns: new[] { "SchoolId", "StudentId", "AcademicTermId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionDelays_UpdatedByUserId",
                table: "SessionDelays",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCaseActions_ActorUserId",
                table: "StudentCaseActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCaseActions_CreatedByUserId",
                table: "StudentCaseActions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCaseActions_DeletedByUserId",
                table: "StudentCaseActions",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCaseActions_SchoolId_StudentReferralId_ActionAt",
                table: "StudentCaseActions",
                columns: new[] { "SchoolId", "StudentReferralId", "ActionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentCaseActions_UpdatedByUserId",
                table: "StudentCaseActions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_CreatedByUserId",
                table: "StudentEnrollments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_DeletedByUserId",
                table: "StudentEnrollments",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_SchoolId_AcademicTermId_ClassroomId_Status",
                table: "StudentEnrollments",
                columns: new[] { "SchoolId", "AcademicTermId", "ClassroomId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_SchoolId_ClassroomId",
                table: "StudentEnrollments",
                columns: new[] { "SchoolId", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_SchoolId_StudentId_AcademicTermId",
                table: "StudentEnrollments",
                columns: new[] { "SchoolId", "StudentId", "AcademicTermId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_UpdatedByUserId",
                table: "StudentEnrollments",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardians_CreatedByUserId",
                table: "StudentGuardians",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardians_DeletedByUserId",
                table: "StudentGuardians",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardians_SchoolId_GuardianProfileId",
                table: "StudentGuardians",
                columns: new[] { "SchoolId", "GuardianProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardians_SchoolId_StudentId_GuardianProfileId",
                table: "StudentGuardians",
                columns: new[] { "SchoolId", "StudentId", "GuardianProfileId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardians_SchoolId_StudentId_IsPrimary",
                table: "StudentGuardians",
                columns: new[] { "SchoolId", "StudentId", "IsPrimary" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsPrimary] = 1 AND [ValidTo] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardians_UpdatedByUserId",
                table: "StudentGuardians",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRecognitions_CreatedByUserId",
                table: "StudentRecognitions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRecognitions_DeletedByUserId",
                table: "StudentRecognitions",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRecognitions_SchoolId_AcademicTermId_RecognizedAt",
                table: "StudentRecognitions",
                columns: new[] { "SchoolId", "AcademicTermId", "RecognizedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentRecognitions_SchoolId_ClassroomId",
                table: "StudentRecognitions",
                columns: new[] { "SchoolId", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentRecognitions_SchoolId_ReportedByInstructorProfileId",
                table: "StudentRecognitions",
                columns: new[] { "SchoolId", "ReportedByInstructorProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentRecognitions_SchoolId_StudentId_RecognizedAt",
                table: "StudentRecognitions",
                columns: new[] { "SchoolId", "StudentId", "RecognizedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentRecognitions_UpdatedByUserId",
                table: "StudentRecognitions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferrals_AssignedSocialWorkerUserId",
                table: "StudentReferrals",
                column: "AssignedSocialWorkerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferrals_CreatedByUserId",
                table: "StudentReferrals",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferrals_DeletedByUserId",
                table: "StudentReferrals",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferrals_SchoolId_AcademicTermId",
                table: "StudentReferrals",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferrals_SchoolId_AssignedSocialWorkerUserId_Status_Priority",
                table: "StudentReferrals",
                columns: new[] { "SchoolId", "AssignedSocialWorkerUserId", "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferrals_SchoolId_RuleTriggerId",
                table: "StudentReferrals",
                columns: new[] { "SchoolId", "RuleTriggerId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferrals_SchoolId_StudentId",
                table: "StudentReferrals",
                columns: new[] { "SchoolId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentReferrals_UpdatedByUserId",
                table: "StudentReferrals",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_CreatedByUserId",
                table: "Students",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_DeletedByUserId",
                table: "Students",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_SchoolId_IsActive_StudentNumber",
                table: "Students",
                columns: new[] { "SchoolId", "IsActive", "StudentNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Students_SchoolId_NationalId",
                table: "Students",
                columns: new[] { "SchoolId", "NationalId" },
                unique: true,
                filter: "[NationalId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Students_SchoolId_StudentNumber",
                table: "Students",
                columns: new[] { "SchoolId", "StudentNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Students_UpdatedByUserId",
                table: "Students",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTermMetrics_CreatedByUserId",
                table: "StudentTermMetrics",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTermMetrics_DeletedByUserId",
                table: "StudentTermMetrics",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTermMetrics_SchoolId_AcademicTermId",
                table: "StudentTermMetrics",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentTermMetrics_SchoolId_StudentId_AcademicTermId_MetricCode",
                table: "StudentTermMetrics",
                columns: new[] { "SchoolId", "StudentId", "AcademicTermId", "MetricCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTermMetrics_UpdatedByUserId",
                table: "StudentTermMetrics",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHours_CreatedByUserId",
                table: "TeacherOfficeHours",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHours_DeletedByUserId",
                table: "TeacherOfficeHours",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHours_SchoolId_AcademicTermId",
                table: "TeacherOfficeHours",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHours_SchoolId_InstructorProfileId_AcademicTermId_Day",
                table: "TeacherOfficeHours",
                columns: new[] { "SchoolId", "InstructorProfileId", "AcademicTermId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherOfficeHours_UpdatedByUserId",
                table: "TeacherOfficeHours",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Schools_SchoolId",
                table: "Notifications",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_ApprovedByUserId",
                table: "Notifications",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_CreatedByUserId",
                table: "Notifications",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_DeletedByUserId",
                table: "Notifications",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UpdatedByUserId",
                table: "Notifications",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolTimetableEntries_Classrooms_SchoolId_ClassroomId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolId", "ClassroomId" },
                principalTable: "Classrooms",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolTimetableEntries_InstructorProfiles_SchoolId_InstructorProfileId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolId", "InstructorProfileId" },
                principalTable: "InstructorProfiles",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolTimetableEntries_SchoolTimetables_SchoolId_SchoolTimetableId",
                table: "SchoolTimetableEntries",
                columns: new[] { "SchoolId", "SchoolTimetableId" },
                principalTable: "SchoolTimetables",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolTimetableEntries_Schools_SchoolId",
                table: "SchoolTimetableEntries",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Schools_SchoolId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_ApprovedByUserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_CreatedByUserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_DeletedByUserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UpdatedByUserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolTimetableEntries_Classrooms_SchoolId_ClassroomId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolTimetableEntries_InstructorProfiles_SchoolId_InstructorProfileId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolTimetableEntries_SchoolTimetables_SchoolId_SchoolTimetableId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolTimetableEntries_Schools_SchoolId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropTable(
                name: "AbsenceExcuseAttachments");

            migrationBuilder.DropTable(
                name: "AcademicConcerns");

            migrationBuilder.DropTable(
                name: "BehaviorIncidents");

            migrationBuilder.DropTable(
                name: "ClassroomEntryPermits");

            migrationBuilder.DropTable(
                name: "ConversationParticipants");

            migrationBuilder.DropTable(
                name: "GatePassTransitions");

            migrationBuilder.DropTable(
                name: "GuardianSummonStatusHistory");

            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropTable(
                name: "MessageReceipts");

            migrationBuilder.DropTable(
                name: "MorningArrivalDelays");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "SessionDelays");

            migrationBuilder.DropTable(
                name: "StudentCaseActions");

            migrationBuilder.DropTable(
                name: "StudentEnrollments");

            migrationBuilder.DropTable(
                name: "StudentGuardians");

            migrationBuilder.DropTable(
                name: "StudentRecognitions");

            migrationBuilder.DropTable(
                name: "StudentTermMetrics");

            migrationBuilder.DropTable(
                name: "TeacherOfficeHours");

            migrationBuilder.DropTable(
                name: "AbsenceExcuses");

            migrationBuilder.DropTable(
                name: "GatePasses");

            migrationBuilder.DropTable(
                name: "GuardianSummons");

            migrationBuilder.DropTable(
                name: "ConversationMessages");

            migrationBuilder.DropTable(
                name: "DailyStudentAttendances");

            migrationBuilder.DropTable(
                name: "GuardianProfiles");

            migrationBuilder.DropTable(
                name: "StudentReferrals");

            migrationBuilder.DropTable(
                name: "ConversationThreads");

            migrationBuilder.DropTable(
                name: "Classrooms");

            migrationBuilder.DropTable(
                name: "AutomationTriggerLedgers");

            migrationBuilder.DropTable(
                name: "AcademicTerms");

            migrationBuilder.DropTable(
                name: "AutomationRuleDefinitions");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "SchoolStudentAffairsSettings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SchoolTimetables_SchoolId_Id",
                table: "SchoolTimetables");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SchoolTimetableEntries_SchoolId_Id",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetableEntries_SchoolId_ClassroomId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetableEntries_SchoolId_InstructorProfileId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropIndex(
                name: "IX_SchoolTimetableEntries_SchoolId_SchoolTimetableId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_ApprovedByUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CreatedByUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_DeletedByUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_SchoolId_UserId_DeduplicationKey",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_SchoolId_UserId_IsRead_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UpdatedByUserId",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_DeliveryStatus",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_Priority",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_RetryCount",
                table: "Notifications");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InstructorProfiles_SchoolId_Id",
                table: "InstructorProfiles");

            migrationBuilder.DropColumn(
                name: "ClassroomId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "SchoolTimetableEntries");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeduplicationKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RequiresApproval",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTimetableEntries_InstructorProfileId",
                table: "SchoolTimetableEntries",
                column: "InstructorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SchoolId",
                table: "Notifications",
                column: "SchoolId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Schools_SchoolId",
                table: "Notifications",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolTimetableEntries_InstructorProfiles_InstructorProfileId",
                table: "SchoolTimetableEntries",
                column: "InstructorProfileId",
                principalTable: "InstructorProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolTimetableEntries_SchoolTimetables_SchoolTimetableId",
                table: "SchoolTimetableEntries",
                column: "SchoolTimetableId",
                principalTable: "SchoolTimetables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
