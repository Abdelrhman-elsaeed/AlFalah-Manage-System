using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class VisitFeedbackTemplateConfiguration : IEntityTypeConfiguration<VisitFeedbackTemplate>
{
    public void Configure(EntityTypeBuilder<VisitFeedbackTemplate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(1000).IsRequired().IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SchoolId, x.Kind, x.IsDeleted });
    }
}
