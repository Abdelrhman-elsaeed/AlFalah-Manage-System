using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // D-31: explicit Unicode on every Arabic-text column (defense-in-depth).
        // FirstName / LastName are concatenated at runtime into FullName; corrupted
        // bytes here (e.g. '?' stored via cp1252 INSERT) make the user unrenderable.
        builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.LastName).IsRequired().HasMaxLength(100).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.PreferredLanguage).HasMaxLength(10);

        // Soft delete FK — NoAction to avoid cascade cycle with UserSchoolRoles.
        builder.HasOne(x => x.DeletedByUser)
            .WithMany()
            .HasForeignKey(x => x.DeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => x.IsActive);
    }
}