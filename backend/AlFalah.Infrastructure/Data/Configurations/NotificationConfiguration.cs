using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", table =>
        {
            table.HasCheckConstraint("CK_Notifications_Priority", "[Priority] BETWEEN 1 AND 3");
            table.HasCheckConstraint("CK_Notifications_DeliveryStatus", "[DeliveryStatus] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Notifications_RetryCount", "[RetryCount] >= 0");
        });
        // D-31: Arabic-text columns get explicit Unicode + Arabic_CI_AS collation.
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.Message).IsRequired().HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.Type).HasMaxLength(50);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(100);
        builder.Property(x => x.RelatedEntityId).HasMaxLength(100);
        builder.Property(x => x.TemplateKey).HasMaxLength(200).IsUnicode(false);
        builder.Property(x => x.DeduplicationKey).HasMaxLength(500).IsUnicode(false);
        builder.Property(x => x.FailureReason).HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.SuppressionReason).HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.ApprovedByUserId).HasMaxLength(450);
        builder.Property(x => x.SuppressedByUserId).HasMaxLength(450);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450);
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SuppressedByUser).WithMany().HasForeignKey(x => x.SuppressedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DeletedByUser).WithMany().HasForeignKey(x => x.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SchoolId, x.UserId, x.IsRead, x.CreatedAt });
        builder.HasIndex(x => new { x.SchoolId, x.RequiresApproval, x.IsSuppressed, x.CreatedAt });
        builder.HasIndex(x => new { x.SchoolId, x.UserId, x.DeduplicationKey })
            .HasFilter("[DeduplicationKey] IS NOT NULL").IsUnique();
    }
}
