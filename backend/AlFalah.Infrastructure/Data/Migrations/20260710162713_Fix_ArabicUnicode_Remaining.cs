using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// D-31 — additive, no data loss.
    /// Audits every Arabic-text column across all entities and applies
    /// Arabic_CI_AS collation so the columns render Arabic glyphs in
    /// sqlcmd, SSMS, and any non-Unicode-aware client. The actual
    /// storage bytes have ALWAYS been nvarchar (Unicode) — the '?'
    /// rendering of seeded school #1 in earlier sqlcmd snapshots was
    /// purely a display-side artifact of the default
    /// SQL_Latin1_General_CP1_CI_AS collation.
    ///
    /// Rows already corrupted at INSERT time (replaced with 0x3F '?')
    /// are UNRECOVERABLE and must be re-entered by the user.
    ///
    /// Follow-up to D-30's Fix_ArabicUnicodeColumns (20260710151422)
    /// which added collation to School.Name/City/LocationDetails only.
    /// </summary>
    public partial class Fix_ArabicUnicode_Remaining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── Users (ApplicationUser) ────────────────────────────────────
            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // ─── Rubric (Phase 3) ───────────────────────────────────────────
            migrationBuilder.AlterColumn<string>(
                name: "NameAr",
                table: "RubricDomains",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "TextAr",
                table: "RubricStandards",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "RubricVersions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            // ─── Visits (Phase 4) ───────────────────────────────────────────
            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "Visits",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GradeClass",
                table: "Visits",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Visits",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EvidenceNote",
                table: "VisitScores",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PerformanceLevelAr",
                table: "VisitAnalyses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "DomainNameAr",
                table: "VisitDomainAverages",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            // ─── Reports / signatures / instructor / roles / permissions ──
            migrationBuilder.AlterColumn<string>(
                name: "ReportHeaderText",
                table: "SchoolReportSettings",
                type: "nvarchar(max)",
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReportFooterText",
                table: "SchoolReportSettings",
                type: "nvarchar(max)",
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "UserSignatures",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SubjectSpecialization",
                table: "InstructorProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "QualificationAr",
                table: "InstructorProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DescriptionAr",
                table: "Permissions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DescriptionAr",
                table: "Roles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "AuditLogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                collation: "Arabic_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "NameAr",
                table: "RubricDomains",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "TextAr",
                table: "RubricStandards",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "RubricVersions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "Visits",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "GradeClass",
                table: "Visits",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Visits",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "EvidenceNote",
                table: "VisitScores",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "PerformanceLevelAr",
                table: "VisitAnalyses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "DomainNameAr",
                table: "VisitDomainAverages",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "ReportHeaderText",
                table: "SchoolReportSettings",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "ReportFooterText",
                table: "SchoolReportSettings",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "UserSignatures",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "SubjectSpecialization",
                table: "InstructorProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "QualificationAr",
                table: "InstructorProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "DescriptionAr",
                table: "Permissions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "DescriptionAr",
                table: "Roles",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldCollation: "Arabic_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "AuditLogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true,
                oldCollation: "Arabic_CI_AS");
        }
    }
}