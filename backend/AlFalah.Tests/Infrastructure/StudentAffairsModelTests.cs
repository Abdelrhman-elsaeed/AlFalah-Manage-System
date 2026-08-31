using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace AlFalah.Tests.Infrastructure;

public sealed class StudentAffairsModelTests
{
    [Fact]
    public void MutableEntities_HaveSoftDeleteFilters_TenantKeys_AndRestrictedForeignKeys()
    {
        using var context = CreateContext();
        var mutableTypes = typeof(IStudentAffairsMutableEntity).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                && typeof(IStudentAffairsMutableEntity).IsAssignableFrom(type));

        foreach (var mutableType in mutableTypes)
        {
            var entityType = context.Model.FindEntityType(mutableType);
            entityType.Should().NotBeNull($"{mutableType.Name} must be mapped");
            entityType!.GetQueryFilter().Should().NotBeNull($"{mutableType.Name} is soft deletable");
            entityType.GetKeys().Should().Contain(key =>
                key.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { nameof(IStudentAffairsMutableEntity.SchoolId), nameof(IStudentAffairsMutableEntity.Id) }));
            entityType.GetForeignKeys().Should().OnlyContain(foreignKey =>
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        }
    }

    [Fact]
    public void StudentAffairsRelationships_ToTenantPrincipals_IncludeSchoolId()
    {
        using var context = CreateContext();
        var studentAffairsNamespace = typeof(Student).Namespace;
        var foreignKeys = context.Model.GetEntityTypes()
            .Where(type => type.ClrType.Namespace == studentAffairsNamespace)
            .SelectMany(type => type.GetForeignKeys())
            .Where(foreignKey => foreignKey.PrincipalEntityType.ClrType.Namespace == studentAffairsNamespace);

        foreach (var foreignKey in foreignKeys)
        {
            foreignKey.Properties.Select(property => property.Name)
                .Should().Contain(nameof(IStudentAffairsMutableEntity.SchoolId));
            foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        }
    }

    [Fact]
    public void ConcurrentAggregates_MapRowVersionAsConcurrencyToken()
    {
        using var context = CreateContext();
        var concurrentTypes = typeof(IStudentAffairsConcurrentEntity).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                && typeof(IStudentAffairsConcurrentEntity).IsAssignableFrom(type));

        foreach (var concurrentType in concurrentTypes)
        {
            var rowVersion = context.Model.FindEntityType(concurrentType)!
                .FindProperty(nameof(IStudentAffairsConcurrentEntity.RowVersion));
            rowVersion.Should().NotBeNull();
            rowVersion!.IsConcurrencyToken.Should().BeTrue();
            rowVersion.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
        }
    }

    private static AlFalahDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"student-affairs-model-{Guid.NewGuid()}")
            .Options;
        return new AlFalahDbContext(options);
    }
}
