using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Moves teacher evidence files from OneDrive to Google Drive.
    ///
    /// The two dropped tables are Microsoft-specific and have no Google equivalent:
    /// <c>SchoolMicrosoftDrives</c> is replaced by <c>SchoolGoogleDrives</c> (one credential
    /// per school instead of a tenant + drive id), and <c>TeacherMicrosoftAccounts</c>
    /// disappears entirely because teachers no longer sign in a second time — a teacher is
    /// now simply the instructor profile behind the application session.
    ///
    /// <c>TeacherDriveFolders</c> and <c>TeacherEvidenceSubmissions</c> keep their shape:
    /// their <c>DriveId</c>/<c>RootItemId</c>/<c>DriveItemId</c> columns are provider-neutral
    /// and now hold Google shared-drive, folder and file ids.
    /// </summary>
    public partial class SwitchTeacherEvidenceToGoogleDrive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolMicrosoftDrives");

            migrationBuilder.DropTable(
                name: "TeacherMicrosoftAccounts");

            // Any surviving grant points at a OneDrive item id, which resolves to nothing on
            // Google Drive. Deactivating them is what keeps the new folder guard honest: an
            // active grant must always name a folder that really exists in the school's tree,
            // so an administrator has to re-grant it deliberately. Uploaded evidence is left
            // untouched — the matrix must keep showing what teachers already submitted.
            migrationBuilder.Sql("UPDATE [TeacherDriveFolders] SET [IsActive] = 0 WHERE [IsActive] = 1;");

            migrationBuilder.CreateTable(
                name: "SchoolGoogleDrives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    CredentialType = table.Column<int>(type: "int", nullable: false),
                    SchoolGoogleEmail = table.Column<string>(type: "varchar(320)", unicode: false, maxLength: 320, nullable: false),
                    ProtectedCredential = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OAuthClientId = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: true),
                    ProtectedOAuthClientSecret = table.Column<string>(type: "varchar(2048)", unicode: false, maxLength: 2048, nullable: true),
                    ImpersonatedUserEmail = table.Column<string>(type: "varchar(320)", unicode: false, maxLength: 320, nullable: true),
                    SharedDriveId = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: true),
                    RootFolderId = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false),
                    RootFolderDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ConnectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolGoogleDrives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolGoogleDrives_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolGoogleDrives_SchoolId",
                table: "SchoolGoogleDrives",
                column: "SchoolId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolGoogleDrives");

            migrationBuilder.CreateTable(
                name: "SchoolMicrosoftDrives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ConnectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DriveId = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RootFolderDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RootItemId = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false),
                    SchoolMicrosoftEmail = table.Column<string>(type: "varchar(320)", unicode: false, maxLength: 320, nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolMicrosoftDrives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolMicrosoftDrives_Schools_SchoolId",
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
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsLinked = table.Column<bool>(type: "bit", nullable: false),
                    LastLoginAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MicrosoftEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedMicrosoftEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ObjectId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                name: "IX_SchoolMicrosoftDrives_SchoolId",
                table: "SchoolMicrosoftDrives",
                column: "SchoolId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolMicrosoftDrives_TenantId",
                table: "SchoolMicrosoftDrives",
                column: "TenantId");

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
    }
}
