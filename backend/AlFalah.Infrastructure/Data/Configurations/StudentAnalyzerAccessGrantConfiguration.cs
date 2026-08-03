using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class StudentAnalyzerAccessGrantConfiguration : IEntityTypeConfiguration<StudentAnalyzerAccessGrant>
{
    public void Configure(EntityTypeBuilder<StudentAnalyzerAccessGrant> builder)
    {
        builder.ToTable("StudentAnalyzerAccessGrants");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.SchoolId, x.UserId }).HasFilter("[IsDeleted] = 0").IsUnique();
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.GrantedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GrantedByUser).WithMany().HasForeignKey(x => x.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DeletedByUser).WithMany().HasForeignKey(x => x.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
