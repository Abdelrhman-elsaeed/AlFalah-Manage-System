using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class TeacherEvidenceSubmissionConfiguration : IEntityTypeConfiguration<TeacherEvidenceSubmission>
{
    public void Configure(EntityTypeBuilder<TeacherEvidenceSubmission> builder)
    {
        builder.ToTable("TeacherEvidenceSubmissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DriveId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.DriveItemId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ParentItemId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.FileExtension).HasMaxLength(32);
        builder.Property(x => x.MimeType).HasMaxLength(256);
        builder.Property(x => x.WebUrl).HasMaxLength(2048);
        builder.Property(x => x.ETag).HasMaxLength(512);
        builder.Property(x => x.ReviewedByUserId).HasMaxLength(450);
        builder.Property(x => x.ReviewNote).HasMaxLength(1000).IsUnicode(true);
        // Graph item IDs are only unique inside a drive. This constraint makes
        // retries idempotent across the whole database, not only one teacher.
        builder.HasIndex(x => new { x.DriveId, x.DriveItemId }).IsUnique();
        builder.HasIndex(x => new { x.TeacherId, x.AcademicYearId, x.TaskId, x.IsDeleted });
        builder.HasIndex(x => new { x.TeacherId, x.UploadedAtUtc });
        builder.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
    }
}
