using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AutomatedEvidenceMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeacherEvidenceSubmissions_TeacherId_DriveItemId",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.AddColumn<int>(
                name: "AcademicYearId",
                table: "TeacherEvidenceSubmissions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "TeacherEvidenceSubmissions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "TeacherEvidenceSubmissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TeacherEvidenceSubmissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMissingFromDrive",
                table: "TeacherEvidenceSubmissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MissingFromDriveAtUtc",
                table: "TeacherEvidenceSubmissions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "TeacherEvidenceSubmissions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAtUtc",
                table: "TeacherEvidenceSubmissions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "TeacherEvidenceSubmissions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AcademicYears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CategorySortOrder = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceUploadOperations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmissionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceUploadOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeacherTaskStatuses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    ActiveFilesCount = table.Column<int>(type: "int", nullable: false),
                    CellStatus = table.Column<int>(type: "int", nullable: false),
                    LastSubmissionAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastReviewedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherTaskStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherTaskStatuses_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherTaskStatuses_EvidenceTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "EvidenceTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherTaskStatuses_InstructorProfiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "InstructorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherTaskStatuses_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AcademicYears",
                columns: new[] { "Id", "Code", "EndsOn", "IsActive", "NameAr", "StartsOn" },
                values: new object[] { 1, "2026-2027", new DateOnly(2027, 7, 31), true, "العام الدراسي 2026-2027", new DateOnly(2026, 8, 1) });

            migrationBuilder.InsertData(
                table: "EvidenceTasks",
                columns: new[] { "Id", "Category", "CategorySortOrder", "Code", "IsActive", "NameAr", "SortOrder" },
                values: new object[,]
                {
                    { 1, "السيرة الذاتية", 1, "CV-01", true, "البيانات الأساسية", 1 },
                    { 2, "السيرة الذاتية", 1, "CV-02", true, "الإنجازات الشخصية", 2 },
                    { 3, "السيرة الذاتية", 1, "CV-03", true, "التكريمات", 3 },
                    { 4, "السيرة الذاتية", 1, "CV-04", true, "الخبرات", 4 },
                    { 5, "السيرة الذاتية", 1, "CV-05", true, "الدورات التدريبية", 5 },
                    { 6, "السيرة الذاتية", 1, "CV-06", true, "الرخصة المهنية", 6 },
                    { 7, "السيرة الذاتية", 1, "CV-07", true, "الشهادات التدريبية", 7 },
                    { 8, "السيرة الذاتية", 1, "CV-08", true, "ملف إنجاز المعلم", 8 },
                    { 9, "المجتمعات المهنية", 2, "PC-01", true, "تبادل الزيارات", 1 },
                    { 10, "المجتمعات المهنية", 2, "PC-02", true, "الخبرات المهنية", 2 },
                    { 11, "المجتمعات المهنية", 2, "PC-03", true, "مبادرات المعلم", 3 },
                    { 12, "المواد الإثرائية", 3, "EN-01", true, "الأنشطة الصفية", 1 },
                    { 13, "المواد الإثرائية", 3, "EN-02", true, "تصميمات ورسومات", 2 },
                    { 14, "المواد الإثرائية", 3, "EN-03", true, "عروض تقديمية", 3 },
                    { 15, "المواد الإثرائية", 3, "EN-04", true, "مواقع ومنصات", 4 },
                    { 16, "المواد الإثرائية", 3, "EN-05", true, "أوراق العمل", 5 },
                    { 17, "الخطط العلاجية", 4, "RP-01", true, "الفاقد التعليمي", 1 },
                    { 18, "الخطط العلاجية", 4, "RP-02", true, "خطة الطلاب المتعثرين والضعاف", 2 },
                    { 19, "الخطط العلاجية", 4, "RP-03", true, "خطة الطلاب المتفوقين والموهوبين", 3 },
                    { 20, "التقويم", 5, "AS-01", true, "اختبارات قصيرة لكل وحدة", 1 },
                    { 21, "التقويم", 5, "AS-02", true, "بحوث ومشاريع للطلاب", 2 },
                    { 22, "التقويم", 5, "AS-03", true, "تحليل النتائج", 3 },
                    { 23, "حقيبة الطالب", 6, "SP-01", true, "تحفيز الطلاب وتشجيعهم", 1 },
                    { 24, "حقيبة الطالب", 6, "SP-02", true, "سجل المتابعة للطلاب", 2 },
                    { 25, "حقيبة الطالب", 6, "SP-03", true, "شواهد من التواصل الأسري", 3 },
                    { 26, "حقيبة الطالب", 6, "SP-04", true, "عينة من أنشطة الطالب", 4 },
                    { 27, "حقيبة الطالب", 6, "SP-05", true, "كشوف رصد الدرجات", 5 },
                    { 28, "حقيبة الطالب", 6, "SP-06", true, "ملفات إنجاز الطالب", 6 },
                    { 29, "حقيبة المنهج", 7, "CP-01", true, "استراتيجيات التعلم النشط وشواهد", 1 },
                    { 30, "حقيبة المنهج", 7, "CP-02", true, "الخطة الأسبوعية لكل فصل دراسي", 2 },
                    { 31, "حقيبة المنهج", 7, "CP-03", true, "توزيع المنهج", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherEvidenceSubmissions_AcademicYearId",
                table: "TeacherEvidenceSubmissions",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherEvidenceSubmissions_DriveId_DriveItemId",
                table: "TeacherEvidenceSubmissions",
                columns: new[] { "DriveId", "DriveItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherEvidenceSubmissions_TaskId",
                table: "TeacherEvidenceSubmissions",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherEvidenceSubmissions_TeacherId_AcademicYearId_TaskId_IsDeleted",
                table: "TeacherEvidenceSubmissions",
                columns: new[] { "TeacherId", "AcademicYearId", "TaskId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_Code",
                table: "AcademicYears",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceTasks_Code",
                table: "EvidenceTasks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceTasks_IsActive_CategorySortOrder_SortOrder",
                table: "EvidenceTasks",
                columns: new[] { "IsActive", "CategorySortOrder", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceUploadOperations_Status_UpdatedAtUtc",
                table: "EvidenceUploadOperations",
                columns: new[] { "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceUploadOperations_TeacherId_RequestId",
                table: "EvidenceUploadOperations",
                columns: new[] { "TeacherId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherTaskStatuses_AcademicYearId",
                table: "TeacherTaskStatuses",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherTaskStatuses_SchoolId_AcademicYearId_CellStatus",
                table: "TeacherTaskStatuses",
                columns: new[] { "SchoolId", "AcademicYearId", "CellStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherTaskStatuses_TaskId",
                table: "TeacherTaskStatuses",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherTaskStatuses_TeacherId_TaskId_AcademicYearId",
                table: "TeacherTaskStatuses",
                columns: new[] { "TeacherId", "TaskId", "AcademicYearId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherEvidenceSubmissions_AcademicYears_AcademicYearId",
                table: "TeacherEvidenceSubmissions",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherEvidenceSubmissions_EvidenceTasks_TaskId",
                table: "TeacherEvidenceSubmissions",
                column: "TaskId",
                principalTable: "EvidenceTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeacherEvidenceSubmissions_AcademicYears_AcademicYearId",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_TeacherEvidenceSubmissions_EvidenceTasks_TaskId",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropTable(
                name: "EvidenceUploadOperations");

            migrationBuilder.DropTable(
                name: "TeacherTaskStatuses");

            migrationBuilder.DropTable(
                name: "AcademicYears");

            migrationBuilder.DropTable(
                name: "EvidenceTasks");

            migrationBuilder.DropIndex(
                name: "IX_TeacherEvidenceSubmissions_AcademicYearId",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_TeacherEvidenceSubmissions_DriveId_DriveItemId",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_TeacherEvidenceSubmissions_TaskId",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_TeacherEvidenceSubmissions_TeacherId_AcademicYearId_TaskId_IsDeleted",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropColumn(
                name: "IsMissingFromDrive",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropColumn(
                name: "MissingFromDriveAtUtc",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "TeacherEvidenceSubmissions");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherEvidenceSubmissions_TeacherId_DriveItemId",
                table: "TeacherEvidenceSubmissions",
                columns: new[] { "TeacherId", "DriveItemId" },
                unique: true);
        }
    }
}
