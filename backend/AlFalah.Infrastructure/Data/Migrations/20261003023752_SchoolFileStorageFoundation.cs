using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchoolFileStorageFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvidenceRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: true),
                    AcademicYearId = table.Column<int>(type: "int", nullable: true),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DomainCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StandardCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OriginalTaskId = table.Column<int>(type: "int", nullable: true),
                    Importance = table.Column<int>(type: "int", nullable: false),
                    ResponsibleUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ResponsibleRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FulfillmentPolicy = table.Column<int>(type: "int", nullable: false),
                    MinimumApprovedLinks = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceRequirements", x => x.Id);
                    table.CheckConstraint("CK_EvidenceRequirements_Policy", "[MinimumApprovedLinks] > 0 AND [TemplateVersion] > 0 AND [Importance] BETWEEN 1 AND 3 AND [FulfillmentPolicy] BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_EvidenceRequirements_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceRequirements_EvidenceTasks_OriginalTaskId",
                        column: x => x.OriginalTaskId,
                        principalTable: "EvidenceTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceRequirements_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceRequirements_Users_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrototypeImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SourceSHA256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrototypeImportBatches", x => x.Id);
                    table.UniqueConstraint("AK_PrototypeImportBatches_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_PrototypeImportBatches_Status", "[Status] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_PrototypeImportBatches_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrototypeImportBatches_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrototypeImportBatches_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StorageDelegations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    GranteeUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    GrantedByManagerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedByManagerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RevocationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageDelegations", x => x.Id);
                    table.UniqueConstraint("AK_StorageDelegations_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_StorageDelegations_Dates", "[ExpiresAt] IS NULL OR [ExpiresAt] > [StartsAt]");
                    table.ForeignKey(
                        name: "FK_StorageDelegations_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageDelegations_Users_GrantedByManagerUserId",
                        column: x => x.GrantedByManagerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageDelegations_Users_GranteeUserId",
                        column: x => x.GranteeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageDelegations_Users_RevokedByManagerUserId",
                        column: x => x.RevokedByManagerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StorageFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ParentFolderId = table.Column<int>(type: "int", nullable: true),
                    OwnerTeacherId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DriveId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DriveItemId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageFolders", x => x.Id);
                    table.UniqueConstraint("AK_StorageFolders_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_StorageFolders_Kind", "[Kind] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_StorageFolders_Parent", "[ParentFolderId] IS NULL OR [ParentFolderId] <> [Id]");
                    table.ForeignKey(
                        name: "FK_StorageFolders_InstructorProfiles_OwnerTeacherId",
                        column: x => x.OwnerTeacherId,
                        principalTable: "InstructorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageFolders_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageFolders_StorageFolders_SchoolId_ParentFolderId",
                        columns: x => new { x.SchoolId, x.ParentFolderId },
                        principalTable: "StorageFolders",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageFolders_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageFolders_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VisitArchiveOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    VisitId = table.Column<int>(type: "int", nullable: false),
                    ApprovalRevision = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitArchiveOperations", x => x.Id);
                    table.UniqueConstraint("AK_VisitArchiveOperations_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.UniqueConstraint("AK_VisitArchiveOperations_SchoolId_VisitId_ApprovalRevision_Id", x => new { x.SchoolId, x.VisitId, x.ApprovalRevision, x.Id });
                    table.CheckConstraint("CK_VisitArchiveOperations_State", "[ApprovalRevision] > 0 AND [Attempts] >= 0 AND [Status] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_VisitArchiveOperations_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitArchiveOperations_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrototypeImportRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    SourceOrdinal = table.Column<int>(type: "int", nullable: false),
                    SourceRowSHA256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReferencePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExceptionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrototypeImportRows", x => x.Id);
                    table.UniqueConstraint("AK_PrototypeImportRows_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_PrototypeImportRows_State", "[SourceOrdinal] >= 0 AND [Status] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_PrototypeImportRows_PrototypeImportBatches_SchoolId_BatchId",
                        columns: x => new { x.SchoolId, x.BatchId },
                        principalTable: "PrototypeImportBatches",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrototypeImportRows_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    StoredFileId = table.Column<int>(type: "int", nullable: false),
                    RequirementId = table.Column<int>(type: "int", nullable: false),
                    TeacherId = table.Column<int>(type: "int", nullable: true),
                    VersionId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceLinks", x => x.Id);
                    table.UniqueConstraint("AK_EvidenceLinks_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.UniqueConstraint("AK_EvidenceLinks_SchoolId_StoredFileId_Id", x => new { x.SchoolId, x.StoredFileId, x.Id });
                    table.CheckConstraint("CK_EvidenceLinks_Status", "[Status] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_EvidenceLinks_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceLinks_EvidenceRequirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "EvidenceRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceLinks_InstructorProfiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "InstructorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceLinks_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EvidenceLinkId = table.Column<int>(type: "int", nullable: false),
                    StoredFileId = table.Column<int>(type: "int", nullable: false),
                    VersionId = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsLegacyImported = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceReviewDecisions", x => x.Id);
                    table.UniqueConstraint("AK_EvidenceReviewDecisions_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_EvidenceReviewDecisions_Decision", "[Decision] IN (3,4) AND ([IsLegacyImported] = 1 OR ([ReviewedAtUtc] IS NOT NULL AND [ReviewedByUserId] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_EvidenceReviewDecisions_EvidenceLinks_SchoolId_StoredFileId_EvidenceLinkId",
                        columns: x => new { x.SchoolId, x.StoredFileId, x.EvidenceLinkId },
                        principalTable: "EvidenceLinks",
                        principalColumns: new[] { "SchoolId", "StoredFileId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceReviewDecisions_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceReviewDecisions_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoredFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    FolderId = table.Column<int>(type: "int", nullable: false),
                    OwnerTeacherId = table.Column<int>(type: "int", nullable: true),
                    SourceKind = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CurrentVersionId = table.Column<int>(type: "int", nullable: true),
                    NeedsLink = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LegacySubmissionId = table.Column<long>(type: "bigint", nullable: true),
                    LegacyProvenanceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LegacyFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                    table.UniqueConstraint("AK_StoredFiles_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.CheckConstraint("CK_StoredFiles_SourceKind", "[SourceKind] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_StoredFiles_InstructorProfiles_OwnerTeacherId",
                        column: x => x.OwnerTeacherId,
                        principalTable: "InstructorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoredFiles_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoredFiles_StorageFolders_SchoolId_FolderId",
                        columns: x => new { x.SchoolId, x.FolderId },
                        principalTable: "StorageFolders",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoredFiles_TeacherEvidenceSubmissions_LegacySubmissionId",
                        column: x => x.LegacySubmissionId,
                        principalTable: "TeacherEvidenceSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoredFiles_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoredFileVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StoredFileId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    DriveId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DriveItemId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DriveFileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FileExtension = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SHA256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Availability = table.Column<int>(type: "int", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MissingFromDriveAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFileVersions", x => x.Id);
                    table.UniqueConstraint("AK_StoredFileVersions_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.UniqueConstraint("AK_StoredFileVersions_SchoolId_StoredFileId_Id", x => new { x.SchoolId, x.StoredFileId, x.Id });
                    table.CheckConstraint("CK_StoredFileVersions_Availability", "[Availability] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_StoredFileVersions_Size", "[SizeInBytes] >= 0 AND [VersionNumber] > 0");
                    table.ForeignKey(
                        name: "FK_StoredFileVersions_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoredFileVersions_StoredFiles_SchoolId_StoredFileId",
                        columns: x => new { x.SchoolId, x.StoredFileId },
                        principalTable: "StoredFiles",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoredFileVersions_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VisitArchiveArtifacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    VisitId = table.Column<int>(type: "int", nullable: false),
                    ApprovalRevision = table.Column<int>(type: "int", nullable: false),
                    OperationId = table.Column<int>(type: "int", nullable: false),
                    StoredFileVersionId = table.Column<int>(type: "int", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitArchiveArtifacts", x => x.Id);
                    table.UniqueConstraint("AK_VisitArchiveArtifacts_SchoolId_Id", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_VisitArchiveArtifacts_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitArchiveArtifacts_StoredFileVersions_SchoolId_StoredFileVersionId",
                        columns: x => new { x.SchoolId, x.StoredFileVersionId },
                        principalTable: "StoredFileVersions",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitArchiveArtifacts_VisitArchiveOperations_SchoolId_VisitId_ApprovalRevision_OperationId",
                        columns: x => new { x.SchoolId, x.VisitId, x.ApprovalRevision, x.OperationId },
                        principalTable: "VisitArchiveOperations",
                        principalColumns: new[] { "SchoolId", "VisitId", "ApprovalRevision", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinks_AcademicYearId",
                table: "EvidenceLinks",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinks_RequirementId",
                table: "EvidenceLinks",
                column: "RequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinks_SchoolId_AcademicYearId_RequirementId",
                table: "EvidenceLinks",
                columns: new[] { "SchoolId", "AcademicYearId", "RequirementId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinks_SchoolId_StoredFileId_VersionId",
                table: "EvidenceLinks",
                columns: new[] { "SchoolId", "StoredFileId", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinks_TeacherId",
                table: "EvidenceLinks",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "UX_EvidenceLinks_NoTeacher",
                table: "EvidenceLinks",
                columns: new[] { "StoredFileId", "RequirementId", "AcademicYearId" },
                unique: true,
                filter: "[IsActive] = 1 AND [TeacherId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_EvidenceLinks_Teacher",
                table: "EvidenceLinks",
                columns: new[] { "StoredFileId", "RequirementId", "TeacherId", "AcademicYearId" },
                unique: true,
                filter: "[IsActive] = 1 AND [TeacherId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceRequirements_OriginalTaskId",
                table: "EvidenceRequirements",
                column: "OriginalTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceRequirements_ResponsibleUserId",
                table: "EvidenceRequirements",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Requirements_GlobalTemplate",
                table: "EvidenceRequirements",
                columns: new[] { "TemplateVersion", "Code" },
                unique: true,
                filter: "[SchoolId] IS NULL AND [AcademicYearId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Requirements_GlobalYear",
                table: "EvidenceRequirements",
                columns: new[] { "AcademicYearId", "TemplateVersion", "Code" },
                unique: true,
                filter: "[SchoolId] IS NULL AND [AcademicYearId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Requirements_SchoolTemplate",
                table: "EvidenceRequirements",
                columns: new[] { "SchoolId", "TemplateVersion", "Code" },
                unique: true,
                filter: "[SchoolId] IS NOT NULL AND [AcademicYearId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Requirements_SchoolYear",
                table: "EvidenceRequirements",
                columns: new[] { "SchoolId", "AcademicYearId", "TemplateVersion", "Code" },
                unique: true,
                filter: "[SchoolId] IS NOT NULL AND [AcademicYearId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceReviewDecisions_ReviewedByUserId",
                table: "EvidenceReviewDecisions",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceReviewDecisions_SchoolId_StoredFileId_EvidenceLinkId",
                table: "EvidenceReviewDecisions",
                columns: new[] { "SchoolId", "StoredFileId", "EvidenceLinkId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceReviewDecisions_SchoolId_StoredFileId_VersionId",
                table: "EvidenceReviewDecisions",
                columns: new[] { "SchoolId", "StoredFileId", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrototypeImportBatches_AcademicYearId",
                table: "PrototypeImportBatches",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_PrototypeImportBatches_CreatedByUserId",
                table: "PrototypeImportBatches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PrototypeImportBatches_SchoolId_AcademicYearId_SourceSHA256",
                table: "PrototypeImportBatches",
                columns: new[] { "SchoolId", "AcademicYearId", "SourceSHA256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrototypeImportRows_BatchId_SourceOrdinal",
                table: "PrototypeImportRows",
                columns: new[] { "BatchId", "SourceOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrototypeImportRows_SchoolId_BatchId",
                table: "PrototypeImportRows",
                columns: new[] { "SchoolId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_StorageDelegations_GrantedByManagerUserId",
                table: "StorageDelegations",
                column: "GrantedByManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageDelegations_GranteeUserId",
                table: "StorageDelegations",
                column: "GranteeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageDelegations_RevokedByManagerUserId",
                table: "StorageDelegations",
                column: "RevokedByManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageDelegations_SchoolId_GranteeUserId_StartsAt",
                table: "StorageDelegations",
                columns: new[] { "SchoolId", "GranteeUserId", "StartsAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageFolders_CreatedByUserId",
                table: "StorageFolders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageFolders_OwnerTeacherId",
                table: "StorageFolders",
                column: "OwnerTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageFolders_SchoolId_DriveId_DriveItemId",
                table: "StorageFolders",
                columns: new[] { "SchoolId", "DriveId", "DriveItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageFolders_SchoolId_DriveItemId",
                table: "StorageFolders",
                columns: new[] { "SchoolId", "DriveItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StorageFolders_SchoolId_OwnerTeacherId",
                table: "StorageFolders",
                columns: new[] { "SchoolId", "OwnerTeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_StorageFolders_SchoolId_ParentFolderId",
                table: "StorageFolders",
                columns: new[] { "SchoolId", "ParentFolderId" });

            migrationBuilder.CreateIndex(
                name: "IX_StorageFolders_UpdatedByUserId",
                table: "StorageFolders",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_DeletedByUserId",
                table: "StoredFiles",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_LegacySubmissionId",
                table: "StoredFiles",
                column: "LegacySubmissionId",
                unique: true,
                filter: "[LegacySubmissionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_OwnerTeacherId",
                table: "StoredFiles",
                column: "OwnerTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_SchoolId_FolderId",
                table: "StoredFiles",
                columns: new[] { "SchoolId", "FolderId" });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_SchoolId_Id_CurrentVersionId",
                table: "StoredFiles",
                columns: new[] { "SchoolId", "Id", "CurrentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_SchoolId_OwnerTeacherId",
                table: "StoredFiles",
                columns: new[] { "SchoolId", "OwnerTeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFileVersions_SchoolId_DriveId_DriveItemId",
                table: "StoredFileVersions",
                columns: new[] { "SchoolId", "DriveId", "DriveItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoredFileVersions_SchoolId_DriveItemId",
                table: "StoredFileVersions",
                columns: new[] { "SchoolId", "DriveItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFileVersions_SchoolId_StoredFileId_VersionNumber",
                table: "StoredFileVersions",
                columns: new[] { "SchoolId", "StoredFileId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoredFileVersions_UploadedByUserId",
                table: "StoredFileVersions",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitArchiveArtifacts_SchoolId_StoredFileVersionId",
                table: "VisitArchiveArtifacts",
                columns: new[] { "SchoolId", "StoredFileVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitArchiveArtifacts_SchoolId_VisitId_ApprovalRevision_OperationId",
                table: "VisitArchiveArtifacts",
                columns: new[] { "SchoolId", "VisitId", "ApprovalRevision", "OperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitArchiveArtifacts_VisitId_ApprovalRevision",
                table: "VisitArchiveArtifacts",
                columns: new[] { "VisitId", "ApprovalRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_VisitArchiveArtifacts_Current",
                table: "VisitArchiveArtifacts",
                column: "VisitId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_VisitArchiveOperations_Status_NextAttemptAtUtc",
                table: "VisitArchiveOperations",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitArchiveOperations_VisitId_ApprovalRevision",
                table: "VisitArchiveOperations",
                columns: new[] { "VisitId", "ApprovalRevision" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EvidenceLinks_StoredFileVersions_SchoolId_StoredFileId_VersionId",
                table: "EvidenceLinks",
                columns: new[] { "SchoolId", "StoredFileId", "VersionId" },
                principalTable: "StoredFileVersions",
                principalColumns: new[] { "SchoolId", "StoredFileId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvidenceLinks_StoredFiles_SchoolId_StoredFileId",
                table: "EvidenceLinks",
                columns: new[] { "SchoolId", "StoredFileId" },
                principalTable: "StoredFiles",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvidenceReviewDecisions_StoredFileVersions_SchoolId_StoredFileId_VersionId",
                table: "EvidenceReviewDecisions",
                columns: new[] { "SchoolId", "StoredFileId", "VersionId" },
                principalTable: "StoredFileVersions",
                principalColumns: new[] { "SchoolId", "StoredFileId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StoredFiles_StoredFileVersions_SchoolId_Id_CurrentVersionId",
                table: "StoredFiles",
                columns: new[] { "SchoolId", "Id", "CurrentVersionId" },
                principalTable: "StoredFileVersions",
                principalColumns: new[] { "SchoolId", "StoredFileId", "Id" },
                onDelete: ReferentialAction.Restrict);
            AddStorageIntegrity(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Use SchoolFileStorage flags to roll back; preserve stored files and history.");
        }
    }
}
