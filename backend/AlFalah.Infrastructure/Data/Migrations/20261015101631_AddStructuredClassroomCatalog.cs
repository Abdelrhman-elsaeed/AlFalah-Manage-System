using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredClassroomCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InstructorClasses_InstructorProfileId_ClassLabel",
                table: "InstructorClasses");

            migrationBuilder.AddColumn<int>(
                name: "TeachingRevision",
                table: "InstructorProfiles",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ClassroomId",
                table: "InstructorClasses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "InstructorClasses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionNumber",
                table: "Classrooms",
                type: "int",
                nullable: true);

            // Preserve every existing ID and display label. Only unambiguous numeric
            // sections and exact current-year teacher labels are eligible for backfill.
            // Letter-to-number conversion is an explicit manager decision in the UI.
            migrationBuilder.Sql("""
                ;WITH normalized AS (
                  SELECT Id, SchoolId, AcademicYearId, Stage, GradeLevel,
                    TRANSLATE(LTRIM(RTRIM(Section)), N'٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹', N'01234567890123456789') AS digits
                  FROM Classrooms WHERE IsDeleted=0
                ), numeric_sections AS (
                  SELECT *, TRY_CONVERT(int,digits) AS number FROM normalized
                  WHERE digits<>N'' AND digits COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^0-9]%'
                ), candidates AS (
                  SELECT *, COUNT(*) OVER(PARTITION BY SchoolId,AcademicYearId,Stage,GradeLevel,number) AS matches
                  FROM numeric_sections WHERE number>0
                )
                UPDATE c SET SectionNumber=x.number FROM Classrooms c JOIN candidates x ON c.Id=x.Id
                WHERE x.matches=1;

                UPDATE ic SET SchoolId=p.SchoolId FROM InstructorClasses ic JOIN InstructorProfiles p ON p.Id=ic.InstructorProfileId;
                ;WITH current_years AS (
                  SELECT t.SchoolId, MIN(t.AcademicYearId) AS AcademicYearId FROM AcademicTerms t
                  JOIN AcademicYears y ON y.Id=t.AcademicYearId AND y.IsActive=1
                  WHERE t.IsActive=1 AND t.IsDeleted=0
                  GROUP BY t.SchoolId HAVING COUNT(DISTINCT t.AcademicYearId)=1
                ), exact_matches AS (
                  SELECT ic.Id, MIN(c.Id) AS ClassroomId FROM InstructorClasses ic
                  JOIN current_years y ON y.SchoolId=ic.SchoolId
                  JOIN Classrooms c ON c.SchoolId=y.SchoolId AND c.AcademicYearId=y.AcademicYearId
                    AND c.ClassLabel=ic.ClassLabel AND c.IsDeleted=0 AND c.IsActive=1
                  WHERE ic.IsDeleted=0 GROUP BY ic.Id HAVING COUNT(*)=1
                )
                UPDATE ic SET ClassroomId=x.ClassroomId FROM InstructorClasses ic JOIN exact_matches x ON x.Id=ic.Id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_InstructorClasses_InstructorProfileId_ClassLabel",
                table: "InstructorClasses",
                columns: new[] { "InstructorProfileId", "ClassLabel" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [ClassroomId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorClasses_InstructorProfileId_ClassroomId",
                table: "InstructorClasses",
                columns: new[] { "InstructorProfileId", "ClassroomId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [ClassroomId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorClasses_SchoolId_ClassroomId",
                table: "InstructorClasses",
                columns: new[] { "SchoolId", "ClassroomId" });

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_SchoolId_AcademicYearId_Stage_GradeLevel_SectionNumber",
                table: "Classrooms",
                columns: new[] { "SchoolId", "AcademicYearId", "Stage", "GradeLevel", "SectionNumber" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [SectionNumber] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_InstructorClasses_Classrooms_SchoolId_ClassroomId",
                table: "InstructorClasses",
                columns: new[] { "SchoolId", "ClassroomId" },
                principalTable: "Classrooms",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS(SELECT 1 FROM InstructorClasses WHERE ClassroomId IS NOT NULL)
                  OR EXISTS(SELECT 1 FROM Classrooms WHERE SectionNumber IS NOT NULL)
                  OR EXISTS(SELECT 1 FROM InstructorProfiles WHERE TeachingRevision>1)
                  THROW 51000, 'Export and review structured classroom identities before rollback. No data was removed.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_InstructorClasses_Classrooms_SchoolId_ClassroomId",
                table: "InstructorClasses");

            migrationBuilder.DropIndex(
                name: "IX_InstructorClasses_InstructorProfileId_ClassLabel",
                table: "InstructorClasses");

            migrationBuilder.DropIndex(
                name: "IX_InstructorClasses_InstructorProfileId_ClassroomId",
                table: "InstructorClasses");

            migrationBuilder.DropIndex(
                name: "IX_InstructorClasses_SchoolId_ClassroomId",
                table: "InstructorClasses");

            migrationBuilder.DropIndex(
                name: "IX_Classrooms_SchoolId_AcademicYearId_Stage_GradeLevel_SectionNumber",
                table: "Classrooms");

            migrationBuilder.DropColumn(
                name: "TeachingRevision",
                table: "InstructorProfiles");

            migrationBuilder.DropColumn(
                name: "ClassroomId",
                table: "InstructorClasses");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "InstructorClasses");

            migrationBuilder.DropColumn(
                name: "SectionNumber",
                table: "Classrooms");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorClasses_InstructorProfileId_ClassLabel",
                table: "InstructorClasses",
                columns: new[] { "InstructorProfileId", "ClassLabel" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
