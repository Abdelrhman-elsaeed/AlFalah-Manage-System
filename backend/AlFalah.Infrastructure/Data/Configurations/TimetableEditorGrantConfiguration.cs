using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class TimetableEditorGrantConfiguration : IEntityTypeConfiguration<TimetableEditorGrant>
{
    public void Configure(EntityTypeBuilder<TimetableEditorGrant> builder)
    {
        builder.ToTable("TimetableEditorGrants");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.SchoolId, x.ModeratorUserId })
            .HasFilter("[IsDeleted] = 0")
            .IsUnique();
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ModeratorUser).WithMany().HasForeignKey(x => x.ModeratorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GrantedByUser).WithMany().HasForeignKey(x => x.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
