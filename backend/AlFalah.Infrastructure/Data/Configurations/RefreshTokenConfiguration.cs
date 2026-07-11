using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Token).IsRequired().HasMaxLength(500);
        builder.Property(x => x.IpAddress).HasMaxLength(50);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.ReplacedByToken).HasMaxLength(500);

        builder.HasOne(x => x.User)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}
