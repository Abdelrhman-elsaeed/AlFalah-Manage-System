using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolDutyD2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationThreads_ThreadType",
                table: "ConversationThreads");

            migrationBuilder.AlterColumn<string>(
                name: "SenderUserId",
                table: "ConversationMessages",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemOrigin",
                table: "ConversationMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SourceFactId",
                table: "ConversationMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceFactRevision",
                table: "ConversationMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SystemCorrelationId",
                table: "ConversationMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudentDismissalLateness",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    ClassroomId = table.Column<int>(type: "int", nullable: false),
                    AcademicTermId = table.Column<int>(type: "int", nullable: false),
                    DutyDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AssignmentId = table.Column<int>(type: "int", nullable: false),
                    AssignmentRevision = table.Column<int>(type: "int", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Classroom = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ObservedAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    DismissalAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    BellScheduleRevisionId = table.Column<int>(type: "int", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DepartureAt = table.Column<TimeOnly>(type: "time", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    IsRetracted = table.Column<bool>(type: "bit", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentDismissalLateness", x => x.Id);
                    table.UniqueConstraint("AK_StudentDismissalLateness_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_StudentDismissalLateness_AcademicTerms_SchoolId_AcademicTermId",
                        columns: x => new { x.SchoolId, x.AcademicTermId },
                        principalTable: "AcademicTerms",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_StudentDismissalLateness_Classrooms_SchoolId_ClassroomId",
                        columns: x => new { x.SchoolId, x.ClassroomId },
                        principalTable: "Classrooms",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_StudentDismissalLateness_DutyAssignments_SchoolId_AssignmentId",
                        columns: x => new { x.SchoolId, x.AssignmentId },
                        principalTable: "DutyAssignments",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_StudentDismissalLateness_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentDismissalLateness_StudentEnrollments_SchoolId_EnrollmentId",
                        columns: x => new { x.SchoolId, x.EnrollmentId },
                        principalTable: "StudentEnrollments",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_StudentDismissalLateness_Students_SchoolId_StudentId",
                        columns: x => new { x.SchoolId, x.StudentId },
                        principalTable: "Students",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_StudentDismissalLateness_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DismissalLatenessRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    FactId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DismissalLatenessRevisions", x => x.Id);
                    table.UniqueConstraint("AK_DismissalLatenessRevisions_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_DismissalLatenessRevisions_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DismissalLatenessRevisions_StudentDismissalLateness_SchoolId_FactId",
                        columns: x => new { x.SchoolId, x.FactId },
                        principalTable: "StudentDismissalLateness",
                        principalColumns: new[] { "SchoolId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "DismissalNoticeBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    FactId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DismissalNoticeBatches", x => x.Id);
                    table.UniqueConstraint("AK_DismissalNoticeBatches_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_DismissalNoticeBatches_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DismissalNoticeBatches_StudentDismissalLateness_SchoolId_FactId",
                        columns: x => new { x.SchoolId, x.FactId },
                        principalTable: "StudentDismissalLateness",
                        principalColumns: new[] { "SchoolId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "DismissalNoticeRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationId = table.Column<int>(type: "int", nullable: true),
                    MessageId = table.Column<int>(type: "int", nullable: true),
                    ThreadId = table.Column<int>(type: "int", nullable: true),
                    DashboardState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MessageState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DismissalNoticeRecipients", x => x.Id);
                    table.UniqueConstraint("AK_DismissalNoticeRecipients_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_DismissalNoticeRecipients_ConversationMessages_SchoolId_MessageId",
                        columns: x => new { x.SchoolId, x.MessageId },
                        principalTable: "ConversationMessages",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_DismissalNoticeRecipients_ConversationThreads_SchoolId_ThreadId",
                        columns: x => new { x.SchoolId, x.ThreadId },
                        principalTable: "ConversationThreads",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_DismissalNoticeRecipients_DismissalNoticeBatches_SchoolId_BatchId",
                        columns: x => new { x.SchoolId, x.BatchId },
                        principalTable: "DismissalNoticeBatches",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_DismissalNoticeRecipients_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DismissalNoticeRecipients_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DismissalNoticeRecipients_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DutyArchiveIntents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyArchiveIntents", x => x.Id);
                    table.UniqueConstraint("AK_DutyArchiveIntents_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_DutyArchiveIntents_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DutyReportDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AssignmentId = table.Column<int>(type: "int", nullable: false),
                    AssignmentRevision = table.Column<int>(type: "int", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PreviousDocumentId = table.Column<int>(type: "int", nullable: true),
                    SavedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyReportDrafts", x => x.Id);
                    table.UniqueConstraint("AK_DutyReportDrafts_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_DutyReportDrafts_DutyAssignments_SchoolId_AssignmentId",
                        columns: x => new { x.SchoolId, x.AssignmentId },
                        principalTable: "DutyAssignments",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_DutyReportDrafts_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DutyReportDrafts_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DutySignedDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AssignmentId = table.Column<int>(type: "int", nullable: false),
                    AssignmentRevision = table.Column<int>(type: "int", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DraftId = table.Column<int>(type: "int", nullable: true),
                    PreviousDocumentId = table.Column<int>(type: "int", nullable: true),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TemplateVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignatureBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    SignatureHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutySignedDocuments", x => x.Id);
                    table.UniqueConstraint("AK_DutySignedDocuments_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_DutySignedDocuments_DutyAssignments_SchoolId_AssignmentId",
                        columns: x => new { x.SchoolId, x.AssignmentId },
                        principalTable: "DutyAssignments",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_DutySignedDocuments_DutyReportDrafts_SchoolId_DraftId",
                        columns: x => new { x.SchoolId, x.DraftId },
                        principalTable: "DutyReportDrafts",
                        principalColumns: new[] { "SchoolId", "Id" });
                    table.ForeignKey(
                        name: "FK_DutySignedDocuments_DutySignedDocuments_PreviousDocumentId",
                        column: x => x.PreviousDocumentId,
                        principalTable: "DutySignedDocuments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DutySignedDocuments_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DutySignedDocuments_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationThreads_ThreadType",
                table: "ConversationThreads",
                sql: "[ThreadType] BETWEEN 1 AND 4");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_SchoolId_SourceFactId",
                table: "ConversationMessages",
                columns: new[] { "SchoolId", "SourceFactId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_SystemCorrelationId",
                table: "ConversationMessages",
                column: "SystemCorrelationId",
                unique: true,
                filter: "[SystemCorrelationId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationMessages_Origin",
                table: "ConversationMessages",
                sql: "([IsSystemOrigin] = 0 AND [SenderUserId] IS NOT NULL AND [SystemCorrelationId] IS NULL AND [SourceFactId] IS NULL AND [SourceFactRevision] IS NULL) OR ([IsSystemOrigin] = 1 AND [SenderUserId] IS NULL AND [SystemCorrelationId] IS NOT NULL AND [SourceFactId] IS NOT NULL AND [SourceFactRevision] > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_DismissalLatenessRevisions_FactId_Revision",
                table: "DismissalLatenessRevisions",
                columns: new[] { "FactId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DismissalLatenessRevisions_SchoolId_FactId",
                table: "DismissalLatenessRevisions",
                columns: new[] { "SchoolId", "FactId" });

            migrationBuilder.CreateIndex(
                name: "IX_DismissalNoticeBatches_FactId_Revision",
                table: "DismissalNoticeBatches",
                columns: new[] { "FactId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DismissalNoticeBatches_SchoolId_FactId",
                table: "DismissalNoticeBatches",
                columns: new[] { "SchoolId", "FactId" });

            migrationBuilder.CreateIndex(
                name: "IX_DismissalNoticeBatches_State_NextAttemptAt",
                table: "DismissalNoticeBatches",
                columns: new[] { "State", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DismissalNoticeRecipients_BatchId_RecipientUserId",
                table: "DismissalNoticeRecipients",
                columns: new[] { "BatchId", "RecipientUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DismissalNoticeRecipients_CorrelationId",
                table: "DismissalNoticeRecipients",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DismissalNoticeRecipients_NotificationId",
                table: "DismissalNoticeRecipients",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_DismissalNoticeRecipients_RecipientUserId",
                table: "DismissalNoticeRecipients",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DismissalNoticeRecipients_SchoolId_BatchId",
                table: "DismissalNoticeRecipients",
                columns: new[] { "SchoolId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_DismissalNoticeRecipients_SchoolId_MessageId",
                table: "DismissalNoticeRecipients",
                columns: new[] { "SchoolId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_DismissalNoticeRecipients_SchoolId_ThreadId",
                table: "DismissalNoticeRecipients",
                columns: new[] { "SchoolId", "ThreadId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyArchiveIntents_DocumentId",
                table: "DutyArchiveIntents",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DutyArchiveIntents_SchoolId_DocumentId",
                table: "DutyArchiveIntents",
                columns: new[] { "SchoolId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyReportDrafts_AuthorUserId",
                table: "DutyReportDrafts",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyReportDrafts_SchoolId_AssignmentId_AuthorUserId",
                table: "DutyReportDrafts",
                columns: new[] { "SchoolId", "AssignmentId", "AuthorUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DutyReportDrafts_PreviousDocumentId",
                table: "DutyReportDrafts",
                column: "PreviousDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DutySignedDocuments_AuthorUserId",
                table: "DutySignedDocuments",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DutySignedDocuments_SchoolId_AssignmentId_AssignmentRevision_AuthorUserId_Kind",
                table: "DutySignedDocuments",
                columns: new[] { "SchoolId", "AssignmentId", "AssignmentRevision", "AuthorUserId", "Kind" },
                unique: true,
                filter: "[Kind] = 'Letter'");

            migrationBuilder.CreateIndex(
                name: "IX_DutySignedDocuments_SchoolId_DraftId",
                table: "DutySignedDocuments",
                columns: new[] { "SchoolId", "DraftId" });

            migrationBuilder.CreateIndex(
                name: "IX_DutySignedDocuments_PreviousDocumentId",
                table: "DutySignedDocuments",
                column: "PreviousDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDismissalLateness_AuthorUserId",
                table: "StudentDismissalLateness",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDismissalLateness_SchoolId_AcademicTermId",
                table: "StudentDismissalLateness",
                columns: new[] { "SchoolId", "AcademicTermId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentDismissalLateness_SchoolId_AssignmentId",
                table: "StudentDismissalLateness",
                columns: new[] { "SchoolId", "AssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentDismissalLateness_SchoolId_ClassroomId",
                table: "StudentDismissalLateness",
                columns: new[] { "SchoolId", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentDismissalLateness_SchoolId_DutyDate_IsRetracted_ClassroomId",
                table: "StudentDismissalLateness",
                columns: new[] { "SchoolId", "DutyDate", "IsRetracted", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentDismissalLateness_SchoolId_EnrollmentId",
                table: "StudentDismissalLateness",
                columns: new[] { "SchoolId", "EnrollmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentDismissalLateness_SchoolId_StudentId_DutyDate",
                table: "StudentDismissalLateness",
                columns: new[] { "SchoolId", "StudentId", "DutyDate" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationMessages_StudentDismissalLateness_SchoolId_SourceFactId",
                table: "ConversationMessages",
                columns: new[] { "SchoolId", "SourceFactId" },
                principalTable: "StudentDismissalLateness",
                principalColumns: new[] { "SchoolId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_DutyArchiveIntents_DutySignedDocuments_SchoolId_DocumentId",
                table: "DutyArchiveIntents",
                columns: new[] { "SchoolId", "DocumentId" },
                principalTable: "DutySignedDocuments",
                principalColumns: new[] { "SchoolId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_DutyReportDrafts_DutySignedDocuments_PreviousDocumentId",
                table: "DutyReportDrafts",
                column: "PreviousDocumentId",
                principalTable: "DutySignedDocuments",
                principalColumn: "Id");
            migrationBuilder.Sql("CREATE TRIGGER TR_DutySignedDocuments_Immutable ON DutySignedDocuments AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 51020, 'Signed duty documents are immutable', 1; END");
            migrationBuilder.Sql("CREATE TRIGGER TR_DismissalLatenessRevisions_Immutable ON DismissalLatenessRevisions AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 51021, 'Dismissal history is immutable', 1; END");
            migrationBuilder.Sql("CREATE TRIGGER TR_DutyDocument_PreviousScope ON DutySignedDocuments AFTER INSERT AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN DutySignedDocuments p ON p.Id=i.PreviousDocumentId WHERE i.SchoolId<>p.SchoolId OR i.AssignmentId<>p.AssignmentId OR i.AuthorUserId<>p.AuthorUserId) THROW 51024, 'Previous signed document scope mismatch', 1; END");
            migrationBuilder.Sql("CREATE TRIGGER TR_DutyDraft_PreviousScope ON DutyReportDrafts AFTER INSERT, UPDATE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN DutySignedDocuments p ON p.Id=i.PreviousDocumentId WHERE i.SchoolId<>p.SchoolId OR i.AssignmentId<>p.AssignmentId OR i.AuthorUserId<>p.AuthorUserId) THROW 51025, 'Previous draft document scope mismatch', 1; END");
            migrationBuilder.Sql("""
                CREATE TRIGGER TR_DismissalRecipient_SurfaceScope ON DismissalNoticeRecipients AFTER INSERT, UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS(SELECT 1 FROM inserted i JOIN Notifications n ON n.Id=i.NotificationId
                        WHERE n.SchoolId IS NULL OR n.SchoolId<>i.SchoolId OR n.UserId<>i.RecipientUserId OR n.CorrelationId<>i.CorrelationId)
                        THROW 51026, 'Dismissal dashboard recipient scope mismatch', 1;
                    IF EXISTS(SELECT 1 FROM inserted i JOIN ConversationMessages m ON m.Id=i.MessageId JOIN DismissalNoticeBatches b ON b.Id=i.BatchId
                        WHERE m.SchoolId<>i.SchoolId OR m.IsSystemOrigin<>1 OR m.SystemCorrelationId<>i.CorrelationId
                            OR m.SourceFactId<>b.FactId OR m.SourceFactRevision<>b.Revision OR m.ConversationThreadId<>i.ThreadId
                            OR NOT EXISTS(SELECT 1 FROM MessageReceipts r WHERE r.ConversationMessageId=m.Id AND r.SchoolId=i.SchoolId AND r.RecipientUserId=i.RecipientUserId))
                        THROW 51027, 'Dismissal System message recipient scope mismatch', 1;
                END
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER TR_DismissalLateness_Identity ON StudentDismissalLateness AFTER INSERT, UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM inserted i JOIN StudentEnrollments e ON e.Id=i.EnrollmentId AND e.SchoolId=i.SchoolId
                        JOIN DutyAssignments a ON a.Id=i.AssignmentId AND a.SchoolId=i.SchoolId
                        JOIN DutyPlans p ON p.Id=a.DutyPlanId AND p.SchoolId=a.SchoolId
                        WHERE e.StudentId<>i.StudentId OR e.ClassroomId<>i.ClassroomId OR e.AcademicTermId<>i.AcademicTermId
                            OR p.AcademicTermId<>i.AcademicTermId OR a.Date<>i.DutyDate)
                        THROW 51022, 'Dismissal fact identities do not match', 1;
                    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id WHERE i.SchoolId<>d.SchoolId OR i.StudentId<>d.StudentId
                        OR i.DutyDate<>d.DutyDate OR i.AssignmentId<>d.AssignmentId OR i.AuthorUserId<>d.AuthorUserId)
                        THROW 51023, 'Dismissal fact identity is immutable', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_DutySignedDocuments_Immutable; DROP TRIGGER IF EXISTS TR_DismissalLatenessRevisions_Immutable; DROP TRIGGER IF EXISTS TR_DismissalLateness_Identity;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_DutyDocument_PreviousScope; DROP TRIGGER IF EXISTS TR_DutyDraft_PreviousScope;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_DismissalRecipient_SurfaceScope;");
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationMessages_StudentDismissalLateness_SchoolId_SourceFactId",
                table: "ConversationMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_DutyReportDrafts_DutySignedDocuments_PreviousDocumentId",
                table: "DutyReportDrafts");

            migrationBuilder.DropTable(
                name: "DismissalLatenessRevisions");

            migrationBuilder.DropTable(
                name: "DismissalNoticeRecipients");

            migrationBuilder.DropTable(
                name: "DutyArchiveIntents");

            migrationBuilder.DropTable(
                name: "DismissalNoticeBatches");

            migrationBuilder.DropTable(
                name: "StudentDismissalLateness");

            migrationBuilder.DropTable(
                name: "DutySignedDocuments");

            migrationBuilder.DropTable(
                name: "DutyReportDrafts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationThreads_ThreadType",
                table: "ConversationThreads");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_SchoolId_SourceFactId",
                table: "ConversationMessages");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_SystemCorrelationId",
                table: "ConversationMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationMessages_Origin",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "IsSystemOrigin",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "SourceFactId",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "SourceFactRevision",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "SystemCorrelationId",
                table: "ConversationMessages");

            migrationBuilder.AlterColumn<string>(
                name: "SenderUserId",
                table: "ConversationMessages",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationThreads_ThreadType",
                table: "ConversationThreads",
                sql: "[ThreadType] BETWEEN 1 AND 3");
        }
    }
}
