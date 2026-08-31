using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGatePassWorkflowAndDomainOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GatePasses_SchoolId_RequestedByGuardianProfileId",
                table: "GatePasses");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedWindowEndsAt",
                table: "GatePasses",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedWindowStartsAt",
                table: "GatePasses",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentClassroomId",
                table: "GatePasses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExitGateNote",
                table: "GatePasses",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                collation: "Arabic_CI_AS");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "GatePasses",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [GatePasses] SET [IdempotencyKey] = CONCAT('legacy-', [Id]) WHERE [IdempotencyKey] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                table: "GatePasses",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldUnicode: false,
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_SchoolId_CurrentClassroomId",
                table: "GatePasses",
                columns: new[] { "SchoolId", "CurrentClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_SchoolId_RequestedByGuardianProfileId_IdempotencyKey",
                table: "GatePasses",
                columns: new[] { "SchoolId", "RequestedByGuardianProfileId", "IdempotencyKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GatePasses_ApprovedWindow",
                table: "GatePasses",
                sql: "([ApprovedWindowStartsAt] IS NULL AND [ApprovedWindowEndsAt] IS NULL) OR ([ApprovedWindowStartsAt] IS NOT NULL AND [ApprovedWindowEndsAt] > [ApprovedWindowStartsAt])");

            migrationBuilder.AddForeignKey(
                name: "FK_GatePasses_Classrooms_SchoolId_CurrentClassroomId",
                table: "GatePasses",
                columns: new[] { "SchoolId", "CurrentClassroomId" },
                principalTable: "Classrooms",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GatePasses_Classrooms_SchoolId_CurrentClassroomId",
                table: "GatePasses");

            migrationBuilder.DropIndex(
                name: "IX_GatePasses_SchoolId_CurrentClassroomId",
                table: "GatePasses");

            migrationBuilder.DropIndex(
                name: "IX_GatePasses_SchoolId_RequestedByGuardianProfileId_IdempotencyKey",
                table: "GatePasses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GatePasses_ApprovedWindow",
                table: "GatePasses");

            migrationBuilder.DropColumn(
                name: "ApprovedWindowEndsAt",
                table: "GatePasses");

            migrationBuilder.DropColumn(
                name: "ApprovedWindowStartsAt",
                table: "GatePasses");

            migrationBuilder.DropColumn(
                name: "CurrentClassroomId",
                table: "GatePasses");

            migrationBuilder.DropColumn(
                name: "ExitGateNote",
                table: "GatePasses");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "GatePasses");

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_SchoolId_RequestedByGuardianProfileId",
                table: "GatePasses",
                columns: new[] { "SchoolId", "RequestedByGuardianProfileId" });
        }
    }
}
