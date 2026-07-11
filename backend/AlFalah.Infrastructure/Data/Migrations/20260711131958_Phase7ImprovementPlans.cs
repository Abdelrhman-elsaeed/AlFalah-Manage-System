using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase7ImprovementPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImprovementPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    InstructorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    VisitId = table.Column<int>(type: "int", nullable: false),
                    DomainId = table.Column<int>(type: "int", nullable: true),
                    Goal = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, collation: "Arabic_CI_AS"),
                    Actions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false, collation: "Arabic_CI_AS"),
                    StartDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SuccessIndicators = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, collation: "Arabic_CI_AS"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImprovementPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImprovementPlans_RubricDomains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "RubricDomains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImprovementPlans_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImprovementPlans_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImprovementPlans_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImprovementPlans_Users_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImprovementPlans_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImprovementPlans_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanFollowUps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImprovementPlanId = table.Column<int>(type: "int", nullable: false),
                    FollowDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProgressNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, collation: "Arabic_CI_AS"),
                    EvidenceNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, collation: "Arabic_CI_AS"),
                    ProgressScore = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFollowUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanFollowUps_ImprovementPlans_ImprovementPlanId",
                        column: x => x.ImprovementPlanId,
                        principalTable: "ImprovementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanFollowUps_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanFollowUps_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanFollowUps_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_CreatedByUserId",
                table: "ImprovementPlans",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_DeletedByUserId",
                table: "ImprovementPlans",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_DomainId",
                table: "ImprovementPlans",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_InstructorId",
                table: "ImprovementPlans",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_IsDeleted",
                table: "ImprovementPlans",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_SchoolId",
                table: "ImprovementPlans",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_SchoolId_Status",
                table: "ImprovementPlans",
                columns: new[] { "SchoolId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_Status",
                table: "ImprovementPlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_UpdatedByUserId",
                table: "ImprovementPlans",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementPlans_VisitId",
                table: "ImprovementPlans",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFollowUps_CreatedByUserId",
                table: "PlanFollowUps",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFollowUps_DeletedByUserId",
                table: "PlanFollowUps",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFollowUps_FollowDate",
                table: "PlanFollowUps",
                column: "FollowDate");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFollowUps_ImprovementPlanId",
                table: "PlanFollowUps",
                column: "ImprovementPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFollowUps_IsDeleted",
                table: "PlanFollowUps",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFollowUps_UpdatedByUserId",
                table: "PlanFollowUps",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanFollowUps");

            migrationBuilder.DropTable(
                name: "ImprovementPlans");
        }
    }
}
