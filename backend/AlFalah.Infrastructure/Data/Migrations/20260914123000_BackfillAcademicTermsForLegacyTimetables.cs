using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations;

[DbContext(typeof(AlFalahDbContext))]
[Migration("20260914123000_BackfillAcademicTermsForLegacyTimetables")]
public partial class BackfillAcademicTermsForLegacyTimetables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ;WITH LegacySchoolYears AS
            (
                SELECT
                    timetable.SchoolId,
                    timetable.AcademicYearId,
                    academicYear.StartsOn AS YearStartsOn,
                    academicYear.EndsOn AS YearEndsOn,
                    MAX(timetable.CreatedByUserId) AS ActorUserId
                FROM SchoolTimetables AS timetable
                INNER JOIN AcademicYears AS academicYear ON academicYear.Id = timetable.AcademicYearId
                WHERE timetable.IsDeleted = 0
                GROUP BY
                    timetable.SchoolId,
                    timetable.AcademicYearId,
                    academicYear.StartsOn,
                    academicYear.EndsOn
            ),
            RequiredScopes AS
            (
                SELECT
                    schoolYear.SchoolId,
                    schoolYear.AcademicYearId,
                    semester.Value AS Semester,
                    schoolYear.ActorUserId,
                    schoolYear.YearStartsOn,
                    schoolYear.YearEndsOn,
                    CASE
                        WHEN DATEFROMPARTS(YEAR(schoolYear.YearEndsOn), 1, 1) > schoolYear.YearStartsOn
                            THEN DATEFROMPARTS(YEAR(schoolYear.YearEndsOn), 1, 1)
                        ELSE DATEADD(DAY, DATEDIFF(DAY, schoolYear.YearStartsOn, schoolYear.YearEndsOn) / 2, schoolYear.YearStartsOn)
                    END AS SecondSemesterStartsOn
                FROM LegacySchoolYears AS schoolYear
                CROSS JOIN (VALUES (1), (2)) AS semester(Value)
            ),
            MissingScopes AS
            (
                SELECT
                    scope.*,
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY scope.SchoolId
                        ORDER BY
                            CASE
                                WHEN CAST(SYSUTCDATETIME() AS date) BETWEEN scope.YearStartsOn AND scope.YearEndsOn THEN 0
                                ELSE 1
                            END,
                            scope.YearStartsOn DESC,
                            CASE
                                WHEN CAST(SYSUTCDATETIME() AS date) BETWEEN scope.YearStartsOn AND scope.YearEndsOn
                                 AND ((scope.Semester = 1 AND CAST(SYSUTCDATETIME() AS date) < scope.SecondSemesterStartsOn)
                                      OR (scope.Semester = 2 AND CAST(SYSUTCDATETIME() AS date) >= scope.SecondSemesterStartsOn))
                                    THEN 0
                                WHEN CAST(SYSUTCDATETIME() AS date) NOT BETWEEN scope.YearStartsOn AND scope.YearEndsOn
                                 AND scope.Semester = 1
                                    THEN 0
                                ELSE 1
                            END,
                            scope.Semester
                    ) AS ActiveRank
                FROM RequiredScopes AS scope
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM AcademicTerms AS existing
                    WHERE existing.SchoolId = scope.SchoolId
                      AND existing.AcademicYearId = scope.AcademicYearId
                      AND existing.Semester = scope.Semester
                      AND existing.IsDeleted = 0
                )
            )
            INSERT INTO AcademicTerms
            (
                SchoolId,
                AcademicYearId,
                Semester,
                StartsOn,
                EndsOn,
                IsActive,
                CreatedAt,
                CreatedByUserId,
                UpdatedAt,
                UpdatedByUserId,
                IsDeleted,
                DeletedAt,
                DeletedByUserId
            )
            SELECT
                scope.SchoolId,
                scope.AcademicYearId,
                scope.Semester,
                CASE WHEN scope.Semester = 1 THEN scope.YearStartsOn ELSE scope.SecondSemesterStartsOn END,
                CASE WHEN scope.Semester = 1 THEN DATEADD(DAY, -1, scope.SecondSemesterStartsOn) ELSE scope.YearEndsOn END,
                CASE
                    WHEN scope.ActiveRank = 1
                     AND NOT EXISTS
                     (
                         SELECT 1
                         FROM AcademicTerms AS activeTerm
                         WHERE activeTerm.SchoolId = scope.SchoolId
                           AND activeTerm.IsActive = 1
                           AND activeTerm.IsDeleted = 0
                     )
                     AND
                     (
                         (CAST(SYSUTCDATETIME() AS date) BETWEEN scope.YearStartsOn AND scope.YearEndsOn
                          AND ((scope.Semester = 1 AND CAST(SYSUTCDATETIME() AS date) < scope.SecondSemesterStartsOn)
                               OR (scope.Semester = 2 AND CAST(SYSUTCDATETIME() AS date) >= scope.SecondSemesterStartsOn)))
                         OR
                         (CAST(SYSUTCDATETIME() AS date) NOT BETWEEN scope.YearStartsOn AND scope.YearEndsOn
                          AND scope.Semester = 1)
                     )
                    THEN 1 ELSE 0
                END,
                SYSDATETIMEOFFSET(),
                scope.ActorUserId,
                SYSDATETIMEOFFSET(),
                scope.ActorUserId,
                0,
                NULL,
                NULL
            FROM MissingScopes AS scope;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally left empty: the backfilled terms become school-owned academic data.
    }
}
