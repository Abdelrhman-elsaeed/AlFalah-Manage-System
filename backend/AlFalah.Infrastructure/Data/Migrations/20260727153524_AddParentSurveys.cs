using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParentSurveys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParentSurveys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Arabic_CI_AS"),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, collation: "Arabic_CI_AS"),
                    IsTemplate = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PublicToken = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentSurveys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentSurveys_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentSurveys_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentSurveyItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentSurveyId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, collation: "Arabic_CI_AS"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentSurveyItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentSurveyItems_ParentSurveys_ParentSurveyId",
                        column: x => x.ParentSurveyId,
                        principalTable: "ParentSurveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentSurveySubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentSurveyId = table.Column<int>(type: "int", nullable: false),
                    ParentName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, collation: "Arabic_CI_AS"),
                    MobileNumber = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentSurveySubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentSurveySubmissions_ParentSurveys_ParentSurveyId",
                        column: x => x.ParentSurveyId,
                        principalTable: "ParentSurveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentSurveyAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentSurveySubmissionId = table.Column<int>(type: "int", nullable: false),
                    ParentSurveyItemId = table.Column<int>(type: "int", nullable: false),
                    ItemTextSnapshot = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, collation: "Arabic_CI_AS"),
                    SubmittedRating = table.Column<int>(type: "int", nullable: false),
                    EffectiveRating = table.Column<int>(type: "int", nullable: false),
                    WeakReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, collation: "Arabic_CI_AS"),
                    WasAutoAdjusted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentSurveyAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentSurveyAnswers_ParentSurveyItems_ParentSurveyItemId",
                        column: x => x.ParentSurveyItemId,
                        principalTable: "ParentSurveyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentSurveyAnswers_ParentSurveySubmissions_ParentSurveySubmissionId",
                        column: x => x.ParentSurveySubmissionId,
                        principalTable: "ParentSurveySubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentSurveyAnswers_EffectiveRating",
                table: "ParentSurveyAnswers",
                column: "EffectiveRating");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSurveyAnswers_ParentSurveyItemId",
                table: "ParentSurveyAnswers",
                column: "ParentSurveyItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSurveyAnswers_ParentSurveySubmissionId_ParentSurveyItemId",
                table: "ParentSurveyAnswers",
                columns: new[] { "ParentSurveySubmissionId", "ParentSurveyItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentSurveyItems_IsDeleted",
                table: "ParentSurveyItems",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSurveyItems_ParentSurveyId_SortOrder",
                table: "ParentSurveyItems",
                columns: new[] { "ParentSurveyId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ParentSurveys_CreatedByUserId",
                table: "ParentSurveys",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSurveys_IsDeleted",
                table: "ParentSurveys",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSurveys_PublicToken",
                table: "ParentSurveys",
                column: "PublicToken",
                unique: true,
                filter: "[PublicToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSurveys_SchoolId_IsTemplate_Status",
                table: "ParentSurveys",
                columns: new[] { "SchoolId", "IsTemplate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ParentSurveySubmissions_MobileNumber",
                table: "ParentSurveySubmissions",
                column: "MobileNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSurveySubmissions_ParentSurveyId_SubmittedAt",
                table: "ParentSurveySubmissions",
                columns: new[] { "ParentSurveyId", "SubmittedAt" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParentSurveyAnswers");

            migrationBuilder.DropTable(
                name: "ParentSurveyItems");

            migrationBuilder.DropTable(
                name: "ParentSurveySubmissions");

            migrationBuilder.DropTable(
                name: "ParentSurveys");
        }
    }
}
