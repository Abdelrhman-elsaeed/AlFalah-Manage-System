using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class UserSchoolRoleConfiguration : IEntityTypeConfiguration<UserSchoolRole>
{
    public void Configure(EntityTypeBuilder<UserSchoolRole> builder)
    {
        builder.HasKey(x => x.Id);

        // UserId → Users: Restrict to avoid multiple cascade paths from Users table.
        // UserSchoolRoles are soft-deleted via IsActive; hard delete handled in service.
        builder.HasOne(x => x.User)
            .WithMany(x => x.UserSchoolRoles)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.School)
            .WithMany(x => x.UserSchoolRoles)
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // CreatedByUserId → Users: NoAction to avoid cycle. Nulled manually when creator is deleted.
        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        // UpdatedByUserId and DeletedByUserId: NoAction to avoid multiple cascade paths.
        builder.HasOne(x => x.UpdatedByUser)
            .WithMany()
            .HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        builder.HasOne(x => x.DeletedByUser)
            .WithMany()
            .HasForeignKey(x => x.DeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        builder.HasIndex(x => new { x.UserId, x.SchoolId, x.RoleId }).IsUnique();
        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.IsDeleted);
    }
}
