using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5ApprovalVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "Visits",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserId",
                table: "Visits",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Visits",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                collation: "Arabic_CI_AS");

            migrationBuilder.AddColumn<string>(
                name: "ReopenReason",
                table: "Visits",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                collation: "Arabic_CI_AS");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReopenedAt",
                table: "Visits",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReopenedByUserId",
                table: "Visits",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReportViewLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitId = table.Column<int>(type: "int", nullable: false),
                    InstructorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ViewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportViewLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportViewLogs_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_ApprovedByUserId",
                table: "Visits",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_ReopenedByUserId",
                table: "Visits",
                column: "ReopenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportViewLogs_InstructorUserId",
                table: "ReportViewLogs",
                column: "InstructorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportViewLogs_IsDeleted",
                table: "ReportViewLogs",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ReportViewLogs_ViewedAt",
                table: "ReportViewLogs",
                column: "ViewedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReportViewLogs_VisitId",
                table: "ReportViewLogs",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportViewLogs_VisitId_ViewedAt",
                table: "ReportViewLogs",
                columns: new[] { "VisitId", "ViewedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Visits_Users_ApprovedByUserId",
                table: "Visits",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Visits_Users_ReopenedByUserId",
                table: "Visits",
                column: "ReopenedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Visits_Users_ApprovedByUserId",
                table: "Visits");

            migrationBuilder.DropForeignKey(
                name: "FK_Visits_Users_ReopenedByUserId",
                table: "Visits");

            migrationBuilder.DropTable(
                name: "ReportViewLogs");

            migrationBuilder.DropIndex(
                name: "IX_Visits_ApprovedByUserId",
                table: "Visits");

            migrationBuilder.DropIndex(
                name: "IX_Visits_ReopenedByUserId",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "ReopenReason",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "ReopenedAt",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "ReopenedByUserId",
                table: "Visits");
        }
    }
}
