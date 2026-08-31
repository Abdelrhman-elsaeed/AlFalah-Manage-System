using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations.StudentAffairs;

internal sealed class ConversationThreadConfiguration
    : StudentAffairsMutableEntityConfiguration<ConversationThread>
{
    protected override string TableName => "ConversationThreads";

    protected override void ConfigureEntity(EntityTypeBuilder<ConversationThread> builder)
    {
        builder.Property(x => x.Subject).IsArabicText(250);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.Status });
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ConversationParticipantConfiguration
    : StudentAffairsMutableEntityConfiguration<ConversationParticipant>
{
    protected override string TableName => "ConversationParticipants";

    protected override void ConfigureEntity(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.Property(x => x.ApplicationUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.ParticipantRoleSnapshot).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.HasIndex(x => new { x.SchoolId, x.ConversationThreadId, x.ApplicationUserId })
            .HasFilter("[IsDeleted] = 0 AND [LeftAt] IS NULL").IsUnique();
        builder.HasOne(x => x.ConversationThread).WithMany(x => x.Participants)
            .HasForeignKey(x => new { x.SchoolId, x.ConversationThreadId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApplicationUser).WithMany()
            .HasForeignKey(x => x.ApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ConversationMessageConfiguration
    : StudentAffairsMutableEntityConfiguration<ConversationMessage>
{
    protected override string TableName => "ConversationMessages";

    protected override void ConfigureEntity(EntityTypeBuilder<ConversationMessage> builder)
    {
        builder.Property(x => x.SenderUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Body).HasColumnType("nvarchar(max)").IsUnicode(true).UseCollation("Arabic_CI_AS").IsRequired();
        builder.HasIndex(x => new { x.SchoolId, x.ConversationThreadId, x.QueuedAt });
        builder.HasOne(x => x.ConversationThread).WithMany(x => x.Messages)
            .HasForeignKey(x => new { x.SchoolId, x.ConversationThreadId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SenderUser).WithMany()
            .HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReplyToMessage).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.ReplyToMessageId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MessageReceiptConfiguration : IEntityTypeConfiguration<MessageReceipt>
{
    public void Configure(EntityTypeBuilder<MessageReceipt> builder)
    {
        builder.ToTable("MessageReceipts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RecipientUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.FailureReason).HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.HasEnumCheckConstraints("MessageReceipts");
        builder.HasIndex(x => new { x.SchoolId, x.ConversationMessageId, x.RecipientUserId }).IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.RecipientUserId, x.ReadAt });
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ConversationMessage).WithMany(x => x.Receipts)
            .HasForeignKey(x => new { x.SchoolId, x.ConversationMessageId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecipientUser).WithMany()
            .HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TeacherOfficeHourConfiguration
    : StudentAffairsMutableEntityConfiguration<TeacherOfficeHour>
{
    protected override string TableName => "TeacherOfficeHours";

    protected override void ConfigureEntity(EntityTypeBuilder<TeacherOfficeHour> builder)
    {
        builder.ToTable(TableName, table =>
        {
            table.HasCheckConstraint("CK_TeacherOfficeHours_Period", "[Period] IS NULL OR [Period] BETWEEN 1 AND 8");
            table.HasCheckConstraint("CK_TeacherOfficeHours_TimeShape",
                "([Period] IS NOT NULL AND [LocalStartTime] IS NULL AND [LocalEndTime] IS NULL) OR " +
                "([Period] IS NULL AND [LocalStartTime] IS NOT NULL AND [LocalEndTime] IS NOT NULL AND [LocalEndTime] > [LocalStartTime])");
            table.HasCheckConstraint("CK_TeacherOfficeHours_EffectiveDates",
                "[EffectiveUntil] IS NULL OR [EffectiveUntil] >= [EffectiveFrom]");
        });
        builder.HasIndex(x => new { x.SchoolId, x.InstructorProfileId, x.AcademicTermId, x.Day });
        builder.HasOne(x => x.InstructorProfile).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.InstructorProfileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.AcademicTermId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
