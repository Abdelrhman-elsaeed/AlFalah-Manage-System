using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        // D-31: Arabic-text columns get explicit Unicode + Arabic_CI_AS collation.
        builder.Property(x => x.DescriptionAr).HasMaxLength(500).IsUnicode(true).UseCollation("Arabic_CI_AS");
    }
}