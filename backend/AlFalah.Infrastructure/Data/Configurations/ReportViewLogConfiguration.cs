using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="ReportViewLog"/>.
/// Indexes target the hot query paths:
///  - (VisitId) for "show me the view history for this visit"
///  - (InstructorUserId) for "show me all visits I've viewed"
///  - (VisitId, ViewedAt) for the "first viewed / last viewed / count" aggregation
///    that the manager / moderator view-status endpoint returns.
/// </summary>
public class ReportViewLogConfiguration : IEntityTypeConfiguration<ReportViewLog>
{
    public void Configure(EntityTypeBuilder<ReportViewLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.InstructorUserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.IpAddress).HasMaxLength(50);
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);

        builder.HasOne(x => x.Visit)
            .WithMany(v => v.ViewLogs)
            .HasForeignKey(x => x.VisitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.VisitId);
        builder.HasIndex(x => x.InstructorUserId);
        builder.HasIndex(x => x.ViewedAt);
        builder.HasIndex(x => new { x.VisitId, x.ViewedAt });
        builder.HasIndex(x => x.IsDeleted);
    }
}