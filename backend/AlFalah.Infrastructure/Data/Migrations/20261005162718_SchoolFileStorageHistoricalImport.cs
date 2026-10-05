using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchoolFileStorageHistoricalImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            static void Trigger(MigrationBuilder builder, string sql) => builder.Sql("EXEC(N'" + sql.Replace("'", "''") + "');");
            migrationBuilder.DropIndex(
                name: "IX_PrototypeImportBatches_SchoolId_AcademicYearId_SourceSHA256",
                table: "PrototypeImportBatches");

            migrationBuilder.AddColumn<string>(
                name: "BytesSHA256",
                table: "PrototypeImportRows",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Classification",
                table: "PrototypeImportRows",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RequirementId",
                table: "PrototypeImportRows",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionReason",
                table: "PrototypeImportRows",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleUserId",
                table: "PrototypeImportRows",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceJson",
                table: "PrototypeImportRows",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceRowKey",
                table: "PrototypeImportRows",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "StoredFileId",
                table: "PrototypeImportRows",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UploadOperationId",
                table: "PrototypeImportRows",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CommittedAtUtc",
                table: "PrototypeImportBatches",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommittedByUserId",
                table: "PrototypeImportBatches",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "PrototypeImportBatches",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAtUtc",
                table: "PrototypeImportBatches",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "PrototypeImportBatches",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedDigest",
                table: "PrototypeImportBatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceVersion",
                table: "PrototypeImportBatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TemplateVersion",
                table: "PrototypeImportBatches",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_PrototypeImportRows_BatchId_SourceRowKey",
                table: "PrototypeImportRows",
                columns: new[] { "BatchId", "SourceRowKey" },
                unique: true,
                filter: "[SourceRowKey] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_PrototypeImportRows_RequirementId",
                table: "PrototypeImportRows",
                column: "RequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_PrototypeImportRows_ResponsibleUserId",
                table: "PrototypeImportRows",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PrototypeImportRows_SchoolId_StoredFileId",
                table: "PrototypeImportRows",
                columns: new[] { "SchoolId", "StoredFileId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrototypeImportRows_SchoolId_UploadOperationId",
                table: "PrototypeImportRows",
                columns: new[] { "SchoolId", "UploadOperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrototypeImportBatches_CommittedByUserId",
                table: "PrototypeImportBatches",
                column: "CommittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PrototypeImportBatches_ReviewedByUserId",
                table: "PrototypeImportBatches",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PrototypeImportBatches_SchoolId_AcademicYearId_TemplateVersion_SourceSHA256",
                table: "PrototypeImportBatches",
                columns: new[] { "SchoolId", "AcademicYearId", "TemplateVersion", "SourceSHA256" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PrototypeImportBatches_Users_CommittedByUserId",
                table: "PrototypeImportBatches",
                column: "CommittedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrototypeImportBatches_Users_ReviewedByUserId",
                table: "PrototypeImportBatches",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrototypeImportRows_EvidenceRequirements_RequirementId",
                table: "PrototypeImportRows",
                column: "RequirementId",
                principalTable: "EvidenceRequirements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrototypeImportRows_StorageOperations_SchoolId_UploadOperationId",
                table: "PrototypeImportRows",
                columns: new[] { "SchoolId", "UploadOperationId" },
                principalTable: "StorageOperations",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrototypeImportRows_StoredFiles_SchoolId_StoredFileId",
                table: "PrototypeImportRows",
                columns: new[] { "SchoolId", "StoredFileId" },
                principalTable: "StoredFiles",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrototypeImportRows_Users_ResponsibleUserId",
                table: "PrototypeImportRows",
                column: "ResponsibleUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            Trigger(migrationBuilder, @"CREATE OR ALTER TRIGGER TR_PrototypeImportRows_Provenance ON PrototypeImportRows AFTER INSERT, UPDATE, DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN PrototypeImportBatches b ON b.Id=i.BatchId AND b.SchoolId=i.SchoolId
  LEFT JOIN EvidenceRequirements r ON r.Id=i.RequirementId
  WHERE i.RequirementId IS NOT NULL AND (r.Id IS NULL OR ISNULL(r.SchoolId,0)<>i.SchoolId OR ISNULL(r.AcademicYearId,0)<>b.AcademicYearId OR r.TemplateVersion<>b.TemplateVersion))
 THROW 51001, 'Import requirement is outside its scope', 1;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL
  OR i.SchoolId<>d.SchoolId OR i.BatchId<>d.BatchId OR i.SourceOrdinal<>d.SourceOrdinal
  OR i.SourceRowKey<>d.SourceRowKey OR i.SourceRowSHA256<>d.SourceRowSHA256
  OR i.SourceJson<>d.SourceJson OR ISNULL(i.ReferencePath,'')<>ISNULL(d.ReferencePath,'')
  OR (d.BytesSHA256 IS NOT NULL AND ISNULL(i.BytesSHA256,'')<>d.BytesSHA256)
  OR (EXISTS(SELECT 1 FROM PrototypeImportBatches b WHERE b.Id=d.BatchId AND b.CommittedAtUtc IS NOT NULL)
      AND (ISNULL(i.RequirementId,0)<>ISNULL(d.RequirementId,0) OR ISNULL(i.ResponsibleUserId,'')<>ISNULL(d.ResponsibleUserId,'') OR ISNULL(i.ResolutionReason,'')<>ISNULL(d.ResolutionReason,''))))
 THROW 51001, 'Import provenance is immutable', 1;
END");
            Trigger(migrationBuilder, @"CREATE OR ALTER TRIGGER TR_PrototypeImportBatches_Provenance ON PrototypeImportBatches AFTER UPDATE, DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL
  OR i.SchoolId<>d.SchoolId OR i.AcademicYearId<>d.AcademicYearId OR i.TemplateVersion<>d.TemplateVersion
  OR i.SourceSHA256<>d.SourceSHA256 OR i.SourceVersion<>d.SourceVersion OR i.SourceName<>d.SourceName
  OR (d.CommittedAtUtc IS NOT NULL AND (i.CommittedAtUtc IS NULL OR i.CommittedAtUtc<>d.CommittedAtUtc)))
 THROW 51001, 'Import batch provenance is immutable', 1;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new NotSupportedException("Disable storage flags; retain import records, files and history.");
    }
}
