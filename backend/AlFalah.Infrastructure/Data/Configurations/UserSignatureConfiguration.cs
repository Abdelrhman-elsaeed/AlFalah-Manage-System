using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class UserSignatureConfiguration : IEntityTypeConfiguration<UserSignature>
{
    public void Configure(EntityTypeBuilder<UserSignature> builder)
    {
        // D-31: Arabic-text columns get explicit Unicode + Arabic_CI_AS collation.
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsUnicode(true).UseCollation("Arabic_CI_AS");
    }
}