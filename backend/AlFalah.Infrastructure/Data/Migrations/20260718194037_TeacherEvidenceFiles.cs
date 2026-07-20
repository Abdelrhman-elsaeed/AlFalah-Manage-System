using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TeacherEvidenceFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeacherDriveFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DriveId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RootItemId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FolderDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RootWebUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherDriveFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherDriveFolders_InstructorProfiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "InstructorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherDriveFolders_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherEvidenceSubmissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: true),
                    DriveId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DriveItemId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ParentItemId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FileExtension = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MimeType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    WebUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ETag = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UploadStatus = table.Column<int>(type: "int", nullable: false),
                    ReviewStatus = table.Column<int>(type: "int", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherEvidenceSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherEvidenceSubmissions_InstructorProfiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "InstructorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherEvidenceSubmissions_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherMicrosoftAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ObjectId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MicrosoftEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedMicrosoftEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsLinked = table.Column<bool>(type: "bit", nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastLoginAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherMicrosoftAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherMicrosoftAccounts_InstructorProfiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "InstructorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherDriveFolders_DriveId_RootItemId",
                table: "TeacherDriveFolders",
                columns: new[] { "DriveId", "RootItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherDriveFolders_SchoolId",
                table: "TeacherDriveFolders",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherDriveFolders_TeacherId",
                table: "TeacherDriveFolders",
                column: "TeacherId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherEvidenceSubmissions_SchoolId",
                table: "TeacherEvidenceSubmissions",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherEvidenceSubmissions_TeacherId_DriveItemId",
                table: "TeacherEvidenceSubmissions",
                columns: new[] { "TeacherId", "DriveItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherEvidenceSubmissions_TeacherId_UploadedAtUtc",
                table: "TeacherEvidenceSubmissions",
                columns: new[] { "TeacherId", "UploadedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherMicrosoftAccounts_NormalizedMicrosoftEmail",
                table: "TeacherMicrosoftAccounts",
                column: "NormalizedMicrosoftEmail");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherMicrosoftAccounts_TeacherId",
                table: "TeacherMicrosoftAccounts",
                column: "TeacherId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherMicrosoftAccounts_TenantId_ObjectId",
                table: "TeacherMicrosoftAccounts",
                columns: new[] { "TenantId", "ObjectId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL AND [ObjectId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeacherDriveFolders");

            migrationBuilder.DropTable(
                name: "TeacherEvidenceSubmissions");

            migrationBuilder.DropTable(
                name: "TeacherMicrosoftAccounts");
        }
    }
}
