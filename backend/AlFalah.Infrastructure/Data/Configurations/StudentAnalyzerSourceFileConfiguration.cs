using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class StudentAnalyzerSourceFileConfiguration : IEntityTypeConfiguration<StudentAnalyzerSourceFile>
{
    public void Configure(EntityTypeBuilder<StudentAnalyzerSourceFile> builder)
    {
        builder.ToTable("StudentAnalyzerSourceFiles");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.SchoolId, x.UploadedAt });
        builder.HasIndex(x => new { x.SchoolId, x.Sha256 });
        builder.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Extension).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(x => x.Content).HasColumnType("varbinary(max)").IsRequired();
        builder.Property(x => x.UploadedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DeletedByUser).WithMany().HasForeignKey(x => x.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
