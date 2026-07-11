using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2SchoolUserManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "UserSchoolRoles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "UserSchoolRoles",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "UserSchoolRoles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "UserSchoolRoles",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByUserId",
                table: "UserSchoolRoles",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "Users",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Schools",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "Schools",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Schools",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_UserSchoolRoles_DeletedByUserId",
                table: "UserSchoolRoles",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSchoolRoles_IsDeleted",
                table: "UserSchoolRoles",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_UserSchoolRoles_UpdatedByUserId",
                table: "UserSchoolRoles",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeletedByUserId",
                table: "Users",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsDeleted",
                table: "Users",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_DeletedByUserId",
                table: "Schools",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_IsDeleted",
                table: "Schools",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_Name_City_LocationDetails",
                table: "Schools",
                columns: new[] { "Name", "City", "LocationDetails" });

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_Users_DeletedByUserId",
                table: "Schools",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_DeletedByUserId",
                table: "Users",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSchoolRoles_Users_DeletedByUserId",
                table: "UserSchoolRoles",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSchoolRoles_Users_UpdatedByUserId",
                table: "UserSchoolRoles",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Schools_Users_DeletedByUserId",
                table: "Schools");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_DeletedByUserId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSchoolRoles_Users_DeletedByUserId",
                table: "UserSchoolRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSchoolRoles_Users_UpdatedByUserId",
                table: "UserSchoolRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserSchoolRoles_DeletedByUserId",
                table: "UserSchoolRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserSchoolRoles_IsDeleted",
                table: "UserSchoolRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserSchoolRoles_UpdatedByUserId",
                table: "UserSchoolRoles");

            migrationBuilder.DropIndex(
                name: "IX_Users_DeletedByUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_IsActive",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_IsDeleted",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Schools_DeletedByUserId",
                table: "Schools");

            migrationBuilder.DropIndex(
                name: "IX_Schools_IsDeleted",
                table: "Schools");

            migrationBuilder.DropIndex(
                name: "IX_Schools_Name_City_LocationDetails",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "UserSchoolRoles");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "UserSchoolRoles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "UserSchoolRoles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "UserSchoolRoles");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "UserSchoolRoles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Schools");
        }
    }
}
