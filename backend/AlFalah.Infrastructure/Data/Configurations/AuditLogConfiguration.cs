using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(200).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.EntityName).HasMaxLength(200).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.EntityId).HasMaxLength(200);
        builder.Property(x => x.IpAddress).HasMaxLength(50);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.OldValues).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.NewValues).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.Reason).HasMaxLength(1000).IsUnicode(true).UseCollation("Arabic_CI_AS");

        // No FK cascade on AuditLog — keep logs even if records are deleted
        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.EntityName);
    }
}
