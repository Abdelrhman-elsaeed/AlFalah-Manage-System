using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentAnalyzer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchoolStudentAnalyzerSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ActiveProvider = table.Column<int>(type: "int", nullable: false),
                    ProtectedGroqApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroqModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProtectedGeminiApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeminiModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProtectedOpenRouterApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpenRouterModel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolStudentAnalyzerSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolStudentAnalyzerSettings_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolStudentAnalyzerSettings_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentAnalyzerAccessGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    GrantedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAnalyzerAccessGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAnalyzerAccessGrants_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAnalyzerAccessGrants_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAnalyzerAccessGrants_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAnalyzerAccessGrants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentAnalyzerSourceFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FileKind = table.Column<int>(type: "int", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAnalyzerSourceFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAnalyzerSourceFiles_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAnalyzerSourceFiles_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAnalyzerSourceFiles_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentAnalyzerReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    SourceFileId = table.Column<int>(type: "int", nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    GrantTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductionTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SelectedDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnalysisText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAnalyzerReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAnalyzerReports_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAnalyzerReports_StudentAnalyzerSourceFiles_SourceFileId",
                        column: x => x.SourceFileId,
                        principalTable: "StudentAnalyzerSourceFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAnalyzerReports_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAnalyzerReports_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStudentAnalyzerSettings_SchoolId",
                table: "SchoolStudentAnalyzerSettings",
                column: "SchoolId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStudentAnalyzerSettings_UpdatedByUserId",
                table: "SchoolStudentAnalyzerSettings",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnalyzerAccessGrants_DeletedByUserId",
                table: "StudentAnalyzerAccessGrants",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnalyzerAccessGrants_GrantedByUserId",
                table: "StudentAnalyzerAccessGrants",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnalyzerAccessGrants_SchoolId_UserId",
                table: "StudentAnalyzerAccessGrants",
                columns: new[] { "SchoolId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnalyzerAccessGrants_UserId",
                table: "StudentAnalyzerAccessGrants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnalyzerReports_CreatedByUserId",
                table: "StudentAnalyzerReports",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnalyzerReports_DeletedByUserId",
                table: "StudentAnalyzerReports",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnalyzerReports_SchoolId_CreatedAt",
                table: "StudentAnalyzerReports",
                columns: new[] { "SchoolId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnalyzerReports_SourceFileId_StudentName",
                table: "StudentAnalyzerReports",
                columns: new[] { "SourceFileId", "StudentName" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnalyzerSourceFiles_DeletedByUserId",
                table: "StudentAnalyzerSourceFiles",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnalyzerSourceFiles_SchoolId_Sha256",
                table: "StudentAnalyzerSourceFiles",
                columns: new[] { "SchoolId", "Sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnalyzerSourceFiles_SchoolId_UploadedAt",
                table: "StudentAnalyzerSourceFiles",
                columns: new[] { "SchoolId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnalyzerSourceFiles_UploadedByUserId",
                table: "StudentAnalyzerSourceFiles",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolStudentAnalyzerSettings");

            migrationBuilder.DropTable(
                name: "StudentAnalyzerAccessGrants");

            migrationBuilder.DropTable(
                name: "StudentAnalyzerReports");

            migrationBuilder.DropTable(
                name: "StudentAnalyzerSourceFiles");
        }
    }
}
