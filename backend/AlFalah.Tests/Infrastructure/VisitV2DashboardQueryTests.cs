using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Infrastructure;

public sealed class VisitV2DashboardQueryTests
{
    [Fact]
    public async Task Dashboard_aggregation_executes_on_a_relational_provider_with_and_without_rows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AlFalahDbContext(options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "Visits" (
                "Id" INTEGER PRIMARY KEY,
                "ExperienceVersion" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "SchoolId" INTEGER NOT NULL,
                "CreatedByUserId" TEXT NOT NULL,
                "InstructorId" TEXT NOT NULL,
                "RubricVersionId" INTEGER NOT NULL DEFAULT 0,
                "VisitCategory" INTEGER NOT NULL DEFAULT 1,
                "VisitSequence" INTEGER NOT NULL DEFAULT 1,
                "VisitDate" TEXT NOT NULL DEFAULT '',
                "Subject" TEXT NULL,
                "GradeClass" TEXT NULL,
                "LessonTitle" TEXT NULL,
                "PresentCount" INTEGER NOT NULL DEFAULT 0,
                "AbsentCount" INTEGER NOT NULL DEFAULT 0,
                "ClassroomPeriod" INTEGER NULL,
                "ScoringRuleSetVersion" INTEGER NOT NULL DEFAULT 1,
                "EvaluatorNameSnapshot" TEXT NULL,
                "EvaluatorRoleSnapshot" TEXT NULL,
                "Notes" TEXT NULL,
                "SubmittedAt" TEXT NULL,
                "ApprovedByUserId" TEXT NULL,
                "ApprovedAt" TEXT NULL,
                "RejectionReason" TEXT NULL,
                "ReopenReason" TEXT NULL,
                "ReopenedByUserId" TEXT NULL,
                "ReopenedAt" TEXT NULL,
                "CreatedAt" TEXT NOT NULL DEFAULT '',
                "UpdatedAt" TEXT NOT NULL DEFAULT '',
                "IsDeleted" INTEGER NOT NULL,
                "DeletedAt" TEXT NULL,
                "DeletedByUserId" TEXT NULL
            );
            CREATE TABLE "VisitAnalyses" (
                "Id" INTEGER PRIMARY KEY,
                "VisitId" INTEGER NOT NULL,
                "OverallScore" REAL NOT NULL DEFAULT 0,
                "TotalScore" REAL NOT NULL DEFAULT 0,
                "MaximumScore" REAL NOT NULL DEFAULT 0,
                "PerformanceLevelAr" TEXT NOT NULL DEFAULT '',
                "StrengthsJson" TEXT NOT NULL DEFAULT '[]',
                "ImprovementAreasJson" TEXT NOT NULL DEFAULT '[]',
                "PriorityStandardsJson" TEXT NOT NULL DEFAULT '[]',
                "RuleSetVersion" INTEGER NOT NULL DEFAULT 1,
                "OverallPercentage" INTEGER NULL,
                "ComputedAt" TEXT NOT NULL DEFAULT '',
                "IsDeleted" INTEGER NOT NULL,
                "DeletedAt" TEXT NULL,
                "DeletedByUserId" TEXT NULL
            );
            CREATE TABLE "VisitDomainAverages" (
                "Id" INTEGER PRIMARY KEY,
                "VisitAnalysisId" INTEGER NOT NULL,
                "DomainCode" TEXT NOT NULL,
                "DomainNameAr" TEXT NOT NULL,
                "PercentageScore" INTEGER NULL,
                "IsDeleted" INTEGER NOT NULL
            );
            CREATE TABLE "RubricStandards" (
                "Id" INTEGER PRIMARY KEY,
                "Code" TEXT NOT NULL,
                "TextAr" TEXT NOT NULL,
                "IsDeleted" INTEGER NOT NULL
            );
            CREATE TABLE "VisitScores" (
                "Id" INTEGER PRIMARY KEY,
                "VisitId" INTEGER NOT NULL,
                "RubricStandardId" INTEGER NOT NULL,
                "Score" REAL NULL,
                "IsDeleted" INTEGER NOT NULL
            );
            """);

        var result = await new VisitV2Repository(db)
            .GetDashboardAsync(schoolId: 18, creatorUserId: null, totalActiveTeachers: 0);

        result.TotalVisits.Should().Be(0);
        result.Evaluators.Should().BeEmpty();
        result.Domains.Should().BeEmpty();
        result.TopStandards.Should().BeEmpty();
        result.BottomStandards.Should().BeEmpty();

        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Visits"
                ("Id", "ExperienceVersion", "Status", "SchoolId", "CreatedByUserId", "InstructorId", "IsDeleted")
            VALUES (1, 2, 4, 18, 'manager', 'teacher', 0);
            INSERT INTO "VisitAnalyses"
                ("Id", "VisitId", "OverallPercentage", "IsDeleted")
            VALUES (1, 1, 73, 0);
            INSERT INTO "VisitDomainAverages"
                ("Id", "VisitAnalysisId", "DomainCode", "DomainNameAr", "PercentageScore", "IsDeleted")
            VALUES (1, 1, 'D1', 'بيئة التعلم', 73, 0);
            INSERT INTO "RubricStandards"
                ("Id", "Code", "TextAr", "IsDeleted")
            VALUES (1, 'D1-S1', 'معيار اختباري', 0);
            INSERT INTO "VisitScores"
                ("Id", "VisitId", "RubricStandardId", "Score", "IsDeleted")
            VALUES (1, 1, 1, 3, 0);
            """);

        result = await new VisitV2Repository(db)
            .GetDashboardAsync(schoolId: 18, creatorUserId: null, totalActiveTeachers: 1);

        result.TotalVisits.Should().Be(1);
        result.AverageOverallPercentage.Should().Be(73m);
        result.HighAchievementRate.Should().Be(100m);
        result.Evaluators.Should().ContainSingle().Which.Should().Match<AlFalah.Application.DTOs.Visits.VisitV2EvaluatorWorkloadDto>(
            item => item.UserId == "manager" && item.VisitCount == 1 && item.AveragePercentage == 73m);
        result.Domains.Should().ContainSingle().Which.AveragePercentage.Should().Be(73m);
        result.TopStandards.Should().ContainSingle().Which.AveragePercentage.Should().Be(75m);
        result.BottomStandards.Should().ContainSingle();
    }
}
