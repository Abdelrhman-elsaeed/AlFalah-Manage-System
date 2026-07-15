using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class SchoolLocationConfiguration : IEntityTypeConfiguration<SchoolLocation>
{
    public void Configure(EntityTypeBuilder<SchoolLocation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(120).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.NameEn).HasMaxLength(120).IsUnicode(false);
        builder.Property(x => x.RegionNameAr).IsRequired().HasMaxLength(120).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.RegionNameEn).HasMaxLength(120).IsUnicode(false);
        builder.Property(x => x.Latitude).HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasPrecision(9, 6);

        builder.HasMany(x => x.Schools)
            .WithOne(x => x.Location)
            .HasForeignKey(x => x.SchoolLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.NameAr, x.RegionNameAr }).IsUnique();
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.IsDeleted);

        builder.HasData(SeedLocations());
    }

    private static SchoolLocation[] SeedLocations() =>
    [
        Location(1, "الرياض", "Riyadh", "منطقة الرياض", "Riyadh Region", 24.713552m, 46.675296m),
        Location(2, "جدة", "Jeddah", "منطقة مكة المكرمة", "Makkah Region", 21.485811m, 39.192505m),
        Location(3, "مكة المكرمة", "Makkah", "منطقة مكة المكرمة", "Makkah Region", 21.389082m, 39.857910m),
        Location(4, "المدينة المنورة", "Madinah", "منطقة المدينة المنورة", "Madinah Region", 24.524654m, 39.569184m),
        Location(5, "الدمام", "Dammam", "المنطقة الشرقية", "Eastern Region", 26.420683m, 50.088794m),
        Location(6, "الخبر", "Al Khobar", "المنطقة الشرقية", "Eastern Region", 26.217190m, 50.197138m),
        Location(7, "الهفوف", "Al Hofuf", "المنطقة الشرقية", "Eastern Region", 25.364572m, 49.587238m),
        Location(8, "الطائف", "Taif", "منطقة مكة المكرمة", "Makkah Region", 21.270283m, 40.415833m),
        Location(9, "بريدة", "Buraydah", "منطقة القصيم", "Al-Qassim Region", 26.359230m, 43.981812m),
        Location(10, "أبها", "Abha", "منطقة عسير", "Asir Region", 18.216389m, 42.505280m),
        Location(11, "تبوك", "Tabuk", "منطقة تبوك", "Tabuk Region", 28.383507m, 36.566190m),
        Location(12, "حائل", "Hail", "منطقة حائل", "Hail Region", 27.511410m, 41.720825m),
        Location(13, "عرعر", "Arar", "منطقة الحدود الشمالية", "Northern Borders Region", 30.975310m, 41.038082m),
        Location(14, "جازان", "Jazan", "منطقة جازان", "Jazan Region", 16.889359m, 42.570568m),
        Location(15, "نجران", "Najran", "منطقة نجران", "Najran Region", 17.565603m, 44.228944m),
        Location(16, "الباحة", "Al Bahah", "منطقة الباحة", "Al-Bahah Region", 20.012880m, 41.467670m),
        Location(17, "سكاكا", "Sakaka", "منطقة الجوف", "Al-Jawf Region", 29.969740m, 40.206410m),
        Location(18, "الجبيل", "Jubail", "المنطقة الشرقية", "Eastern Region", 27.017400m, 49.658300m)
    ];

    private static SchoolLocation Location(
        int id, string nameAr, string nameEn, string regionAr, string regionEn,
        decimal latitude, decimal longitude) => new()
    {
        Id = id,
        NameAr = nameAr,
        NameEn = nameEn,
        RegionNameAr = regionAr,
        RegionNameEn = regionEn,
        Latitude = latitude,
        Longitude = longitude,
        IsActive = true,
        CreatedAt = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero)
    };
}
