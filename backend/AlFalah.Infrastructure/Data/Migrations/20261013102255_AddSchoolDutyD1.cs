using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolDutyD1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_InstructorProfiles_SchoolId_Id_UserId",
                table: "InstructorProfiles",
                columns: new[] { "SchoolId", "Id", "UserId" });

            migrationBuilder.CreateTable(
                name: "DutyPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    BellScheduleRevisionId = table.Column<int>(type: "int", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyPlans", x => x.Id);
                    table.UniqueConstraint("AK_DutyPlans_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_DutyPlans_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_DutyPlans_BellScheduleRevisions_BellScheduleRevisionId",
                        column: x => x.BellScheduleRevisionId,
                        principalTable: "BellScheduleRevisions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DutyPlans_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DutyReconciliationOperations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SignalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyReconciliationOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DutyReconciliationOperations_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DutyRequestReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Digest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyRequestReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DutyRequestReceipts_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DutyAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DutyPlanId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    OriginalTeacherId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EffectiveTeacherId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    IsManuallyProtected = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    ExceptionCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyAssignments", x => x.Id);
                    table.UniqueConstraint("AK_DutyAssignments_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_DutyAssignments_DutyPlans_SchoolId_DutyPlanId",
                        columns: x => new { x.SchoolId, x.DutyPlanId },
                        principalTable: "DutyPlans",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_DutyAssignments_Users_EffectiveTeacherId",
                        column: x => x.EffectiveTeacherId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DutyAssignments_Users_OriginalTeacherId",
                        column: x => x.OriginalTeacherId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DutyClosedDates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DutyPlanId = table.Column<int>(type: "int", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    To = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyClosedDates", x => x.Id);
                    table.CheckConstraint("CK_DutyClosure_Range", "[From] <= [To]");
                    table.ForeignKey(
                        name: "FK_DutyClosedDates_DutyPlans_SchoolId_DutyPlanId",
                        columns: x => new { x.SchoolId, x.DutyPlanId },
                        principalTable: "DutyPlans",
                        principalColumns: new[] { "SchoolId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "DutyParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DutyPlanId = table.Column<int>(type: "int", nullable: false),
                    InstructorProfileId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DutyParticipants_DutyPlans_SchoolId_DutyPlanId",
                        columns: x => new { x.SchoolId, x.DutyPlanId },
                        principalTable: "DutyPlans",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_DutyParticipants_InstructorProfiles_SchoolId_InstructorProfileId_UserId",
                        columns: x => new { x.SchoolId, x.InstructorProfileId, x.UserId },
                        principalTable: "InstructorProfiles",
                        principalColumns: new[] { "SchoolId", "Id", "UserId" });
                });

            migrationBuilder.CreateTable(
                name: "DutyAssignmentChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DutyPlanId = table.Column<int>(type: "int", nullable: false),
                    AssignmentId = table.Column<int>(type: "int", nullable: true),
                    OtherAssignmentId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    OtherDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BeforeTeacherId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AfterTeacherId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OtherBeforeTeacherId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    OtherAfterTeacherId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AfterRevision = table.Column<int>(type: "int", nullable: false),
                    OtherAfterRevision = table.Column<int>(type: "int", nullable: true),
                    RelatedChangeId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AttendanceRecordId = table.Column<int>(type: "int", nullable: true),
                    AttendanceUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyAssignmentChanges", x => x.Id);
                    table.UniqueConstraint("AK_DutyAssignmentChanges_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_DutyAssignmentChanges_DutyAssignmentChanges_SchoolId_RelatedChangeId",
                        columns: x => new { x.SchoolId, x.RelatedChangeId },
                        principalTable: "DutyAssignmentChanges",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_DutyAssignmentChanges_DutyAssignments_SchoolId_AssignmentId",
                        columns: x => new { x.SchoolId, x.AssignmentId },
                        principalTable: "DutyAssignments",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_DutyAssignmentChanges_DutyAssignments_SchoolId_OtherAssignmentId",
                        columns: x => new { x.SchoolId, x.OtherAssignmentId },
                        principalTable: "DutyAssignments",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_DutyAssignmentChanges_DutyPlans_SchoolId_DutyPlanId",
                        columns: x => new { x.SchoolId, x.DutyPlanId },
                        principalTable: "DutyPlans",
                        principalColumns: new[] { "SchoolId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignmentChanges_SchoolId_AssignmentId",
                table: "DutyAssignmentChanges",
                columns: new[] { "SchoolId", "AssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignmentChanges_SchoolId_Date",
                table: "DutyAssignmentChanges",
                columns: new[] { "SchoolId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignmentChanges_SchoolId_DutyPlanId",
                table: "DutyAssignmentChanges",
                columns: new[] { "SchoolId", "DutyPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignmentChanges_SchoolId_OtherAssignmentId",
                table: "DutyAssignmentChanges",
                columns: new[] { "SchoolId", "OtherAssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignmentChanges_SchoolId_RelatedChangeId",
                table: "DutyAssignmentChanges",
                columns: new[] { "SchoolId", "RelatedChangeId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_EffectiveTeacherId",
                table: "DutyAssignments",
                column: "EffectiveTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_OriginalTeacherId",
                table: "DutyAssignments",
                column: "OriginalTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_SchoolId_Date",
                table: "DutyAssignments",
                columns: new[] { "SchoolId", "Date" },
                unique: true);
            migrationBuilder.Sql("""
                INSERT INTO Permissions (Name, [Group], DescriptionAr, DescriptionEn, CreatedAt)
                SELECT v.Name, N'Duty', v.Ar, v.En, SYSDATETIMEOFFSET()
                FROM (VALUES (N'Duty.View', N'عرض جدول المناوبة', N'View duty schedule'),
                    (N'Duty.Manage', N'إدارة المناوبة', N'Manage duty')) v(Name, Ar, En)
                WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.Name = v.Name);
                INSERT INTO RolePermissions (RoleId, PermissionId, CreatedAt)
                SELECT r.Id, p.Id, SYSDATETIMEOFFSET() FROM Roles r CROSS JOIN Permissions p
                WHERE (r.Name = N'SchoolManager' AND p.Name IN (N'Duty.View', N'Duty.Manage')
                    OR r.Name = N'Instructor' AND p.Name = N'Duty.View')
                AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER TR_DutyHistory_AppendOnly ON DutyAssignmentChanges
                INSTEAD OF UPDATE, DELETE AS
                BEGIN THROW 51001, 'Duty history is append-only', 1; END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_SchoolId_DutyPlanId",
                table: "DutyAssignments",
                columns: new[] { "SchoolId", "DutyPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyClosedDates_SchoolId_DutyPlanId",
                table: "DutyClosedDates",
                columns: new[] { "SchoolId", "DutyPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyParticipants_DutyPlanId_Position",
                table: "DutyParticipants",
                columns: new[] { "DutyPlanId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DutyParticipants_DutyPlanId_UserId",
                table: "DutyParticipants",
                columns: new[] { "DutyPlanId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DutyParticipants_SchoolId_DutyPlanId",
                table: "DutyParticipants",
                columns: new[] { "SchoolId", "DutyPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyParticipants_SchoolId_InstructorProfileId_UserId",
                table: "DutyParticipants",
                columns: new[] { "SchoolId", "InstructorProfileId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyPlans_BellScheduleRevisionId",
                table: "DutyPlans",
                column: "BellScheduleRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyPlans_SchoolId_AcademicTermId",
                table: "DutyPlans",
                columns: new[] { "SchoolId", "AcademicTermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DutyReconciliationOperations_CompletedAt_NextAttemptAt",
                table: "DutyReconciliationOperations",
                columns: new[] { "CompletedAt", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyReconciliationOperations_SchoolId",
                table: "DutyReconciliationOperations",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyReconciliationOperations_SignalId",
                table: "DutyReconciliationOperations",
                column: "SignalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DutyRequestReceipts_SchoolId_RequestId",
                table: "DutyRequestReceipts",
                columns: new[] { "SchoolId", "RequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DutyAssignmentChanges");

            migrationBuilder.DropTable(
                name: "DutyClosedDates");

            migrationBuilder.DropTable(
                name: "DutyParticipants");

            migrationBuilder.DropTable(
                name: "DutyReconciliationOperations");

            migrationBuilder.DropTable(
                name: "DutyRequestReceipts");

            migrationBuilder.DropTable(
                name: "DutyAssignments");

            migrationBuilder.DropTable(
                name: "DutyPlans");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InstructorProfiles_SchoolId_Id_UserId",
                table: "InstructorProfiles");
        }
    }
}
