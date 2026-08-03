using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class StudentAnalyzerReportConfiguration : IEntityTypeConfiguration<StudentAnalyzerReport>
{
    public void Configure(EntityTypeBuilder<StudentAnalyzerReport> builder)
    {
        builder.ToTable("StudentAnalyzerReports");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.SchoolId, x.CreatedAt });
        builder.HasIndex(x => new { x.SourceFileId, x.StudentName });
        builder.Property(x => x.StudentName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.GrantTotal).HasPrecision(18, 2);
        builder.Property(x => x.DeductionTotal).HasPrecision(18, 2);
        builder.Property(x => x.SelectedDataJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.AnalysisText).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.Model).HasMaxLength(300).IsRequired();
        builder.Property(x => x.PromptVersion).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SourceFile).WithMany(x => x.Reports).HasForeignKey(x => x.SourceFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DeletedByUser).WithMany().HasForeignKey(x => x.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
