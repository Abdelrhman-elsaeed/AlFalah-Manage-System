using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        // D-31: Arabic-text columns get explicit Unicode + Arabic_CI_AS collation.
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.Message).IsRequired().HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.Type).HasMaxLength(50);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(100);
        builder.Property(x => x.RelatedEntityId).HasMaxLength(100);
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
    }
}