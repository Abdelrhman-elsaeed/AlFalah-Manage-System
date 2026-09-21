using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Data.Seeders;
using AlFalah.Tests.Fixtures.ClassroomVisitsV2;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Infrastructure;

public sealed class ClassroomVisitsV2SchemaTests
{
    [Fact]
    public async Task Seeder_creates_exact_inactive_v2_rubric_and_is_idempotent()
    {
        await using var db = CreateContext();
        var seeder = new RubricV2Seeder(db, NullLogger<RubricV2Seeder>.Instance);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var versions = await db.RubricVersions
            .Include(v => v.Domains)
                .ThenInclude(d => d.Standards)
                    .ThenInclude(s => s.Indicators)
            .Where(v => v.VersionNumber == 2)
            .ToListAsync();

        versions.Should().ContainSingle();
        var version = versions.Single();
        version.IsActive.Should().BeFalse("normal startup must not cut over the active rubric");
        version.Domains.Should().HaveCount(ClassroomVisitsV2GoldenFixtures.TotalDomainsCount);
        version.Domains.SelectMany(d => d.Standards).Should().HaveCount(ClassroomVisitsV2GoldenFixtures.TotalStandardsCount);
        version.Domains.SelectMany(d => d.Standards).SelectMany(s => s.Indicators)
            .Should().HaveCount(ClassroomVisitsV2GoldenFixtures.TotalIndicatorsCount);

        foreach (var expectedDomain in ClassroomVisitsV2GoldenFixtures.Domains)
        {
            var actualDomain = version.Domains.Single(d => d.SortOrder == expectedDomain.Id);
            actualDomain.NameAr.Should().Be(expectedDomain.Name);
            actualDomain.Standards.OrderBy(s => s.SortOrder).Select(s => s.TextAr)
                .Should().Equal(expectedDomain.Standards.Select(s => s.Text));
            actualDomain.Standards.OrderBy(s => s.SortOrder)
                .SelectMany(s => s.Indicators.OrderBy(i => i.SortOrder).Select(i => i.TextAr))
                .Should().Equal(expectedDomain.Standards.SelectMany(s => s.Indicators));
        }
    }

    [Fact]
    public async Task Seeder_restores_a_soft_deleted_v2_rubric_instead_of_skipping_it()
    {
        await using var db = CreateContext();
        var seeder = new RubricV2Seeder(db, NullLogger<RubricV2Seeder>.Instance);

        await seeder.SeedAsync();

        var version = await db.RubricVersions
            .Include(v => v.Domains)
                .ThenInclude(d => d.Standards)
                    .ThenInclude(s => s.Indicators)
            .SingleAsync(v => v.VersionNumber == 2);

        version.IsDeleted = true;
        version.DeletedAt = DateTimeOffset.UtcNow;
        foreach (var domain in version.Domains)
        {
            domain.IsDeleted = true;
            foreach (var standard in domain.Standards)
            {
                standard.IsDeleted = true;
                foreach (var indicator in standard.Indicators)
                    indicator.IsDeleted = true;
            }
        }
        await db.SaveChangesAsync();

        await seeder.SeedAsync();

        db.ChangeTracker.Clear();
        var restored = await db.RubricVersions
            .Include(v => v.Domains)
                .ThenInclude(d => d.Standards)
                    .ThenInclude(s => s.Indicators)
            .SingleAsync(v => v.VersionNumber == 2);

        restored.IsDeleted.Should().BeFalse();
        restored.Domains.Should().HaveCount(ClassroomVisitsV2GoldenFixtures.TotalDomainsCount);
        restored.Domains.SelectMany(d => d.Standards)
            .Should().HaveCount(ClassroomVisitsV2GoldenFixtures.TotalStandardsCount);
        restored.Domains.SelectMany(d => d.Standards).SelectMany(s => s.Indicators)
            .Should().HaveCount(ClassroomVisitsV2GoldenFixtures.TotalIndicatorsCount);
    }

    [Fact]
    public void Model_contains_v2_relationships_indexes_and_legacy_defaults()
    {
        using var db = CreateContext();
        var model = db.Model;

        var visit = model.FindEntityType(typeof(Visit))!;
        visit.FindProperty(nameof(Visit.ClassroomPeriod)).Should().NotBeNull();
        visit.FindProperty(nameof(Visit.ExperienceVersion))!.GetDefaultValue().Should().Be(ExperienceVersion.Legacy);
        visit.FindProperty(nameof(Visit.ScoringRuleSetVersion))!.GetDefaultValue().Should().Be(1);

        var observation = model.FindEntityType(typeof(VisitObservedIndicator))!;
        observation.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(VisitObservedIndicator.VisitScoreId),
                nameof(VisitObservedIndicator.RubricIndicatorId)
            })).IsUnique.Should().BeTrue();

        model.FindEntityType(typeof(VisitTreatmentSnapshot)).Should().NotBeNull();
        model.FindEntityType(typeof(RubricIndicator)).Should().NotBeNull();
        model.FindEntityType(typeof(VisitAnalysis))!
            .FindProperty(nameof(VisitAnalysis.RuleSetVersion))!.GetDefaultValue().Should().Be(1);
    }

    private static AlFalahDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"visits-v2-schema-{Guid.NewGuid()}")
            .Options);
}
