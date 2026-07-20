using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("AcademicYears");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(128).IsUnicode(true).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasData(new AcademicYear
        {
            Id = 1,
            Code = "2026-2027",
            NameAr = "العام الدراسي 2026-2027",
            StartsOn = new DateOnly(2026, 8, 1),
            EndsOn = new DateOnly(2027, 7, 31),
            IsActive = true
        });
    }
}
