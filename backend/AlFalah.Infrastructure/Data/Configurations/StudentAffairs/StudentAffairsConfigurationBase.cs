using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations.StudentAffairs;

internal abstract class StudentAffairsMutableEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : class, IStudentAffairsMutableEntity
{
    protected abstract string TableName { get; }

    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.SchoolId, x.Id });
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);

        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
        ConfigureEntity(builder);
        builder.HasEnumCheckConstraints(TableName);
    }

    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
}

internal static class StudentAffairsConfigurationExtensions
{
    private const string ArabicCollation = "Arabic_CI_AS";

    public static PropertyBuilder<string> IsArabicText(
        this PropertyBuilder<string> property,
        int maxLength,
        bool required = true)
    {
        var configured = property.HasMaxLength(maxLength).IsUnicode(true).UseCollation(ArabicCollation);
        return required ? configured.IsRequired() : configured;
    }

    public static PropertyBuilder<string?> IsOptionalArabicText(
        this PropertyBuilder<string?> property,
        int maxLength)
        => property.HasMaxLength(maxLength).IsUnicode(true).UseCollation(ArabicCollation);

    public static void HasEnumCheckConstraints<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        string tableName)
        where TEntity : class
    {
        foreach (var property in typeof(TEntity).GetProperties())
        {
            var enumType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (!enumType.IsEnum) continue;
            if (builder.Metadata.FindProperty(property.Name)?.GetValueConverter() is not null) continue;

            var values = Enum.GetValues(enumType).Cast<object>().Select(Convert.ToInt64).ToArray();
            var minimum = values.Min();
            var maximum = values.Max();
            builder.ToTable(tableName, table => table.HasCheckConstraint(
                $"CK_{tableName}_{property.Name}",
                $"[{property.Name}] BETWEEN {minimum} AND {maximum}"));
        }
    }
}
