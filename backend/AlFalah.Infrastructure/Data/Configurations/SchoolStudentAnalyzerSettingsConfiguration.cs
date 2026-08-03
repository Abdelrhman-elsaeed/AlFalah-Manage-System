using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class SchoolStudentAnalyzerSettingsConfiguration : IEntityTypeConfiguration<SchoolStudentAnalyzerSettings>
{
    public void Configure(EntityTypeBuilder<SchoolStudentAnalyzerSettings> builder)
    {
        builder.ToTable("SchoolStudentAnalyzerSettings");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.SchoolId).IsUnique();
        builder.Property(x => x.ProtectedGroqApiKey).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ProtectedGeminiApiKey).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ProtectedOpenRouterApiKey).HasColumnType("nvarchar(max)");
        builder.Property(x => x.GroqModel).HasMaxLength(200).IsRequired();
        builder.Property(x => x.GeminiModel).HasMaxLength(200).IsRequired();
        builder.Property(x => x.OpenRouterModel).HasMaxLength(300).IsRequired();
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450).IsRequired();
        builder.HasOne(x => x.School).WithOne().HasForeignKey<SchoolStudentAnalyzerSettings>(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
