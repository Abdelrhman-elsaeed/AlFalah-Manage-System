using AlFalah.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class PersonalNotificationConfiguration : IEntityTypeConfiguration<PersonalNotification>
{
    public void Configure(EntityTypeBuilder<PersonalNotification> b)
    {
        b.ToTable("PersonalNotifications"); b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450);
        b.Property(x => x.SourceKey).HasMaxLength(240);
        b.Property(x => x.Category).HasMaxLength(30);
        b.Property(x => x.SourceType).HasMaxLength(40);
        b.Property(x => x.Audience).HasMaxLength(40);
        b.Property(x => x.Permission).HasMaxLength(100);
        b.Property(x => x.Title).HasMaxLength(250);
        b.Property(x => x.Preview).HasMaxLength(800);
        b.Property(x => x.PushDisposition).HasMaxLength(30).HasDefaultValue("Disabled");
        // A 450-character identity plus a canonical key exceeds SQL's clustered key limit.
        b.HasIndex(x => new { x.SchoolId, x.UserId, x.SourceKey }).IsUnique();
        b.HasIndex(x => new { x.SchoolId, x.UserId, x.CreatedAtUtc, x.Id });
        b.HasIndex(x => new { x.SchoolId, x.UserId, x.ReadAtUtc });
    }
}
public sealed class BrowserPushAttemptConfiguration : IEntityTypeConfiguration<BrowserPushAttempt>
{
    public void Configure(EntityTypeBuilder<BrowserPushAttempt> b)
    {
        b.ToTable("BrowserPushAttempts"); b.HasKey(x => x.Id);
        b.Property(x => x.State).HasMaxLength(30);
        b.HasOne(x => x.Target).WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TargetId, x.LeaseId }).IsUnique();
    }
}
public sealed class AttendanceCompletionConfiguration : IEntityTypeConfiguration<AttendanceCompletion>
{
    public void Configure(EntityTypeBuilder<AttendanceCompletion> b)
    {
        b.ToTable("AttendanceCompletions"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.SchoolId, x.Date, x.ClassroomId });
    }
}
public sealed class BrowserPushSubscriptionConfiguration : IEntityTypeConfiguration<BrowserPushSubscription>
{
    public void Configure(EntityTypeBuilder<BrowserPushSubscription> b)
    {
        b.ToTable("BrowserPushSubscriptions"); b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450);
        b.Property(x => x.EndpointHash).HasMaxLength(64);
        b.Property(x => x.VapidKeyId).HasMaxLength(100);
        b.Property(x => x.ProtectedMaterial).HasMaxLength(12000);
        b.HasIndex(x => x.EndpointHash).IsUnique();
        b.HasIndex(x => new { x.SchoolId, x.UserId, x.RevokedAtUtc });
    }
}
public sealed class BrowserPushTargetConfiguration : IEntityTypeConfiguration<BrowserPushTarget>
{
    public void Configure(EntityTypeBuilder<BrowserPushTarget> b)
    {
        b.ToTable("BrowserPushTargets"); b.HasKey(x => x.Id);
        b.Property(x => x.State).HasMaxLength(30);
        b.HasOne(x => x.Entry).WithMany().HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EntryId, x.SubscriptionId }).IsUnique();
        b.HasIndex(x => new { x.State, x.NextAttemptAtUtc, x.LeaseUntilUtc });
    }
}
