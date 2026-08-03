using AlFalah.Application.DTOs.StudentAnalyzer;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Analysis;

public sealed class StudentAnalyzerQueryTranslationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("name")]
    [InlineData("size")]
    [InlineData("analysisCount")]
    public void File_library_page_query_translates_to_sql_server(string? sortBy)
    {
        using var context = CreateContext();
        var request = new StudentAnalyzerFileQuery { SortBy = sortBy, Page = 1, PageSize = 10 };

        var sql = StudentAnalyzerService
            .BuildFilePageQuery(context.StudentAnalyzerSourceFiles.AsNoTracking(), request)
            .ToQueryString();

        sql.Should().Contain("ORDER BY");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("studentName")]
    [InlineData("grantTotal")]
    [InlineData("deductionTotal")]
    public void Report_library_page_query_translates_to_sql_server(string? sortBy)
    {
        using var context = CreateContext();
        var request = new StudentAnalyzerReportQuery { SortBy = sortBy, Page = 1, PageSize = 10 };

        var sql = StudentAnalyzerService
            .BuildReportPageQuery(context.StudentAnalyzerReports.AsNoTracking(), request)
            .ToQueryString();

        sql.Should().Contain("ORDER BY");
    }

    private static AlFalahDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=AlFalahQueryTranslationTests;Trusted_Connection=True;")
            .Options;
        return new AlFalahDbContext(options);
    }
}
