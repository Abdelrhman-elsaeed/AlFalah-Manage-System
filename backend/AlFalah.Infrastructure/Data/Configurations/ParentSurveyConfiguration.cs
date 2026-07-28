using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class ParentSurveyConfiguration : IEntityTypeConfiguration<ParentSurvey>
{
    public void Configure(EntityTypeBuilder<ParentSurvey> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.Description).HasMaxLength(2000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.PublicToken).HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);
        builder.Property(x => x.Status).HasConversion<int>();

        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PublicToken).IsUnique().HasFilter("[PublicToken] IS NOT NULL");
        builder.HasIndex(x => new { x.SchoolId, x.IsTemplate, x.Status });
        builder.HasIndex(x => x.IsDeleted);
    }
}

public class ParentSurveyItemConfiguration : IEntityTypeConfiguration<ParentSurveyItem>
{
    public void Configure(EntityTypeBuilder<ParentSurveyItem> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).IsRequired().HasMaxLength(500).IsUnicode(true).UseCollation("Arabic_CI_AS");

        builder.HasOne(x => x.ParentSurvey)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.ParentSurveyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ParentSurveyId, x.SortOrder });
        builder.HasIndex(x => x.IsDeleted);
    }
}

public class ParentSurveySubmissionConfiguration : IEntityTypeConfiguration<ParentSurveySubmission>
{
    public void Configure(EntityTypeBuilder<ParentSurveySubmission> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ParentName).IsRequired().HasMaxLength(150).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.MobileNumber).IsRequired().HasMaxLength(30).IsUnicode(false);

        builder.HasOne(x => x.ParentSurvey)
            .WithMany(x => x.Submissions)
            .HasForeignKey(x => x.ParentSurveyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ParentSurveyId, x.SubmittedAt });
        builder.HasIndex(x => x.MobileNumber);
    }
}

public class ParentSurveyAnswerConfiguration : IEntityTypeConfiguration<ParentSurveyAnswer>
{
    public void Configure(EntityTypeBuilder<ParentSurveyAnswer> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ItemTextSnapshot).IsRequired().HasMaxLength(500).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.WeakReason).HasMaxLength(1000).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.SubmittedRating).HasConversion<int>();
        builder.Property(x => x.EffectiveRating).HasConversion<int>();

        builder.HasOne(x => x.Submission)
            .WithMany(x => x.Answers)
            .HasForeignKey(x => x.ParentSurveySubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Item)
            .WithMany(x => x.Answers)
            .HasForeignKey(x => x.ParentSurveyItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ParentSurveySubmissionId, x.ParentSurveyItemId }).IsUnique();
        builder.HasIndex(x => x.EffectiveRating);
    }
}
