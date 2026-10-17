using AlFalah.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class InboxStreamConfiguration : IEntityTypeConfiguration<RecipientInboxStream>
{
    public void Configure(EntityTypeBuilder<RecipientInboxStream> b)
    {
        b.ToTable("RecipientInboxStreams");
        b.HasKey(x => new { x.SchoolId, x.UserId }).IsClustered(false);
        b.Property(x => x.UserId).HasMaxLength(450);
    }
}
public sealed class InboxChangeConfiguration : IEntityTypeConfiguration<InboxChange>
{
    public void Configure(EntityTypeBuilder<InboxChange> b)
    {
        b.ToTable("InboxChanges"); b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450);
        b.HasIndex(x => new { x.SchoolId, x.UserId, x.Sequence }).IsUnique();
        b.HasIndex(x => x.OccurredAtUtc);
    }
}
public sealed class InboxIntentConfiguration : IEntityTypeConfiguration<NotificationDeliveryIntent>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryIntent> b)
    {
        b.ToTable("NotificationDeliveryIntents"); b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450);
        b.Property(x => x.SourceKey).HasMaxLength(100);
        b.Property(x => x.Channel).HasMaxLength(16);
        b.HasIndex(x => new { x.SchoolId, x.UserId, x.SourceKey, x.Channel }).IsUnique();
        b.HasIndex(x => new { x.CompletedAtUtc, x.NextAttemptAtUtc, x.LeaseUntilUtc });
    }
}
public sealed class InboxAttemptConfiguration : IEntityTypeConfiguration<InboxDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<InboxDeliveryAttempt> b)
    {
        b.ToTable("InboxDeliveryAttempts"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.IntentId, x.LeaseId }).IsUnique();
    }
}
